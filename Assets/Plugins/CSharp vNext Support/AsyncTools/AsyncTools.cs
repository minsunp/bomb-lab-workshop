using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class AsyncTools {
    static Awaiter updateAwaiter, fixedAwaiter, lateAwaiter, editorAwaiter, threadPoolAwaiter = new ThreadPoolContextAwaiter();

    public static void WhereAmI(string text) {
        if (!IsMainThread()) Debug.Log($"{text}: background thread, id: {Thread.CurrentThread.ManagedThreadId}");
        else Debug.Log($"{text}: main thread, {(SynchronizationContext.Current as UnitySynchronizationContext)?.Name ?? "No context"}, frame: {Time.frameCount}");
    }

    /// Returns true if called from the Unity's main thread, and false otherwise.
    public static bool IsMainThread() => Thread.CurrentThread.ManagedThreadId == UnityScheduler.MainThreadId;

    /// Switches execution to a background thread.
    public static Awaiter ToThreadPool() => threadPoolAwaiter;

    /// Switches execution to the Update context of the main thread.
    [Obsolete("Use ToUpdate(), ToLateUpdate() or ToFixedUpdate() instead.")]
    public static Awaiter ToMainThread() => ToUpdate();

    /// Switches execution to the EditorUpdate context of the main thread.
    public static Awaiter ToEditorUpdate() => editorAwaiter ?? (editorAwaiter = new SynchronizationContextAwaiter(UnityScheduler.EditorUpdateScheduler.Context));

    /// Switches execution to the Update context of the main thread.
    public static Awaiter ToUpdate() => updateAwaiter ?? (updateAwaiter = new SynchronizationContextAwaiter(UnityScheduler.UpdateScheduler.Context));

    /// Switches execution to the LateUpdate context of the main thread.
    public static Awaiter ToLateUpdate() => lateAwaiter ?? (lateAwaiter = new SynchronizationContextAwaiter(UnityScheduler.LateUpdateScheduler.Context));

    /// Switches execution to the FixedUpdate context of the main thread.
    public static Awaiter ToFixedUpdate() => fixedAwaiter ?? (fixedAwaiter = new SynchronizationContextAwaiter(UnityScheduler.FixedUpdateScheduler.Context));

    /// Downloads a file as an array of bytes.
    public static Task<byte[]> DownloadAsBytesAsync(string url, CancellationToken cancellationToken = new CancellationToken()) =>
        Task.Factory.StartNew(delegate { using (var d = new WebClient()) return d.DownloadData(url);}, cancellationToken);

    /// Downloads a file as a string
    public static Task<string> DownloadAsStringAsync(string url, CancellationToken cancellationToken = new CancellationToken()) =>
        Task.Factory.StartNew(delegate { using (var d = new WebClient()) return d.DownloadString(url); }, cancellationToken);

    /// waits for specified number of seconds or until next physics or render update based on calling context
    public static Awaiter GetAwaiter(this int delay) => GetAwaiter((double) delay);
    public static Awaiter GetAwaiter(this float delay) => GetAwaiter((double) delay);
    public static Awaiter GetAwaiter(this double delay) {
        var context = SynchronizationContext.Current as UnitySynchronizationContext;
        if (delay<=0 && context != null) return new ContextActivationAwaiter(context);
        return new DelayAwaiter(delay); }

    /// Waits until condition is met
    public static Awaiter GetAwaiter(this Func<bool> cond) {
        var context = SynchronizationContext.Current as UnitySynchronizationContext;
        if (cond() && context!=null) return new ContextActivationAwaiter(context);
        return new ConditionAwaiter(cond); }

    /// Waits until coroutine is finished
    public static Awaiter GetAwaiter(this CustomYieldInstruction coroutine) {
        var context = SynchronizationContext.Current as UnitySynchronizationContext;
        if (!coroutine.keepWaiting && context != null) return new ContextActivationAwaiter(context);
        return new YieldInstructionAwaiter(coroutine); }

    /// waits until all the tasks are completed
    public static TaskAwaiter GetAwaiter(this IEnumerable<Task> tasks) => TaskEx.WhenAll(tasks).GetAwaiter();

    /// waits until the process exits
    public static TaskAwaiter<int> GetAwaiter(this Process process) {
        var tcs = new TaskCompletionSource<int>();
        process.EnableRaisingEvents = true;
        process.Exited += (sender, eventArgs) => tcs.TrySetResult(process.ExitCode);
        if (process.HasExited) tcs.TrySetResult(process.ExitCode);
        return tcs.Task.GetAwaiter();
    }

    /// Waits for AsyncOperation completion
    public static Awaiter GetAwaiter(this AsyncOperation asyncOp) => new AsyncOperationAwaiter(asyncOp);

    public abstract class Awaiter : INotifyCompletion {
        public abstract bool IsCompleted { get; }
        public abstract void OnCompleted(Action action);
        public Awaiter GetAwaiter() => this;
        public void GetResult() { }
    }

    class DelayAwaiter : Awaiter {
        readonly SynchronizationContext context;
        readonly double delay;
        public DelayAwaiter(double delay) { (this.delay, context) = (delay, SynchronizationContext.Current); }
        public override bool IsCompleted => delay<=0;
        public override void OnCompleted(Action action) =>
            TaskEx.Delay((int)(delay * 1000)).ContinueWith(prevTask => {
                if (context!=null) context.Post(state => action(), null); else action(); });
    }

    class ConditionAwaiter : Awaiter {
        readonly SynchronizationContext context;
        readonly Func<bool> cond;
        public ConditionAwaiter(Func<bool> cond) { (this.cond, context) = (cond, SynchronizationContext.Current); }
        public override bool IsCompleted => cond();
        public override void OnCompleted(Action action) => action();
    }


    class ContextActivationAwaiter : Awaiter {
        readonly UnitySynchronizationContext context;
        Action continuation;
        public ContextActivationAwaiter(UnitySynchronizationContext context) { this.context = context; }
        public override bool IsCompleted => false;
        public override void OnCompleted(Action action) {
            continuation = action;
            context.Activated += ContextActivationEventHandler;
        }

        void ContextActivationEventHandler(object sender, EventArgs eventArgs) {
            context.Activated -= ContextActivationEventHandler;
            context.Post(state => continuation(), null);
        }
    }

    class SynchronizationContextAwaiter : Awaiter {
        readonly UnitySynchronizationContext context;
        public SynchronizationContextAwaiter(UnitySynchronizationContext context) { this.context = context; }
        public override bool IsCompleted => context == null || context == SynchronizationContext.Current;
        public override void OnCompleted(Action action) => context.Post(state => action(), null);
    }

    class ThreadPoolContextAwaiter : Awaiter {
        public override bool IsCompleted => IsMainThread() == false;
        public override void OnCompleted(Action action) => ThreadPool.QueueUserWorkItem(state => action(), null);
    }

    class YieldInstructionAwaiter : Awaiter {
        readonly CustomYieldInstruction coroutine;
        public YieldInstructionAwaiter(CustomYieldInstruction coroutine) { this.coroutine = coroutine; }
        public override bool IsCompleted => coroutine == null || !coroutine.keepWaiting;
        public override void OnCompleted(Action action) => Task.Factory.StartNew(
            async () => { while (coroutine.keepWaiting) await 0; action(); },
            CancellationToken.None, TaskCreationOptions.None, UnityScheduler.UpdateScheduler);
    }

    class AsyncOperationAwaiter : Awaiter {
        readonly AsyncOperation asyncOp;
        public AsyncOperationAwaiter(AsyncOperation asyncOp) { this.asyncOp = asyncOp; }
        public override bool IsCompleted => asyncOp.isDone;
        public override void OnCompleted(Action action) => Task.Factory.StartNew(
            async () => { while (asyncOp.isDone == false) await 0; action(); },
            CancellationToken.None, TaskCreationOptions.None, UnityScheduler.UpdateScheduler);
    }
}
