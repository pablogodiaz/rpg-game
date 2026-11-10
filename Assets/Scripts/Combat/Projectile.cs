using System;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// A homing bullet seen from above: a bright pixel bullet pointing along its flight path, a short
    /// glowing trail, and a small spark ring on impact. Damage or healing is applied by the callback.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);

        Unit target;
        float speed;
        Color color;
        Action onHit;

        public static void Fire(Vector3 from, Unit target, Color color, float speed, Action onHit)
        {
            if (target == null) return;
            var go = new GameObject("Projectile");
            var enc = EncounterManager.Instance;
            if (enc != null && enc.Root != null) go.transform.SetParent(enc.Root, false);
            from.y = 0.2f;
            var toTarget = target.Position - from;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.25f) from += toTarget.normalized * 0.45f; // muzzle in front of the shooter
            go.transform.position = from;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Bullet(color);
            sr.sortingOrder = 40;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            glowGo.transform.localScale = Vector3.one * 0.45f;
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = PixelArt.Glow();
            glow.color = new Color(color.r, color.g, color.b, 0.45f);
            glow.sortingOrder = 39;

            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = PixelArt.SpriteMaterial;
            trail.time = 0.12f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 0.16f;
            trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.sortingOrder = 38;

            var p = go.AddComponent<Projectile>();
            p.target = target;
            p.speed = speed;
            p.color = color;
            p.onHit = onHit;
            p.Face(target.Position - from);
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f) * Flat;
        }

        void Update()
        {
            if (target == null || target.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            var aim = target.Position;
            aim.y = 0.2f;
            var before = transform.position;
            transform.position = Vector3.MoveTowards(before, aim, speed * Time.deltaTime);
            Face(transform.position - before);

            // Hit the edge of the target, not its center.
            var flat = transform.position - aim;
            flat.y = 0f;
            if (flat.magnitude <= target.Radius * 0.6f + 0.05f)
            {
                onHit?.Invoke();
                Telegraph.Flash(transform.position, 0.35f, color, 0.15f);
                Destroy(gameObject);
            }
        }
    }
}
