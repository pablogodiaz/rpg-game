using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// Krakenna the Clingy, the second boss. A giant octopus in the middle of the room with eight named arms.
    /// Every arm is its own target; breaking one off rips 4% of her health away and removes its attacks.
    ///  - Tentacle Slam  → dodge the pink line (Awareness-driven for the AI)
    ///  - Clingy Hug     → FOCUS the arm holding someone: 450 damage frees them, or they get squeezed and thrown
    ///  - Ink Cloud      → INTERRUPT, or everyone gets an ink puddle that slows and burns
    ///  - Clingy Frenzy  → at 70% and 40%: arms go wild for 10s, then an Ink Tsunami hits the raid for every
    ///                     arm still attached. Breaking arms early makes these survivable.
    /// Losing all eight arms leaves her Exposed (+50% damage taken). Enrage at 4:00.
    /// </summary>
    public class KrakennaBoss : BossController
    {
        public const string Id = "krakenna";
        public const string Name = "Krakenna the Clingy";
        public const float MaxHealth = 15000f;
        public const int ArmCount = 8;

        const float BiteDamage = 110f;
        const float SpitDamage = 90f;
        const float TsunamiPerArm = 110f;
        const float FrenzyDuration = 10f;
        static readonly float[] FrenzyThresholds = { 0.7f, 0.4f };
        static readonly string[] ArmNames = { "Gary", "Hugsy", "Lefty", "Snacky", "Tickles", "Pointy", "Waver", "Sir Reginald" };

        public override string BossId => Id;
        public override string BossName => Name;
        public override float EnrageTime => 240f;
        public override string[] PullLines => BarkLines.KrakenPull;
        public override string[] RetryLines => BarkLines.KrakenRetry;
        public override string[] KillLines => BarkLines.KrakenKill;
        public override string[] WipeLines => BarkLines.KrakenWipe;
        public override string[] DeathLines => BarkLines.KrakenDeath;
        public override string[] InterruptedLines => BarkLines.KrakenInterrupted;
        public override string[] LootTable => Loot;
        public override string PhaseLabel => frenzyActive ? "FRENZY" : exposed ? "EXPOSED" : null;
        public override string StatusText => $"Arms {IntactArms}/{ArmCount}";
        public override IReadOnlyList<Unit> ExtraTargets => armUnits;

        static readonly string[] Loot =
        {
            "Suction Cup of Eternal Hugging (Epic)",
            "Ink-Stained Love Letter (Rare)",
            "Krakenna's Pink Bow (Legendary, for fashion)",
            "Calamari Platter (Food, Grenn wants it)",
        };

        public readonly List<KrakenArm> Arms = new List<KrakenArm>();
        readonly List<Unit> armUnits = new List<Unit>();
        public int IntactArms
        {
            get
            {
                int n = 0;
                foreach (var a in Arms) if (!a.IsBroken) n++;
                return n;
            }
        }

        float nextSlam, nextGrab, nextInk, nextFrenzySlam;
        int frenzyIndex;
        bool frenzyActive;
        bool exposed;

        float DamageScale => Enraged ? 5f : 1f;

        public static BossController Spawn(Transform root)
        {
            var go = new GameObject(Name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 0f, 2f);
            var unit = go.AddComponent<Unit>();
            unit.Init(Name, Team.Enemy, Role.Boss, MaxHealth, 1.7f);
            var visual = UnitVisual.Create(unit, PixelArt.Kraken(), null, 0f, 0f, 20, 180f);
            unit.Visual = visual;
            var boss = go.AddComponent<KrakennaBoss>();
            boss.Init(unit, visual);
            visual.AimDirection = () => boss.FacingDirection;

            for (int i = 0; i < ArmCount; i++)
            {
                var arm = KrakenArm.Create(boss, i, i * 360f / ArmCount + 22.5f, ArmNames[i], root);
                boss.Arms.Add(arm);
                boss.armUnits.Add(arm.Unit);
            }
            return boss;
        }

        public override void BeginFight(PartyMember tank)
        {
            base.BeginFight(tank);
            float now = Time.time;
            nextSlam = now + 5f;
            nextInk = now + 11f;
            nextGrab = now + 15f;
        }

        protected override void OnEnrage() => Barks.Say(Unit, "FINE! If I can't hug you, NOBODY CAN!", 3f, true);

        protected override void OnBossDied()
        {
            frenzyActive = false;
            foreach (var a in Arms) a.Disengage();
        }

        // ----- Main loop ---------------------------------------------------------------------------

        protected override void FightUpdate(float now, bool isCasting)
        {
            // Arms act on their own, even while the head is casting.
            if (frenzyActive)
            {
                if (now >= nextFrenzySlam)
                {
                    TrySlam(1.4f);
                    nextFrenzySlam = now + 1.1f;
                }
                return; // the frenzy is a channel; it ends with the Ink Tsunami (cast completion)
            }

            if (frenzyIndex < FrenzyThresholds.Length && Unit.HealthPercent <= FrenzyThresholds[frenzyIndex] && !isCasting)
            {
                StartFrenzy();
                return;
            }

            if (now >= nextSlam)
            {
                bool slammed = TrySlam(Enraged ? 1.2f : 2f);
                float interval = Enraged ? 1.2f : frenzyIndex == 0 ? 6f : frenzyIndex == 1 ? 5f : 4f;
                nextSlam = now + (slammed ? interval : 1f);
            }

            if (now >= nextGrab)
            {
                TryGrab();
                nextGrab = now + 18f;
            }

            if (isCasting) return;

            if (now >= nextInk)
            {
                StartInkCloud();
                nextInk = now + 24f;
                return;
            }

            UpdateBite(now);
        }

        void UpdateBite(float now)
        {
            var target = UpdateTarget();
            if (target == null || now < nextAuto) return;
            float dist = Vector3.Distance(Unit.Position, target.Position);
            if (dist <= Unit.Radius + target.Radius + 1.2f)
            {
                nextAuto = now + 2f;
                target.TakeDamage(BiteDamage * DamageScale, Unit, "Beak Bite");
                var hit = Unit.Position + FacingDirection * (Unit.Radius + 0.2f);
                FX.Slash(hit, Mathf.Atan2(FacingDirection.x, FacingDirection.z) * Mathf.Rad2Deg, new Color(0.95f, 0.5f, 0.65f), 2.4f);
                FX.Spray(hit, FacingDirection, new Color(0.9f, 0.8f, 0.85f), 8, 6f);
            }
            else
            {
                // Nobody in reach: spit ink at them instead.
                nextAuto = now + 2.5f;
                var victim = target;
                var self = Unit;
                float dmg = SpitDamage * DamageScale;
                Projectile.Fire(Unit.Position, victim, new Color(0.35f, 0.25f, 0.55f), 14f,
                    () => victim.TakeDamage(dmg, self, "Ink Spit"), null, 1.6f, 0.6f);
            }
        }

        // ----- Arms --------------------------------------------------------------------------------

        static float AngleTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        /// <summary>The idle, unbroken arm pointing closest to a world position.</summary>
        KrakenArm ArmToward(Vector3 point)
        {
            KrakenArm best = null;
            float bestDelta = float.MaxValue;
            float angle = AngleTo(Unit.Position, point);
            foreach (var a in Arms)
            {
                if (a.IsBroken || !a.IsIdle) continue;
                float delta = Mathf.Abs(Mathf.DeltaAngle(a.BaseAngle, angle));
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = a;
                }
            }
            return best;
        }

        bool TrySlam(float windUp)
        {
            var candidates = new List<PartyMember>();
            foreach (var m in Enc.Party)
                if (!m.Unit.IsDead && !m.IsHeld) candidates.Add(m);
            if (candidates.Count == 0) return false;
            var target = PickRandom(candidates);
            var arm = ArmToward(target.Unit.Position);
            if (arm == null) return false;
            arm.Slam(target.Unit.Position, windUp, DamageScale);
            if (Random.value < 0.3f) Barks.Say(Unit, BarkLines.Pick(BarkLines.KrakenSlam), 1.6f);
            return true;
        }

        void TryGrab()
        {
            var candidates = new List<PartyMember>();
            foreach (var m in Enc.Party)
                if (!m.Unit.IsDead && !m.IsHeld && m.Profile.Role != Role.Tank) candidates.Add(m);
            if (candidates.Count == 0) return;
            var target = PickRandom(candidates);
            var arm = ArmToward(target.Unit.Position);
            if (arm == null) return;
            arm.Reach(target);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.KrakenGrab), 2.2f, true);

            string who = target.IsLeader ? "YOU" : target.Profile.Name.ToUpperInvariant();
            Enc.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.ArmGrab, Target = target.Unit, Source = arm.Unit, SuggestedCall = RaidCall.Focus,
                ResolveAt = Time.time + 0.7f + KrakenArm.GripDuration,
                Hint = $"{arm.ArmName.ToUpperInvariant()} IS HUGGING {who}! Call FOCUS [{{call}}] on that arm: " +
                       $"{KrakenArm.GripHealth:0} damage frees them." + (target.IsLeader ? " Dodge to wriggle!" : ""),
            });
        }

        public void OnArmBroken(KrakenArm arm, Unit killer)
        {
            int left = IntactArms;
            Unit.TakeDamage(MaxHealth * 0.04f, killer, "Arm Torn Off");
            CommandSystem.Instance.Shout($"{arm.ArmName.ToUpperInvariant()} IS GONE! ({left}/{ArmCount} arms left)", new Color(1f, 0.55f, 0.75f));
            Barks.Say(Unit, string.Format(BarkLines.Pick(BarkLines.KrakenArmLost), arm.ArmName, left), 2.8f, true);
            if (Enc.FocusTarget == arm.Unit) Enc.ClearFocus();

            if (left == 0 && !exposed)
            {
                exposed = true;
                Unit.AddBuff("exposed", "EXPOSED", BuffKind.DamageTaken, 1.5f, 9999f);
                CommandSystem.Instance.Shout("NO ARMS LEFT! Krakenna takes +50% damage!", new Color(1f, 0.85f, 0.3f));
            }
        }

        // ----- Ink Cloud (INTERRUPT) ---------------------------------------------------------------

        void StartInkCloud()
        {
            StartCast("Ink Cloud", 3f, true, CompleteInkCloud);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.KrakenInk), 2.2f, true);
            Enc.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.InterruptibleCast, ResolveAt = Time.time + 3f, SuggestedCall = RaidCall.Interrupt,
                Hint = "INK CLOUD (interruptible)! Call INTERRUPT [{call}] or everyone gets inked (slowed + burned).",
            });
        }

        void CompleteInkCloud()
        {
            foreach (var u in LivingParty(true))
                FirePuddle.SpawnInk(u.Position, 1.8f, 16f, Unit);
            FX.Burst(Unit.Position, new Color(0.2f, 0.12f, 0.3f), 40, 9f, 0.8f, 1.8f);
            FX.Glow(Unit.Position, new Color(0.35f, 0.2f, 0.5f), 5f, 0.7f);
            CommandSystem.Instance.Shout("INKED! Get out of the puddles!", new Color(0.7f, 0.55f, 1f));
        }

        // ----- Clingy Frenzy → Ink Tsunami ----------------------------------------------------------

        void StartFrenzy()
        {
            frenzyIndex++;
            frenzyActive = true;
            nextFrenzySlam = Time.time + 1f;
            foreach (var a in Arms) if (!a.IsBroken) a.Frenzied = true;
            StartCast("Clingy Frenzy", FrenzyDuration, false, InkTsunami);
            Barks.Say(Unit, BarkLines.Pick(BarkLines.KrakenFrenzy), 3f, true);
            CommandSystem.Instance.Shout("CLINGY FRENZY!", new Color(1f, 0.4f, 0.6f));
            Enc.BroadcastMechanic(new MechanicEvent
            {
                Kind = MechanicKind.Frenzy, ResolveAt = Time.time + FrenzyDuration, SuggestedCall = RaidCall.Focus,
                Hint = "CLINGY FRENZY! Every arm still attached adds to the Ink Tsunami in 10s. " +
                       "FOCUS [{call}] arms, dodge the slams, then DEFENSIVES.",
            });
        }

        void InkTsunami()
        {
            frenzyActive = false;
            foreach (var a in Arms) a.Frenzied = false;
            int arms = IntactArms;
            float dmg = TsunamiPerArm * arms * DamageScale;
            foreach (var u in LivingParty(true)) u.TakeDamage(dmg, Unit, $"Ink Tsunami ({arms} arms)");
            Telegraph.Flash(Unit.Position, 14f, new Color(0.25f, 0.55f, 0.9f), 0.9f);
            FX.Burst(Unit.Position, new Color(0.3f, 0.6f, 0.9f), 80, 16f, 0.9f, 2f);
            FX.Burst(Unit.Position, new Color(0.2f, 0.12f, 0.3f), 40, 12f, 1f, 2f);
            CameraRig.Shake(0.5f, 0.5f);
            FX.HitStop(0.1f);
            CommandSystem.Instance.Shout(arms == 0 ? "INK TSUNAMI... with no arms. A light drizzle." : $"INK TSUNAMI! ({arms} arms x {TsunamiPerArm:0})",
                new Color(0.4f, 0.7f, 1f));
            nextSlam = Time.time + 3f;
        }
    }
}
