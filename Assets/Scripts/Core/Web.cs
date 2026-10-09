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

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int PttTouchFirst();
#else
        static int PttTouchFirst() => 0;
#endif

        /// <summary>
        /// The page is on a phone or tablet: its main pointer is a finger and it has no mouse or trackpad (the page
        /// asks the browser before the game starts). Such a device starts on the lightest graphics step, since a
        /// phone's browser tab has far less memory to spend than a desktop's.
        /// </summary>
        public static readonly bool IsTouchFirst = IsBrowser && PttTouchFirst() == 1;

        /// <summary>
        /// The page's on-screen touch controls are showing: the last thing used was a finger (set by the page through
        /// WebBridge). Fingers reach the game as a mouse; while this is on, a held thing aims a little above the
        /// finger and drops when the finger lifts (see GameController.Touch.cs), and the key hints stand aside.
        /// </summary>
        public static bool TouchActive { get; private set; }

        /// <summary>How far above the finger a held thing aims, in screen pixels (about a fingertip).</summary>
        public static float TouchAimOffset { get; private set; }

        /// <summary>How much of the screen the touch controls cover: the width of their bottom-left corner and the height of the row along the bottom (0-1).</summary>
        public static float TouchLeft { get; private set; }
        public static float TouchBottom { get; private set; }

        public static event Action TouchActiveChanged;

        public static void SetTouch(bool on, float aimOffset, float left = 0f, float bottom = 0f)
        {
            TouchAimOffset = Math.Max(0f, aimOffset);
            TouchLeft = Math.Clamp(left, 0f, 0.5f);
            TouchBottom = Math.Clamp(bottom, 0f, 0.5f);
            if (TouchActive == on) return;
            TouchActive = on;
            TouchActiveChanged?.Invoke();
        }

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
