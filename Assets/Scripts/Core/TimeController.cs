using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// Slow-mo while the raid leader is commanding. The game never fully pauses in combat.
    /// UI code uses unscaled time so menus stay responsive while the world is slowed down.
    /// </summary>
    public static class TimeController
    {
        public const float SlowMoScale = 0.2f;
        const float BaseFixedDeltaTime = 0.02f;

        public static bool SlowMo { get; private set; }

        public static void SetSlowMo(bool enabled)
        {
            SlowMo = enabled;
            Time.timeScale = enabled ? SlowMoScale : 1f;
            Time.fixedDeltaTime = BaseFixedDeltaTime * Time.timeScale;
        }
    }
}
