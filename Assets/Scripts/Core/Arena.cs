using UnityEngine;

namespace ReadyCheck
{
    /// <summary>Visual look of the arena; each boss picks one.</summary>
    public enum ArenaTheme { BuffetHall, SunkenGrotto }

    /// <summary>The boss room: a round floor of plates, a ring of rock walls and glowing lamps. Built from code.</summary>
    public static class Arena
    {
        /// <summary>How far from the center units can walk.</summary>
        public const float Radius = 12.5f;

        public static Vector3 Clamp(Vector3 p)
        {
            p.y = 0f;
            var flat = new Vector2(p.x, p.z);
            if (flat.magnitude > Radius)
            {
                flat = flat.normalized * Radius;
                p.x = flat.x;
                p.z = flat.y;
            }
            return p;
        }

        static SpriteRenderer Flat(string name, Transform parent, Vector3 position, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static SpriteRenderer floor;
        static readonly System.Collections.Generic.List<SpriteRenderer> lamps = new System.Collections.Generic.List<SpriteRenderer>();
        static readonly System.Collections.Generic.List<LampFlicker> glows = new System.Collections.Generic.List<LampFlicker>();

        public static ArenaTheme Theme { get; private set; } = ArenaTheme.BuffetHall;

        public static void Build()
        {
            var root = new GameObject("Arena").transform;
            floor = Flat("Floor", root, Vector3.zero, PixelArt.ArenaFloor(Theme), -100);

            // Lamps set into the wall, each throwing a pool of light on the floor.
            const int lampCount = 8;
            for (int i = 0; i < lampCount; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / lampCount;
                var pos = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 13.9f;
                var glow = Flat("LampGlow", root, pos + Vector3.up * 0.002f, PixelArt.Glow(), -95);
                glow.transform.localScale = Vector3.one * 3.6f;
                var flicker = glow.gameObject.AddComponent<LampFlicker>();
                flicker.Init(glow, i * 1.7f);
                glows.Add(flicker);
                lamps.Add(Flat("Lamp", root, pos + Vector3.up * 0.004f, null, -90));
            }
            SetTheme(Theme);
        }

        /// <summary>Recolors the arena for the selected boss.</summary>
        public static void SetTheme(ArenaTheme theme)
        {
            Theme = theme;
            if (floor == null) return;
            floor.sprite = PixelArt.ArenaFloor(theme);
            bool grotto = theme == ArenaTheme.SunkenGrotto;
            var core = grotto ? new Color32(110, 220, 255, 255) : new Color32(255, 186, 96, 255);
            var light = grotto ? new Color(0.35f, 0.85f, 1f, 0.24f) : new Color(1f, 0.62f, 0.3f, 0.22f);
            foreach (var lamp in lamps) lamp.sprite = PixelArt.Lamp(core);
            foreach (var g in glows) g.SetColor(light);
            if (CameraRig.MainCam != null)
                CameraRig.MainCam.backgroundColor = grotto ? new Color(0.03f, 0.07f, 0.11f) : new Color(0.086f, 0.07f, 0.11f);
        }
    }

    /// <summary>Makes a lamp's light pool breathe a little.</summary>
    public class LampFlicker : MonoBehaviour
    {
        SpriteRenderer sr;
        Color baseColor;
        float phase;

        public void Init(SpriteRenderer renderer, float phaseOffset)
        {
            sr = renderer;
            baseColor = renderer.color;
            phase = phaseOffset;
        }

        public void SetColor(Color c) => baseColor = c;

        void Update()
        {
            if (sr == null) return;
            float k = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.3f + phase) * Mathf.Sin(Time.unscaledTime * 0.9f + phase * 2f);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k);
        }
    }
}
