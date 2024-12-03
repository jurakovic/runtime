// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace System
{
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public abstract partial class Delegate : ICloneable, ISerializable
    {
        // _target is the object we will invoke on
        internal object? _target; // Initialized by VM as needed; null if static delegate

        // MethodBase, either cached after first request or assigned from a DynamicMethod
        // For open delegates to collectible types, this may be a LoaderAllocator object
        internal object? _methodBase; // Initialized by VM as needed

        // _methodPtr is a pointer to the method we will invoke
        // It could be a small thunk if this is a static or UM call
        internal IntPtr _methodPtr;

        // In the case of a static method passed to a delegate, this field stores
        // whatever _methodPtr would have stored: and _methodPtr points to a
        // small thunk which removes the "this" pointer before going on
        // to _methodPtrAux.
        internal IntPtr _methodPtrAux;

        // This constructor is called from the class generated by the
        //  compiler generated code
        [RequiresUnreferencedCode("The target method might be removed")]
        protected Delegate(object target, string method)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(method);

            // This API existed in v1/v1.1 and only expected to create closed
            // instance delegates. Constrain the call to BindToMethodName to
            // such and don't allow relaxed signature matching (which could make
            // the choice of target method ambiguous) for backwards
            // compatibility. The name matching was case sensitive and we
            // preserve that as well.
            if (!BindToMethodName(target, (RuntimeType)target.GetType(), method,
                                  DelegateBindingFlags.InstanceMethodOnly |
                                  DelegateBindingFlags.ClosedDelegateOnly))
                throw new ArgumentException(SR.Arg_DlgtTargMeth);
        }

        // This constructor is called from a class to generate a
        // delegate based upon a static method name and the Type object
        // for the class defining the method.
        protected Delegate([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.AllMethods)] Type target, string method)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(method);

            if (target.ContainsGenericParameters)
                throw new ArgumentException(SR.Arg_UnboundGenParam, nameof(target));
            if (target is not RuntimeType rtTarget)
                throw new ArgumentException(SR.Argument_MustBeRuntimeType, nameof(target));

            // This API existed in v1/v1.1 and only expected to create open
            // static delegates. Constrain the call to BindToMethodName to such
            // and don't allow relaxed signature matching (which could make the
            // choice of target method ambiguous) for backwards compatibility.
            // The name matching was case insensitive (no idea why this is
            // different from the constructor above) and we preserve that as
            // well.
            BindToMethodName(null, rtTarget, method,
                             DelegateBindingFlags.StaticMethodOnly |
                             DelegateBindingFlags.OpenDelegateOnly |
                             DelegateBindingFlags.CaselessMatching);
        }

        protected virtual object? DynamicInvokeImpl(object?[]? args)
        {
            RuntimeMethodHandleInternal method = new RuntimeMethodHandleInternal(GetInvokeMethod());
            RuntimeMethodInfo invoke = (RuntimeMethodInfo)RuntimeType.GetMethodBase((RuntimeType)this.GetType(), method)!;

            return invoke.Invoke(this, BindingFlags.Default, null, args, null);
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj == null || !InternalEqualTypes(this, obj))
                return false;

            Delegate d = (Delegate)obj;

            // do an optimistic check first. This is hopefully cheap enough to be worth
            if (_target == d._target && _methodPtr == d._methodPtr && _methodPtrAux == d._methodPtrAux)
                return true;

            // even though the fields were not all equals the delegates may still match
            // When target carries the delegate itself the 2 targets (delegates) may be different instances
            // but the delegates are logically the same
            // It may also happen that the method pointer was not jitted when creating one delegate and jitted in the other
            // if that's the case the delegates may still be equals but we need to make a more complicated check

            if (_methodPtrAux == IntPtr.Zero)
            {
                if (d._methodPtrAux != IntPtr.Zero)
                    return false; // different delegate kind

                // they are both closed over the first arg
                if (_target != d._target)
                    return false;

                // fall through method handle check
            }
            else
            {
                if (d._methodPtrAux == IntPtr.Zero)
                    return false; // different delegate kind

                // Ignore the target as it will be the delegate instance, though it may be a different one
                /*
                if (_methodPtr != d._methodPtr)
                    return false;
                */

                if (_methodPtrAux == d._methodPtrAux)
                    return true;

                // fall through method handle check
            }

            // method ptrs don't match, go down long path

            if (_methodBase is MethodInfo && d._methodBase is MethodInfo)
                return _methodBase.Equals(d._methodBase);
            else
                return InternalEqualMethodHandles(this, d);
        }

        public override int GetHashCode()
        {
            //
            // this is not right in the face of a method being jitted in one delegate and not in another
            // in that case the delegate is the same and Equals will return true but GetHashCode returns a
            // different hashcode which is not true.
            /*
            if (_methodPtrAux == IntPtr.Zero)
                return unchecked((int)((long)this._methodPtr));
            else
                return unchecked((int)((long)this._methodPtrAux));
            */
            if (_methodPtrAux == IntPtr.Zero)
                return (_target != null ? RuntimeHelpers.GetHashCode(_target) * 33 : 0) + GetType().GetHashCode();
            else
                return GetType().GetHashCode();
        }

        protected virtual MethodInfo GetMethodImpl()
        {
            if (_methodBase is MethodInfo methodInfo)
            {
                return methodInfo;
            }

            IRuntimeMethodInfo method = FindMethodHandle();
            RuntimeType? declaringType = RuntimeMethodHandle.GetDeclaringType(method);

            // need a proper declaring type instance method on a generic type
            if (declaringType.IsGenericType)
            {
                bool isStatic = (RuntimeMethodHandle.GetAttributes(method) & MethodAttributes.Static) != (MethodAttributes)0;
                if (!isStatic)
                {
                    if (_methodPtrAux == IntPtr.Zero)
                    {
                        // The target may be of a derived type that doesn't have visibility onto the
                        // target method. We don't want to call RuntimeType.GetMethodBase below with that
                        // or reflection can end up generating a MethodInfo where the ReflectedType cannot
                        // see the MethodInfo itself and that breaks an important invariant. But the
                        // target type could include important generic type information we need in order
                        // to work out what the exact instantiation of the method's declaring type is. So
                        // we'll walk up the inheritance chain (which will yield exactly instantiated
                        // types at each step) until we find the declaring type. Since the declaring type
                        // we get from the method is probably shared and those in the hierarchy we're
                        // walking won't be we compare using the generic type definition forms instead.
                        Type targetType = declaringType.GetGenericTypeDefinition();
                        Type? currentType;
                        for (currentType = _target!.GetType(); currentType != null; currentType = currentType.BaseType)
                        {
                            if (currentType.IsGenericType &&
                                currentType.GetGenericTypeDefinition() == targetType)
                            {
                                declaringType = currentType as RuntimeType;
                                break;
                            }
                        }

                        // RCWs don't need to be "strongly-typed" in which case we don't find a base type
                        // that matches the declaring type of the method. This is fine because interop needs
                        // to work with exact methods anyway so declaringType is never shared at this point.
                        // The targetType may also be an interface with a Default interface method (DIM).
                        Debug.Assert(
                            currentType != null
                            || _target.GetType().IsCOMObject
                            || targetType.IsInterface, "The class hierarchy should declare the method or be a DIM");
                    }
                    else
                    {
                        // it's an open one, need to fetch the first arg of the instantiation
                        MethodInfo invoke = this.GetType().GetMethod("Invoke")!;
                        declaringType = (RuntimeType)invoke.GetParametersAsSpan()[0].ParameterType;
                    }
                }
            }

            _methodBase = (MethodInfo)RuntimeType.GetMethodBase(declaringType, method)!;
            return (MethodInfo)_methodBase;
        }

        public object? Target => GetTarget();

        // V1 API.
        [RequiresUnreferencedCode("The target method might be removed")]
        public static Delegate? CreateDelegate(Type type, object target, string method, bool ignoreCase, bool throwOnBindFailure)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(method);

            if (type is not RuntimeType rtType)
                throw new ArgumentException(SR.Argument_MustBeRuntimeType, nameof(type));
            if (!rtType.IsDelegate())
                throw new ArgumentException(SR.Arg_MustBeDelegate, nameof(type));

            Delegate d = InternalAlloc(rtType);
            // This API existed in v1/v1.1 and only expected to create closed
            // instance delegates. Constrain the call to BindToMethodName to such
            // and don't allow relaxed signature matching (which could make the
            // choice of target method ambiguous) for backwards compatibility.
            // We never generate a closed over null delegate and this is
            // actually enforced via the check on target above, but we pass
            // NeverCloseOverNull anyway just for clarity.
            if (!d.BindToMethodName(target, (RuntimeType)target.GetType(), method,
                                    DelegateBindingFlags.InstanceMethodOnly |
                                    DelegateBindingFlags.ClosedDelegateOnly |
                                    DelegateBindingFlags.NeverCloseOverNull |
                                    (ignoreCase ? DelegateBindingFlags.CaselessMatching : 0)))
            {
                if (throwOnBindFailure)
                    throw new ArgumentException(SR.Arg_DlgtTargMeth);

                return null;
            }

            return d;
        }

        // V1 API.
        public static Delegate? CreateDelegate(Type type, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.AllMethods)] Type target, string method, bool ignoreCase, bool throwOnBindFailure)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(method);

            if (target.ContainsGenericParameters)
                throw new ArgumentException(SR.Arg_UnboundGenParam, nameof(target));
            if (type is not RuntimeType rtType)
                throw new ArgumentException(SR.Argument_MustBeRuntimeType, nameof(type));
            if (target is not RuntimeType rtTarget)
                throw new ArgumentException(SR.Argument_MustBeRuntimeType, nameof(target));

            if (!rtType.IsDelegate())
                throw new ArgumentException(SR.Arg_MustBeDelegate, nameof(type));

            Delegate d = InternalAlloc(rtType);
            // This API existed in v1/v1.1 and only expected to create open
            // static delegates. Constrain the call to BindToMethodName to such
            // and don't allow relaxed signature matching (which could make the
            // choice of target method ambiguous) for backwards compatibility.
            if (!d.BindToMethodName(null, rtTarget, method,
                                    DelegateBindingFlags.StaticMethodOnly |
                                    DelegateBindingFlags.OpenDelegateOnly |
                                    (ignoreCase ? DelegateBindingFlags.CaselessMatching : 0)))
            {
                if (throwOnBindFailure)
                    throw new ArgumentException(SR.Arg_DlgtTargMeth);

                return null;
            }

            return d;
        }

        // V1 API.
        public static Delegate? CreateDelegate(Type type, MethodInfo method, bool throwOnBindFailure)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(method);

            if (type is not RuntimeType rtType)
                throw new ArgumentException(SR.Argument_MustBeRuntimeType, nameof(type));

            if (method is not RuntimeMethodInfo rmi)
                throw new ArgumentException(SR.Argument_MustBeRuntimeMethodInfo, nameof(method));

            if (!rtType.IsDelegate())
                throw new ArgumentException(SR.Arg_MustBeDelegate, nameof(type));

            // This API existed in v1/v1.1 and only expected to create closed
            // instance delegates. Constrain the call to BindToMethodInfo to
            // open delegates only for backwards compatibility. But we'll allow
            // relaxed signature checking and open static delegates because
            // there's no ambiguity there (the caller would have to explicitly
            // pass us a static method or a method with a non-exact signature
            // and the only change in behavior from v1.1 there is that we won't
            // fail the call).
            Delegate? d = CreateDelegateInternal(
                rtType,
                rmi,
                null,
                DelegateBindingFlags.OpenDelegateOnly | DelegateBindingFlags.RelaxedSignature);

            if (d == null && throwOnBindFailure)
                throw new ArgumentException(SR.Arg_DlgtTargMeth);

            return d;
        }

        // V2 API.
        public static Delegate? CreateDelegate(Type type, object? firstArgument, MethodInfo method, bool throwOnBindFailure)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(method);

            if (type is not RuntimeType rtType)
                throw new ArgumentException(SR.Argument_MustBeRuntimeType, nameof(type));

            if (method is not RuntimeMethodInfo rmi)
                throw new ArgumentException(SR.Argument_MustBeRuntimeMethodInfo, nameof(method));

            if (!rtType.IsDelegate())
                throw new ArgumentException(SR.Arg_MustBeDelegate, nameof(type));

            // This API is new in Whidbey and allows the full range of delegate
            // flexability (open or closed delegates binding to static or
            // instance methods with relaxed signature checking. The delegate
            // can also be closed over null. There's no ambiguity with all these
            // options since the caller is providing us a specific MethodInfo.
            Delegate? d = CreateDelegateInternal(
                rtType,
                rmi,
                firstArgument,
                DelegateBindingFlags.RelaxedSignature);

            if (d == null && throwOnBindFailure)
                throw new ArgumentException(SR.Arg_DlgtTargMeth);

            return d;
        }

        //
        // internal implementation details (FCALLS and utilities)
        //

        // V2 internal API.
        internal static Delegate CreateDelegateNoSecurityCheck(Type type, object? target, RuntimeMethodHandle method)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (method.IsNullHandle())
                throw new ArgumentNullException(nameof(method));

            if (type is not RuntimeType rtType)
                throw new ArgumentException(SR.Argument_MustBeRuntimeType, nameof(type));

            if (!rtType.IsDelegate())
                throw new ArgumentException(SR.Arg_MustBeDelegate, nameof(type));

            // Initialize the method...
            Delegate d = InternalAlloc(rtType);
            // This is a new internal API added in Whidbey. Currently it's only
            // used by the dynamic method code to generate a wrapper delegate.
            // Allow flexible binding options since the target method is
            // unambiguously provided to us.

            if (!d.BindToMethodInfo(target,
                                    method.GetMethodInfo(),
                                    RuntimeMethodHandle.GetDeclaringType(method.GetMethodInfo()),
                                    DelegateBindingFlags.RelaxedSignature))
                throw new ArgumentException(SR.Arg_DlgtTargMeth);
            return d;
        }

        internal static Delegate? CreateDelegateInternal(RuntimeType rtType, RuntimeMethodInfo rtMethod, object? firstArgument, DelegateBindingFlags flags)
        {
            Delegate d = InternalAlloc(rtType);

            if (d.BindToMethodInfo(firstArgument, rtMethod, rtMethod.GetDeclaringTypeInternal(), flags))
                return d;
            else
                return null;
        }

        //
        // internal implementation details (FCALLS and utilities)
        //

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2067:ParameterDoesntMeetParameterRequirements",
            Justification = "The parameter 'methodType' is passed by ref to QCallTypeHandle")]
        private bool BindToMethodName(object? target, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.AllMethods)] RuntimeType methodType, string method, DelegateBindingFlags flags)
        {
            Delegate d = this;
            return BindToMethodName(ObjectHandleOnStack.Create(ref d), ObjectHandleOnStack.Create(ref target),
                new QCallTypeHandle(ref methodType), method, flags);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "Delegate_BindToMethodName", StringMarshalling = StringMarshalling.Utf8)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool BindToMethodName(ObjectHandleOnStack d, ObjectHandleOnStack target, QCallTypeHandle methodType, string method, DelegateBindingFlags flags);

        private bool BindToMethodInfo(object? target, IRuntimeMethodInfo method, RuntimeType methodType, DelegateBindingFlags flags)
        {
            Delegate d = this;
            bool ret = BindToMethodInfo(ObjectHandleOnStack.Create(ref d), ObjectHandleOnStack.Create(ref target),
                method.Value, new QCallTypeHandle(ref methodType), flags);
            GC.KeepAlive(method);
            return ret;
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "Delegate_BindToMethodInfo")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool BindToMethodInfo(ObjectHandleOnStack d, ObjectHandleOnStack target, RuntimeMethodHandleInternal method, QCallTypeHandle methodType, DelegateBindingFlags flags);

        private static MulticastDelegate InternalAlloc(RuntimeType type)
        {
            Debug.Assert(type.IsAssignableTo(typeof(MulticastDelegate)));
            return Unsafe.As<MulticastDelegate>(RuntimeTypeHandle.InternalAlloc(type));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe bool InternalEqualTypes(object a, object b)
        {
            if (a.GetType() == b.GetType())
                return true;
#if FEATURE_TYPEEQUIVALENCE
            MethodTable* pMTa = RuntimeHelpers.GetMethodTable(a);
            MethodTable* pMTb = RuntimeHelpers.GetMethodTable(b);

            bool ret;

            // only use QCall to check the type equivalence scenario
            if (pMTa->HasTypeEquivalence && pMTb->HasTypeEquivalence)
                ret = RuntimeHelpers.AreTypesEquivalent(pMTa, pMTb);
            else
                ret = false;

            GC.KeepAlive(a);
            GC.KeepAlive(b);

            return ret;
#else
            return false;
#endif // FEATURE_TYPEEQUIVALENCE
        }

        // Used by the ctor. Do not call directly.
        // The name of this function will appear in managed stacktraces as delegate constructor.
        private void DelegateConstruct(object target, IntPtr method)
        {
            // Via reflection you can pass in just about any value for the method.
            // We can do some basic verification up front to prevent EE exceptions.
            if (method == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(method));
            }

            Delegate _this = this;
            Construct(ObjectHandleOnStack.Create(ref _this), ObjectHandleOnStack.Create(ref target), method);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "Delegate_Construct")]
        private static partial void Construct(ObjectHandleOnStack _this, ObjectHandleOnStack target, IntPtr method);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern unsafe void* GetMulticastInvoke(MethodTable* pMT);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "Delegate_GetMulticastInvokeSlow")]
        private static unsafe partial void* GetMulticastInvokeSlow(MethodTable* pMT);

        internal unsafe IntPtr GetMulticastInvoke()
        {
            MethodTable* pMT = RuntimeHelpers.GetMethodTable(this);
            void* ptr = GetMulticastInvoke(pMT);
            if (ptr == null)
            {
                ptr = GetMulticastInvokeSlow(pMT);
                Debug.Assert(ptr != null);
                Debug.Assert(ptr == GetMulticastInvoke(pMT));
            }
            // No GC.KeepAlive() since the caller must keep instance alive to use returned pointer.
            return (IntPtr)ptr;
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern unsafe void* GetInvokeMethod(MethodTable* pMT);

        internal unsafe IntPtr GetInvokeMethod()
        {
            MethodTable* pMT = RuntimeHelpers.GetMethodTable(this);
            void* ptr = GetInvokeMethod(pMT);
            // No GC.KeepAlive() since the caller must keep instance alive to use returned pointer.
            return (IntPtr)ptr;
        }

        internal IRuntimeMethodInfo FindMethodHandle()
        {
            Delegate d = this;
            IRuntimeMethodInfo? methodInfo = null;
            FindMethodHandle(ObjectHandleOnStack.Create(ref d), ObjectHandleOnStack.Create(ref methodInfo));
            return methodInfo!;
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "Delegate_FindMethodHandle")]
        private static partial void FindMethodHandle(ObjectHandleOnStack d, ObjectHandleOnStack retMethodInfo);

        private static bool InternalEqualMethodHandles(Delegate left, Delegate right)
        {
            return InternalEqualMethodHandles(ObjectHandleOnStack.Create(ref left), ObjectHandleOnStack.Create(ref right));
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "Delegate_InternalEqualMethodHandles")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool InternalEqualMethodHandles(ObjectHandleOnStack left, ObjectHandleOnStack right);

        internal static IntPtr AdjustTarget(object target, IntPtr methodPtr)
        {
            return AdjustTarget(ObjectHandleOnStack.Create(ref target), methodPtr);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "Delegate_AdjustTarget")]
        private static partial IntPtr AdjustTarget(ObjectHandleOnStack target, IntPtr methodPtr);

        internal void InitializeVirtualCallStub(IntPtr methodPtr)
        {
            Delegate d = this;
            InitializeVirtualCallStub(ObjectHandleOnStack.Create(ref d), methodPtr);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "Delegate_InitializeVirtualCallStub")]
        private static partial void InitializeVirtualCallStub(ObjectHandleOnStack d, IntPtr methodPtr);

        internal virtual object? GetTarget()
        {
            return (_methodPtrAux == IntPtr.Zero) ? _target : null;
        }
    }

    // These flags effect the way BindToMethodInfo and BindToMethodName are allowed to bind a delegate to a target method. Their
    // values must be kept in sync with the definition in vm\comdelegate.h.
    internal enum DelegateBindingFlags
    {
        StaticMethodOnly = 0x00000001, // Can only bind to static target methods
        InstanceMethodOnly = 0x00000002, // Can only bind to instance (including virtual) methods
        OpenDelegateOnly = 0x00000004, // Only allow the creation of delegates open over the 1st argument
        ClosedDelegateOnly = 0x00000008, // Only allow the creation of delegates closed over the 1st argument
        NeverCloseOverNull = 0x00000010, // A null target will never been considered as a possible null 1st argument
        CaselessMatching = 0x00000020, // Use case insensitive lookup for methods matched by name
        RelaxedSignature = 0x00000040, // Allow relaxed signature matching (co/contra variance)
    }
}
