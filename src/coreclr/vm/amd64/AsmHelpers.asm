; Licensed to the .NET Foundation under one or more agreements.
; The .NET Foundation licenses this file to you under the MIT license.

include AsmMacros.inc
include asmconstants.inc

extern NDirectImportWorker:proc
extern ThePreStub:proc
extern  ProfileEnter:proc
extern  ProfileLeave:proc
extern  ProfileTailcall:proc
extern OnHijackWorker:proc
extern JIT_RareDisableHelperWorker:proc

extern g_pPollGC:QWORD
extern g_TrapReturningThreads:DWORD

; EXTERN_C int __fastcall HelperMethodFrameRestoreState(
;         INDEBUG_COMMA(HelperMethodFrame *pFrame)
;         MachState *pState
;         )
LEAF_ENTRY HelperMethodFrameRestoreState, _TEXT

ifdef _DEBUG
        mov     rcx, rdx
endif

        ; Check if the MachState is valid
        xor     eax, eax
        cmp     qword ptr [rcx + OFFSETOF__MachState___pRetAddr], rax
        jne     @F
        REPRET
@@:

        ;
        ; If a preserved register were pushed onto the stack between
        ; the managed caller and the H_M_F, m_pReg will point to its
        ; location on the stack and it would have been updated on the
        ; stack by the GC already and it will be popped back into the
        ; appropriate register when the appropriate epilog is run.
        ;
        ; Otherwise, the register is preserved across all the code
        ; in this HCALL or FCALL, so we need to update those registers
        ; here because the GC will have updated our copies in the
        ; frame.
        ;
        ; So, if m_pReg points into the MachState, we need to update
        ; the register here.  That's what this macro does.
        ;
RestoreReg macro reg, regnum
        lea     rax, [rcx + OFFSETOF__MachState__m_Capture + 8 * regnum]
        mov     rdx, [rcx + OFFSETOF__MachState__m_Ptrs + 8 * regnum]
        cmp     rax, rdx
        cmove   reg, [rax]
        endm

        ; regnum has to match ENUM_CALLEE_SAVED_REGISTERS macro
        RestoreReg Rdi, 0
        RestoreReg Rsi, 1
        RestoreReg Rbx, 2
        RestoreReg Rbp, 3
        RestoreReg R12, 4
        RestoreReg R13, 5
        RestoreReg R14, 6
        RestoreReg R15, 7

        xor     eax, eax
        ret

LEAF_END HelperMethodFrameRestoreState, _TEXT

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;;
;; NDirectImportThunk
;;
;; In addition to being called by the EE, this function can be called
;;  directly from code generated by JIT64 for CRT optimized direct
;;  P/Invoke calls. If it is modified, the JIT64 compiler's code
;;  generation will need to altered accordingly.
;;
; EXTERN_C VOID __stdcall NDirectImportThunk();
NESTED_ENTRY NDirectImportThunk, _TEXT

        ;
        ; Allocate space for XMM parameter registers and callee scratch area.
        ;
        alloc_stack     68h

        ;
        ; Save integer parameter registers.
        ; Make sure to preserve r11 as well as it is used to pass the stack argument size from JIT
        ;
        save_reg_postrsp    rcx, 70h
        save_reg_postrsp    rdx, 78h
        save_reg_postrsp    r8,  80h
        save_reg_postrsp    r9,  88h
        save_reg_postrsp    r11,  60h

        save_xmm128_postrsp xmm0, 20h
        save_xmm128_postrsp xmm1, 30h
        save_xmm128_postrsp xmm2, 40h
        save_xmm128_postrsp xmm3, 50h
    END_PROLOGUE

        ;
        ; Call NDirectImportWorker w/ the NDirectMethodDesc*
        ;
        mov             rcx, METHODDESC_REGISTER
        call            NDirectImportWorker

        ;
        ; Restore parameter registers
        ;
        mov             rcx, [rsp + 70h]
        mov             rdx, [rsp + 78h]
        mov             r8,  [rsp + 80h]
        mov             r9,  [rsp + 88h]
        mov             r11, [rsp + 60h]
        movdqa          xmm0, [rsp + 20h]
        movdqa          xmm1, [rsp + 30h]
        movdqa          xmm2, [rsp + 40h]
        movdqa          xmm3, [rsp + 50h]

        ;
        ; epilogue, rax contains the native target address
        ;
        add             rsp, 68h

    TAILJMP_RAX
NESTED_END NDirectImportThunk, _TEXT


;------------------------------------------------
; JIT_RareDisableHelper
;
; The JIT expects this helper to preserve all
; registers that can be used for return values
;

NESTED_ENTRY JIT_RareDisableHelper, _TEXT

    alloc_stack         38h
    END_PROLOGUE

    movdqa      [rsp+20h], xmm0     ; Save xmm0
    mov         [rsp+30h], rax      ; Save rax

    call        JIT_RareDisableHelperWorker

    movdqa      xmm0, [rsp+20h]     ; Restore xmm0
    mov         rax,  [rsp+30h]     ; Restore rax

    add         rsp, 38h
    ret

NESTED_END JIT_RareDisableHelper, _TEXT


; extern "C" void setFPReturn(int fpSize, INT64 retVal);
LEAF_ENTRY setFPReturn, _TEXT
        cmp     ecx, 4
        je      setFPReturn4
        cmp     ecx, 8
        jne     setFPReturnNot8
        mov     [rsp+10h], rdx
        movsd   xmm0, real8 ptr [rsp+10h]
setFPReturnNot8:
        REPRET

setFPReturn4:
        mov     [rsp+10h], rdx
        movss   xmm0, real4 ptr [rsp+10h]
        ret
LEAF_END setFPReturn, _TEXT


; extern "C" void getFPReturn(int fpSize, INT64 *retval);
LEAF_ENTRY getFPReturn, _TEXT
        cmp     ecx, 4
        je      getFPReturn4
        cmp     ecx, 8
        jne     getFPReturnNot8
        movsd   real8 ptr [rdx], xmm0
getFPReturnNot8:
        REPRET

getFPReturn4:
        movss   real4 ptr [rdx], xmm0
        ret
LEAF_END getFPReturn, _TEXT


; A JITted method's return address was hijacked to return to us here.
; VOID OnHijackTripThread()
NESTED_ENTRY OnHijackTripThread, _TEXT

        ; Don't fiddle with this unless you change HijackFrame::UpdateRegDisplay
        ; and HijackArgs
        mov                 rdx, rsp
        push                rax ; make room for the real return address (Rip)
        push                rdx
        PUSH_CALLEE_SAVED_REGISTERS
        push_vol_reg        rax
        mov                 rcx, rsp

        alloc_stack         38h ; make extra room for xmm0, argument home slots and align the SP
        save_xmm128_postrsp xmm0, 20h


        END_PROLOGUE

        call                OnHijackWorker

        movdqa              xmm0, [rsp + 20h]

        add                 rsp, 38h
        pop                 rax
        POP_CALLEE_SAVED_REGISTERS
        pop                 rdx
        ret                 ; return to the correct place, adjusted by our caller
NESTED_END OnHijackTripThread, _TEXT


;
;    typedef struct _PROFILE_PLATFORM_SPECIFIC_DATA
;    {
;        FunctionID *functionId; // function ID comes in the r11 register
;        void       *rbp;
;        void       *probersp;
;        void       *ip;
;        void       *profiledRsp;
;        UINT64      rax;
;        LPVOID      hiddenArg;
;        UINT64      flt0;
;        UINT64      flt1;
;        UINT64      flt2;
;        UINT64      flt3;
;        UINT32      flags;
;    } PROFILE_PLATFORM_SPECIFIC_DATA, *PPROFILE_PLATFORM_SPECIFIC_DATA;
;
SIZEOF_PROFILE_PLATFORM_SPECIFIC_DATA   equ 8h*11 + 4h*2    ; includes fudge to make FP_SPILL right
SIZEOF_OUTGOING_ARGUMENT_HOMES          equ 8h*4
SIZEOF_FP_ARG_SPILL                     equ 10h*1

; Need to be careful to keep the stack 16byte aligned here, since we are pushing 3
; arguments that will align the stack and we just want to keep it aligned with our
; SIZEOF_STACK_FRAME

OFFSETOF_PLATFORM_SPECIFIC_DATA         equ SIZEOF_OUTGOING_ARGUMENT_HOMES

; we'll just spill into the PROFILE_PLATFORM_SPECIFIC_DATA structure
OFFSETOF_FP_ARG_SPILL                   equ SIZEOF_OUTGOING_ARGUMENT_HOMES + \
                                            SIZEOF_PROFILE_PLATFORM_SPECIFIC_DATA

SIZEOF_STACK_FRAME                      equ SIZEOF_OUTGOING_ARGUMENT_HOMES + \
                                            SIZEOF_PROFILE_PLATFORM_SPECIFIC_DATA + \
                                            SIZEOF_MAX_FP_ARG_SPILL

PROFILE_ENTER                           equ 1h
PROFILE_LEAVE                           equ 2h
PROFILE_TAILCALL                        equ 4h

; ***********************************************************
;   NOTE:
;
;   Register preservation scheme:
;
;       Preserved:
;           - all non-volatile registers
;           - rax
;           - xmm0
;
;       Not Preserved:
;           - integer argument registers (rcx, rdx, r8, r9)
;           - floating point argument registers (xmm1-3)
;           - volatile integer registers (r10, r11)
;           - volatile floating point registers (xmm4-5)
;
; ***********************************************************

; void JIT_ProfilerEnterLeaveTailcallStub(UINT_PTR ProfilerHandle)
LEAF_ENTRY JIT_ProfilerEnterLeaveTailcallStub, _TEXT
        REPRET
LEAF_END JIT_ProfilerEnterLeaveTailcallStub, _TEXT

;EXTERN_C void ProfileEnterNaked(FunctionIDOrClientID functionIDOrClientID, size_t profiledRsp);
NESTED_ENTRY ProfileEnterNaked, _TEXT
        push_nonvol_reg         rax

;       Upon entry :
;           rcx = clientInfo
;           rdx = profiledRsp

        lea                     rax, [rsp + 10h]    ; caller rsp
        mov                     r10, [rax - 8h]     ; return address

        alloc_stack             SIZEOF_STACK_FRAME

        ; correctness of return value in structure doesn't matter for enter probe


        ; setup ProfilePlatformSpecificData structure
        xor                     r8, r8;
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA +  0h], r8     ; r8 is null      -- struct functionId field
        save_reg_postrsp        rbp, OFFSETOF_PLATFORM_SPECIFIC_DATA +    8h          ;                 -- struct rbp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 10h], rax    ; caller rsp      -- struct probeRsp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 18h], r10    ; return address  -- struct ip field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 20h], rdx    ;                 -- struct profiledRsp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 28h], r8     ; r8 is null      -- struct rax field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 30h], r8     ; r8 is null      -- struct hiddenArg field
        movsd                   real8 ptr [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 38h], xmm0    ;      -- struct flt0 field
        movsd                   real8 ptr [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 40h], xmm1    ;      -- struct flt1 field
        movsd                   real8 ptr [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 48h], xmm2    ;      -- struct flt2 field
        movsd                   real8 ptr [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 50h], xmm3    ;      -- struct flt3 field
        mov                     r10, PROFILE_ENTER
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 58h], r10d   ; flags    ;      -- struct flags field

        ; we need to be able to restore the fp return register
        save_xmm128_postrsp     xmm0, OFFSETOF_FP_ARG_SPILL +  0h
    END_PROLOGUE

        ; rcx already contains the clientInfo
        lea                     rdx, [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA]
        call                    ProfileEnter

        ; restore fp return register
        movdqa                  xmm0, [rsp + OFFSETOF_FP_ARG_SPILL +  0h]

        ; begin epilogue
        add                     rsp, SIZEOF_STACK_FRAME
        pop                     rax
        ret
NESTED_END ProfileEnterNaked, _TEXT

;EXTERN_C void ProfileLeaveNaked(FunctionIDOrClientID functionIDOrClientID, size_t profiledRsp);
NESTED_ENTRY ProfileLeaveNaked, _TEXT
        push_nonvol_reg         rax

;       Upon entry :
;           rcx = clientInfo
;           rdx = profiledRsp

        ; need to be careful with rax here because it contains the return value which we want to harvest

        lea                     r10, [rsp + 10h]    ; caller rsp
        mov                     r11, [r10 - 8h]     ; return address

        alloc_stack             SIZEOF_STACK_FRAME

        ; correctness of argument registers in structure doesn't matter for leave probe

        ; setup ProfilePlatformSpecificData structure
        xor                     r8, r8;
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA +  0h], r8     ; r8 is null      -- struct functionId field
        save_reg_postrsp        rbp, OFFSETOF_PLATFORM_SPECIFIC_DATA +    8h          ;                 -- struct rbp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 10h], r10    ; caller rsp      -- struct probeRsp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 18h], r11    ; return address  -- struct ip field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 20h], rdx    ;                 -- struct profiledRsp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 28h], rax    ; return value    -- struct rax field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 30h], r8     ; r8 is null      -- struct hiddenArg field
        movsd                   real8 ptr [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 38h], xmm0    ;      -- struct flt0 field
        movsd                   real8 ptr [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 40h], xmm1    ;      -- struct flt1 field
        movsd                   real8 ptr [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 48h], xmm2    ;      -- struct flt2 field
        movsd                   real8 ptr [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 50h], xmm3    ;      -- struct flt3 field
        mov                     r10, PROFILE_LEAVE
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 58h], r10d   ; flags           -- struct flags field

        ; we need to be able to restore the fp return register
        save_xmm128_postrsp     xmm0, OFFSETOF_FP_ARG_SPILL +  0h
    END_PROLOGUE

        ; rcx already contains the clientInfo
        lea                     rdx, [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA]
        call                    ProfileLeave

        ; restore fp return register
        movdqa                  xmm0, [rsp + OFFSETOF_FP_ARG_SPILL +  0h]

        ; begin epilogue
        add                     rsp, SIZEOF_STACK_FRAME
        pop                     rax
        ret
NESTED_END ProfileLeaveNaked, _TEXT

;EXTERN_C void ProfileTailcallNaked(FunctionIDOrClientID functionIDOrClientID, size_t profiledRsp);
NESTED_ENTRY ProfileTailcallNaked, _TEXT
        push_nonvol_reg         rax

;       Upon entry :
;           rcx = clientInfo
;           rdx = profiledRsp

        lea                     rax, [rsp + 10h]    ; caller rsp
        mov                     r11, [rax - 8h]     ; return address

        alloc_stack             SIZEOF_STACK_FRAME

        ; correctness of return values and argument registers in structure
        ; doesn't matter for tailcall probe


        ; setup ProfilePlatformSpecificData structure
        xor                     r8, r8;
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA +  0h], r8     ; r8 is null      -- struct functionId field
        save_reg_postrsp        rbp, OFFSETOF_PLATFORM_SPECIFIC_DATA +    8h          ;                 -- struct rbp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 10h], rax    ; caller rsp      -- struct probeRsp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 18h], r11    ; return address  -- struct ip field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 20h], rdx    ;                 -- struct profiledRsp field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 28h], r8     ; r8 is null      -- struct rax field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 30h], r8     ; r8 is null      -- struct hiddenArg field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 38h], r8     ; r8 is null      -- struct flt0 field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 40h], r8     ; r8 is null      -- struct flt1 field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 48h], r8     ; r8 is null      -- struct flt2 field
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 50h], r8     ; r8 is null      -- struct flt3 field
        mov                     r10, PROFILE_TAILCALL
        mov                     [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA + 58h], r10d   ; flags           -- struct flags field

        ; we need to be able to restore the fp return register
        save_xmm128_postrsp     xmm0, OFFSETOF_FP_ARG_SPILL +  0h
    END_PROLOGUE

        ; rcx already contains the clientInfo
        lea                     rdx, [rsp + OFFSETOF_PLATFORM_SPECIFIC_DATA]
        call                    ProfileTailcall

        ; restore fp return register
        movdqa                  xmm0, [rsp + OFFSETOF_FP_ARG_SPILL +  0h]

        ; begin epilogue
        add                     rsp, SIZEOF_STACK_FRAME
        pop                     rax
        ret
NESTED_END ProfileTailcallNaked, _TEXT

ifdef FEATURE_TIERED_COMPILATION

extern OnCallCountThresholdReached:proc

NESTED_ENTRY OnCallCountThresholdReachedStub, _TEXT
        PROLOG_WITH_TRANSITION_BLOCK

        lea     rcx, [rsp + __PWTB_TransitionBlock] ; TransitionBlock *
        mov     rdx, rax ; stub-identifying token, see OnCallCountThresholdReachedStub
        call    OnCallCountThresholdReached

        EPILOG_WITH_TRANSITION_BLOCK_TAILCALL
        TAILJMP_RAX
NESTED_END OnCallCountThresholdReachedStub, _TEXT

extern JIT_PatchpointWorkerWorkerWithPolicy:proc

NESTED_ENTRY JIT_Patchpoint, _TEXT
        PROLOG_WITH_TRANSITION_BLOCK

        lea     rcx, [rsp + __PWTB_TransitionBlock] ; TransitionBlock *
        call    JIT_PatchpointWorkerWorkerWithPolicy

        EPILOG_WITH_TRANSITION_BLOCK_RETURN
        TAILJMP_RAX
NESTED_END JIT_Patchpoint, _TEXT

; first arg register holds iloffset, which needs to be moved to the second register, and the first register filled with NULL
LEAF_ENTRY JIT_PartialCompilationPatchpoint, _TEXT
        mov rdx, rcx
        xor rcx, rcx
        jmp JIT_Patchpoint
LEAF_END JIT_PartialCompilationPatchpoint, _TEXT

endif ; FEATURE_TIERED_COMPILATION

LEAF_ENTRY JIT_PollGC, _TEXT
    cmp [g_TrapReturningThreads], 0
    jnz             JIT_PollGCRarePath
    ret
JIT_PollGCRarePath:
    mov rax, g_pPollGC
    TAILJMP_RAX
LEAF_END JIT_PollGC, _TEXT

        end