using UnityEngine;

namespace ReadyCheck
{
    public enum OrderKind { None, Stack, Spread, MoveTo, Interrupt }

    /// <summary>
    /// AI for party members. Priority stack:
    ///   standing-in-fire reflex  >  Leader Call / self-response order  >  role default behaviour.
    ///
    /// Two stats control how much the leader has to call:
    ///  - Awareness (per member): reaction speed, and the chance to handle a mechanic without a call.
    ///  - Familiarity (per member, per boss): grows with every pull, win or wipe, so the raid "learns".
    /// </summary>
    public class PartyAI : MonoBehaviour
    {
        PartyMember m;

        OrderKind order;
        Vector3 orderPoint;
        float orderAngle;
        float orderStartsAt;
        float orderEndsAt;

        float fireReactAt = -1f;
        float nextBackseatBark;

        void Awake()
        {
            m = GetComponent<PartyMember>();
            nextBackseatBark = Time.time + Random.Range(20f, 35f);
        }

        public OrderKind CurrentOrder =>
            Time.time >= orderStartsAt && Time.time < orderEndsAt ? order : OrderKind.None;

        public bool HasPendingOrder => order != OrderKind.None && Time.time < orderEndsAt;

        public string OrderLabel
        {
            get
            {
                switch (CurrentOrder)
                {
                    case OrderKind.Stack: return "STACK";
                    case OrderKind.Spread: return "SPREAD";
                    case OrderKind.MoveTo: return "MOVING";
                    case OrderKind.Interrupt: return "KICK";
                    default: return HasPendingOrder ? "..." : null;
                }
            }
        }

        /// <summary>How quickly this member reacts to a call from the leader.</summary>
        public float CallReactionDelay => Mathf.Lerp(0.7f, 0.15f, m.Profile.Awareness);

        /// <summary>Chance (0..1) to handle a known mechanic on their own.</summary>
        public float Knowledge => Mathf.Clamp01(m.Profile.Awareness * 0.5f + m.Familiarity * 0.12f);

        float SelfReactionDelay =>
            Mathf.Max(0.4f, Mathf.Lerp(2.2f, 0.5f, m.Profile.Awareness) - m.Familiarity * 0.1f);

        float FireReactionDelay
        {
            get
            {
                float d = Mathf.Lerp(2.0f, 0.35f, m.Profile.Awareness) - m.Familiarity * 0.1f;
                if (m.Profile.Has(Trait.FireEnthusiast)) d += 1.5f;
                return Mathf.Max(0.25f, d);
            }
        }

        public void GiveOrder(OrderKind kind, float duration, float delay, Vector3 point = default, float angle = 0f)
        {
            order = kind;
            orderPoint = point;
            orderAngle = angle;
            orderStartsAt = Time.time + delay;
            orderEndsAt = orderStartsAt + duration;
        }

        public void ClearOrder()
        {
            order = OrderKind.None;
            orderEndsAt = 0f;
        }

        // ----- Reacting to mechanics on their own --------------------------------------------------

        public void OnMechanic(MechanicEvent e)
        {
            if (m.Unit.IsDead) return;
            bool knows = Random.value < Knowledge;

            if (m.Profile.Has(Trait.BackseatLeader) && Random.value < 0.25f)
                Barks.Say(m.Unit, BarkLines.BackseatCorrect(e.Kind), 2.5f, true);

            switch (e.Kind)
            {
                case MechanicKind.BellyFlop:
                {
                    if (m.Profile.Role == Role.Tank) return;
                    float remaining = e.ResolveAt - Time.time;
                    if (e.Target == m.Unit)
                    {
                        // The target holds still so others can find the circle.
                        GiveOrder(OrderKind.MoveTo, remaining + 0.3f, 0.2f, m.Unit.Position);
                        return;
                    }
                    if (!knows) return;
                    float delay = SelfReactionDelay;
                    var jitter = Random.insideUnitCircle * 0.9f;
                    GiveOrder(OrderKind.MoveTo, remaining - delay + 0.3f, delay,
                        e.Point + new Vector3(jitter.x, 0f, jitter.y));
                    BarkKnown();
                    break;
                }
                case MechanicKind.Indigestion:
                {
                    if (m.Profile.Role == Role.Tank || !knows) return;
                    float delay = SelfReactionDelay;
                    GiveOrder(OrderKind.Spread, e.ResolveAt - Time.time - delay + 0.3f, delay,
                        angle: CommandSystem.SpreadAngleFor(m));
                    BarkKnown();
                    break;
                }
                case MechanicKind.SecondBreakfast:
                {
                    if (!m.Profile.CanInterrupt || !m.CanInterruptNow || !knows) return;
                    float delay = SelfReactionDelay;
                    GiveOrder(OrderKind.Interrupt, e.ResolveAt - Time.time, delay);
                    break;
                }
            }
        }

        void BarkKnown()
        {
            if (Random.value > 0.5f) return;
            Barks.Say(m.Unit, m.Familiarity > 0 ? BarkLines.Pick(BarkLines.SelfRespondFamiliar)
                                                : BarkLines.Pick(BarkLines.SelfRespond));
        }

        // ----- Per frame ---------------------------------------------------------------------------

        void Update()
        {
            var enc = EncounterManager.Instance;
            if (enc == null || m.Unit.IsDead || enc.State != EncounterState.Fighting) return;
            var boss = enc.Boss;
            if (boss == null) return;

            Vector3 desired = DefaultPosition(boss);
            float stop = 0.25f;

            switch (CurrentOrder)
            {
                case OrderKind.Stack:
                    desired = enc.Leader.Unit.Position + StackOffset();
                    stop = 0.2f;
                    break;
                case OrderKind.Spread:
                    desired = CommandSystem.SpreadPoint(boss, orderAngle);
                    stop = 0.2f;
                    break;
                case OrderKind.MoveTo:
                    desired = orderPoint;
                    stop = 0.15f;
                    break;
                case OrderKind.Interrupt:
                    if (boss.IsCasting && boss.CastInterruptible)
                    {
                        float range = m.Profile.InterruptRange;
                        if (m.DistanceToBossEdge(boss) > range)
                        {
                            var dir = (m.Unit.Position - boss.Unit.Position).normalized;
                            desired = boss.Unit.Position + dir * (boss.Unit.Radius + range * 0.7f);
                        }
                        else if (m.TryInterrupt(boss))
                        {
                            ClearOrder();
                        }
                    }
                    break;
            }

            // Standing-in-fire reflex. How fast it kicks in depends on Awareness (and traits).
            var fire = FirePuddle.At(m.Unit.Position, -0.1f);
            if (fire != null)
            {
                if (fireReactAt < 0f)
                {
                    fireReactAt = Time.time + FireReactionDelay;
                    if (m.Profile.Has(Trait.FireEnthusiast) && Random.value < 0.5f)
                        Barks.Say(m.Unit, BarkLines.Pick(BarkLines.FireEnthusiast));
                }
                if (Time.time >= fireReactAt)
                {
                    desired = EscapePoint(fire, desired);
                    stop = 0.05f;
                }
            }
            else
            {
                fireReactAt = -1f;
                // Don't path into a fire puddle on purpose when no one ordered it.
                if (CurrentOrder == OrderKind.None)
                {
                    var fireAtDest = FirePuddle.At(desired, 0.3f);
                    if (fireAtDest != null) desired = EscapePoint(fireAtDest, desired);
                }
            }

            m.MoveTowards(desired, stop);
            m.TickCombat(Time.deltaTime);

            if (m.Profile.Has(Trait.BackseatLeader) && Time.time >= nextBackseatBark)
            {
                nextBackseatBark = Time.time + Random.Range(25f, 40f);
                Barks.Say(m.Unit, BarkLines.Pick(BarkLines.BackseatWrong), 2.8f);
            }
        }

        Vector3 StackOffset()
        {
            float a = m.Index * 72f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.6f;
        }

        Vector3 EscapePoint(FirePuddle fire, Vector3 preferred)
        {
            var center = fire.transform.position;
            center.y = 0f;
            var dir = m.Unit.Position - center;
            if (dir.sqrMagnitude < 0.01f) dir = preferred - center;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
            dir.y = 0f;
            return Arena.Clamp(center + dir.normalized * (fire.Radius + 0.8f));
        }

        /// <summary>Standard raid positioning relative to the boss: tank in front, melee behind, ranged back.</summary>
        Vector3 DefaultPosition(RotgutBoss boss)
        {
            var bp = boss.Unit.Position;
            float r = boss.Unit.Radius;
            var facing = boss.FacingDirection;

            switch (m.Profile.Role)
            {
                case Role.Tank:
                    return Arena.Clamp(bp + facing * (r + 0.9f));
                case Role.Melee:
                    return Arena.Clamp(bp - facing * (r + 0.9f));
                default:
                {
                    var side = Vector3.Cross(Vector3.up, facing);
                    float offset = m.Profile.Role == Role.Healer ? -2.2f : 2.2f;
                    return Arena.Clamp(bp - facing * 7.5f + side * offset);
                }
            }
        }
    }
}
