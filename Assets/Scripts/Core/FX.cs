using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// The juice: pooled particles, floating damage numbers and ability callouts, beams, slashes,
    /// afterimages, meteors, screen shake helpers and hit-stop. Everything is fire-and-forget from gameplay code.
    /// </summary>
    public class FX : MonoBehaviour
    {
        public static FX Instance { get; private set; }

        static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);
        const int MaxParticles = 600;

        struct Particle
        {
            public SpriteRenderer Sr;
            public Vector3 Pos, Vel;
            public float Born, Life, Size, Drag;
            public Color Color;
            public bool Active;
        }

        public class FloatingText
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public int Size;
            public float Born;   // unscaled
            public float Life;
            public float DriftX;
            public bool Pop;
        }

        public static readonly List<FloatingText> Texts = new List<FloatingText>();

        readonly Particle[] particles = new Particle[MaxParticles];
        int nextParticle;
        Transform poolRoot;

        void Awake()
        {
            Instance = this;
            poolRoot = new GameObject("FX Particles").transform;
            var sprite = PixelArt.Particle();
            for (int i = 0; i < MaxParticles; i++)
            {
                var go = new GameObject("p");
                go.transform.SetParent(poolRoot, false);
                go.transform.rotation = Flat;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 42;
                sr.enabled = false;
                particles[i].Sr = sr;
            }
        }

        /// <summary>Removes all running effects (used when the encounter resets).</summary>
        public static void Clear()
        {
            Texts.Clear();
            if (Instance == null) return;
            for (int i = 0; i < MaxParticles; i++)
            {
                Instance.particles[i].Active = false;
                Instance.particles[i].Sr.enabled = false;
            }
        }

        void Update()
        {
            TimeController.Tick();

            float now = Time.time;
            float dt = Time.deltaTime;
            for (int i = 0; i < MaxParticles; i++)
            {
                ref var p = ref particles[i];
                if (!p.Active) continue;
                float t = (now - p.Born) / p.Life;
                if (t >= 1f)
                {
                    p.Active = false;
                    p.Sr.enabled = false;
                    continue;
                }
                p.Vel *= Mathf.Max(0f, 1f - p.Drag * dt);
                p.Pos += p.Vel * dt;
                var tr = p.Sr.transform;
                tr.position = p.Pos;
                tr.localScale = Vector3.one * (p.Size * (1f - t * 0.7f));
                p.Sr.color = new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * (1f - t * t));
            }

            float unow = Time.unscaledTime;
            Texts.RemoveAll(ft => unow - ft.Born > ft.Life);
        }

        // ----- Particles ---------------------------------------------------------------------------

        /// <summary>A burst of pixel particles flying outward from a point.</summary>
        public static void Burst(Vector3 pos, Color color, int count, float speed, float life = 0.45f, float size = 1.2f)
        {
            if (Instance == null) return;
            for (int i = 0; i < count; i++)
            {
                var dir = UnityEngine.Random.insideUnitCircle.normalized;
                float sp = speed * UnityEngine.Random.Range(0.35f, 1f);
                Instance.Spawn(pos, new Vector3(dir.x, 0f, dir.y) * sp, color,
                    life * UnityEngine.Random.Range(0.6f, 1.1f), size * UnityEngine.Random.Range(0.7f, 1.3f), 3f);
            }
        }

        /// <summary>Particles sprayed in a cone around a direction.</summary>
        public static void Spray(Vector3 pos, Vector3 direction, Color color, int count, float speed, float spreadDeg = 50f, float life = 0.35f)
        {
            if (Instance == null) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                var dir = Quaternion.AngleAxis(UnityEngine.Random.Range(-spreadDeg, spreadDeg), Vector3.up) * direction.normalized;
                Instance.Spawn(pos, dir * speed * UnityEngine.Random.Range(0.4f, 1f), color,
                    life * UnityEngine.Random.Range(0.6f, 1.1f), UnityEngine.Random.Range(0.9f, 1.5f), 4f);
            }
        }

        void Spawn(Vector3 pos, Vector3 vel, Color color, float life, float size, float drag)
        {
            ref var p = ref particles[nextParticle];
            nextParticle = (nextParticle + 1) % MaxParticles;
            pos.y = 0.3f;
            p.Pos = pos;
            p.Vel = vel;
            p.Color = color;
            p.Born = Time.time;
            p.Life = Mathf.Max(0.05f, life);
            p.Size = size;
            p.Drag = drag;
            p.Active = true;
            p.Sr.enabled = true;
            p.Sr.transform.position = pos;
            p.Sr.color = color;
        }

        // ----- Floating text -----------------------------------------------------------------------

        public static void Text(Vector3 world, string text, Color color, int size = 13, float life = 0.9f, bool pop = false)
        {
            Texts.Add(new FloatingText
            {
                World = world, Text = text, Color = color, Size = size, Born = Time.unscaledTime, Life = life,
                DriftX = UnityEngine.Random.Range(-0.35f, 0.35f), Pop = pop,
            });
            if (Texts.Count > 120) Texts.RemoveAt(0);
        }

        /// <summary>Damage and healing numbers. Crits are bigger, yellow and pop.</summary>
        public static void Number(Unit target, float amount, bool crit, bool heal)
        {
            if (target == null || amount < 1f) return;
            string text = heal ? $"+{amount:0}" : crit ? $"{amount:0}!" : $"{amount:0}";
            Color color;
            int size;
            if (heal) { color = new Color(0.45f, 1f, 0.5f); size = 13; }
            else if (target.Team == Team.Party) { color = new Color(1f, 0.38f, 0.32f); size = 12; }
            else if (crit) { color = new Color(1f, 0.85f, 0.2f); size = 20; }
            else { color = Color.white; size = 14; }
            float above = target.Role == Role.Boss ? 2.2f : 0.7f;
            Text(target.Position + Vector3.forward * above, text, color, size, crit ? 1.1f : 0.8f, crit);
        }

        /// <summary>The ability name popping up over a character's head.</summary>
        public static void Callout(Unit unit, string text, Color color)
        {
            if (unit == null) return;
            float above = unit.Role == Role.Boss ? 2.9f : 1.25f;
            Texts.Add(new FloatingText
            {
                World = unit.Position + Vector3.forward * above, Text = text, Color = color, Size = 16,
                Born = Time.unscaledTime, Life = 1.3f, DriftX = 0f, Pop = true,
            });
        }

        // ----- Shapes ------------------------------------------------------------------------------

        static Transform Parent()
        {
            var enc = EncounterManager.Instance;
            return enc != null ? enc.Root : null;
        }

        /// <summary>A flat sprite that scales and fades over its life.</summary>
        public static FxSprite FlatSprite(Sprite sprite, Vector3 pos, float yaw, Color color, float life,
            float scaleFrom, float scaleTo, int order, Unit follow = null)
        {
            var go = new GameObject("fx");
            go.transform.SetParent(Parent(), false);
            pos.y = 0.25f;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Flat;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            var fx = go.AddComponent<FxSprite>();
            fx.Init(sr, color, life, scaleFrom, scaleTo, follow);
            return fx;
        }

        public static void Glow(Vector3 pos, Color color, float radius, float life, Unit follow = null) =>
            FlatSprite(PixelArt.Glow(), pos, 0f, color, life, radius * 0.6f, radius, 41, follow);

        /// <summary>A melee slash arc in front of a point, facing yaw (degrees).</summary>
        public static void Slash(Vector3 pos, float yaw, Color color, float size = 1f)
        {
            FlatSprite(PixelArt.SlashArc(), pos, yaw, color, 0.16f, size * 0.7f, size * 1.15f, 43);
        }

        /// <summary>A fading copy of a sprite, for dashes and blinks.</summary>
        public static void Afterimage(SpriteRenderer source, Color color, float life = 0.3f)
        {
            if (source == null) return;
            var go = new GameObject("afterimage");
            go.transform.SetParent(Parent(), false);
            go.transform.position = source.transform.position;
            go.transform.rotation = source.transform.rotation;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.sortingOrder = source.sortingOrder - 1;
            var fx = go.AddComponent<FxSprite>();
            float s = source.transform.lossyScale.x;
            fx.Init(sr, color, life, s, s, null);
        }

        /// <summary>A straight beam (lance, taunt, shield link) that thins out and fades.</summary>
        public static void Beam(Vector3 from, Vector3 to, Color color, float width, float life)
        {
            var go = new GameObject("beam");
            go.transform.SetParent(Parent(), false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = PixelArt.SpriteMaterial;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            from.y = to.y = 0.35f;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.numCapVertices = 4;
            lr.sortingOrder = 44;
            go.AddComponent<FxBeam>().Init(lr, color, width, life);
            Glow(from, color, width * 3f, life);
            Glow(to, color, width * 5f, life * 1.3f);
        }

        /// <summary>A meteor falling from the sky onto a point; onImpact runs when it lands.</summary>
        public static void Meteor(Vector3 target, float fallTime, Action onImpact)
        {
            var go = new GameObject("meteor");
            go.transform.SetParent(Parent(), false);
            go.AddComponent<FxMeteor>().Init(target, fallTime, onImpact);
        }

        // ----- Time --------------------------------------------------------------------------------

        /// <summary>A tiny freeze on big hits so they feel heavy.</summary>
        public static void HitStop(float seconds) => TimeController.HitStop(seconds);
    }

    /// <summary>Scales and fades a flat sprite over its lifetime, optionally following a unit.</summary>
    public class FxSprite : MonoBehaviour
    {
        SpriteRenderer sr;
        Color color;
        float born, life, s0, s1;
        Unit follow;

        public void Init(SpriteRenderer renderer, Color c, float lifetime, float scaleFrom, float scaleTo, Unit followUnit)
        {
            sr = renderer;
            color = c;
            born = Time.time;
            life = Mathf.Max(0.01f, lifetime);
            s0 = scaleFrom;
            s1 = scaleTo;
            follow = followUnit;
            Update();
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - born) / life);
            if (follow != null)
            {
                var p = follow.Position;
                transform.position = new Vector3(p.x, transform.position.y, p.z);
            }
            transform.localScale = Vector3.one * Mathf.Lerp(s0, s1, 1f - (1f - t) * (1f - t));
            sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
            if (t >= 1f) Destroy(gameObject);
        }
    }

    public class FxBeam : MonoBehaviour
    {
        LineRenderer lr;
        Color color;
        float width, born, life;

        public void Init(LineRenderer line, Color c, float w, float lifetime)
        {
            lr = line;
            color = c;
            width = w;
            born = Time.time;
            life = lifetime;
            Update();
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - born) / life);
            float w = width * (1f - t * t);
            lr.startWidth = w;
            lr.endWidth = w * 0.8f;
            var c = new Color(Mathf.Lerp(1f, color.r, t), Mathf.Lerp(1f, color.g, t), Mathf.Lerp(1f, color.b, t), 1f - t * t);
            lr.startColor = c;
            lr.endColor = c;
            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Falls from up-left toward the target, growing a shadow, then calls back on impact.</summary>
    public class FxMeteor : MonoBehaviour
    {
        static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);
        Vector3 target;
        float born, fallTime;
        Action onImpact;
        Transform rock, shadow;
        SpriteRenderer glow;

        public void Init(Vector3 point, float time, Action impact)
        {
            target = new Vector3(point.x, 0f, point.z);
            fallTime = time;
            onImpact = impact;
            born = Time.time;

            shadow = MakeChild("shadow", PixelArt.Disc(), new Color(0f, 0f, 0f, 0.4f), -7);
            var glowT = MakeChild("glow", PixelArt.Glow(), new Color(1f, 0.55f, 0.2f, 0.7f), 45);
            glow = glowT.GetComponent<SpriteRenderer>();
            rock = MakeChild("rock", PixelArt.Meteor(), Color.white, 46);
            Update();
        }

        Transform MakeChild(string n, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.rotation = Flat;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go.transform;
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - born) / fallTime);
            float h = 1f - t;
            var pos = target + new Vector3(-6f, 0.5f, 7f) * (h * h);
            rock.position = pos;
            rock.localScale = Vector3.one * (1.6f + h * 2.2f);
            rock.rotation = Quaternion.Euler(0f, t * 540f, 0f) * Flat;
            glow.transform.position = pos;
            glow.transform.localScale = Vector3.one * (1.4f + h * 2f);
            shadow.position = target + Vector3.up * 0.02f;
            shadow.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.3f, t);
            if (UnityEngine.Random.value < 0.6f) FX.Burst(pos, new Color(1f, 0.6f, 0.2f), 1, 1.5f, 0.3f, 1.4f);

            if (t >= 1f)
            {
                onImpact?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
