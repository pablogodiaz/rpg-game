using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// Straight-down orthographic camera. The zoom is always a whole number of screen pixels per art pixel,
    /// and the position snaps to the screen-pixel grid, so the pixel art stays crisp at any resolution.
    /// Follows the leader and leans toward the boss.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public static Camera MainCam { get; private set; }

        public Transform Target;
        public Transform SecondaryTarget;
        [Range(0f, 1f)] public float SecondaryWeight = 0.3f;
        /// <summary>Roughly how many world units should fit vertically on screen.</summary>
        public float TargetVisibleHeight = 19f;
        public const float Height = 30f;

        /// <summary>Screen pixels per art pixel.</summary>
        public int Zoom { get; private set; } = 1;

        Vector3 focus;
        float shakeUntil;
        float shakeStrength;

        void Awake()
        {
            Instance = this;
            MainCam = GetComponent<Camera>();
            MainCam.orthographic = true;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f); // looking straight down, screen-up = +Z
            UpdateZoom();
        }

        public static void Shake(float strength, float duration = 0.3f)
        {
            if (Instance == null) return;
            Instance.shakeStrength = strength;
            Instance.shakeUntil = Time.unscaledTime + duration;
        }

        Vector3 DesiredFocus()
        {
            if (Target == null) return Vector3.zero;
            var f = Target.position;
            if (SecondaryTarget != null) f = Vector3.Lerp(f, SecondaryTarget.position, SecondaryWeight);
            f.y = 0f;
            return f;
        }

        public void Snap()
        {
            focus = DesiredFocus();
            Apply(Vector3.zero);
        }

        void UpdateZoom()
        {
            int h = Mathf.Max(1, Screen.height);
            Zoom = Mathf.Max(1, Mathf.RoundToInt(h / (PixelArt.PPU * TargetVisibleHeight)));
            MainCam.orthographicSize = h / (2f * PixelArt.PPU * Zoom);
        }

        void Apply(Vector3 shake)
        {
            var p = focus + shake;
            float unit = 1f / (PixelArt.PPU * Zoom); // one screen pixel in world units
            p.x = Mathf.Round(p.x / unit) * unit;
            p.z = Mathf.Round(p.z / unit) * unit;
            transform.position = new Vector3(p.x, Height, p.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void LateUpdate()
        {
            UpdateZoom();
            float t = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            focus = Vector3.Lerp(focus, DesiredFocus(), t);
            var shake = Vector3.zero;
            if (Time.unscaledTime < shakeUntil)
            {
                var r = Random.insideUnitCircle * shakeStrength;
                shake = new Vector3(r.x, 0f, r.y);
            }
            Apply(shake);
        }
    }
}
