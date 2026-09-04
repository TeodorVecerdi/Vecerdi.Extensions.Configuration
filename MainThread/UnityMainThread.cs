using System.Threading;
using UnityEngine;

namespace Vecerdi.Extensions.Configuration.MainThread;

/// <summary>
/// Posts work to Unity's main thread through the synchronization context captured there. Captured
/// at subsystem registration, at editor load, and lazily by the sources on first use, so it works in
/// play mode, in players, and in edit mode alike.
/// </summary>
public static class UnityMainThread {
    private static SynchronizationContext? s_Context;
    private static int s_ThreadId;

    /// <summary>Whether the calling thread is the one whose context was captured.</summary>
    public static bool IsMainThread => s_Context is not null && Environment.CurrentManagedThreadId == s_ThreadId;

    /// <summary>Captures the current thread's synchronization context as the main thread. Call from the main thread.</summary>
    public static void Capture() {
        if (SynchronizationContext.Current is { } context) {
            s_Context = context;
            s_ThreadId = Environment.CurrentManagedThreadId;
        }
    }

    /// <summary>Runs <paramref name="action"/> on the main thread: inline when already there, otherwise posted to run on a later frame.</summary>
    public static void Post(Action action) {
        if (s_Context is null) {
            Capture();
        }

        if (s_Context is null || IsMainThread) {
            action();
            return;
        }

        s_Context.Post(static state => ((Action)state!)(), action);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void CaptureAtStartup() => Capture();

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void CaptureInEditor() => Capture();
#endif
}
