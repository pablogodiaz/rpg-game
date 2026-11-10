using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    public enum RaidCall { Stack, Spread, Interrupt, Burn, Defensives, AsYouWere }

    public static class RaidCallInfo
    {
        public static string Name(RaidCall c)
        {
            switch (c)
            {
                case RaidCall.Stack: return "Stack";
                case RaidCall.Spread: return "Spread";
                case RaidCall.Interrupt: return "Interrupt";
                case RaidCall.Burn: return "Burn";
                case RaidCall.Defensives: return "Defensives";
                case RaidCall.AsYouWere: return "As You Were";
                default: return c.ToString();
            }
        }

        public static string Shout(RaidCall c)
        {
            switch (c)
            {
                case RaidCall.Stack: return "STACK ON ME!";
                case RaidCall.Spread: return "SPREAD OUT!";
                case RaidCall.Interrupt: return "KICK THAT CAST!";
                case RaidCall.Burn: return "BURN IT DOWN!";
                case RaidCall.Defensives: return "DEFENSIVES NOW!";
                case RaidCall.AsYouWere: return "As you were.";
                default: return c.ToString();
            }
        }

        public static string Description(RaidCall c)
        {
            switch (c)
            {
                case RaidCall.Stack: return "Group up on you";
                case RaidCall.Spread: return "Fan out around the boss";
                case RaidCall.Interrupt: return "Nearest kicker interrupts";
                case RaidCall.Burn: return "x2 damage + big abilities";
                case RaidCall.Defensives: return "-40% damage + shields";
                case RaidCall.AsYouWere: return "Cancel orders";
                default: return "";
            }
        }

        public static float Cooldown(RaidCall c)
        {
            switch (c)
            {
                case RaidCall.Burn: return 90f;
                case RaidCall.Defensives: return 60f;
                default: return 0f;
            }
        }

        public static Color Color(RaidCall c)
        {
            switch (c)
            {
                case RaidCall.Stack: return new Color(1f, 0.65f, 0.2f);
                case RaidCall.Spread: return new Color(0.75f, 0.45f, 1f);
                case RaidCall.Interrupt: return new Color(1f, 0.95f, 0.3f);
                case RaidCall.Burn: return new Color(1f, 0.35f, 0.25f);
                case RaidCall.Defensives: return new Color(0.4f, 0.8f, 1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }

    /// <summary>Turns the leader's calls into orders for the party, and tracks call cooldowns.</summary>
    public class CommandSystem : MonoBehaviour
    {
        public static CommandSystem Instance { get; private set; }

        public const float SpreadRadius = 6.5f;
        public const float SpreadStep = 60f;

        readonly Dictionary<RaidCall, float> readyAt = new Dictionary<RaidCall, float>();

        public string LastShout { get; private set; }
        public Color LastShoutColor { get; private set; }
        public float LastShoutAt { get; private set; } = -100f; // unscaled time

        /// <summary>While a Spread is active, where the leader should stand (drawn on the ground by the HUD).</summary>
        public float LeaderSpreadAngle { get; private set; }
        public float LeaderSpreadUntil { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        public void ResetState()
        {
            readyAt.Clear();
            LastShout = null;
            LeaderSpreadUntil = 0f;
        }

        public float Remaining(RaidCall c) =>
            readyAt.TryGetValue(c, out var t) ? Mathf.Max(0f, t - Time.time) : 0f;

        public void Shout(string text, Color color)
        {
            LastShout = text;
            LastShoutColor = color;
            LastShoutAt = Time.unscaledTime;
        }

        public bool Issue(RaidCall call)
        {
            var enc = EncounterManager.Instance;
            if (enc == null || enc.State != EncounterState.Fighting) return false;
            var leader = enc.Leader;
            if (leader == null || leader.Unit.IsDead) return false;

            float remaining = Remaining(call);
            if (remaining > 0f)
            {
                Barks.Say(leader.Unit, $"{RaidCallInfo.Name(call)} is on cooldown ({remaining:0}s).", 1.5f, true);
                return false;
            }

            float cd = RaidCallInfo.Cooldown(call);
            if (cd > 0f) readyAt[call] = Time.time + cd;

            Shout(RaidCallInfo.Shout(call), RaidCallInfo.Color(call));
            Barks.Say(leader.Unit, RaidCallInfo.Shout(call), 1.4f, true);
            CombatLog.RecordCall(RaidCallInfo.Name(call));

            switch (call)
            {
                case RaidCall.Stack: CallStack(enc); break;
                case RaidCall.Spread: CallSpread(enc); break;
                case RaidCall.Interrupt: CallInterrupt(enc); break;
                case RaidCall.Burn:
                    CallBuff(enc, "burn", "BURN", BuffKind.DamageDone, 2f, 10f, BarkLines.AckBurn);
                    FireAbilities(enc, a => a.IsBurst);
                    break;
                case RaidCall.Defensives:
                    CallBuff(enc, "def", "DEF", BuffKind.DamageTaken, 0.6f, 6f, BarkLines.AckDefensives);
                    FireAbilities(enc, a => a.IsDefensive);
                    break;
                case RaidCall.AsYouWere:
                    foreach (var member in enc.Party)
                        if (member.AI != null) member.AI.ClearOrder();
                    LeaderSpreadUntil = 0f;
                    break;
            }
            return true;
        }

        static void AckFromSomeone(EncounterManager enc, string[] lines)
        {
            var candidates = new List<PartyMember>();
            foreach (var member in enc.Party)
                if (member.AI != null && !member.Unit.IsDead) candidates.Add(member);
            if (candidates.Count == 0) return;
            var who = candidates[Random.Range(0, candidates.Count)];
            Barks.Say(who.Unit, BarkLines.Pick(lines), 1.8f);
        }

        void CallStack(EncounterManager enc)
        {
            foreach (var member in enc.Party)
            {
                if (member.AI == null || member.Unit.IsDead || member.Profile.Role == Role.Tank) continue;
                member.AI.GiveOrder(OrderKind.Stack, 6.5f, member.AI.CallReactionDelay);
            }
            AckFromSomeone(enc, BarkLines.AckStack);
        }

        void CallSpread(EncounterManager enc)
        {
            foreach (var member in enc.Party)
            {
                if (member.Unit.IsDead || member.Profile.Role == Role.Tank) continue;
                float angle = SpreadAngleFor(member);
                if (member.AI != null) member.AI.GiveOrder(OrderKind.Spread, 7.5f, member.AI.CallReactionDelay, angle: angle);
                else if (member.IsLeader)
                {
                    LeaderSpreadAngle = angle;
                    LeaderSpreadUntil = Time.time + 7.5f;
                }
            }
            AckFromSomeone(enc, BarkLines.AckSpread);
        }

        void CallInterrupt(EncounterManager enc)
        {
            var boss = enc.Boss;
            PartyMember best = null;
            PartyMember anyKicker = null;
            float bestDist = float.MaxValue;
            foreach (var member in enc.Party)
            {
                if (member.AI == null || member.Unit.IsDead || !member.Profile.CanInterrupt) continue;
                anyKicker = member;
                if (!member.CanInterruptNow) continue;
                // Prefer whoever can reach the boss soonest.
                float dist = Mathf.Max(0f, member.DistanceToBossEdge(boss) - member.Profile.InterruptRange);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = member;
                }
            }

            if (best != null)
            {
                best.AI.GiveOrder(OrderKind.Interrupt, 4f, best.AI.CallReactionDelay);
                Barks.Say(best.Unit, BarkLines.Pick(BarkLines.AckKick), 1.6f, true);
            }
            else if (anyKicker != null)
            {
                Barks.Say(anyKicker.Unit, BarkLines.Pick(BarkLines.KickOnCooldown), 2f, true);
            }
            else
            {
                Barks.Say(enc.Leader.Unit, "...we have no interrupts left.", 2f, true);
            }
        }

        /// <summary>Makes every AI member fire their matching abilities right now (if off cooldown).</summary>
        static void FireAbilities(EncounterManager enc, System.Func<Ability, bool> filter)
        {
            foreach (var member in enc.Party)
            {
                if (member.AI == null || member.Unit.IsDead) continue;
                foreach (var ability in member.Abilities)
                {
                    if (!filter(ability)) continue;
                    if (!ability.TryUse(true) && !ability.Ready)
                        Barks.Say(member.Unit, $"{ability.Name} on cooldown ({ability.Remaining:0}s)!", 1.6f);
                }
            }
        }

        static void CallBuff(EncounterManager enc, string id, string label, BuffKind kind, float value, float duration, string[] ack)
        {
            foreach (var member in enc.Party)
            {
                if (member.Unit.IsDead) continue;
                member.Unit.AddBuff(id, label, kind, value, duration);
                FX.Burst(member.Unit.Position, kind == BuffKind.DamageDone ? new Color(1f, 0.4f, 0.2f) : new Color(0.45f, 0.75f, 1f), 10, 4f);
            }
            AckFromSomeone(enc, ack);
        }

        // ----- Spread geometry (shared with self-responding AI) ------------------------------------

        /// <summary>Spread slots fan out behind the boss (relative to the tank), one per non-tank member.</summary>
        public static float SpreadAngleFor(PartyMember pm)
        {
            var enc = EncounterManager.Instance;
            int index = 0, count = 0;
            foreach (var member in enc.Party)
            {
                if (member.Unit.IsDead || member.Profile.Role == Role.Tank) continue;
                if (member == pm) index = count;
                count++;
            }
            return 180f + (index - (count - 1) / 2f) * SpreadStep;
        }

        public static Vector3 SpreadPoint(RotgutBoss boss, float angle)
        {
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * boss.FacingDirection;
            return Arena.Clamp(boss.Unit.Position + dir * SpreadRadius);
        }
    }
}
