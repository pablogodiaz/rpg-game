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
    public class RotgutBoss : BossController
    {
        public const string Id = "rotgut";
        public const string Name = "Rotgut the Hungry";
        public const float MaxHealth = 13000f;

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

        public override string BossId => Id;
        public override string BossName => Name;
        public override float EnrageTime => 180f;
        public override string[] PullLines => BarkLines.BossPull;
        public override string[] RetryLines => BarkLines.BossRetry;
        public override string[] KillLines => BarkLines.BossKill;
        public override string[] WipeLines => BarkLines.BossWipe;
        public override string[] DeathLines => BarkLines.BossDeath;
        public override string[] InterruptedLines => BarkLines.BossInterrupted;
        public override string[] LootTable => Loot;
        public override string PhaseLabel => Phase2 ? "PHASE 2" : null;
        public override string StatusText => WellFedStacks > 0 ? $"Well Fed x{WellFedStacks}" : null;
        public override bool IsLeaping => leaping;
        public override bool IsMarked(Unit u) => indigestionResolveAt > 0f && marked.Contains(u);

        static readonly string[] Loot =
        {
            "Rotgut's Stained Bib (Epic)",
            "Half-Eaten Drumstick of Haste (Rare)",
            "Belt of Infinite Loosening (Epic)",
            "Gravy Boat of the Bottomless (Legendary... ish)",
        };

        public int WellFedStacks { get; private set; }
        public bool Phase2 { get; private set; }

        float nextFire, nextFlop, nextIndigestion, nextBreakfast;
        bool leaping;
        Vector3 leapFrom, leapTo;
        float leapStart;
        Telegraph flopTelegraph;
        float indigestionResolveAt = -1f;
        readonly List<Unit> marked = new List<Unit>();
        readonly List<Telegraph> markRings = new List<Telegraph>();

        float DamageScale => (1f + 0.15f * WellFedStacks) * (Phase2 ? 1.1f : 1f) * (Enraged ? 5f : 1f);

        public static BossController Spawn(Transform root)
        {
            var go = new GameObject(Name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 0f, 3f);
            var unit = go.AddComponent<Unit>();
            unit.Init(Name, Team.Enemy, Role.Boss, MaxHealth, 1.6f);
            var visual = UnitVisual.Create(unit, PixelArt.Boss(), PixelArt.Foot(new Color32(112, 72, 52, 255), true),
                0.95f, 0.35f, 20, 180f);
            unit.Visual = visual;
            var boss = go.AddComponent<RotgutBoss>();
            boss.Init(unit, visual);
            visual.AimDirection = () => boss.FacingDirection;
            return boss;
        }

        public override void BeginFight(PartyMember tank)
        {
            base.BeginFight(tank);
            float now = Time.time;
            nextFire = now + 7f;
            nextBreakfast = now + 10f;
            nextFlop = now + 22f;
            nextIndigestion = now + 42f;
        }

        protected override void OnEnrage() => Barks.Say(Unit, "I'm not hungry anymore. I'm HANGRY!", 3f, true);

        protected override void OnBossDied()
        {
            leaping = false;
            if (Visual != null) Visual.HeightOffset = 0f;
            ClearMarks();
            if (flopTelegraph != null) Destroy(flopTelegraph.gameObject);
        }

        protected override void FightUpdate(float now, bool isCasting)
        {
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
            if (isCasting) return;

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

        void UpdateMelee(float now)
        {
            var target = UpdateTarget();
            if (target == null) return;

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
                var hit = Unit.Position + FacingDirection * (Unit.Radius + 0.3f);
                FX.Slash(hit, Mathf.Atan2(FacingDirection.x, FacingDirection.z) * Mathf.Rad2Deg, new Color(0.75f, 0.9f, 0.5f), 2.2f);
                FX.Spray(hit, FacingDirection, new Color(0.8f, 0.8f, 0.7f), 8, 6f);
            }
        }

        // ----- Belly Flop (STACK) ------------------------------------------------------------------

        void StartBellyFlop()
        {
            var candidates = LivingParty(false);
            if (candidates.Count == 0) candidates = LivingParty(true);
            if (candidates.Count == 0) return;

            var target = PickRandom(candidates);
            var point = target.Position;
            flopTelegraph = Telegraph.Circle(point, FlopRadius, new Color(1f, 0.55f, 0.1f), FlopTelegraphTime);
            StartCast("Belly Flop", FlopTelegraphTime - LeapDuration, false, () => BeginLeap(point));
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossFlop), 2.5f, true);

            string who = target.DisplayName == "You" ? "YOU! Stay in" : target.DisplayName + "! Run into";
            Enc.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.BellyFlop, Point = point, Radius = FlopRadius,
                ResolveAt = Time.time + FlopTelegraphTime, Target = target, SuggestedCall = RaidCall.Stack,
                Hint = $"BELLY FLOP on {who} the orange circle and call STACK [{{call}}] so 3+ people share the hit.",
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
            var inside = new List<Unit>();
            foreach (var member in Enc.Party)
            {
                if (member.Unit.IsDead) continue;
                if (Vector3.Distance(member.Unit.Position, point) <= FlopRadius + member.Unit.Radius * 0.5f)
                    inside.Add(member.Unit);
            }

            if (inside.Count == 0)
            {
                foreach (var member in Enc.Party)
                    member.Unit.TakeDamage(FlopNobodySoaked * DamageScale, Unit, "Belly Flop (nobody caught him)");
                CommandSystem.Instance.Shout("Nobody caught him! The whole floor shakes!", new Color(1f, 0.5f, 0.2f));
            }
            else
            {
                float each = FlopDamage * DamageScale / inside.Count;
                foreach (var u in inside) u.TakeDamage(each, Unit, $"Belly Flop (split {inside.Count} ways)");
            }

            Telegraph.Flash(point, FlopRadius + 0.5f, new Color(1f, 0.7f, 0.3f), 0.45f);
            FX.Burst(point, new Color(0.55f, 0.5f, 0.45f), 40, 10f, 0.7f, 1.8f); // dust
            CameraRig.Shake(0.35f, 0.35f);
            FX.HitStop(0.08f);
            if (flopTelegraph != null) Destroy(flopTelegraph.gameObject);
            nextAuto = Time.time + 1f;
        }

        // ----- Indigestion (SPREAD) ----------------------------------------------------------------

        void StartIndigestion()
        {
            StartCast("Indigestion", 2f, false, ApplyIndigestionMarks);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossIndigestion), 2.5f, true);
            Enc.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.Indigestion, Radius = IndigestionRadius,
                ResolveAt = Time.time + 2f + IndigestionDelay, SuggestedCall = RaidCall.Spread,
                Hint = "INDIGESTION! Everyone is about to explode. Call SPREAD [{call}] and go to your YOU spot.",
            });
        }

        void ApplyIndigestionMarks()
        {
            ClearMarks();
            foreach (var member in Enc.Party)
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
            foreach (var u in marked)
            {
                if (u == null || u.IsDead) continue;
                Telegraph.Flash(u.Position, IndigestionRadius, new Color(0.7f, 0.35f, 1f), 0.35f);
                FX.Burst(u.Position, new Color(0.55f, 0.85f, 0.3f), 16, 7f, 0.6f, 1.6f); // gas
                foreach (var member in Enc.Party)
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
            Enc.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.InterruptibleCast, ResolveAt = Time.time + 3f, SuggestedCall = RaidCall.Interrupt,
                Hint = "SECOND BREAKFAST (interruptible)! Call INTERRUPT [{call}] before he finishes eating.",
            });
        }

        void CompleteBreakfast()
        {
            Unit.Heal(Unit.MaxHealth * 0.04f, Unit);
            WellFedStacks++;
            FX.Burst(Unit.Position + FacingDirection * 1.8f, new Color(0.75f, 0.5f, 0.3f), 24, 5f, 0.6f); // crumbs
            FX.Glow(Unit.Position, new Color(1f, 0.5f, 0.2f), 3f, 0.6f, Unit);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.BossAte), 2.5f, true);
            CommandSystem.Instance.Shout($"Rotgut is Well Fed x{WellFedStacks} (+{WellFedStacks * 15}% damage)",
                new Color(1f, 0.6f, 0.25f));
        }

        // ----- Grease Fire (move out of bad) -------------------------------------------------------

        void SpawnGreaseFire()
        {
            var candidates = LivingParty(false);
            int count = Phase2 ? 3 : 2;
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int pick = Random.Range(0, candidates.Count);
                FirePuddle.SpawnFire(candidates[pick].Position, 1.6f, 25f, Unit);
                candidates.RemoveAt(pick);
            }
            if (Random.value < 0.35f) Barks.Say(Unit, BarkLines.Pick(BarkLines.BossFire), 2f);
        }
    }
}
