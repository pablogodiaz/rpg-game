using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// Rotgut the Hungry, the first boss. Each mechanic teaches one call:
    ///  - Belly Flop      → STACK     (split damage: needs 3+ people in the circle)
    ///  - Indigestion     → SPREAD    (everyone explodes on everyone near them)
    ///  - Second Breakfast→ INTERRUPT (heals him and stacks a damage buff if he finishes eating)
    ///  - Grease Fire     → move out of bad (handled by each member's Awareness)
    /// Phase 2 at 50%: everything comes faster. Enrage at 3:00.
    /// </summary>
    public class RotgutBoss : MonoBehaviour
    {
        public const string BossId = "rotgut";
        public const string BossName = "Rotgut the Hungry";
        public const float MaxHealth = 13000f;
        public const float EnrageTime = 180f;

        const float AutoAttackDamage = 110f;
        const float AutoAttackInterval = 2f;
        const float ChaseSpeed = 2.4f;
        const float FlopRadius = 3f;
        const float FlopDamage = 2500f;
        const float FlopNobodySoaked = 700f;
        const float FlopTelegraphTime = 4.5f;
        const float LeapDuration = 0.9f;
        const float IndigestionRadius = 4f;
        const float IndigestionDamage = 300f;
        const float IndigestionDelay = 5f;

        public Unit Unit { get; private set; }
        public UnitVisual Visual { get; private set; }

        public string CastName { get; private set; }
        public bool CastInterruptible { get; private set; }
        public float CastStartedAt { get; private set; }
        public float CastEndsAt { get; private set; }
        public bool IsCasting => CastName != null;
        public float CastFraction => IsCasting ? Mathf.InverseLerp(CastStartedAt, CastEndsAt, Time.time) : 0f;

        public int WellFedStacks { get; private set; }
        public bool Phase2 { get; private set; }
        public bool Enraged { get; private set; }
        public bool Fighting { get; private set; }
        public float FightStartedAt { get; private set; }
        public float FightTime => Fighting ? Time.time - FightStartedAt : 0f;
        public Vector3 FacingDirection { get; private set; } = Vector3.back;
        public Unit CurrentTarget { get; private set; }
        public float IndigestionResolveAt => indigestionResolveAt;

        Action onCastComplete;
        readonly Dictionary<Unit, float> threat = new Dictionary<Unit, float>();
        float nextAuto, nextFire, nextFlop, nextIndigestion, nextBreakfast;

        bool leaping;
        Vector3 leapFrom, leapTo;
        float leapStart;
        Telegraph flopTelegraph;

        float indigestionResolveAt = -1f;
        readonly List<Unit> marked = new List<Unit>();
        readonly List<Telegraph> markRings = new List<Telegraph>();

        public void Init(Unit unit, UnitVisual visual)
        {
            Unit = unit;
            Visual = visual;
            unit.Damaged += OnDamaged;
            unit.Died += OnDied;
        }

        float DamageScale => (1f + 0.15f * WellFedStacks) * (Phase2 ? 1.1f : 1f) * (Enraged ? 5f : 1f);

        public void BeginFight(PartyMember tank)
        {
            Fighting = true;
            FightStartedAt = Time.time;
            float now = Time.time;
            nextAuto = now + 1.5f;
            nextFire = now + 7f;
            nextBreakfast = now + 10f;
            nextFlop = now + 22f;
            nextIndigestion = now + 42f;
            threat.Clear();
            if (tank != null) threat[tank.Unit] = 400f; // opening taunt
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossPull), 3f, true);
        }

        void OnDamaged(Unit source, float amount)
        {
            if (source == null || source.Team != Team.Party) return;
            threat.TryGetValue(source, out var t);
            threat[source] = t + amount * (source.Role == Role.Tank ? 6f : 1f);
        }

        void OnDied(Unit u)
        {
            Fighting = false;
            CastName = null;
            leaping = false;
            if (Visual != null) Visual.HeightOffset = 0f;
            ClearMarks();
            if (flopTelegraph != null) Destroy(flopTelegraph.gameObject);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossDeath), 4f, true);
            EncounterManager.Instance.OnBossDefeated();
        }

        void Update()
        {
            var enc = EncounterManager.Instance;
            if (!Fighting || Unit.IsDead || enc == null || enc.State != EncounterState.Fighting) return;
            float now = Time.time;

            if (!Enraged && FightTime >= EnrageTime)
            {
                Enraged = true;
                Barks.Say(Unit, "I'm not hungry anymore. I'm HANGRY!", 3f, true);
                CommandSystem.Instance.Shout("ROTGUT IS HANGRY! (Enrage)", new Color(1f, 0.25f, 0.2f));
            }

            if (!Phase2 && Unit.HealthPercent <= 0.5f)
            {
                Phase2 = true;
                Barks.Say(Unit, "Loosening the belt. Things are about to get messy.", 3.5f, true);
                CommandSystem.Instance.Shout("PHASE 2: Rotgut gets faster", new Color(1f, 0.6f, 0.3f));
            }

            if (indigestionResolveAt > 0f && now >= indigestionResolveAt) ResolveIndigestion();

            if (leaping)
            {
                UpdateLeap(now);
                return;
            }

            if (IsCasting)
            {
                if (now >= CastEndsAt) CompleteCast();
                else FaceTarget();
                return;
            }

            if (now >= nextFire)
            {
                SpawnGreaseFire();
                nextFire = now + (Phase2 ? 10f : 14f);
            }

            if (now >= nextFlop && indigestionResolveAt < 0f)
            {
                StartBellyFlop();
                nextFlop = now + (Phase2 ? 26f : 34f);
                nextIndigestion = Mathf.Max(nextIndigestion, now + 11f);
                return;
            }

            if (now >= nextIndigestion)
            {
                StartIndigestion();
                nextIndigestion = now + (Phase2 ? 26f : 34f);
                nextFlop = Mathf.Max(nextFlop, now + 12f);
                return;
            }

            if (now >= nextBreakfast)
            {
                StartSecondBreakfast();
                nextBreakfast = now + (Phase2 ? 17f : 22f);
                return;
            }

            UpdateMelee(now);
        }

        // ----- Targeting & melee -------------------------------------------------------------------

        Unit HighestThreat()
        {
            var enc = EncounterManager.Instance;
            Unit best = null;
            float bestThreat = -1f;
            foreach (var member in enc.Party)
            {
                if (member.Unit.IsDead) continue;
                threat.TryGetValue(member.Unit, out var t);
                if (t > bestThreat)
                {
                    bestThreat = t;
                    best = member.Unit;
                }
            }
            return best;
        }

        void FaceTarget()
        {
            if (CurrentTarget == null || CurrentTarget.IsDead) return;
            var dir = CurrentTarget.Position - Unit.Position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) FacingDirection = dir.normalized;
        }

        void UpdateMelee(float now)
        {
            var target = HighestThreat();
            if (target == null) return;
            if (target != CurrentTarget && CurrentTarget != null && !CurrentTarget.IsDead && target.Role != Role.Tank)
                Barks.Say(target, BarkLines.Pick(BarkLines.PulledAggro), 2f);
            CurrentTarget = target;
            FaceTarget();

            float dist = Vector3.Distance(Unit.Position, target.Position);
            float reach = Unit.Radius + target.Radius + 0.8f;
            if (dist > reach)
            {
                var step = FacingDirection * (ChaseSpeed * Time.deltaTime);
                transform.position = Arena.Clamp(transform.position + step);
            }
            else if (now >= nextAuto)
            {
                nextAuto = now + AutoAttackInterval;
                target.TakeDamage(AutoAttackDamage * DamageScale, Unit, "Rotgut's Fist");
            }
        }

        // ----- Casting -----------------------------------------------------------------------------

        void StartCast(string castName, float duration, bool interruptible, Action onComplete)
        {
            CastName = castName;
            CastInterruptible = interruptible;
            CastStartedAt = Time.time;
            CastEndsAt = Time.time + duration;
            onCastComplete = onComplete;
        }

        void CompleteCast()
        {
            var callback = onCastComplete;
            CastName = null;
            onCastComplete = null;
            callback?.Invoke();
        }

        public void Interrupt(PartyMember by)
        {
            if (!IsCasting || !CastInterruptible) return;
            CastName = null;
            onCastComplete = null;
            nextAuto = Time.time + 1f;
            CombatLog.RecordInterrupt(by.Unit);
            Barks.Say(by.Unit, BarkLines.Pick(BarkLines.Kicked), 1.6f, true);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossInterrupted), 2.2f, true);
            CommandSystem.Instance.Shout($"{by.Profile.Name} interrupted Second Breakfast!", new Color(1f, 0.95f, 0.4f));
        }

        // ----- Belly Flop (STACK) ------------------------------------------------------------------

        void StartBellyFlop()
        {
            var enc = EncounterManager.Instance;
            var candidates = new List<Unit>();
            foreach (var member in enc.Party)
                if (!member.Unit.IsDead && member.Profile.Role != Role.Tank) candidates.Add(member.Unit);
            if (candidates.Count == 0)
                foreach (var member in enc.Party)
                    if (!member.Unit.IsDead) candidates.Add(member.Unit);
            if (candidates.Count == 0) return;

            var target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var point = target.Position;
            flopTelegraph = Telegraph.Circle(point, FlopRadius, new Color(1f, 0.55f, 0.1f), FlopTelegraphTime);
            StartCast("Belly Flop", FlopTelegraphTime - LeapDuration, false, () => BeginLeap(point));
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossFlop), 2.5f, true);

            enc.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.BellyFlop, Point = point, Radius = FlopRadius,
                ResolveAt = Time.time + FlopTelegraphTime, Target = target, SuggestedCall = RaidCall.Stack,
            });
        }

        void BeginLeap(Vector3 point)
        {
            leaping = true;
            leapFrom = Unit.Position;
            leapTo = point;
            leapStart = Time.time;
        }

        void UpdateLeap(float now)
        {
            float t = (now - leapStart) / LeapDuration;
            if (t >= 1f)
            {
                leaping = false;
                transform.position = leapTo;
                Visual.HeightOffset = 0f;
                ResolveBellyFlop(leapTo);
                return;
            }
            transform.position = Vector3.Lerp(leapFrom, leapTo, t);
            Visual.HeightOffset = Mathf.Sin(t * Mathf.PI) * 3f;
        }

        void ResolveBellyFlop(Vector3 point)
        {
            var enc = EncounterManager.Instance;
            var inside = new List<Unit>();
            foreach (var member in enc.Party)
            {
                if (member.Unit.IsDead) continue;
                if (Vector3.Distance(member.Unit.Position, point) <= FlopRadius + member.Unit.Radius * 0.5f)
                    inside.Add(member.Unit);
            }

            if (inside.Count == 0)
            {
                foreach (var member in enc.Party)
                    member.Unit.TakeDamage(FlopNobodySoaked * DamageScale, Unit, "Belly Flop (nobody caught him)");
                CommandSystem.Instance.Shout("Nobody caught him! The whole floor shakes!", new Color(1f, 0.5f, 0.2f));
            }
            else
            {
                float each = FlopDamage * DamageScale / inside.Count;
                foreach (var u in inside) u.TakeDamage(each, Unit, $"Belly Flop (split {inside.Count} ways)");
            }

            Telegraph.Flash(point, FlopRadius + 0.5f, new Color(1f, 0.7f, 0.3f), 0.45f);
            CameraRig.Shake(0.35f, 0.35f);
            if (flopTelegraph != null) Destroy(flopTelegraph.gameObject);
            nextAuto = Time.time + 1f;
        }

        // ----- Indigestion (SPREAD) ----------------------------------------------------------------

        void StartIndigestion()
        {
            StartCast("Indigestion", 2f, false, ApplyIndigestionMarks);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossIndigestion), 2.5f, true);
            EncounterManager.Instance.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.Indigestion, Radius = IndigestionRadius,
                ResolveAt = Time.time + 2f + IndigestionDelay, SuggestedCall = RaidCall.Spread,
            });
        }

        void ApplyIndigestionMarks()
        {
            ClearMarks();
            foreach (var member in EncounterManager.Instance.Party)
            {
                if (member.Unit.IsDead) continue;
                marked.Add(member.Unit);
                markRings.Add(Telegraph.Circle(member.Unit.Position, IndigestionRadius,
                    new Color(0.7f, 0.35f, 1f), IndigestionDelay, member.Unit));
            }
            indigestionResolveAt = Time.time + IndigestionDelay;
        }

        void ResolveIndigestion()
        {
            indigestionResolveAt = -1f;
            var party = EncounterManager.Instance.Party;
            foreach (var u in marked)
            {
                if (u == null || u.IsDead) continue;
                Telegraph.Flash(u.Position, IndigestionRadius, new Color(0.7f, 0.35f, 1f), 0.35f);
                foreach (var member in party)
                {
                    var v = member.Unit;
                    if (v == u || v.IsDead) continue;
                    if (Vector3.Distance(u.Position, v.Position) < IndigestionRadius)
                        v.TakeDamage(IndigestionDamage * DamageScale, Unit, "Indigestion");
                }
            }
            CameraRig.Shake(0.2f, 0.25f);
            ClearMarks();
        }

        void ClearMarks()
        {
            marked.Clear();
            foreach (var ring in markRings)
                if (ring != null) Destroy(ring.gameObject);
            markRings.Clear();
            indigestionResolveAt = -1f;
        }

        // ----- Second Breakfast (INTERRUPT) --------------------------------------------------------

        void StartSecondBreakfast()
        {
            StartCast("Second Breakfast", 3f, true, CompleteBreakfast);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossBreakfast), 2.5f, true);
            EncounterManager.Instance.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.SecondBreakfast, ResolveAt = Time.time + 3f, SuggestedCall = RaidCall.Interrupt,
            });
        }

        void CompleteBreakfast()
        {
            Unit.Heal(Unit.MaxHealth * 0.04f, Unit);
            WellFedStacks++;
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossAte), 2.5f, true);
            CommandSystem.Instance.Shout($"Rotgut is Well Fed x{WellFedStacks} (+{WellFedStacks * 15}% damage)",
                new Color(1f, 0.6f, 0.25f));
        }

        // ----- Grease Fire (move out of bad) -------------------------------------------------------

        void SpawnGreaseFire()
        {
            var enc = EncounterManager.Instance;
            var candidates = new List<Unit>();
            foreach (var member in enc.Party)
                if (!member.Unit.IsDead && member.Profile.Role != Role.Tank) candidates.Add(member.Unit);

            int count = Phase2 ? 3 : 2;
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int pick = UnityEngine.Random.Range(0, candidates.Count);
                FirePuddle.Spawn(candidates[pick].Position, 1.6f, 25f, Unit);
                candidates.RemoveAt(pick);
            }
            if (UnityEngine.Random.value < 0.35f) Barks.Say(Unit, BarkLines.Pick(BarkLines.BossFire), 2f);
        }
    }
}
