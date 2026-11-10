using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// What every boss shares: casting (with interrupts), a threat table, enrage, phases, staggers,
    /// barks, and the hooks the party AI / HUD / abilities use. Each boss subclass only writes its mechanics.
    /// </summary>
    public abstract class BossController : MonoBehaviour
    {
        // ----- Identity (per boss) -----------------------------------------------------------------
        public abstract string BossId { get; }
        public abstract string BossName { get; }
        public abstract float EnrageTime { get; }
        /// <summary>Lines the boss says. Used by the encounter flow.</summary>
        public abstract string[] PullLines { get; }
        public abstract string[] RetryLines { get; }
        public abstract string[] KillLines { get; }
        public abstract string[] WipeLines { get; }
        public abstract string[] DeathLines { get; }
        public abstract string[] InterruptedLines { get; }
        public abstract string[] LootTable { get; }

        /// <summary>Shown next to the boss name, e.g. "PHASE 2".</summary>
        public virtual string PhaseLabel => null;
        /// <summary>Shown on the right of the boss frame, e.g. "Well Fed x2" or "Arms 5/8".</summary>
        public virtual string StatusText => null;
        /// <summary>Extra targets the party can attack (Krakenna's arms).</summary>
        public virtual IReadOnlyList<Unit> ExtraTargets => Array.Empty<Unit>();
        /// <summary>True while the boss can't be hit by abilities that need it in place (Rotgut mid-leap).</summary>
        public virtual bool IsLeaping => false;
        /// <summary>For raid frame tags ("MARKED").</summary>
        public virtual bool IsMarked(Unit u) => false;

        // ----- State -------------------------------------------------------------------------------
        public Unit Unit { get; private set; }
        public UnitVisual Visual { get; private set; }

        public string CastName { get; private set; }
        public bool CastInterruptible { get; private set; }
        public float CastStartedAt { get; private set; }
        public float CastEndsAt { get; private set; }
        public bool IsCasting => CastName != null;
        public float CastFraction => IsCasting ? Mathf.InverseLerp(CastStartedAt, CastEndsAt, Time.time) : 0f;

        public bool Enraged { get; private set; }
        public bool Fighting { get; private set; }
        public float FightStartedAt { get; private set; }
        public float FightTime => Fighting ? Time.time - FightStartedAt : 0f;
        public Vector3 FacingDirection { get; protected set; } = Vector3.back;
        public Unit CurrentTarget { get; protected set; }

        protected float nextAuto;
        Action onCastComplete;
        readonly Dictionary<Unit, float> threat = new Dictionary<Unit, float>();

        protected EncounterManager Enc => EncounterManager.Instance;

        public void Init(Unit unit, UnitVisual visual)
        {
            Unit = unit;
            Visual = visual;
            unit.Damaged += OnDamaged;
            unit.Died += OnDied;
            OnInit();
        }

        protected virtual void OnInit() { }

        public virtual void BeginFight(PartyMember tank)
        {
            Fighting = true;
            FightStartedAt = Time.time;
            nextAuto = Time.time + 1.5f;
            threat.Clear();
            if (tank != null) threat[tank.Unit] = 400f; // opening taunt
            Barks.Say(Unit, BarkLines.Pick(PullLines), 3f, true);
        }

        // ----- Threat ------------------------------------------------------------------------------

        public void AddThreat(Unit source, float amount)
        {
            if (source == null) return;
            threat.TryGetValue(source, out var t);
            threat[source] = t + amount;
        }

        /// <summary>Taunt: jump to the top of the threat table and become the target immediately.</summary>
        public void ForceTarget(Unit source)
        {
            float max = 0f;
            foreach (var kv in threat) max = Mathf.Max(max, kv.Value);
            threat[source] = max + 800f;
            CurrentTarget = source;
        }

        /// <summary>Delays the next swing (Shield Slam).</summary>
        public void Stagger(float seconds)
        {
            nextAuto = Mathf.Max(nextAuto, Time.time + seconds);
            if (Visual != null) Visual.Flash(Color.white, 0.12f);
        }

        void OnDamaged(Unit source, float amount)
        {
            if (source == null || source.Team != Team.Party) return;
            threat.TryGetValue(source, out var t);
            threat[source] = t + amount * (source.Role == Role.Tank ? 6f : 1f);
        }

        protected Unit HighestThreat()
        {
            Unit best = null;
            float bestThreat = -1f;
            foreach (var member in Enc.Party)
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

        protected void FaceTarget()
        {
            if (CurrentTarget == null || CurrentTarget.IsDead) return;
            var dir = CurrentTarget.Position - Unit.Position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) FacingDirection = dir.normalized;
        }

        /// <summary>Picks the highest-threat target, barks when aggro is pulled, and faces it.</summary>
        protected Unit UpdateTarget()
        {
            var target = HighestThreat();
            if (target == null) return null;
            if (target != CurrentTarget && CurrentTarget != null && !CurrentTarget.IsDead && target.Role != Role.Tank)
                Barks.Say(target, BarkLines.Pick(BarkLines.PulledAggro), 2f);
            CurrentTarget = target;
            FaceTarget();
            return target;
        }

        // ----- Casting -----------------------------------------------------------------------------

        protected void StartCast(string castName, float duration, bool interruptible, Action onComplete)
        {
            CastName = castName;
            CastInterruptible = interruptible;
            CastStartedAt = Time.time;
            CastEndsAt = Time.time + duration;
            onCastComplete = onComplete;
        }

        protected void CancelCast()
        {
            CastName = null;
            onCastComplete = null;
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
            string interrupted = CastName;
            CancelCast();
            nextAuto = Time.time + 1f;
            CombatLog.RecordInterrupt(by.Unit);
            Barks.Say(by.Unit, BarkLines.Pick(BarkLines.Kicked), 1.6f, true);
            Barks.Say(Unit, BarkLines.Pick(InterruptedLines), 2.2f, true);
            CommandSystem.Instance.Shout($"{by.Profile.Name} interrupted {interrupted}!", new Color(1f, 0.95f, 0.4f));
        }

        // ----- Lifecycle ---------------------------------------------------------------------------

        void OnDied(Unit u)
        {
            Fighting = false;
            CancelCast();
            OnBossDied();
            Barks.Say(Unit, BarkLines.Pick(DeathLines), 4f, true);
            Enc.OnBossDefeated();
        }

        /// <summary>Clean up telegraphs etc. when the boss dies.</summary>
        protected virtual void OnBossDied() { }

        /// <summary>Called when the boss enrages (after the base announces it).</summary>
        protected virtual void OnEnrage() { }

        void Update()
        {
            var enc = Enc;
            if (!Fighting || Unit.IsDead || enc == null || enc.State != EncounterState.Fighting) return;

            if (!Enraged && FightTime >= EnrageTime)
            {
                Enraged = true;
                CommandSystem.Instance.Shout($"{BossName.ToUpperInvariant()} ENRAGES!", new Color(1f, 0.25f, 0.2f));
                OnEnrage();
            }

            bool casting = IsCasting;
            if (casting && Time.time >= CastEndsAt)
            {
                CompleteCast();
                casting = IsCasting; // a completed cast may start another
            }
            else if (casting)
            {
                FaceTarget();
            }

            FightUpdate(Time.time, casting);
        }

        /// <summary>The boss's own logic, every frame while fighting. isCasting = a cast is still in progress.</summary>
        protected abstract void FightUpdate(float now, bool isCasting);

        // ----- Helpers -----------------------------------------------------------------------------

        protected List<Unit> LivingParty(bool includeTank)
        {
            var list = new List<Unit>();
            foreach (var member in Enc.Party)
                if (!member.Unit.IsDead && (includeTank || member.Profile.Role != Role.Tank)) list.Add(member.Unit);
            return list;
        }

        protected static T PickRandom<T>(List<T> list) => list[UnityEngine.Random.Range(0, list.Count)];
    }
}
