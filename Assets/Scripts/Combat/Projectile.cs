using System;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>A homing pixel bolt. Damage or healing is applied by the callback on impact.</summary>
    public class Projectile : MonoBehaviour
    {
        Unit target;
        float speed;
        Action onHit;

        public static void Fire(Vector3 from, Unit target, Color color, float speed, Action onHit)
        {
            if (target == null) return;
            var go = new GameObject("Projectile");
            var enc = EncounterManager.Instance;
            if (enc != null && enc.Root != null) go.transform.SetParent(enc.Root, false);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 1.6f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Bolt();
            sr.color = color;
            sr.sortingOrder = 5;
            var p = go.AddComponent<Projectile>();
            p.target = target;
            p.speed = speed;
            p.onHit = onHit;
        }

        void Update()
        {
            if (target == null || target.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            float height = target.Role == Role.Boss ? 1.8f : 0.8f;
            var aim = target.Position + Vector3.up * height;
            transform.position = Vector3.MoveTowards(transform.position, aim, speed * Time.deltaTime);
            var cam = CameraRig.MainCam;
            if (cam != null) transform.rotation = cam.transform.rotation;

            if ((transform.position - aim).sqrMagnitude < 0.04f)
            {
                onHit?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
