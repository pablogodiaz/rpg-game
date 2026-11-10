using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    public enum MechanicKind
    {
        // Rotgut
        BellyFlop,
        Indigestion,
        // Shared
        InterruptibleCast,
        // Krakenna
        ArmGrab,
        Frenzy,
    }

    /// <summary>Raised by a boss when a mechanic starts, so the AI, hints and barks can react.</summary>
    public class MechanicEvent
    {
        public MechanicKind Kind;
        public Vector3 Point;
        public float Radius;
        public float ResolveAt;
        public Unit Target;
        /// <summary>A second unit involved, e.g. the arm doing the grabbing.</summary>
        public Unit Source;
        public RaidCall SuggestedCall;
        /// <summary>Hint text for the HUD. "{call}" is replaced by the button for SuggestedCall.</summary>
        public string Hint;
    }

    /// <summary>
    /// Ground telegraphs: circles and lines with a fill that grows until the mechanic resolves.
    /// "Danger" telegraphs register themselves so party AI can step out of them (speed based on Awareness).
    /// </summary>
    public class Telegraph : MonoBehaviour
    {
        public static readonly List<Telegraph> Dangers = new List<Telegraph>();

        SpriteRenderer ring;
        SpriteRenderer fill;
        readonly List<SpriteRenderer> edges = new List<SpriteRenderer>();
        Unit follow;
        Color color;
        float startsAt;
        float endsAt;
        bool isFlash;

        bool isLine;
        Vector3 lineStart, lineDir, lineRight;
        float lineLength, lineWidth, circleRadius;

        static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);

        static GameObject NewRoot(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            var enc = EncounterManager.Instance;
            if (enc != null && enc.Root != null) go.transform.SetParent(enc.Root, false);
            go.transform.position = new Vector3(pos.x, 0.05f, pos.z);
            return go;
        }

        static SpriteRenderer Child(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Flat;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static Telegraph Create(Vector3 pos, float radius, Color color, float duration, Unit follow, bool flash, bool danger)
        {
            var go = NewRoot(flash ? "Flash" : "Telegraph", pos);
            go.transform.localScale = Vector3.one * radius;

            var t = go.AddComponent<Telegraph>();
            t.follow = follow;
            t.color = color;
            t.startsAt = Time.time;
            t.endsAt = Time.time + duration;
            t.isFlash = flash;
            t.circleRadius = radius;

            t.fill = Child(go.transform, "Fill", PixelArt.Disc(), flash ? 44 : -10);
            t.ring = Child(go.transform, "Ring", PixelArt.RingSprite(), flash ? 45 : -9);
            t.ring.transform.localPosition = Vector3.up * 0.01f;
            if (danger) Dangers.Add(t);
            t.Update();
            return t;
        }

        public static Telegraph Circle(Vector3 pos, float radius, Color color, float duration, Unit follow = null, bool danger = false) =>
            Create(pos, radius, color, duration, follow, false, danger);

        /// <summary>A quick expanding, fading disc for impacts and explosions.</summary>
        public static void Flash(Vector3 pos, float radius, Color color, float duration = 0.35f) =>
            Create(pos, radius, color, duration, null, true, false);

        /// <summary>A rectangle from start along dir (e.g. a tentacle slam). Fill sweeps from start to end.</summary>
        public static Telegraph Line(Vector3 start, Vector3 dir, float length, float width, Color color, float duration, bool danger = true)
        {
            dir.y = 0f;
            dir.Normalize();
            var go = NewRoot("LineTelegraph", start);
            go.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);

            var t = go.AddComponent<Telegraph>();
            t.color = color;
            t.startsAt = Time.time;
            t.endsAt = Time.time + duration;
            t.isLine = true;
            t.lineStart = new Vector3(start.x, 0f, start.z);
            t.lineDir = dir;
            t.lineRight = Vector3.Cross(Vector3.up, dir);
            t.lineLength = length;
            t.lineWidth = width;

            t.fill = Child(go.transform, "Fill", PixelArt.Square(), -10);
            const float edge = 0.09f;
            var left = Child(go.transform, "EdgeL", PixelArt.Square(), -9);
            left.transform.localPosition = new Vector3(-width / 2f, 0.01f, 0f);
            left.transform.localScale = new Vector3(edge, length, 1f);
            var right = Child(go.transform, "EdgeR", PixelArt.Square(), -9);
            right.transform.localPosition = new Vector3(width / 2f, 0.01f, 0f);
            right.transform.localScale = new Vector3(edge, length, 1f);
            var end = Child(go.transform, "EdgeEnd", PixelArt.Square(), -9);
            end.transform.localPosition = new Vector3(0f, 0.01f, length - edge);
            end.transform.localScale = new Vector3(width, edge, 1f);
            t.edges.Add(left);
            t.edges.Add(right);
            t.edges.Add(end);
            if (danger) Dangers.Add(t);
            t.Update();
            return t;
        }

        // ----- Danger zone queries (for AI) --------------------------------------------------------

        public bool Contains(Vector3 p, float margin = 0f)
        {
            if (isLine)
            {
                var local = p - lineStart;
                local.y = 0f;
                float along = Vector3.Dot(local, lineDir);
                float perp = Vector3.Dot(local, lineRight);
                return along >= -margin && along <= lineLength + margin && Mathf.Abs(perp) <= lineWidth / 2f + margin;
            }
            var c = transform.position;
            float dx = p.x - c.x, dz = p.z - c.z;
            float r = circleRadius + margin;
            return dx * dx + dz * dz <= r * r;
        }

        /// <summary>The nearest safe spot just outside this zone.</summary>
        public Vector3 EscapePoint(Vector3 p)
        {
            if (isLine)
            {
                var local = p - lineStart;
                local.y = 0f;
                float along = Mathf.Clamp(Vector3.Dot(local, lineDir), 0f, lineLength);
                float perp = Vector3.Dot(local, lineRight);
                float side = Mathf.Abs(perp) < 0.05f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(perp);
                return Arena.Clamp(lineStart + lineDir * along + lineRight * side * (lineWidth / 2f + 0.9f));
            }
            var c = transform.position;
            c.y = 0f;
            var dir = p - c;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
            return Arena.Clamp(c + dir.normalized * (circleRadius + 0.9f));
        }

        public static Telegraph DangerAt(Vector3 p)
        {
            foreach (var d in Dangers)
                if (d != null && d.Contains(p)) return d;
            return null;
        }

        void OnDestroy()
        {
            Dangers.Remove(this);
        }

        void Update()
        {
            float t = Mathf.InverseLerp(startsAt, endsAt, Time.time);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);

            if (isLine)
            {
                fill.transform.localScale = new Vector3(lineWidth, lineLength * Mathf.Lerp(0.05f, 1f, t), 1f);
                fill.color = new Color(color.r, color.g, color.b, 0.3f);
                foreach (var e in edges) e.color = new Color(color.r, color.g, color.b, pulse);
            }
            else
            {
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
                    ring.color = new Color(color.r, color.g, color.b, pulse);
                }
            }

            if (Time.time >= endsAt) Destroy(gameObject);
        }
    }

    public enum PuddleKind { Fire, Ink }

    /// <summary>
    /// A lingering ground hazard: Grease Fire (Rotgut) burns, Ink (Krakenna) burns a little and slows you down.
    /// </summary>
    public class FirePuddle : MonoBehaviour
    {
        public const string FireName = "Grease Fire";
        public const string InkName = "Ink";
        public const float TickInterval = 0.5f;
        public const int MaxPuddles = 10;

        public static readonly List<FirePuddle> All = new List<FirePuddle>();

        public float Radius { get; private set; }
        public PuddleKind Kind { get; private set; }
        public string Label => Kind == PuddleKind.Fire ? "IN FIRE" : "INKED";
        /// <summary>Movement speed multiplier while standing in it.</summary>
        public float SpeedMultiplier => Kind == PuddleKind.Ink ? 0.5f : 1f;

        SpriteRenderer sr;
        Sprite[] frames;
        float bornAt;
        float expiresAt;
        float nextTick;
        float tickDamage;
        string abilityName;
        Unit source;

        public static bool IsPuddleAbility(string ability) => ability == FireName || ability == InkName;

        public static FirePuddle SpawnFire(Vector3 pos, float radius, float duration, Unit source) =>
            Spawn(pos, radius, duration, source, PuddleKind.Fire, 30f, FireName, PixelArt.FireFrames(radius));

        public static FirePuddle SpawnInk(Vector3 pos, float radius, float duration, Unit source) =>
            Spawn(pos, radius, duration, source, PuddleKind.Ink, 12f, InkName, PixelArt.InkFrames(radius));

        static FirePuddle Spawn(Vector3 pos, float radius, float duration, Unit source, PuddleKind kind,
            float tickDamage, string abilityName, Sprite[] frames)
        {
            All.RemoveAll(f => f == null);
            if (All.Count >= MaxPuddles)
            {
                var oldest = All[0];
                All.RemoveAt(0);
                Destroy(oldest.gameObject);
            }

            var go = new GameObject(abilityName);
            var enc = EncounterManager.Instance;
            if (enc != null && enc.Root != null) go.transform.SetParent(enc.Root, false);
            go.transform.position = new Vector3(pos.x, 0.04f, pos.z);
            go.transform.rotation = Quaternion.Euler(90f, Random.Range(0, 4) * 90f, 0f);
            var p = go.AddComponent<FirePuddle>();
            p.Radius = radius;
            p.Kind = kind;
            p.source = source;
            p.tickDamage = tickDamage;
            p.abilityName = abilityName;
            p.frames = frames;
            p.sr = go.AddComponent<SpriteRenderer>();
            p.sr.sprite = frames[0];
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

        public static float SpeedMultiplierAt(Vector3 point)
        {
            var p = At(point, -0.1f);
            return p != null ? p.SpeedMultiplier : 1f;
        }

        void Update()
        {
            float age = Time.time - bornAt;
            sr.sprite = frames[Mathf.FloorToInt(Time.time * (Kind == PuddleKind.Ink ? 3f : 8f)) % frames.Length];
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
                        member.Unit.TakeDamage(tickDamage, source, abilityName);
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
