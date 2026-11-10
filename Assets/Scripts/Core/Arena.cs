using UnityEngine;

namespace ReadyCheck
{
    /// <summary>The boss room: a round floor of metal plates, a ring of rock walls and glowing lamps. Built from code.</summary>
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

        public static void Build()
        {
            var root = new GameObject("Arena").transform;
            Flat("Floor", root, Vector3.zero, PixelArt.ArenaFloor(), -100);

            // Lamps set into the wall, each throwing a warm pool of light on the floor.
            const int lampCount = 8;
            for (int i = 0; i < lampCount; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / lampCount;
                var pos = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 13.9f;
                var glow = Flat("LampGlow", root, pos + Vector3.up * 0.002f, PixelArt.Glow(), -95);
                glow.transform.localScale = Vector3.one * 3.6f;
                glow.color = new Color(1f, 0.62f, 0.3f, 0.22f);
                Flat("Lamp", root, pos + Vector3.up * 0.004f, PixelArt.Lamp(), -90);
                glow.gameObject.AddComponent<LampFlicker>().Init(glow, i * 1.7f);
            }
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

        void Update()
        {
            if (sr == null) return;
            float k = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.3f + phase) * Mathf.Sin(Time.unscaledTime * 0.9f + phase * 2f);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k);
        }
    }
}
