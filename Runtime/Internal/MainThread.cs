using System;
using System.Threading;
using UnityEngine;

namespace EncoreKit.Internal
{
    // Native callbacks arrive on native threads; Unity APIs are only safe on the main thread.
    internal static class MainThread
    {
        private static SynchronizationContext _context;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Capture() => CaptureCurrent();

        // Also called from Encore.Configure, which runs on the main thread, in case load-time capture saw no context.
        internal static void CaptureCurrent()
        {
            var current = SynchronizationContext.Current;
            if (current != null) _context = current;
        }

        // Tests only: a context the test pumps itself. Null clears it.
        internal static void UseForTests(SynchronizationContext context) => _context = context;

        internal static void Post(Action action)
        {
            var context = _context;
            if (context == null)
            {
                Log.Warn("No Unity main-thread context captured; running the callback in place.");
                Run(action);
                return;
            }
            context.Post(_ => Run(action), null);
        }

        // Calls each subscriber on its own, so a throw in one game handler neither reaches the SDK nor skips the rest.
        internal static void Raise<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                Run(() => handler(value));
            }
        }

        // A game exception must never unwind into native frames. It is the game's own error, so it is logged at every level.
        private static void Run(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
