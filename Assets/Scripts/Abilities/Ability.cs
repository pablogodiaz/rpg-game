using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// A signature ability on a cooldown. AI members use theirs on their own when <see cref="Prepare"/>
    /// says the moment is right; raid calls (Burn, Defensives) can force them; the leader presses buttons.
    /// Every use shows a callout over the character's head, so the player sees what their raid is doing.
    /// </summary>
    public abstract class Ability
    {
        public PartyMember Owner { get; private set; }
        public abstract string Name { get; }
        /// <summary>2-3 letter label for raid frame chips.</summary>
        public abstract string Short { get; }
        public abstract float Cooldown { get; }
        public abstract string Description { get; }
        public virtual Color Color => Color.white;
        /// <summary>Fired off by the BURN call.</summary>
        public virtual bool IsBurst => false;
        /// <summary>Fired off by the DEFENSIVES call.</summary>
        public virtual bool IsDefensive => false;
        /// <summary>False for abilities only triggered by special logic (e.g. Blink) or by a human.</summary>
        public virtual bool AutoUse => true;
        /// <summary>Remaining charges, or -1 for unlimited.</summary>
        public virtual int Charges => -1;

        public float ReadyAt { get; protected set; }
        public float LastUsedAt { get; private set; } = -100f;
        public bool Ready => Time.time >= ReadyAt && Charges != 0;
        public float Remaining => Mathf.Max(0f, ReadyAt - Time.time);

        protected EncounterManager Enc => EncounterManager.Instance;
        protected BossController Boss => Enc != null ? Enc.Boss : null;
        /// <summary>What offensive abilities hit: the member's current attack target (focus, rescue or boss).</summary>
        protected Unit Target => Owner.AttackTarget;
        protected float DistToTargetEdge => Target != null ? Owner.DistanceToEdge(Target) : float.MaxValue;
        protected Unit Me => Owner.Unit;

        public void Bind(PartyMember owner) => Owner = owner;

        /// <summary>Called on pull. Cooldowns start partially charged so not everything fires at once.</summary>
        public virtual void ResetForPull()
        {
            ReadyAt = Time.time + Cooldown * OpeningDelay;
        }

        protected virtual float OpeningDelay => 0.3f;

        /// <summary>
        /// Decide whether to fire now (and pick targets). forced = a call or a button press asked for it,
        /// so only hard requirements (range, a valid target) should block.
        /// </summary>
        protected abstract bool Prepare(bool forced);

        protected abstract void Execute();

        public bool TryUse(bool forced = false)
        {
            if (Owner == null || Me.IsDead || !Ready || Owner.IsBusy) return false;
            var enc = Enc;
            if (enc == null || enc.State != EncounterState.Fighting || Boss == null || Boss.Unit.IsDead) return false;
            if (!Prepare(forced)) return false;
            ReadyAt = Time.time + Cooldown;
            LastUsedAt = Time.time;
            FX.Callout(Me, Name.ToUpperInvariant() + "!", Color);
            CombatLog.RecordAbility(Me, Name);
            Execute();
            return true;
        }

        // ----- Helpers for subclasses --------------------------------------------------------------

        protected float DistToBossEdge => Owner.DistanceToBossEdge(Boss);

        /// <summary>Every living enemy unit: the boss and any extra targets (arms).</summary>
        protected IEnumerable<Unit> LivingEnemies()
        {
            if (Boss == null) yield break;
            if (!Boss.Unit.IsDead) yield return Boss.Unit;
            foreach (var u in Boss.ExtraTargets)
                if (u != null && !u.IsDead) yield return u;
        }

        protected IEnumerable<PartyMember> LivingParty()
        {
            foreach (var p in Enc.Party)
                if (!p.Unit.IsDead) yield return p;
        }

        protected static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
    }
}
