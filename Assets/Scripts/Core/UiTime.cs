using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Clock for menus, camera and audio fades: ignores the pause time scale, but follows
    /// <see cref="Time.captureDeltaTime"/> so frame-locked video capture stays in step.
    /// </summary>
    public static class UiTime
    {
        public static float Delta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        public static float Now => Time.captureDeltaTime > 0f ? Time.frameCount * Time.captureDeltaTime : Time.unscaledTime;
    }
}
