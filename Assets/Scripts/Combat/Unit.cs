using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    public enum Team { Party, Enemy }

    public enum Role { Tank, Healer, Melee, Ranged, Boss }

    public enum BuffKind
    {
        /// <summary>Multiplies outgoing damage (2 = double damage).</summary>
        DamageDone,
        /// <summary>Multiplies incoming damage (0.6 = take 40% less).</summary>
        DamageTaken,
    }

    public class Buff
    {
        public string Id;
        public string Label;
        public BuffKind Kind;
        public float Value;
        public float ExpiresAt;
    }

    /// <summary>Anything that has health and can fight: party members and bosses.</summary>
    public class Unit : MonoBehaviour
    {
        public string DisplayName = "Unit";
        public Team Team;
        public Role Role;
        public float MaxHealth = 100f;
        public float Health = 100f;
        public float Radius = 0.4f;
        public bool Invulnerable;
        public UnitVisual Visual;

        public readonly List<Buff> Buffs = new List<Buff>();

        public bool IsDead { get; private set; }
        public string DeathCause { get; private set; }

        /// <summary>Raised after damage is applied: (source, amount).</summary>
        public event Action<Unit, float> Damaged;
        public event Action<Unit> Died;

        public Vector3 Position
        {
            get
            {
                var p = transform.position;
                return new Vector3(p.x, 0f, p.z);
            }
        }

        public float HealthPercent => MaxHealth > 0f ? Mathf.Clamp01(Health / MaxHealth) : 0f;

        public void Init(string displayName, Team team, Role role, float maxHealth, float radius)
        {
            DisplayName = displayName;
            Team = team;
            Role = role;
            MaxHealth = maxHealth;
            Health = maxHealth;
            Radius = radius;
        }

        public void AddBuff(string id, string label, BuffKind kind, float value, float duration)
        {
            Buffs.RemoveAll(b => b.Id == id);
            Buffs.Add(new Buff { Id = id, Label = label, Kind = kind, Value = value, ExpiresAt = Time.time + duration });
        }

        public bool HasBuff(string id) => Buffs.Exists(b => b.Id == id);

        public float Multiplier(BuffKind kind)
        {
            float m = 1f;
            foreach (var b in Buffs)
                if (b.Kind == kind) m *= b.Value;
            return m;
        }

        public float TakeDamage(float amount, Unit source, string ability)
        {
            if (IsDead || amount <= 0f || Invulnerable) return 0f;
            amount *= Multiplier(BuffKind.DamageTaken);
            Health -= amount;
            CombatLog.RecordDamage(source, this, amount, ability);
            if (Visual != null) Visual.Flash(new Color(1f, 0.45f, 0.45f));
            Damaged?.Invoke(source, amount);
            if (Health <= 0f)
            {
                Health = 0f;
                Die(ability);
            }
            return amount;
        }

        public float Heal(float amount, Unit source)
        {
            if (IsDead || amount <= 0f) return 0f;
            float effective = Mathf.Min(amount, MaxHealth - Health);
            Health += effective;
            CombatLog.RecordHeal(source, this, effective);
            if (Visual != null && effective > 0f) Visual.Flash(new Color(0.6f, 1f, 0.6f));
            return effective;
        }

        void Die(string cause)
        {
            IsDead = true;
            DeathCause = string.IsNullOrEmpty(cause) ? "Unknown" : cause;
            Buffs.Clear();
            CombatLog.RecordDeath(this, DeathCause);
            Died?.Invoke(this);
        }

        void Update()
        {
            if (Buffs.Count > 0) Buffs.RemoveAll(b => Time.time >= b.ExpiresAt);
        }
    }
}
