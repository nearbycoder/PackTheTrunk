using System;
using System.Threading.Tasks;

namespace PackTheTrunk
{
    /// <summary>
    /// What the browser build does differently. A browser page has no worker threads (Unity's web player is
    /// single-threaded without SharedArrayBuffer, which GitHub Pages can't enable), no Quit, and no window modes
    /// or monitor resolutions of its own.
    /// </summary>
    public static class Web
    {
        /// <summary>Running as the browser build (not in the editor).</summary>
        public static readonly bool IsBrowser =
#if UNITY_WEBGL && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>
        /// <see cref="Task.Run{T}(Func{T})"/> on desktop. In the browser the work runs right away on the main thread
        /// (a short hitch rather than a task that never finishes), and a failure ends up in the task as it would.
        /// </summary>
        public static Task<T> Run<T>(Func<T> work)
        {
            if (!IsBrowser) return Task.Run(work);
            try { return Task.FromResult(work()); }
            catch (Exception e) { return Task.FromException<T>(e); }
        }

        public static Task Run(Action work)
        {
            if (!IsBrowser) return Task.Run(work);
            try
            {
                work();
                return Task.CompletedTask;
            }
            catch (Exception e) { return Task.FromException(e); }
        }
    }
}
