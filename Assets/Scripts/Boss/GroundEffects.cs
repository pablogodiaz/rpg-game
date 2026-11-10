using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    public enum MechanicKind { BellyFlop, Indigestion, SecondBreakfast }

    /// <summary>Raised by a boss when a mechanic starts, so the AI, hints and barks can react.</summary>
    public class MechanicEvent
    {
        public MechanicKind Kind;
        public Vector3 Point;
        public float Radius;
        public float ResolveAt;
        public Unit Target;
        public RaidCall SuggestedCall;
    }

    /// <summary>
    /// A ground telegraph: an outlined circle with an inner disc that grows until the mechanic resolves.
    /// Can follow a unit (e.g. a debuff around a player).
    /// </summary>
    public class Telegraph : MonoBehaviour
    {
        SpriteRenderer ring;
        SpriteRenderer fill;
        Unit follow;
        Color color;
        float startsAt;
        float endsAt;
        bool isFlash;

        static Telegraph Create(Vector3 pos, float radius, Color color, float duration, Unit follow, bool flash)
        {
            var go = new GameObject(flash ? "Flash" : "Telegraph");
            var enc = EncounterManager.Instance;
            if (enc != null && enc.Root != null) go.transform.SetParent(enc.Root, false);
            go.transform.position = new Vector3(pos.x, 0.05f, pos.z);
            go.transform.localScale = Vector3.one * radius;

            var t = go.AddComponent<Telegraph>();
            t.follow = follow;
            t.color = color;
            t.startsAt = Time.time;
            t.endsAt = Time.time + duration;
            t.isFlash = flash;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            fillGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            t.fill = fillGo.AddComponent<SpriteRenderer>();
            t.fill.sprite = PixelArt.Disc();
            t.fill.sortingOrder = -10;

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localPosition = Vector3.up * 0.01f;
            ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            t.ring = ringGo.AddComponent<SpriteRenderer>();
            t.ring.sprite = PixelArt.RingSprite();
            t.ring.sortingOrder = flash ? 45 : -9;
            if (flash) t.fill.sortingOrder = 44;
            t.Update();
            return t;
        }

        public static Telegraph Circle(Vector3 pos, float radius, Color color, float duration, Unit follow = null) =>
            Create(pos, radius, color, duration, follow, false);

        /// <summary>A quick expanding, fading disc for impacts and explosions.</summary>
        public static void Flash(Vector3 pos, float radius, Color color, float duration = 0.35f) =>
            Create(pos, radius, color, duration, null, true);

        void Update()
        {
            float t = Mathf.InverseLerp(startsAt, endsAt, Time.time);
            if (follow != null)
            {
                var p = follow.Position;
                transform.position = new Vector3(p.x, 0.05f, p.z);
            }

            if (isFlash)
            {
                // Shockwave: an expanding ring over a quickly fading flash.
                fill.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 0.9f, t);
                fill.color = new Color(color.r, color.g, color.b, (1f - t) * (1f - t) * 0.55f);
                ring.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.15f, t);
                ring.color = new Color(color.r, color.g, color.b, 1f - t);
            }
            else
            {
                fill.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 1f, t);
                fill.color = new Color(color.r, color.g, color.b, 0.28f);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);
                ring.color = new Color(color.r, color.g, color.b, pulse);
            }

            if (Time.time >= endsAt) Destroy(gameObject);
        }
    }

    /// <summary>Grease Fire: a lingering puddle that burns anyone standing in it.</summary>
    public class FirePuddle : MonoBehaviour
    {
        public const string AbilityName = "Grease Fire";
        public const float TickInterval = 0.5f;
        public const float TickDamage = 30f;
        public const int MaxPuddles = 8;

        public static readonly List<FirePuddle> All = new List<FirePuddle>();

        public float Radius { get; private set; }

        SpriteRenderer sr;
        Sprite[] frames;
        float bornAt;
        float expiresAt;
        float nextTick;
        Unit source;

        public static FirePuddle Spawn(Vector3 pos, float radius, float duration, Unit source)
        {
            All.RemoveAll(f => f == null);
            if (All.Count >= MaxPuddles)
            {
                var oldest = All[0];
                All.RemoveAt(0);
                Destroy(oldest.gameObject);
            }

            var go = new GameObject("GreaseFire");
            var enc = EncounterManager.Instance;
            if (enc != null && enc.Root != null) go.transform.SetParent(enc.Root, false);
            go.transform.position = new Vector3(pos.x, 0.04f, pos.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var p = go.AddComponent<FirePuddle>();
            p.Radius = radius;
            p.source = source;
            p.frames = PixelArt.FireFrames(radius);
            p.sr = go.AddComponent<SpriteRenderer>();
            p.sr.sprite = p.frames[0];
            p.sr.sortingOrder = -8;
            p.bornAt = Time.time;
            p.expiresAt = Time.time + duration;
            p.nextTick = Time.time + 0.6f; // a short grace period to react
            All.Add(p);
            return p;
        }

        /// <summary>Returns a puddle covering this point (margin grows or shrinks the check radius).</summary>
        public static FirePuddle At(Vector3 point, float margin = 0f)
        {
            foreach (var f in All)
            {
                if (f == null) continue;
                var c = f.transform.position;
                float dx = point.x - c.x, dz = point.z - c.z;
                float r = f.Radius + margin;
                if (dx * dx + dz * dz < r * r) return f;
            }
            return null;
        }

        void Update()
        {
            float age = Time.time - bornAt;
            sr.sprite = frames[Mathf.FloorToInt(Time.time * 8f) % frames.Length];
            float grow = Mathf.Clamp01(age / 0.4f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, grow);
            float fade = Mathf.Clamp01(expiresAt - Time.time);
            sr.color = new Color(1f, 1f, 1f, fade);

            var enc = EncounterManager.Instance;
            if (enc != null && enc.State == EncounterState.Fighting && Time.time >= nextTick)
            {
                nextTick += TickInterval;
                foreach (var member in enc.Party)
                {
                    if (member.Unit.IsDead) continue;
                    if (At(member.Unit.Position, -0.1f) == this)
                        member.Unit.TakeDamage(TickDamage, source, AbilityName);
                }
            }

            if (Time.time >= expiresAt) Destroy(gameObject);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }
    }
}
