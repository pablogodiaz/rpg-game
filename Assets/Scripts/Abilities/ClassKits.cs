using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>Gives each class its kit. Tuning lives in the ability classes below.</summary>
    public static class ClassKits
    {
        public static List<Ability> Create(ClassKind kind)
        {
            var list = new List<Ability>();
            switch (kind)
            {
                case ClassKind.Leader:
                    list.Add(new PiercingLance());
                    list.Add(new RallyAbility());
                    list.Add(new BattleRez());
                    break;
                case ClassKind.Guardian:
                    list.Add(new Taunt());
                    list.Add(new ShieldSlam());
                    list.Add(new Bulwark());
                    break;
                case ClassKind.Cleric:
                    list.Add(new DivineShield());
                    list.Add(new HolyNova());
                    break;
                case ClassKind.Duelist:
                    list.Add(new BladeFlurry());
                    break;
                case ClassKind.Mage:
                    list.Add(new Meteor());
                    list.Add(new ArcaneBarrage());
                    list.Add(new Blink());
                    break;
            }
            return list;
        }
    }

    // =============================================================================================
    // Grenn — Guardian (tank)
    // =============================================================================================

    /// <summary>Yanks the boss back when it turns on someone squishy. A yellow tether and a shout.</summary>
    public class Taunt : Ability
    {
        public override string Name => "Taunt";
        public override string Short => "TA";
        public override float Cooldown => 8f;
        public override string Description => "Forces Rotgut to attack him";
        public override Color Color => new Color(1f, 0.85f, 0.3f);
        protected override float OpeningDelay => 0f;

        protected override bool Prepare(bool forced)
        {
            var target = Boss.CurrentTarget;
            return forced || (target != null && target != Me && !target.IsDead);
        }

        protected override void Execute()
        {
            Boss.ForceTarget(Me);
            FX.Beam(Me.Position, Boss.Unit.Position, Color, 0.22f, 0.4f);
            FX.Burst(Boss.Unit.Position, Color, 14, 6f);
            Barks.Say(Me, BarkLines.Pick(BarkLines.Taunt), 1.8f, true);
        }
    }

    /// <summary>Slams the shield into the ground: shockwave, big threat, staggers the boss.</summary>
    public class ShieldSlam : Ability
    {
        public override string Name => "Shield Slam";
        public override string Short => "SS";
        public override float Cooldown => 9f;
        public override string Description => "Shockwave: damage, threat, staggers the boss";
        public override Color Color => new Color(1f, 0.92f, 0.6f);

        protected override bool Prepare(bool forced) => DistToBossEdge <= 1.8f;

        protected override void Execute()
        {
            Owner.Deal(Boss.Unit, 90f, Name);
            Boss.AddThreat(Me, 900f);
            Boss.Stagger(0.8f);
            Telegraph.Flash(Me.Position, 2.6f, Color, 0.35f);
            FX.Burst(Me.Position, new Color(0.85f, 0.88f, 0.95f), 22, 9f, 0.4f);
            CameraRig.Shake(0.12f, 0.15f);
            FX.HitStop(0.045f);
        }
    }

    /// <summary>A golden shield bubble: takes 55% less damage for 6 seconds.</summary>
    public class Bulwark : Ability
    {
        public override string Name => "Bulwark";
        public override string Short => "BW";
        public override float Cooldown => 28f;
        public override string Description => "-55% damage taken for 6s (DEFENSIVES)";
        public override Color Color => new Color(1f, 0.8f, 0.25f);
        public override bool IsDefensive => true;

        protected override bool Prepare(bool forced) => forced || Me.HealthPercent < 0.45f;

        protected override void Execute()
        {
            Me.AddBuff("bulwark", "BULWARK", BuffKind.DamageTaken, 0.45f, 6f);
            Telegraph.Flash(Me.Position, 1.6f, Color, 0.3f);
            FX.Burst(Me.Position, Color, 16, 4f);
        }
    }

    // =============================================================================================
    // Pip — Cleric (healer)
    // =============================================================================================

    /// <summary>A cyan bubble that absorbs 450 damage on whoever is about to die.</summary>
    public class DivineShield : Ability
    {
        public override string Name => "Divine Shield";
        public override string Short => "DS";
        public override float Cooldown => 20f;
        public override string Description => "Bubble that absorbs 450 damage (DEFENSIVES)";
        public override Color Color => new Color(0.55f, 0.9f, 1f);
        public override bool IsDefensive => true;

        Unit target;

        protected override bool Prepare(bool forced)
        {
            target = null;
            float best = forced ? 2f : 0.35f;
            foreach (var p in LivingParty())
            {
                if (Vector3.Distance(p.Unit.Position, Me.Position) > PartyMember.HealRange + 2f) continue;
                float pct = p.Unit.HealthPercent - (p.Profile.Role == Role.Tank && forced ? 0.25f : 0f);
                if (pct < best)
                {
                    best = pct;
                    target = p.Unit;
                }
            }
            return target != null;
        }

        protected override void Execute()
        {
            target.AddAbsorb(450f, 8f);
            FX.Beam(Me.Position, target.Position, Color, 0.16f, 0.35f);
            Telegraph.Flash(target.Position, 1.4f, Color, 0.35f);
            FX.Burst(target.Position, Color, 16, 4f);
            if (target != Me) Barks.Say(target, BarkLines.Pick(BarkLines.Shielded), 1.6f);
        }
    }

    /// <summary>An expanding ring of holy light that heals everyone nearby.</summary>
    public class HolyNova : Ability
    {
        public override string Name => "Holy Nova";
        public override string Short => "HN";
        public override float Cooldown => 14f;
        public override string Description => "Heals everyone within 7m for 220";
        public override Color Color => new Color(1f, 0.95f, 0.55f);
        const float Radius = 7f;

        protected override bool Prepare(bool forced)
        {
            int hurt = 0;
            bool critical = false;
            foreach (var p in LivingParty())
            {
                if (Vector3.Distance(p.Unit.Position, Me.Position) > Radius) continue;
                if (p.Unit.HealthPercent < 0.75f) hurt++;
                if (p.Unit.HealthPercent < 0.4f) critical = true;
            }
            return forced || hurt >= 3 || (hurt >= 2 && critical);
        }

        protected override void Execute()
        {
            Telegraph.Flash(Me.Position, Radius, Color, 0.55f);
            FX.Glow(Me.Position, Color, 3f, 0.6f);
            foreach (var p in LivingParty())
            {
                if (Vector3.Distance(p.Unit.Position, Me.Position) > Radius) continue;
                p.Unit.Heal(220f, Me);
                FX.Burst(p.Unit.Position, new Color(0.6f, 1f, 0.6f), 8, 3f, 0.5f);
            }
        }
    }

    // =============================================================================================
    // Vex — Duelist (melee)
    // =============================================================================================

    /// <summary>Shadowsteps through the boss leaving afterimages, then a flurry of slashes and a finisher.</summary>
    public class BladeFlurry : Ability
    {
        public override string Name => "Blade Flurry";
        public override string Short => "BF";
        public override float Cooldown => 12f;
        public override string Description => "Dash through the boss + 6 slashes (BURN)";
        public override Color Color => new Color(1f, 0.4f, 0.4f);
        public override bool IsBurst => true;

        protected override bool Prepare(bool forced) => DistToBossEdge <= (forced ? 8f : 4f);

        protected override void Execute() => Owner.StartCoroutine(Run());

        IEnumerator Run()
        {
            Owner.SetBusy(1.3f);
            var boss = Boss;
            var start = Me.Position;
            var through = (boss.Unit.Position - start);
            through.y = 0f;
            var end = Arena.Clamp(boss.Unit.Position + through.normalized * (boss.Unit.Radius + 0.9f));

            // Shadowstep
            const int steps = 6;
            for (int i = 1; i <= steps; i++)
            {
                if (Me.IsDead) yield break;
                FX.Afterimage(Me.Visual.Body, new Color(0.7f, 0.2f, 0.3f, 0.7f), 0.35f);
                Owner.transform.position = Vector3.Lerp(start, end, i / (float)steps);
                yield return new WaitForSeconds(0.025f);
            }
            FX.Burst(Me.Position, new Color(0.6f, 0.2f, 0.3f), 10, 5f);

            // Flurry
            for (int i = 0; i < 6; i++)
            {
                if (Me.IsDead || boss.Unit.IsDead) yield break;
                bool finisher = i == 5;
                var toBoss = boss.Unit.Position - Me.Position;
                float yaw = Mathf.Atan2(toBoss.x, toBoss.z) * Mathf.Rad2Deg + Random.Range(-50f, 50f);
                var hitPoint = Me.Position + toBoss.normalized * 0.9f;
                Owner.Deal(boss.Unit, finisher ? 60f : 24f, Name);
                FX.Slash(hitPoint, yaw, finisher ? new Color(1f, 0.95f, 0.8f) : Color, finisher ? 2.2f : 1.4f);
                FX.Spray(hitPoint, toBoss, new Color(1f, 0.85f, 0.6f), finisher ? 12 : 5, 7f);
                if (finisher)
                {
                    CameraRig.Shake(0.12f, 0.12f);
                    FX.HitStop(0.05f);
                }
                yield return new WaitForSeconds(finisher ? 0.1f : 0.1f);
            }
        }
    }

    // =============================================================================================
    // Moira — Mage (ranged)
    // =============================================================================================

    /// <summary>Calls down a meteor on the boss: telegraph, fall, big explosion.</summary>
    public class Meteor : Ability
    {
        public override string Name => "Meteor";
        public override string Short => "ME";
        public override float Cooldown => 20f;
        public override string Description => "1s later: 300 damage explosion (BURN)";
        public override Color Color => new Color(1f, 0.5f, 0.15f);
        public override bool IsBurst => true;
        const float Radius = 2.2f;

        protected override bool Prepare(bool forced) => DistToBossEdge <= PartyMember.SpellRange && !Boss.IsLeaping;

        protected override void Execute()
        {
            var point = Boss.Unit.Position;
            Telegraph.Circle(point, Radius, Color, 1.1f);
            if (Random.value < 0.4f) Barks.Say(Me, BarkLines.Pick(BarkLines.Meteor), 1.8f);
            var owner = Owner;
            var boss = Boss;
            FX.Meteor(point, 1.1f, () =>
            {
                if (boss != null && !boss.Unit.IsDead &&
                    Vector3.Distance(boss.Unit.Position, point) <= Radius + boss.Unit.Radius)
                    owner.Deal(boss.Unit, 300f, "Meteor");
                Telegraph.Flash(point, 3f, new Color(1f, 0.6f, 0.2f), 0.5f);
                FX.Glow(point, new Color(1f, 0.55f, 0.2f), 4f, 0.5f);
                FX.Burst(point, new Color(1f, 0.75f, 0.3f), 30, 11f, 0.55f, 1.6f);
                FX.Burst(point, new Color(0.35f, 0.3f, 0.3f), 16, 6f, 0.8f, 1.8f);
                FX.FlatSprite(PixelArt.Scorch(), point, Random.Range(0f, 360f), Color.white, 4f, 2.4f, 2.4f, -7);
                CameraRig.Shake(0.35f, 0.3f);
                FX.HitStop(0.07f);
            });
        }
    }

    /// <summary>Six arcane missiles that spray outward and curve into the boss.</summary>
    public class ArcaneBarrage : Ability
    {
        public override string Name => "Arcane Barrage";
        public override string Short => "AB";
        public override float Cooldown => 8f;
        public override string Description => "6 curving missiles, 18 damage each";
        public override Color Color => new Color(0.78f, 0.5f, 1f);

        protected override bool Prepare(bool forced) => DistToBossEdge <= PartyMember.SpellRange;

        protected override void Execute() => Owner.StartCoroutine(Run());

        IEnumerator Run()
        {
            for (int i = 0; i < 6; i++)
            {
                var boss = Boss;
                if (Me.IsDead || boss == null || boss.Unit.IsDead) yield break;
                var toBoss = boss.Unit.Position - Me.Position;
                toBoss.y = 0f;
                float side = (i % 2 == 0 ? 1f : -1f) * Random.Range(45f, 85f);
                var launch = Quaternion.AngleAxis(side, Vector3.up) * toBoss.normalized * 11f;
                var owner = Owner;
                var target = boss.Unit;
                Projectile.Fire(Me.Position, target, Color, 16f, () => owner.Deal(target, 18f, "Arcane Barrage"), launch, 0.8f, 0.5f);
                FX.Burst(Me.Position, Color, 3, 3f, 0.25f);
                yield return new WaitForSeconds(0.07f);
            }
        }
    }

    /// <summary>Teleports instead of walking: out of fire, into stacks, to spread spots.</summary>
    public class Blink : Ability
    {
        public override string Name => "Blink";
        public override string Short => "BL";
        public override float Cooldown => 7f;
        public override string Description => "Teleports out of fire or into position";
        public override Color Color => new Color(0.75f, 0.45f, 1f);
        public override bool AutoUse => false;
        protected override float OpeningDelay => 0f;

        public Vector3 Destination;

        protected override bool Prepare(bool forced) => Vector3.Distance(Me.Position, Destination) > 2.5f;

        protected override void Execute()
        {
            var from = Me.Position;
            FX.Afterimage(Me.Visual.Body, new Color(0.7f, 0.4f, 1f, 0.8f), 0.4f);
            Telegraph.Flash(from, 1f, Color, 0.25f);
            FX.Burst(from, Color, 12, 5f);
            Owner.transform.position = Arena.Clamp(Destination);
            Telegraph.Flash(Me.Position, 1.2f, Color, 0.3f);
            FX.Burst(Me.Position, Color, 12, 4f);
        }
    }

    // =============================================================================================
    // You — Raid Leader (player-controlled)
    // =============================================================================================

    /// <summary>A thick golden beam straight into the boss.</summary>
    public class PiercingLance : Ability
    {
        public override string Name => "Piercing Lance";
        public override string Short => "PL";
        public override float Cooldown => 7f;
        public override string Description => "Golden beam, 150 damage";
        public override Color Color => new Color(1f, 0.88f, 0.4f);
        public override bool AutoUse => false;
        protected override float OpeningDelay => 0f;

        protected override bool Prepare(bool forced) => DistToBossEdge <= LeaderController.AttackRange + 2f;

        protected override void Execute()
        {
            var boss = Boss.Unit;
            Owner.Deal(boss, 150f, Name);
            FX.Beam(Me.Position, boss.Position, Color, 0.45f, 0.35f);
            FX.Spray(boss.Position, boss.Position - Me.Position, Color, 16, 9f, 40f);
            CameraRig.Shake(0.1f, 0.12f);
            FX.HitStop(0.035f);
        }
    }

    /// <summary>+30% damage for the whole party, with a golden shockwave from the leader.</summary>
    public class RallyAbility : Ability
    {
        public override string Name => "Rally";
        public override string Short => "RA";
        public override float Cooldown => 90f;
        public override string Description => "+30% party damage for 12s";
        public override Color Color => new Color(1f, 0.85f, 0.3f);
        public override bool AutoUse => false;
        protected override float OpeningDelay => 0f;

        protected override bool Prepare(bool forced) => true;

        protected override void Execute()
        {
            foreach (var p in LivingParty())
            {
                p.Unit.AddBuff("rally", "RALLY", BuffKind.DamageDone, 1.3f, 12f);
                FX.Burst(p.Unit.Position, Color, 8, 3f);
            }
            Telegraph.Flash(Me.Position, 9f, Color, 0.6f);
            CommandSystem.Instance.Shout("RALLY! (+30% damage)", Color);
            Barks.Say(Me, BarkLines.Pick(BarkLines.Rally), 2f, true);
        }
    }

    /// <summary>Brings the most recently fallen ally back with a pillar of light. One charge per pull.</summary>
    public class BattleRez : Ability
    {
        public override string Name => "Battle Rez";
        public override string Short => "BR";
        public override float Cooldown => 3f;
        public override string Description => "Revive a fallen ally at 40% (1 per pull)";
        public override Color Color => new Color(1f, 1f, 0.8f);
        public override bool AutoUse => false;
        public override int Charges => charges;
        protected override float OpeningDelay => 0f;

        int charges = 1;
        Unit target;

        public override void ResetForPull()
        {
            base.ResetForPull();
            charges = 1;
        }

        protected override bool Prepare(bool forced)
        {
            target = null;
            var deaths = CombatLog.DeathOrder;
            for (int i = deaths.Count - 1; i >= 0; i--)
            {
                var u = deaths[i];
                if (u != null && u.IsDead && u.Team == Team.Party && u != Me)
                {
                    target = u;
                    break;
                }
            }
            if (target == null) Barks.Say(Me, "Nobody to rez. Yet.", 1.4f, true);
            return target != null;
        }

        protected override void Execute()
        {
            charges--;
            target.Revive(0.4f);
            Telegraph.Flash(target.Position, 2.6f, Color, 0.7f);
            FX.Glow(target.Position, new Color(1f, 0.95f, 0.7f), 3.2f, 1.1f);
            FX.Burst(target.Position, new Color(1f, 0.95f, 0.6f), 30, 6f, 0.8f, 1.4f);
            FX.Beam(Me.Position, target.Position, Color, 0.2f, 0.5f);
            CommandSystem.Instance.Shout($"{target.DisplayName} is back!", Color);
            Barks.Say(target, BarkLines.Pick(BarkLines.Rezzed), 2.4f, true);
        }
    }
}
