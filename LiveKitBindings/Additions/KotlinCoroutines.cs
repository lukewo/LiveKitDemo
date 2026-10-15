using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Runtime;
using Kotlin.Coroutines;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;

namespace LiveKit.Xamarin
{
    /// <summary>
    /// Bridges Kotlin suspend functions to .NET Tasks.
    ///
    /// A Kotlin <c>suspend fun foo(a: A): R</c> compiles to the Java method
    /// <c>Object foo(A a, Continuation&lt;? super R&gt; c)</c>. It either returns R
    /// immediately, or returns the COROUTINE_SUSPENDED marker and later calls
    /// <c>c.resumeWith(result)</c>, where result is R or a <c>kotlin.Result$Failure</c>.
    /// </summary>
    public static class Suspend
    {
        static Java.Lang.Object suspendedMarker;

        /// <summary>kotlin.coroutines.intrinsics.IntrinsicsKt.getCOROUTINE_SUSPENDED()</summary>
        static Java.Lang.Object SuspendedMarker
        {
            get
            {
                if (suspendedMarker == null)
                {
                    IntPtr cls = JNIEnv.FindClass("kotlin/coroutines/intrinsics/IntrinsicsKt");
                    try
                    {
                        IntPtr mid = JNIEnv.GetStaticMethodID(cls, "getCOROUTINE_SUSPENDED", "()Ljava/lang/Object;");
                        IntPtr h = JNIEnv.CallStaticObjectMethod(cls, mid);
                        suspendedMarker = Java.Lang.Object.GetObject<Java.Lang.Object>(h, JniHandleOwnership.TransferLocalRef);
                    }
                    finally
                    {
                        JNIEnv.DeleteGlobalRef(cls);
                    }
                }
                return suspendedMarker;
            }
        }

        /// <summary>
        /// Calls a suspend function and returns a Task for its result.
        /// <paramref name="call"/> receives the Continuation to pass as the last argument.
        /// Cancelling <paramref name="cancellationToken"/> cancels the coroutine's Job.
        /// </summary>
        public static Task<Java.Lang.Object> InvokeAsync(
            Func<IContinuation, Java.Lang.Object> call,
            CancellationToken cancellationToken = default)
        {
            var continuation = new TaskContinuation();
            CancellationTokenRegistration reg = default;
            if (cancellationToken.CanBeCanceled)
                reg = cancellationToken.Register(continuation.Cancel);

            try
            {
                Java.Lang.Object result = call(continuation);
                if (!IsSame(result, SuspendedMarker))
                    continuation.Complete(result);   // returned without suspending
            }
            catch (Exception ex)
            {
                continuation.Fail(ex);
            }

            continuation.Task.ContinueWith(_ => reg.Dispose(), TaskScheduler.Default);
            return continuation.Task;
        }

        /// <summary>Same as <see cref="InvokeAsync"/> for functions returning Unit.</summary>
        public static Task InvokeUnitAsync(
            Func<IContinuation, Java.Lang.Object> call,
            CancellationToken cancellationToken = default)
            => InvokeAsync(call, cancellationToken);

        internal static bool IsSame(Java.Lang.Object a, Java.Lang.Object b)
        {
            if (a == null || b == null) return a == null && b == null;
            return JNIEnv.IsSameObject(a.Handle, b.Handle);
        }

        /// <summary>
        /// Unwraps a resumeWith() value. Kotlin's Result is an inline class: a success
        /// is the raw value, a failure is an instance of kotlin.Result$Failure holding
        /// the Throwable in its "exception" field.
        /// </summary>
        internal static Java.Lang.Throwable GetFailure(Java.Lang.Object result)
        {
            if (result == null) return null;
            IntPtr cls = JNIEnv.GetObjectClass(result.Handle);
            try
            {
                IntPtr failureCls = JNIEnv.FindClass("kotlin/Result$Failure");
                try
                {
                    if (!JNIEnv.IsInstanceOf(result.Handle, failureCls)) return null;
                    IntPtr fid = JNIEnv.GetFieldID(failureCls, "exception", "Ljava/lang/Throwable;");
                    IntPtr h = JNIEnv.GetObjectField(result.Handle, fid);
                    return Java.Lang.Object.GetObject<Java.Lang.Throwable>(h, JniHandleOwnership.TransferLocalRef);
                }
                finally
                {
                    JNIEnv.DeleteGlobalRef(failureCls);
                }
            }
            finally
            {
                JNIEnv.DeleteLocalRef(cls);
            }
        }

        /// <summary>A CompletableJob usable as a CoroutineContext, so callers can cancel.</summary>
        internal static ICompletableJob NewJob() => JobKt.Job(null);
    }

    /// <summary>Continuation that completes a TaskCompletionSource.</summary>
    sealed class TaskContinuation : Java.Lang.Object, IContinuation
    {
        readonly TaskCompletionSource<Java.Lang.Object> tcs =
            new TaskCompletionSource<Java.Lang.Object>(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly ICompletableJob job = Suspend.NewJob();

        public Task<Java.Lang.Object> Task => tcs.Task;

        // The Job is the coroutine context: cancelling it cancels the suspend call.
        public ICoroutineContext Context => job.JavaCast<ICoroutineContext>();

        public void ResumeWith(Java.Lang.Object result)
        {
            Java.Lang.Throwable failure = Suspend.GetFailure(result);
            if (failure == null)
                Complete(result);
            else if (failure is Java.Util.Concurrent.CancellationException)
                tcs.TrySetCanceled();
            else
                Fail(failure);
        }

        public void Complete(Java.Lang.Object value)
        {
            tcs.TrySetResult(value);
            job.Complete();
        }

        public void Fail(Exception ex)
        {
            tcs.TrySetException(ex);
            job.Cancel(null);
        }

        public void Cancel()
        {
            job.Cancel(null);
            tcs.TrySetCanceled();
        }
    }

    /// <summary>
    /// Collects a Kotlin Flow on a background coroutine and forwards each value
    /// to a callback. Dispose to stop collecting.
    /// </summary>
    public sealed class FlowSubscription : IDisposable
    {
        readonly CancellationTokenSource cts = new CancellationTokenSource();

        /// <summary>Completes when the flow ends, errors, or is disposed.</summary>
        public Task Completion { get; }

        FlowSubscription(IFlow flow, Action<Java.Lang.Object> onEach)
        {
            var collector = new CallbackCollector(onEach);
            Completion = Suspend.InvokeUnitAsync(c => flow.Collect(collector, c), cts.Token);
        }

        public static FlowSubscription Collect(IFlow flow, Action<Java.Lang.Object> onEach)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (onEach == null) throw new ArgumentNullException(nameof(onEach));
            return new FlowSubscription(flow, onEach);
        }

        public void Dispose()
        {
            if (!cts.IsCancellationRequested) cts.Cancel();
        }

        sealed class CallbackCollector : Java.Lang.Object, IFlowCollector
        {
            readonly Action<Java.Lang.Object> onEach;
            public CallbackCollector(Action<Java.Lang.Object> onEach) => this.onEach = onEach;

            // suspend fun emit(value: T). Returning Unit means "done, didn't suspend".
            public Java.Lang.Object Emit(Java.Lang.Object value, IContinuation continuation)
            {
                onEach(value);
                return Kotlin.Unit.Instance;
            }
        }
    }
}
