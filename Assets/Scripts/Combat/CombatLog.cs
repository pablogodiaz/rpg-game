using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    public class UnitStats
    {
        public float Damage;
        public float Healing;
        public float DamageTaken;
        public int FireTicks;
        public int Interrupts;
        public int AbilitiesUsed;
        public string DeathCause;
        public float DeathTime = -1f;
    }

    /// <summary>Records everything that happens in a pull, for the post-fight report and its joke awards.</summary>
    public static class CombatLog
    {
        static readonly Dictionary<Unit, UnitStats> Stats = new Dictionary<Unit, UnitStats>();
        public static readonly List<string> Calls = new List<string>();
        public static readonly List<Unit> DeathOrder = new List<Unit>();
        public static float FightStartTime;

        public static float FightTime => Time.time - FightStartTime;

        public static void Reset()
        {
            Stats.Clear();
            Calls.Clear();
            DeathOrder.Clear();
            FightStartTime = Time.time;
        }

        public static UnitStats Get(Unit unit)
        {
            if (!Stats.TryGetValue(unit, out var s))
            {
                s = new UnitStats();
                Stats[unit] = s;
            }
            return s;
        }

        public static void RecordDamage(Unit source, Unit target, float amount, string ability)
        {
            if (source != null) Get(source).Damage += amount;
            var t = Get(target);
            t.DamageTaken += amount;
            if (ability == FirePuddle.AbilityName) t.FireTicks++;
        }

        public static void RecordHeal(Unit source, Unit target, float amount)
        {
            if (source != null) Get(source).Healing += amount;
        }

        public static void RecordInterrupt(Unit source)
        {
            if (source != null) Get(source).Interrupts++;
        }

        public static void RecordAbility(Unit unit, string ability)
        {
            if (unit != null) Get(unit).AbilitiesUsed++;
        }

        public static void RecordDeath(Unit unit, string cause)
        {
            var s = Get(unit);
            s.DeathCause = cause;
            s.DeathTime = FightTime;
            DeathOrder.Add(unit);
        }

        public static void RecordCall(string call)
        {
            Calls.Add($"{FormatTime(FightTime)} {call}");
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m}:{s:00}";
        }
    }
}
