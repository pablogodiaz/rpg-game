using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// One of Krakenna's eight arms: its own target with its own health. Drawn as a chain of swaying
    /// tentacle segments. Arms slam the floor (telegraphed lines), grab party members, and can be broken off.
    /// </summary>
    public class KrakenArm : MonoBehaviour
    {
        public enum ArmState { Idle, WindUp, Slammed, Reaching, Grabbing, Broken }

        public const int Segments = 10;
        public const float ArmMaxHealth = 800f;
        public const float GripHealth = 450f;
        public const float GripDuration = 8f;
        const float RestLength = 5.2f;
        const float SlamLength = 9.5f;
        const float SlamWidth = 2f;
        const float SlamDamage = 400f;
        const float HugTickDamage = 35f;
        const float SqueezeDamage = 600f;

        public string ArmName { get; private set; }
        public Unit Unit { get; private set; }
        public ArmState State { get; private set; }
        public float BaseAngle { get; private set; }
        public bool Frenzied;

        public bool IsBroken => State == ArmState.Broken;
        public bool IsIdle => State == ArmState.Idle;
        public PartyMember Grabbed { get; private set; }
        public float GripFraction => Grabbed != null ? Mathf.Clamp01(gripTaken / GripHealth) : 0f;
        public float GrabTimeLeft => Grabbed != null ? Mathf.Max(0f, grabEndsAt - Time.time) : 0f;

        KrakennaBoss boss;
        Transform visualRoot;
        readonly SpriteRenderer[] segs = new SpriteRenderer[Segments];
        readonly SpriteRenderer[] shadows = new SpriteRenderer[Segments];
        readonly Vector3[] points = new Vector3[Segments];
        float phase;
        float curAngle, targetAngle;
        float curLength, targetLength;
        float lift;
        float stateEndsAt;
        Telegraph slamTelegraph;
        Vector3 slamStart, slamDir;
        PartyMember reachTarget;
        float gripTaken, grabEndsAt, nextHugTick;
        float flashUntil;
        Unit lastAttacker;

        static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);

        static Vector3 Dir(float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        public Vector3 RootPoint => boss.Unit.Position + Dir(BaseAngle) * (boss.Unit.Radius * 0.75f);
        public Vector3 Tip => points[Segments - 1];

        public static KrakenArm Create(KrakennaBoss boss, int index, float angle, string armName, Transform root)
        {
            var go = new GameObject("Arm " + armName);
            go.transform.SetParent(root, false);
            var unit = go.AddComponent<Unit>();
            unit.Init(armName, Team.Enemy, Role.Add, ArmMaxHealth, 0.75f);

            var arm = go.AddComponent<KrakenArm>();
            arm.boss = boss;
            arm.Unit = unit;
            arm.ArmName = armName;
            arm.BaseAngle = angle;
            arm.curAngle = arm.targetAngle = angle;
            arm.curLength = arm.targetLength = RestLength;
            arm.phase = index * 1.37f;

            arm.visualRoot = new GameObject("ArmVisual " + armName).transform;
            arm.visualRoot.SetParent(root, false);
            for (int i = 0; i < Segments; i++)
            {
                float t = i / (Segments - 1f);
                var sprite = PixelArt.TentacleSegment(Mathf.RoundToInt(Mathf.Lerp(16f, 6f, t)));
                arm.shadows[i] = MakeSeg(arm.visualRoot, sprite, 1);
                arm.shadows[i].color = new Color(0f, 0f, 0f, 0.3f);
                arm.segs[i] = MakeSeg(arm.visualRoot, sprite, 2 + (Segments - i) / 2);
            }

            unit.Damaged += arm.OnDamaged;
            unit.Died += arm.OnBroken;
            arm.UpdateShape();
            return arm;
        }

        static SpriteRenderer MakeSeg(Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject("seg");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Flat;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // ----- Commands from the boss --------------------------------------------------------------

        /// <summary>Raise the arm and slam it along a line toward a point.</summary>
        public void Slam(Vector3 toward, float windUp, float damageScale)
        {
            slamStart = RootPoint;
            slamDir = toward - slamStart;
            slamDir.y = 0f;
            if (slamDir.sqrMagnitude < 0.01f) slamDir = Dir(BaseAngle);
            slamDir.Normalize();
            targetAngle = Mathf.Atan2(slamDir.x, slamDir.z) * Mathf.Rad2Deg;
            targetLength = SlamLength;
            slamTelegraph = Telegraph.Line(slamStart, slamDir, SlamLength, SlamWidth, new Color(0.95f, 0.35f, 0.55f), windUp);
            State = ArmState.WindUp;
            stateEndsAt = Time.time + windUp;
            pendingScale = damageScale;
        }

        float pendingScale = 1f;

        /// <summary>Reach for a party member; grabs them after a short telegraph unless they dodge or get away.</summary>
        public void Reach(PartyMember target)
        {
            pendingScale = boss.Enraged ? 5f : 1f;
            reachTarget = target;
            State = ArmState.Reaching;
            stateEndsAt = Time.time + 0.7f;
            Telegraph.Circle(target.Unit.Position, 1.1f, new Color(0.95f, 0.35f, 0.55f), 0.7f, target.Unit);
        }

        // ----- Per frame ---------------------------------------------------------------------------

        void Update()
        {
            float now = Time.time;
            switch (State)
            {
                case ArmState.Idle:
                    targetAngle = BaseAngle;
                    targetLength = RestLength;
                    lift = Mathf.MoveTowards(lift, Frenzied ? 0.45f : 0f, Time.deltaTime * 2f);
                    break;
                case ArmState.WindUp:
                    lift = Mathf.MoveTowards(lift, 1f, Time.deltaTime * 2.5f);
                    if (now >= stateEndsAt) DoSlam();
                    break;
                case ArmState.Slammed:
                    lift = 0f;
                    if (now >= stateEndsAt) State = ArmState.Idle;
                    break;
                case ArmState.Reaching:
                    UpdateReach(now);
                    break;
                case ArmState.Grabbing:
                    UpdateGrab(now);
                    break;
                case ArmState.Broken:
                    lift = 0f;
                    break;
            }

            float turn = State == ArmState.Slammed ? 2000f : 200f;
            curAngle = Mathf.MoveTowardsAngle(curAngle, targetAngle, turn * Time.deltaTime);
            float reel = State == ArmState.Slammed ? 40f : State == ArmState.Grabbing ? 0.7f : 6f;
            curLength = Mathf.MoveTowards(curLength, targetLength, reel * Time.deltaTime);
            UpdateShape();

            if (Grabbed != null)
            {
                Grabbed.transform.position = Arena.Clamp(Tip);
                if (Grabbed.Unit.Visual != null) Grabbed.Unit.Visual.HeightOffset = 0.6f;
            }
        }

        void UpdateShape()
        {
            var p = RootPoint;
            int visible = IsBroken ? 3 : Segments;
            float step = curLength / (Segments - 1);
            float swayAmount = State == ArmState.Idle ? 16f : State == ArmState.WindUp ? 5f : 2f;
            float swaySpeed = State == ArmState.WindUp ? 10f : 1.7f;
            bool flash = Time.time < flashUntil;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f + phase);

            for (int i = 0; i < Segments; i++)
            {
                float t = i / (Segments - 1f);
                points[i] = p;
                float yaw = curAngle + Mathf.Sin(Time.time * swaySpeed + phase + t * 3f) * swayAmount * t;
                if (i < Segments - 1) p += Dir(yaw) * step;

                bool show = i < visible;
                segs[i].enabled = show;
                shadows[i].enabled = show;
                if (!show) continue;

                float scale = 1f + lift * 0.4f * t;
                segs[i].transform.position = points[i] + Vector3.up * (0.06f + i * 0.002f);
                segs[i].transform.localScale = Vector3.one * scale;
                float shadowDist = 0.12f + lift * t * 0.6f;
                shadows[i].transform.position = points[i] + new Vector3(-shadowDist * 0.7f, 0.03f, -shadowDist);
                shadows[i].transform.localScale = Vector3.one * scale;

                Color c = Color.white;
                if (IsBroken) c = i == visible - 1 ? new Color(0.55f, 0.15f, 0.25f) : new Color(0.7f, 0.6f, 0.65f);
                else if (flash) c = new Color(1f, 0.55f, 0.6f);
                else if (Frenzied || State == ArmState.WindUp) c = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.5f), 0.4f + 0.4f * pulse);
                segs[i].color = c;
            }

            // The arm's targetable body sits around the middle of the tentacle.
            transform.position = points[IsBroken ? 1 : Segments / 2];
        }

        void DoSlam()
        {
            State = ArmState.Slammed;
            stateEndsAt = Time.time + 0.9f;
            curAngle = targetAngle;
            var enc = EncounterManager.Instance;
            foreach (var member in enc.Party)
            {
                if (member.Unit.IsDead || member.IsHeld) continue;
                if (slamTelegraph != null && slamTelegraph.Contains(member.Unit.Position, member.Unit.Radius * 0.5f))
                    member.Unit.TakeDamage(SlamDamage * pendingScale, boss.Unit, "Tentacle Slam");
            }
            for (int i = 0; i < 6; i++)
            {
                var along = slamStart + slamDir * (SlamLength * (i + 0.5f) / 6f);
                FX.Burst(along, new Color(0.45f, 0.65f, 0.7f), 6, 6f, 0.5f, 1.5f); // water splash
            }
            Telegraph.Flash(slamStart + slamDir * (SlamLength * 0.75f), 1.6f, new Color(0.95f, 0.45f, 0.6f), 0.3f);
            CameraRig.Shake(0.18f, 0.18f);
            FX.HitStop(0.03f);
            if (slamTelegraph != null) Destroy(slamTelegraph.gameObject);
        }

        void UpdateReach(float now)
        {
            if (reachTarget == null || reachTarget.Unit.IsDead)
            {
                State = ArmState.Idle;
                return;
            }
            var to = reachTarget.Unit.Position - RootPoint;
            targetAngle = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            targetLength = Mathf.Clamp(to.magnitude, 2f, SlamLength);
            lift = Mathf.MoveTowards(lift, 0.6f, Time.deltaTime * 3f);
            if (now < stateEndsAt) return;

            bool inReach = to.magnitude <= SlamLength + 0.5f;
            if (!inReach || reachTarget.Unit.Invulnerable || reachTarget.IsHeld)
            {
                FX.Text(reachTarget.Unit.Position + Vector3.forward * 0.8f, "TOO SLIPPERY!", new Color(0.8f, 0.9f, 1f), 13, 0.8f, true);
                State = ArmState.Idle;
                return;
            }

            Grabbed = reachTarget;
            Grabbed.HeldBy = Unit;
            gripTaken = 0f;
            grabEndsAt = now + GripDuration;
            nextHugTick = now + 0.5f;
            State = ArmState.Grabbing;
            FX.Burst(Grabbed.Unit.Position, new Color(0.95f, 0.5f, 0.7f), 14, 5f);
            Barks.Say(Grabbed.Unit, BarkLines.Pick(BarkLines.Grabbed), 2.5f, true);
        }

        void UpdateGrab(float now)
        {
            if (Grabbed == null || Grabbed.Unit.IsDead)
            {
                Release(false);
                return;
            }
            targetLength = 3f; // slowly reels the victim in toward the beak
            lift = 0.6f;

            if (now >= nextHugTick)
            {
                nextHugTick += 0.5f;
                Grabbed.Unit.TakeDamage(HugTickDamage * pendingScale, boss.Unit, "Clingy Hug");
            }

            if (gripTaken >= GripHealth)
            {
                var freed = Grabbed;
                Release(true);
                FX.Callout(freed.Unit, "FREED!", new Color(0.6f, 1f, 0.7f));
                Barks.Say(freed.Unit, BarkLines.Pick(BarkLines.Freed), 2f, true);
                return;
            }

            if (now >= grabEndsAt)
            {
                var victim = Grabbed;
                Release(false);
                victim.Unit.TakeDamage(SqueezeDamage * pendingScale, boss.Unit, "Clingy Squeeze");
                // ...and throws them across the room.
                var away = victim.Unit.Position - boss.Unit.Position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                victim.transform.position = Arena.Clamp(boss.Unit.Position + away.normalized * 11f);
                FX.Burst(victim.Unit.Position, new Color(0.6f, 0.6f, 0.65f), 20, 7f, 0.6f, 1.6f);
                Telegraph.Flash(victim.Unit.Position, 1.6f, new Color(0.95f, 0.45f, 0.6f), 0.35f);
                CameraRig.Shake(0.2f, 0.2f);
                FX.Callout(victim.Unit, "THROWN!", new Color(1f, 0.5f, 0.6f));
            }
        }

        /// <summary>Lets go of everything and stops attacking (the boss died).</summary>
        public void Disengage()
        {
            if (Grabbed != null) Release(true);
            if (slamTelegraph != null) Destroy(slamTelegraph.gameObject);
            Frenzied = false;
            if (!IsBroken) State = ArmState.Idle;
        }

        /// <summary>Struggling (the leader's dodge while held) loosens the grip.</summary>
        public void AddGrip(float amount)
        {
            if (State != ArmState.Grabbing) return;
            gripTaken += amount;
            FX.Burst(Tip, new Color(1f, 0.9f, 0.6f), 8, 4f);
        }

        void Release(bool freed)
        {
            if (Grabbed != null)
            {
                Grabbed.HeldBy = null;
                if (Grabbed.Unit.Visual != null) Grabbed.Unit.Visual.HeightOffset = 0f;
                if (freed) FX.Burst(Grabbed.Unit.Position, new Color(0.6f, 1f, 0.7f), 16, 5f);
            }
            Grabbed = null;
            if (!IsBroken) State = ArmState.Idle;
        }

        // ----- Damage & breaking -------------------------------------------------------------------

        void OnDamaged(Unit source, float amount)
        {
            flashUntil = Time.time + 0.08f;
            if (source != null) lastAttacker = source;
            if (State == ArmState.Grabbing) gripTaken += amount;
        }

        void OnBroken(Unit u)
        {
            if (Grabbed != null) Release(true);
            if (slamTelegraph != null) Destroy(slamTelegraph.gameObject);
            State = ArmState.Broken;
            Frenzied = false;

            // The severed part flies off.
            var cut = points[3];
            for (int i = 3; i < Segments; i++)
            {
                var outward = points[i] - boss.Unit.Position;
                outward.y = 0f;
                var vel = outward.normalized * Random.Range(4f, 8f) + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                FX.Debris(segs[i].sprite, points[i], vel, Random.Range(-540f, 540f), 1.4f);
            }
            FX.Burst(cut, new Color(0.25f, 0.15f, 0.35f), 30, 8f, 0.7f, 1.6f); // ink
            FX.Burst(cut, new Color(0.95f, 0.5f, 0.7f), 16, 6f, 0.5f);
            Telegraph.Flash(cut, 2f, new Color(0.6f, 0.4f, 0.9f), 0.45f);
            FirePuddle.SpawnInk(cut, 1.3f, 10f, boss.Unit);
            CameraRig.Shake(0.25f, 0.25f);
            FX.HitStop(0.08f);
            boss.OnArmBroken(this, lastAttacker);
        }
    }
}
