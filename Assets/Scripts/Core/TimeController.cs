using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// Owns Time.timeScale:
    ///  - Slow-mo while the raid leader is commanding (the game never fully pauses in combat).
    ///  - Hit-stop: a few hundredths of a second of near-freeze on big hits, so they feel heavy.
    /// UI code uses unscaled time so menus stay responsive while the world is slowed down.
    /// </summary>
    public static class TimeController
    {
        public const float SlowMoScale = 0.2f;
        const float HitStopScale = 0.04f;
        const float BaseFixedDeltaTime = 0.02f;

        public static bool SlowMo { get; private set; }
        static float hitStopUntil = -1f;

        public static void SetSlowMo(bool enabled)
        {
            SlowMo = enabled;
            hitStopUntil = -1f;
            Apply(enabled ? SlowMoScale : 1f);
        }

        /// <summary>Briefly freezes the action. Ignored while slow-mo is active (the wheel is open).</summary>
        public static void HitStop(float seconds)
        {
            if (SlowMo || seconds <= 0f) return;
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
            Apply(HitStopScale);
        }

        /// <summary>Called every frame (by FX) to end hit-stops.</summary>
        public static void Tick()
        {
            if (hitStopUntil > 0f && Time.unscaledTime >= hitStopUntil)
            {
                hitStopUntil = -1f;
                Apply(SlowMo ? SlowMoScale : 1f);
            }
        }

        static void Apply(float scale)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = BaseFixedDeltaTime * scale;
        }
    }
}
