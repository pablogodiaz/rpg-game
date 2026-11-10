using System;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// What every party member has in common: movement, role abilities and cooldowns.
    /// It is "driven" either by <see cref="PartyAI"/> or by <see cref="LeaderController"/> (a human).
    /// Keeping this split is what makes drop-in local co-op possible later: a second player just
    /// replaces a member's AI with a player controller.
    /// </summary>
    public class PartyMember : MonoBehaviour
    {
        public MemberProfile Profile;
        public Unit Unit;
        public PartyAI AI;
        public bool IsLeader;
        public int Index;

        /// <summary>Pulls this member has already done against the current boss (loaded from save).</summary>
        public int Familiarity;

        public float MoveSpeed = 4.6f;
        public float InterruptReadyAt;

        public bool IsMoving { get; private set; }
        public string CastName { get; private set; }
        public float CastProgress { get; private set; }
        public float CastTime { get; private set; }
        public float CastFraction => CastTime > 0f && CastName != null ? CastProgress / CastTime : 0f;

        public const float HealRange = 12f;
        public const float SpellRange = 11f;
        public const float MeleeRange = 1.3f;

        float swingTimer;
        Unit healTarget;
        Vector3 lastPosition;

        void Start()
        {
            lastPosition = transform.position;
        }

        void LateUpdate()
        {
            IsMoving = (transform.position - lastPosition).sqrMagnitude > 1e-7f;
            lastPosition = transform.position;
        }

        public bool CanInterruptNow => Profile.CanInterrupt && !Unit.IsDead && Time.time >= InterruptReadyAt;

        // ----- Movement ----------------------------------------------------------------------------

        public void Move(Vector3 direction, float speedMultiplier = 1f)
        {
            if (Unit.IsDead) return;
            direction.y = 0f;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            var p = transform.position + direction * (MoveSpeed * speedMultiplier * Time.deltaTime);
            transform.position = Arena.Clamp(p);
        }

        /// <summary>Walks toward a point. Returns true once within stopDistance.</summary>
        public bool MoveTowards(Vector3 point, float stopDistance)
        {
            var to = point - Unit.Position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= stopDistance) return true;
            float maxStep = MoveSpeed * Time.deltaTime;
            if (maxStep >= dist)
            {
                transform.position = Arena.Clamp(point);
                return true;
            }
            Move(to / dist);
            return false;
        }

        // ----- Combat ------------------------------------------------------------------------------

        public float DamageMultiplier()
        {
            float m = Unit.Multiplier(BuffKind.DamageDone);
            var boss = EncounterManager.Instance != null ? EncounterManager.Instance.Boss : null;
            if (Profile.Has(Trait.Clutch) && boss != null && boss.Unit.HealthPercent < 0.2f) m *= 1.3f;
            return m;
        }

        public float DistanceToBossEdge(RotgutBoss boss)
        {
            return Vector3.Distance(Unit.Position, boss.Unit.Position) - boss.Unit.Radius;
        }

        /// <summary>The role's default action for this frame. Called by the AI. The leader attacks manually.</summary>
        public void TickCombat(float dt)
        {
            var enc = EncounterManager.Instance;
            if (enc == null || enc.State != EncounterState.Fighting || Unit.IsDead) return;
            var boss = enc.Boss;
            if (boss == null || boss.Unit.IsDead) return;
            float dist = DistanceToBossEdge(boss);

            switch (Profile.Role)
            {
                case Role.Tank:
                    Swing(dt, boss, dist, 26f, 1.1f, "Shield Bash");
                    break;
                case Role.Melee:
                    Swing(dt, boss, dist, 32f, 0.65f, "Stab");
                    break;
                case Role.Ranged:
                    Cast(dt, "Fireball", 1.2f, dist <= SpellRange,
                        () => FireAt(boss.Unit, 52f, new Color(1f, 0.55f, 0.2f), "Fireball"));
                    break;
                case Role.Healer:
                    HealerTick(dt, boss, dist);
                    break;
            }
        }

        void Swing(float dt, RotgutBoss boss, float dist, float damage, float interval, string ability)
        {
            swingTimer -= dt;
            if (dist > MeleeRange) return;
            if (swingTimer <= 0f)
            {
                swingTimer = interval;
                boss.Unit.TakeDamage(damage * DamageMultiplier(), Unit, ability);
            }
        }

        /// <summary>Cast-time abilities only progress while standing still. Moving cancels the cast.</summary>
        void Cast(float dt, string name, float time, bool inRange, Action onComplete)
        {
            if (IsMoving || !inRange)
            {
                CastName = null;
                CastProgress = 0f;
                return;
            }
            if (CastName != name)
            {
                CastName = name;
                CastTime = time;
                CastProgress = 0f;
            }
            CastProgress += dt;
            if (CastProgress >= CastTime)
            {
                CastName = null;
                CastProgress = 0f;
                onComplete();
            }
        }

        void HealerTick(float dt, RotgutBoss boss, float distToBoss)
        {
            if (CastName == "Heal" && healTarget != null && !healTarget.IsDead)
            {
                Cast(dt, "Heal", 1.3f, InRange(healTarget, HealRange), DoHeal);
                return;
            }

            var target = FindHealTarget();
            if (target != null)
            {
                healTarget = target;
                Cast(dt, "Heal", 1.3f, InRange(target, HealRange), DoHeal);
                if (target.HealthPercent < 0.3f && UnityEngine.Random.value < 0.01f)
                    Barks.Say(Unit, BarkLines.Pick(BarkLines.HealerPanic));
            }
            else
            {
                Cast(dt, "Smite", 1.4f, distToBoss <= SpellRange,
                    () => FireAt(boss.Unit, 20f, new Color(1f, 1f, 0.6f), "Smite"));
            }
        }

        void DoHeal()
        {
            if (healTarget == null || healTarget.IsDead) return;
            var target = healTarget;
            Projectile.Fire(Unit.Position, target, new Color(0.5f, 1f, 0.5f), 18f,
                () => target.Heal(230f, Unit));
        }

        bool InRange(Unit target, float range) => Vector3.Distance(Unit.Position, target.Position) <= range;

        Unit FindHealTarget()
        {
            var enc = EncounterManager.Instance;
            Unit best = null;
            float bestPct = 0.92f;
            foreach (var m in enc.Party)
            {
                if (m.Unit.IsDead) continue;
                float pct = m.Unit.HealthPercent;
                // Healers naturally prioritise the tank a little.
                if (m.Profile.Role == Role.Tank) pct -= 0.1f;
                if (pct < bestPct)
                {
                    bestPct = pct;
                    best = m.Unit;
                }
            }
            return best;
        }

        public void FireAt(Unit target, float damage, Color color, string ability)
        {
            float dmg = damage * DamageMultiplier();
            var source = Unit;
            Projectile.Fire(Unit.Position, target, color, 18f,
                () => target.TakeDamage(dmg, source, ability));
        }

        public bool TryInterrupt(RotgutBoss boss)
        {
            if (!CanInterruptNow || boss == null || !boss.IsCasting || !boss.CastInterruptible) return false;
            if (DistanceToBossEdge(boss) > Profile.InterruptRange) return false;
            InterruptReadyAt = Time.time + Profile.InterruptCooldown;
            boss.Interrupt(this);
            return true;
        }
    }
}
