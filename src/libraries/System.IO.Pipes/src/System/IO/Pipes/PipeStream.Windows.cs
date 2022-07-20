// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace System.IO.Pipes
{
    public abstract partial class PipeStream : Stream
    {
        internal const bool CheckOperationsRequiresSetHandle = true;
        internal ThreadPoolBoundHandle? _threadPoolBinding;
        private ReadWriteValueTaskSource? _reusableReadValueTaskSource; // reusable ReadWriteValueTaskSource for read operations, that is currently NOT being used
        private ReadWriteValueTaskSource? _reusableWriteValueTaskSource; // reusable ReadWriteValueTaskSource for write operations, that is currently NOT being used

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_isAsync)
            {
                return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
            }

            ValidateBufferArguments(buffer, offset, count);
            if (!CanRead)
            {
                throw Error.GetReadNotSupported();
            }
            CheckReadOperations();

            return ReadCore(new Span<byte>(buffer, offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (_isAsync)
            {
                return base.Read(buffer);
            }

            if (!CanRead)
            {
                throw Error.GetReadNotSupported();
            }
            CheckReadOperations();

            return ReadCore(buffer);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (!CanRead)
            {
                throw Error.GetReadNotSupported();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<int>(cancellationToken);
            }

            CheckReadOperations();

            if (count == 0)
            {
                UpdateMessageCompletion(false);
                return Task.FromResult(0);
            }

            return _isAsync ?
                ReadAsyncCore(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask() :
                AsyncOverSyncRead(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!CanRead)
            {
                throw Error.GetReadNotSupported();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<int>(cancellationToken);
            }

            CheckReadOperations();

            if (buffer.Length == 0)
            {
                UpdateMessageCompletion(false);
                return new ValueTask<int>(0);
            }

            return _isAsync ?
                ReadAsyncCore(buffer, cancellationToken) :
                AsyncOverSyncRead(buffer, cancellationToken);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            if (_isAsync)
                return TaskToApm.Begin(ReadAsync(buffer, offset, count, CancellationToken.None), callback, state);
            else
                return base.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (_isAsync)
                return TaskToApm.End<int>(asyncResult);
            else
                return base.EndRead(asyncResult);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_isAsync)
            {
                WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
                return;
            }

            ValidateBufferArguments(buffer, offset, count);
            if (!CanWrite)
            {
                throw Error.GetWriteNotSupported();
            }
            CheckWriteOperations();

            WriteCore(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_isAsync)
            {
                base.Write(buffer);
                return;
            }

            if (!CanWrite)
            {
                throw Error.GetWriteNotSupported();
            }
            CheckWriteOperations();

            WriteCore(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (!CanWrite)
            {
                throw Error.GetWriteNotSupported();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<int>(cancellationToken);
            }

            CheckWriteOperations();

            return
                count == 0 ? Task.CompletedTask :
                _isAsync ? WriteAsyncCore(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask() :
                AsyncOverSyncWrite(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!CanWrite)
            {
                throw Error.GetWriteNotSupported();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled(cancellationToken);
            }

            CheckWriteOperations();

            return
                buffer.Length == 0 ? default :
                _isAsync ? WriteAsyncCore(buffer, cancellationToken) :
                AsyncOverSyncWrite(buffer, cancellationToken);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            if (_isAsync)
                return TaskToApm.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), callback, state);
            else
                return base.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (_isAsync)
                TaskToApm.End(asyncResult);
            else
                base.EndWrite(asyncResult);
        }

        /// <summary>Initiates an async-over-sync read for a pipe opened for non-overlapped I/O.</summary>
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        private async ValueTask<int> AsyncOverSyncRead(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            // Create the work item state object.  This is used to pass around state through various APIs,
            // while also serving double duty as the work item used to queue the operation to the thread pool.
            var workItem = new SyncAsyncWorkItem();

            // Queue the work to the thread pool.  This is implemented as a custom awaiter that queues the
            // awaiter itself to the thread pool.
            await workItem;

            // Register for cancellation.
            using (workItem.RegisterCancellation(cancellationToken))
            {
                try
                {
                    // Perform the read.
                    return ReadCore(buffer.Span);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // If the read fails because of cancellation, it will have been a Win32 error code
                    // that ReadCore translated into an OperationCanceledException without a stored
                    // CancellationToken.  We want to ensure the token is stored.
                    throw new OperationCanceledException(cancellationToken);
                }
                finally
                {
                    // Prior to calling Dispose on the CancellationTokenRegistration, we need to tell
                    // the registration callback to exit if it's currently running; otherwise, we could deadlock.
                    workItem.ContinueTryingToCancel = false;
                }
            }
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private async ValueTask AsyncOverSyncWrite(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            // Create the work item state object.  This is used to pass around state through various APIs,
            // while also serving double duty as the work item used to queue the operation to the thread pool.
            var workItem = new SyncAsyncWorkItem();

            // Queue the work to the thread pool.  This is implemented as a custom awaiter that queues the
            // awaiter itself to the thread pool.
            await workItem;

            // Register for cancellation.
            using (workItem.RegisterCancellation(cancellationToken))
            {
                try
                {
                    // Perform the write.
                    WriteCore(buffer.Span);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // If the write fails because of cancellation, it will have been a Win32 error code
                    // that WriteCore translated into an OperationCanceledException without a stored
                    // CancellationToken.  We want to ensure the token is stored.
                    throw new OperationCanceledException(cancellationToken);
                }
                finally
                {
                    // Prior to calling Dispose on the CancellationTokenRegistration, we need to tell
                    // the registration callback to exit if it's currently running; otherwise, we could deadlock.
                    workItem.ContinueTryingToCancel = false;
                }
            }
        }

        /// <summary>
        /// State object used for implementing async pipe operations as async-over-sync
        /// (queueing a work item to invoke a synchronous operation).
        /// </summary>
        private protected sealed class SyncAsyncWorkItem : IThreadPoolWorkItem, ICriticalNotifyCompletion
        {
            /// <summary>A thread handle for the current OS thread.</summary>
            /// <remarks>This is lazily-initialized for the current OS thread. We rely on finalization to clean up after it when the thread goes away.</remarks>
            [ThreadStatic]
            private static SafeThreadHandle? t_currentThreadHandle;

            /// <summary>The OS handle of the thread performing the I/O.</summary>
            public SafeThreadHandle? ThreadHandle;

            /// <summary>Whether the call to CancellationToken.UnsafeRegister completed.</summary>
            public volatile bool FinishedCancellationRegistration;
            /// <summary>Whether the I/O operation has finished (successfully or unsuccessfully) and is requesting cancellation attempts stop.</summary>
            public volatile bool ContinueTryingToCancel = true;
            /// <summary>The Action continuation object handed to this instance when used as an awaiter to scheduler work to the thread pool.</summary>
            private Action? _continuation;

            // awaitable / awaiter implementation that enables this instance to be awaited in order to queue
            // execution to the thread pool.  This is purely a cost-saving measure in order to reuse this
            // object we already need as the queued work item.
            public SyncAsyncWorkItem GetAwaiter() => this;
            public bool IsCompleted => false;
            public void GetResult() { }
            public void OnCompleted(Action continuation) => throw new NotSupportedException();
            public void UnsafeOnCompleted(Action continuation)
            {
                Debug.Assert(_continuation is null);
                _continuation = continuation;
                ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);
            }
            void IThreadPoolWorkItem.Execute() => _continuation!();

            /// <summary>Registers for cancellation with the specified token.</summary>
            /// <remarks>Upon cancellation being requested, the implementation will attempt to CancelSynchronousIo for the thread calling RegisterCancellation.</remarks>
            public CancellationTokenRegistration RegisterCancellation(CancellationToken cancellationToken)
            {
                // If the token can't be canceled, there's nothing to register.
                if (!cancellationToken.CanBeCanceled)
                {
                    return default;
                }

                // Get a handle for the current thread. This is stored and used to cancel the I/O on this thread
                // in response to the cancellation token having cancellation requested.  If the handle is invalid,
                // which could happen if OpenThread fails, skip attempts at cancellation. The handle needs to be
                // opened with THREAD_TERMINATE in order to be able to call CancelSynchronousIo.
                ThreadHandle = t_currentThreadHandle ??= Interop.Kernel32.OpenThread(Interop.Kernel32.THREAD_TERMINATE, bInheritHandle: false, Interop.Kernel32.GetCurrentThreadId());
                if (ThreadHandle.IsInvalid)
                {
                    return default;
                }

                // Register with the token.
                CancellationTokenRegistration reg = cancellationToken.UnsafeRegister(static s =>
                {
                    var state = (SyncAsyncWorkItem)s!;

                    // If cancellation was already requested when UnsafeRegister was called, it'll invoke
                    // the callback immediately.  If we allowed that to loop until cancellation was successful,
                    // we'd deadlock, as we'd never perform the very I/O it was waiting for.  As such, if
                    // the callback is invoked prior to be ready for it, we ignore the callback.
                    if (!state.FinishedCancellationRegistration)
                    {
                        return;
                    }

                    // Cancel the I/O.  If the cancellation happens too early and we haven't yet initiated
                    // the synchronous operation, CancelSynchronousIo will fail with ERROR_NOT_FOUND, and
                    // we'll loop to try again.
                    SpinWait sw = default;
                    while (state.ContinueTryingToCancel)
                    {
                        if (Interop.Kernel32.CancelSynchronousIo(state.ThreadHandle!))
                        {
                            // Successfully canceled I/O.
                            break;
                        }

                        if (Marshal.GetLastPInvokeError() != Interop.Errors.ERROR_NOT_FOUND)
                        {
                            // Failed to cancel even though there may have been I/O to cancel.
                            // Attempting to keep trying could result in an infinite loop, so
                            // give up on trying to cancel.
                            break;
                        }

                        sw.SpinOnce();
                    }
                }, this);

                // Now that we've registered with the token, tell the callback it's safe to enter
                // its cancellation loop if the callback is invoked.
                FinishedCancellationRegistration = true;

                // And now since cancellation may have been requested and we may have suppressed it
                // until the previous line, check to see if cancellation has now been requested, and
                // if it has, stop any callback, remove the registration, and throw.
                if (cancellationToken.IsCancellationRequested)
                {
                    ContinueTryingToCancel = false;
                    reg.Dispose();
                    throw new OperationCanceledException(cancellationToken);
                }

                // Return the registration.  Now and moving forward, a cancellation request could come in,
                // and the callback will end up spinning until we reach the actual I/O.
                return reg;
            }
        }

        internal static string GetPipePath(string serverName, string pipeName)
        {
            string normalizedPipePath = Path.GetFullPath(@"\\" + serverName + @"\pipe\" + pipeName);
            if (string.Equals(normalizedPipePath, @"\\.\pipe\" + AnonymousPipeName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentOutOfRangeException(nameof(pipeName), SR.ArgumentOutOfRange_AnonymousReserved);
            }
            return normalizedPipePath;
        }

        /// <summary>Throws an exception if the supplied handle does not represent a valid pipe.</summary>
        /// <param name="safePipeHandle">The handle to validate.</param>
        private protected static void ValidateHandleIsPipe(SafePipeHandle safePipeHandle)
        {
            // Check that this handle is infact a handle to a pipe.
            if (Interop.Kernel32.GetFileType(safePipeHandle) != Interop.Kernel32.FileTypes.FILE_TYPE_PIPE)
            {
                throw new IOException(SR.IO_InvalidPipeHandle);
            }
        }

        /// <summary>Initializes the handle to be used asynchronously.</summary>
        /// <param name="handle">The handle.</param>
        private void InitializeAsyncHandle(SafePipeHandle handle)
        {
            // If the handle is of async type, bind the handle to the ThreadPool so that we can use
            // the async operations (it's needed so that our native callbacks get called).
            _threadPoolBinding = ThreadPoolBoundHandle.BindHandle(handle);
        }

        internal virtual void TryToReuse(PipeValueTaskSource source)
        {
            source._source.Reset();

            if (source is ReadWriteValueTaskSource readWriteSource)
            {
                ref ReadWriteValueTaskSource? field = ref readWriteSource._isWrite ? ref _reusableWriteValueTaskSource : ref _reusableReadValueTaskSource;
                if (Interlocked.CompareExchange(ref field, readWriteSource, null) is not null)
                {
                    source._preallocatedOverlapped.Dispose();
                }
            }
        }

        private void DisposeCore(bool disposing)
        {
            if (disposing)
            {
                _threadPoolBinding?.Dispose();
                Interlocked.Exchange(ref _reusableReadValueTaskSource, null)?.Dispose();
                Interlocked.Exchange(ref _reusableWriteValueTaskSource, null)?.Dispose();
            }
        }

        private unsafe int ReadCore(Span<byte> buffer)
        {
            DebugAssertHandleValid(_handle!);
            Debug.Assert(!_isAsync);

            if (buffer.Length == 0)
            {
                return 0;
            }

            fixed (byte* p = &MemoryMarshal.GetReference(buffer))
            {
                int bytesRead = 0;
                if (Interop.Kernel32.ReadFile(_handle!, p, buffer.Length, out bytesRead, IntPtr.Zero) != 0)
                {
                    _isMessageComplete = true;
                    return bytesRead;
                }
                else
                {
                    int errorCode = Marshal.GetLastPInvokeError();
                    _isMessageComplete = errorCode != Interop.Errors.ERROR_MORE_DATA;
                    switch (errorCode)
                    {
                        case Interop.Errors.ERROR_MORE_DATA:
                            return bytesRead;

                        case Interop.Errors.ERROR_BROKEN_PIPE:
                        case Interop.Errors.ERROR_PIPE_NOT_CONNECTED:
                            State = PipeState.Broken;
                            return 0;

                        default:
                            throw Win32Marshal.GetExceptionForWin32Error(errorCode, string.Empty);
                    }
                }
            }
        }

        private unsafe ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            Debug.Assert(_isAsync);

            ReadWriteValueTaskSource vts = Interlocked.Exchange(ref _reusableReadValueTaskSource, null) ?? new ReadWriteValueTaskSource(this, isWrite: false);
            try
            {
                vts.PrepareForOperation(buffer);
                Debug.Assert(vts._memoryHandle.Pointer != null);

                // Queue an async ReadFile operation.
                if (Interop.Kernel32.ReadFile(_handle!, (byte*)vts._memoryHandle.Pointer, buffer.Length, IntPtr.Zero, vts._overlapped) == 0)
                {
                    // The operation failed, or it's pending.
                    int errorCode = Marshal.GetLastPInvokeError();
                    switch (errorCode)
                    {
                        case Interop.Errors.ERROR_IO_PENDING:
                            // Common case: IO was initiated, completion will be handled by callback.
                            // Register for cancellation now that the operation has been initiated.
                            vts.RegisterForCancellation(cancellationToken);
                            break;

                        case Interop.Errors.ERROR_MORE_DATA:
                            // The operation is completing asynchronously but there's nothing to cancel.
                            break;

                        // One side has closed its handle or server disconnected.
                        // Set the state to Broken and do some cleanup work
                        case Interop.Errors.ERROR_BROKEN_PIPE:
                        case Interop.Errors.ERROR_PIPE_NOT_CONNECTED:
                            State = PipeState.Broken;
                            vts._overlapped->InternalLow = IntPtr.Zero;
                            vts.Dispose();
                            UpdateMessageCompletion(true);
                            return new ValueTask<int>(0);

                        default:
                            // Error. Callback will not be called.
                            vts.Dispose();
                            return ValueTask.FromException<int>(Win32Marshal.GetExceptionForWin32Error(errorCode));
                    }
                }
            }
            catch
            {
                vts.Dispose();
                throw;
            }

            vts.FinishedScheduling();
            return new ValueTask<int>(vts, vts.Version);
        }

        private unsafe void WriteCore(ReadOnlySpan<byte> buffer)
        {
            DebugAssertHandleValid(_handle!);
            Debug.Assert(!_isAsync);

            if (buffer.Length == 0)
            {
                return;
            }

            fixed (byte* p = &MemoryMarshal.GetReference(buffer))
            {
                int bytesWritten = 0;
                if (Interop.Kernel32.WriteFile(_handle!, p, buffer.Length, out bytesWritten, IntPtr.Zero) == 0)
                {
                    throw WinIOError(Marshal.GetLastPInvokeError());
                }
            }
        }

        private unsafe ValueTask WriteAsyncCore(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            Debug.Assert(_isAsync);

            ReadWriteValueTaskSource vts = Interlocked.Exchange(ref _reusableWriteValueTaskSource, null) ?? new ReadWriteValueTaskSource(this, isWrite: true);
            try
            {
                vts.PrepareForOperation(buffer);
                Debug.Assert(vts._memoryHandle.Pointer != null);

                // Queue an async WriteFile operation.
                if (Interop.Kernel32.WriteFile(_handle!, (byte*)vts._memoryHandle.Pointer, buffer.Length, IntPtr.Zero, vts._overlapped) == 0)
                {
                    // The operation failed, or it's pending.
                    int errorCode = Marshal.GetLastPInvokeError();
                    switch (errorCode)
                    {
                        case Interop.Errors.ERROR_IO_PENDING:
                            // Common case: IO was initiated, completion will be handled by callback.
                            // Register for cancellation now that the operation has been initiated.
                            vts.RegisterForCancellation(cancellationToken);
                            break;

                        default:
                            // Error. Callback will not be invoked.
                            vts.Dispose();
                            return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(WinIOError(errorCode)));
                    }
                }
            }
            catch
            {
                vts.Dispose();
                throw;
            }

            // Completion handled by callback.
            vts.FinishedScheduling();
            return new ValueTask(vts, vts.Version);
        }

        // Blocks until the other end of the pipe has read in all written buffer.
        [SupportedOSPlatform("windows")]
        public void WaitForPipeDrain()
        {
            CheckWriteOperations();
            if (!CanWrite)
            {
                throw Error.GetWriteNotSupported();
            }

            // Block until other end of the pipe has read everything.
            if (!Interop.Kernel32.FlushFileBuffers(_handle!))
            {
                throw WinIOError(Marshal.GetLastPInvokeError());
            }
        }

        // Gets the transmission mode for the pipe.  This is virtual so that subclassing types can
        // override this in cases where only one mode is legal (such as anonymous pipes)
        public virtual unsafe PipeTransmissionMode TransmissionMode
        {
            get
            {
                CheckPipePropertyOperations();

                if (_isFromExistingHandle)
                {
                    uint pipeFlags;
                    if (!Interop.Kernel32.GetNamedPipeInfo(_handle!, &pipeFlags, null, null, null))
                    {
                        throw WinIOError(Marshal.GetLastPInvokeError());
                    }
                    if ((pipeFlags & Interop.Kernel32.PipeOptions.PIPE_TYPE_MESSAGE) != 0)
                    {
                        return PipeTransmissionMode.Message;
                    }
                    else
                    {
                        return PipeTransmissionMode.Byte;
                    }
                }
                else
                {
                    return _transmissionMode;
                }
            }
        }

        // Gets the buffer size in the inbound direction for the pipe. This checks if pipe has read
        // access. If that passes, call to GetNamedPipeInfo will succeed.
        public virtual unsafe int InBufferSize
        {
            get
            {
                CheckPipePropertyOperations();
                if (!CanRead)
                {
                    throw new NotSupportedException(SR.NotSupported_UnreadableStream);
                }

                uint inBufferSize;
                if (!Interop.Kernel32.GetNamedPipeInfo(_handle!, null, null, &inBufferSize, null))
                {
                    throw WinIOError(Marshal.GetLastPInvokeError());
                }

                return (int)inBufferSize;
            }
        }

        // Gets the buffer size in the outbound direction for the pipe. This uses cached version
        // if it's an outbound only pipe because GetNamedPipeInfo requires read access to the pipe.
        // However, returning cached is good fallback, especially if user specified a value in
        // the ctor.
        public virtual unsafe int OutBufferSize
        {
            get
            {
                CheckPipePropertyOperations();
                if (!CanWrite)
                {
                    throw new NotSupportedException(SR.NotSupported_UnwritableStream);
                }

                uint outBufferSize;

                // Use cached value if direction is out; otherwise get fresh version
                if (_pipeDirection == PipeDirection.Out)
                {
                    outBufferSize = _outBufferSize;
                }
                else if (!Interop.Kernel32.GetNamedPipeInfo(_handle!, null, &outBufferSize, null, null))
                {
                    throw WinIOError(Marshal.GetLastPInvokeError());
                }

                return (int)outBufferSize;
            }
        }

        public virtual PipeTransmissionMode ReadMode
        {
            get
            {
                CheckPipePropertyOperations();

                // get fresh value if it could be stale
                if (_isFromExistingHandle || IsHandleExposed)
                {
                    UpdateReadMode();
                }
                return _readMode;
            }
            set
            {
                // Nothing fancy here.  This is just a wrapper around the Win32 API.  Note, that NamedPipeServerStream
                // and the AnonymousPipeStreams override this.

                CheckPipePropertyOperations();
                if (value < PipeTransmissionMode.Byte || value > PipeTransmissionMode.Message)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), SR.ArgumentOutOfRange_TransmissionModeByteOrMsg);
                }

                unsafe
                {
                    int pipeReadType = (int)value << 1;
                    if (!Interop.Kernel32.SetNamedPipeHandleState(_handle!, &pipeReadType, IntPtr.Zero, IntPtr.Zero))
                    {
                        throw WinIOError(Marshal.GetLastPInvokeError());
                    }
                    else
                    {
                        _readMode = value;
                    }
                }
            }
        }

        internal static unsafe Interop.Kernel32.SECURITY_ATTRIBUTES GetSecAttrs(HandleInheritability inheritability)
        {
            Interop.Kernel32.SECURITY_ATTRIBUTES secAttrs = new Interop.Kernel32.SECURITY_ATTRIBUTES
            {
                nLength = (uint)sizeof(Interop.Kernel32.SECURITY_ATTRIBUTES),
                bInheritHandle = ((inheritability & HandleInheritability.Inheritable) != 0) ? Interop.BOOL.TRUE : Interop.BOOL.FALSE
            };

            return secAttrs;
        }

        internal static unsafe Interop.Kernel32.SECURITY_ATTRIBUTES GetSecAttrs(HandleInheritability inheritability, PipeSecurity? pipeSecurity, ref GCHandle pinningHandle)
        {
            Interop.Kernel32.SECURITY_ATTRIBUTES secAttrs = GetSecAttrs(inheritability);

            if (pipeSecurity != null)
            {
                byte[] securityDescriptor = pipeSecurity.GetSecurityDescriptorBinaryForm();
                pinningHandle = GCHandle.Alloc(securityDescriptor, GCHandleType.Pinned);
                fixed (byte* pSecurityDescriptor = securityDescriptor)
                {
                    secAttrs.lpSecurityDescriptor = (IntPtr)pSecurityDescriptor;
                }
            }

            return secAttrs;
        }



        /// <summary>
        /// Determine pipe read mode from Win32
        /// </summary>
        private unsafe void UpdateReadMode()
        {
            uint flags;
            if (!Interop.Kernel32.GetNamedPipeHandleStateW(SafePipeHandle, &flags, null, null, null, null, 0))
            {
                throw WinIOError(Marshal.GetLastPInvokeError());
            }

            if ((flags & Interop.Kernel32.PipeOptions.PIPE_READMODE_MESSAGE) != 0)
            {
                _readMode = PipeTransmissionMode.Message;
            }
            else
            {
                _readMode = PipeTransmissionMode.Byte;
            }
        }

        /// <summary>
        /// Filter out all pipe related errors and do some cleanup before calling Error.WinIOError.
        /// </summary>
        /// <param name="errorCode"></param>
        internal Exception WinIOError(int errorCode)
        {
            switch (errorCode)
            {
                case Interop.Errors.ERROR_BROKEN_PIPE:
                case Interop.Errors.ERROR_PIPE_NOT_CONNECTED:
                case Interop.Errors.ERROR_NO_DATA:
                    // Other side has broken the connection
                    _state = PipeState.Broken;
                    return new IOException(SR.IO_PipeBroken, Win32Marshal.MakeHRFromErrorCode(errorCode));

                case Interop.Errors.ERROR_HANDLE_EOF:
                    return Error.GetEndOfFile();

                case Interop.Errors.ERROR_INVALID_HANDLE:
                    // For invalid handles, detect the error and mark our handle
                    // as invalid to give slightly better error messages.  Also
                    // help ensure we avoid handle recycling bugs.
                    _handle!.SetHandleAsInvalid();
                    _state = PipeState.Broken;
                    break;
            }

            return Win32Marshal.GetExceptionForWin32Error(errorCode);
        }
    }
}
