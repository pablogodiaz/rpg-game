using System;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// Top-down presentation for a unit. Sprites lie flat on the ground and are seen from above.
    /// - Body rotates toward its aim (the boss in combat). Feet rotate toward movement and step while walking.
    /// - Drop shadow: a dark copy of the body, offset down-left, that grows with height (leaps).
    /// - Hit flash, a small explosion and a scorch mark on death (undone by Battle Rez).
    /// - Buff auras (BURN / RALLY glow under the unit) and a shield bubble (Divine Shield, Bulwark, DEFENSIVES).
    /// </summary>
    public class UnitVisual : MonoBehaviour
    {
        static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);

        public SpriteRenderer Body;
        /// <summary>World units above the ground (used by leaps). Scales the body up and pushes the shadow out.</summary>
        public float HeightOffset;
        public Color BaseTint = Color.white;
        /// <summary>Where the body should face. Return Vector3.zero to face the movement direction instead.</summary>
        public Func<Vector3> AimDirection;

        Unit unit;
        Transform bodyT, legsT, footL, footR, shadowT;
        SpriteRenderer shadow, footLsr, footRsr, scorch, aura, bubble;
        float footSpread, stride;
        float bodyYaw, legYaw, walkPhase;
        Vector3 lastPosition;
        float flashUntil;
        Color flashColor;
        bool deathShown;

        public static UnitVisual Create(Unit unit, Sprite body, Sprite foot, float footSpread, float stride,
            int sortingBase, float initialYaw)
        {
            var v = unit.gameObject.AddComponent<UnitVisual>();
            v.unit = unit;
            v.footSpread = footSpread;
            v.stride = stride;
            v.bodyYaw = v.legYaw = initialYaw;
            v.lastPosition = unit.transform.position;

            v.legsT = new GameObject("Legs").transform;
            v.legsT.SetParent(unit.transform, false);
            v.footL = MakeFlat("FootL", v.legsT, foot, sortingBase - 1, out v.footLsr);
            v.footR = MakeFlat("FootR", v.legsT, foot, sortingBase - 1, out v.footRsr);

            v.shadowT = MakeFlat("Shadow", unit.transform, body, sortingBase - 3, out v.shadow);
            v.shadow.color = new Color(0f, 0f, 0f, 0.35f);

            v.bodyT = MakeFlat("Body", unit.transform, body, sortingBase, out v.Body);
            v.bodyT.localPosition = new Vector3(0f, 0.03f, 0f);

            MakeFlat("Scorch", unit.transform, PixelArt.Scorch(), sortingBase - 5, out v.scorch);
            v.scorch.enabled = false;

            float size = Mathf.Max(0.8f, unit.Radius * 2f);
            MakeFlat("Aura", unit.transform, PixelArt.Glow(), sortingBase - 2, out v.aura);
            v.aura.transform.localScale = Vector3.one * size * 1.1f;
            v.aura.enabled = false;
            MakeFlat("Bubble", unit.transform, PixelArt.RingSprite(), sortingBase + 1, out v.bubble);
            v.bubble.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            v.bubble.transform.localScale = Vector3.one * size * 0.75f;
            v.bubble.enabled = false;
            unit.Revived += v.OnRevived;

            v.LateUpdate();
            return v;
        }

        static Transform MakeFlat(string name, Transform parent, Sprite sprite, int order, out SpriteRenderer sr)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Flat;
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return go.transform;
        }

        public void Flash(Color color, float duration = 0.08f)
        {
            flashColor = color;
            flashUntil = Time.time + duration;
        }

        static float YawOf(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var pos = transform.position;
            var delta = pos - lastPosition;
            delta.y = 0f;
            lastPosition = pos;
            float speed = dt > 0f ? delta.magnitude / dt : 0f;

            if (unit != null && unit.IsDead)
            {
                ShowDeath();
                return;
            }

            bool moving = speed > 0.3f;
            if (moving)
            {
                legYaw = Mathf.MoveTowardsAngle(legYaw, YawOf(delta), 720f * dt);
                walkPhase += dt * Mathf.Clamp(speed, 2f, 8f) * 2.6f;
            }
            else
            {
                walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 6f);
            }

            var aim = AimDirection != null ? AimDirection() : Vector3.zero;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.01f && moving) aim = delta;
            if (aim.sqrMagnitude > 0.0001f) bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, YawOf(aim), 540f * dt);

            float step = Mathf.Sin(walkPhase) * stride;
            legsT.localRotation = Quaternion.Euler(0f, legYaw, 0f);
            footL.localPosition = new Vector3(-footSpread, 0.01f, step);
            footR.localPosition = new Vector3(footSpread, 0.01f, -step);
            footL.gameObject.SetActive(HeightOffset < 0.2f);
            footR.gameObject.SetActive(HeightOffset < 0.2f);

            float scale = 1f + HeightOffset * 0.12f;
            var bodyRot = Quaternion.Euler(0f, bodyYaw, 0f) * Flat;
            bodyT.rotation = bodyRot;
            bodyT.localScale = Vector3.one * scale;

            float shadowDist = 0.14f + HeightOffset * 0.35f;
            shadowT.position = pos + new Vector3(-shadowDist * 0.7f, 0.005f, -shadowDist);
            shadowT.rotation = bodyRot;
            shadowT.localScale = Vector3.one * scale;

            Body.color = Time.time < flashUntil ? flashColor : BaseTint;
            UpdateAuras();
        }

        void UpdateAuras()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
            if (unit.HasBuff("burn")) Show(aura, new Color(1f, 0.35f, 0.12f, 0.75f * pulse));
            else if (unit.HasBuff("rally")) Show(aura, new Color(1f, 0.85f, 0.3f, 0.55f * pulse));
            else aura.enabled = false;

            if (unit.Absorb > 0f) Show(bubble, new Color(0.55f, 0.9f, 1f, 0.35f + 0.6f * unit.Absorb / Mathf.Max(1f, unit.AbsorbMax)));
            else if (unit.HasBuff("bulwark")) Show(bubble, new Color(1f, 0.8f, 0.25f, 0.9f * pulse));
            else if (unit.HasBuff("def")) Show(bubble, new Color(0.45f, 0.75f, 1f, 0.7f * pulse));
            else bubble.enabled = false;
        }

        static void Show(SpriteRenderer sr, Color c)
        {
            sr.enabled = true;
            sr.color = c;
        }

        void OnRevived(Unit u)
        {
            deathShown = false;
            Body.color = BaseTint;
            bodyT.localScale = Vector3.one;
            footL.gameObject.SetActive(true);
            footR.gameObject.SetActive(true);
            shadow.enabled = true;
            scorch.enabled = false;
        }

        void ShowDeath()
        {
            if (deathShown) return;
            deathShown = true;
            Body.color = new Color(0.32f, 0.3f, 0.36f, 1f);
            bodyT.localScale = Vector3.one * 0.9f;
            footL.gameObject.SetActive(false);
            footR.gameObject.SetActive(false);
            shadow.enabled = false;
            aura.enabled = false;
            bubble.enabled = false;
            scorch.enabled = true;
            scorch.transform.localScale = Vector3.one * Mathf.Max(1f, unit.Radius * 2.2f);
            Telegraph.Flash(unit.Position, Mathf.Max(0.9f, unit.Radius * 2f), new Color(1f, 0.6f, 0.25f), 0.4f);
        }
    }
}
