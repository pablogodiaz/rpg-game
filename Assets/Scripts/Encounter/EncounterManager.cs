using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    public enum EncounterState { PrePull, Countdown, Fighting, Wipe, Victory }

    /// <summary>
    /// Runs one boss encounter: spawns the party and boss, Ready Check → countdown → pull,
    /// detects wipes and kills, saves progress, and resets for "one more pull".
    /// </summary>
    public class EncounterManager : MonoBehaviour
    {
        public static EncounterManager Instance { get; private set; }

        public EncounterState State { get; private set; }
        public float StateEnteredAt { get; private set; } // unscaled time
        public Transform Root { get; private set; }
        public readonly List<PartyMember> Party = new List<PartyMember>();
        public PartyMember Leader { get; private set; }
        public RotgutBoss Boss { get; private set; }

        public bool ShowHints = true;
        public MechanicEvent ActiveHint { get; private set; }

        // Results of the last pull, for the report screen.
        public int AttemptNumber { get; private set; }
        public float BossPercentAtEnd { get; private set; }
        public bool NewBest { get; private set; }
        public float FightDuration { get; private set; }
        public string LootDrop { get; private set; }
        public string EndQuote { get; private set; }
        public string EndQuoteBy { get; private set; }

        float countdownEndsAt;

        static readonly string[] LootTable =
        {
            "Rotgut's Stained Bib (Epic)",
            "Half-Eaten Drumstick of Haste (Rare)",
            "Belt of Infinite Loosening (Epic)",
            "Gravy Boat of the Bottomless (Legendary... ish)",
        };

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Build();
        }

        public void Build()
        {
            Teardown();
            TimeController.SetSlowMo(false);
            Barks.Clear();
            Root = new GameObject("Encounter").transform;
            CombatLog.Reset();
            if (CommandSystem.Instance != null) CommandSystem.Instance.ResetState();

            var profiles = Roster.StarterParty();
            for (int i = 0; i < profiles.Count; i++)
            {
                var start = new Vector3((i - (profiles.Count - 1) / 2f) * 1.4f, 0f, -9f);
                Party.Add(SpawnMember(profiles[i], i, start));
            }
            Leader = Party[0];
            Boss = SpawnBoss(new Vector3(0f, 0f, 3f));

            var rig = CameraRig.Instance;
            if (rig != null)
            {
                rig.Target = Leader.transform;
                rig.SecondaryTarget = Boss.transform;
                rig.Snap();
            }

            ActiveHint = null;
            SetState(EncounterState.PrePull);
        }

        void Teardown()
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
                Destroy(Root.gameObject);
            }
            Root = null;
            Party.Clear();
            FirePuddle.All.Clear();
            Leader = null;
            Boss = null;
        }

        PartyMember SpawnMember(MemberProfile profile, int index, Vector3 position)
        {
            var go = new GameObject(profile.Name);
            go.transform.SetParent(Root, false);
            go.transform.position = position;

            var unit = go.AddComponent<Unit>();
            unit.Init(profile.Name, Team.Party, profile.Role, profile.MaxHealth, 0.5f);

            var member = go.AddComponent<PartyMember>();
            member.Profile = profile;
            member.Unit = unit;
            member.Index = index;
            member.IsLeader = profile.Class == ClassKind.Leader;
            member.Familiarity = Familiarity.Get(RotgutBoss.BossId, profile.Name);

            var visual = UnitVisual.Create(unit, PixelArt.Character(profile), PixelArt.Foot(profile.Secondary, false),
                0.3f, 0.22f, 10, 0f);
            visual.AimDirection = () => AimAtBoss(unit);
            unit.Visual = visual;

            if (member.IsLeader)
            {
                go.AddComponent<LeaderController>();
                // Gold ring under the leader so you can always find yourself.
                var ringGo = new GameObject("LeaderRing");
                ringGo.transform.SetParent(go.transform, false);
                ringGo.transform.localPosition = new Vector3(0f, 0.002f, 0f);
                ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ringGo.transform.localScale = Vector3.one * 0.75f;
                var sr = ringGo.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArt.RingSprite();
                sr.color = new Color(1f, 0.85f, 0.3f, 0.9f);
                sr.sortingOrder = -6;
            }
            else
            {
                member.AI = go.AddComponent<PartyAI>();
            }

            unit.Died += OnMemberDied;
            return member;
        }

        RotgutBoss SpawnBoss(Vector3 position)
        {
            var go = new GameObject(RotgutBoss.BossName);
            go.transform.SetParent(Root, false);
            go.transform.position = position;
            var unit = go.AddComponent<Unit>();
            unit.Init(RotgutBoss.BossName, Team.Enemy, Role.Boss, RotgutBoss.MaxHealth, 1.6f);
            var visual = UnitVisual.Create(unit, PixelArt.Boss(), PixelArt.Foot(new Color32(112, 72, 52, 255), true),
                0.95f, 0.35f, 20, 180f);
            unit.Visual = visual;
            var boss = go.AddComponent<RotgutBoss>();
            boss.Init(unit, visual);
            visual.AimDirection = () => boss.FacingDirection;
            return boss;
        }

        /// <summary>Party members face the boss while fighting, otherwise the way they walk.</summary>
        Vector3 AimAtBoss(Unit unit)
        {
            if (State != EncounterState.Fighting || Boss == null || Boss.Unit.IsDead) return Vector3.zero;
            return Boss.Unit.Position - unit.Position;
        }

        void SetState(EncounterState state)
        {
            State = state;
            StateEnteredAt = Time.unscaledTime;
        }

        void Update()
        {
            var input = GameInput.Instance;
            if (input == null) return;

            if (input.ToggleHints.WasPressedThisFrame()) ShowHints = !ShowHints;

            switch (State)
            {
                case EncounterState.PrePull:
                    if (input.ResetProgress.WasPressedThisFrame())
                    {
                        var names = new List<string>();
                        foreach (var p in Party) names.Add(p.Profile.Name);
                        Familiarity.ResetBoss(RotgutBoss.BossId, names);
                        Build();
                    }
                    else if (input.Confirm.WasPressedThisFrame() && Time.unscaledTime - StateEnteredAt > 0.3f)
                    {
                        StartReadyCheck();
                    }
                    break;

                case EncounterState.Countdown:
                    if (Time.unscaledTime >= countdownEndsAt) Pull();
                    break;

                case EncounterState.Fighting:
                    if (ActiveHint != null && Time.time > ActiveHint.ResolveAt + 0.5f) ActiveHint = null;
                    bool allDead = true;
                    foreach (var p in Party)
                        if (!p.Unit.IsDead) allDead = false;
                    if (allDead) EndFight(false);
                    break;

                case EncounterState.Wipe:
                case EncounterState.Victory:
                    if (Time.unscaledTime - StateEnteredAt > 1.5f && input.Confirm.WasPressedThisFrame()) Build();
                    break;
            }
        }

        void StartReadyCheck()
        {
            SetState(EncounterState.Countdown);
            countdownEndsAt = Time.unscaledTime + 3f;
            foreach (var p in Party)
            {
                if (p.IsLeader) continue;
                var line = p.Profile.Has(Trait.BackseatLeader)
                    ? BarkLines.Pick(BarkLines.ReadyBackseat)
                    : BarkLines.Pick(BarkLines.Ready);
                Barks.Say(p.Unit, line, 2.5f, true);
            }
            if (Familiarity.Attempts(RotgutBoss.BossId) > 0)
                Barks.Say(Boss.Unit, BarkLines.Pick(BarkLines.BossRetry), 3f, true);
        }

        void Pull()
        {
            SetState(EncounterState.Fighting);
            AttemptNumber = Familiarity.IncrementAttempts(RotgutBoss.BossId);
            Familiarity.Save();
            CombatLog.Reset();
            PartyMember tank = null;
            foreach (var p in Party)
                if (p.Profile.Role == Role.Tank) tank = p;
            Boss.BeginFight(tank);
            if (CommandSystem.Instance != null) CommandSystem.Instance.Shout("PULL!", new Color(1f, 0.9f, 0.4f));
        }

        public void BroadcastMechanic(MechanicEvent e)
        {
            ActiveHint = e;
            foreach (var p in Party)
                if (p.AI != null) p.AI.OnMechanic(e);
        }

        void OnMemberDied(Unit unit)
        {
            if (State != EncounterState.Fighting) return;
            if (Random.value < 0.5f && Boss != null && !Boss.Unit.IsDead)
                Barks.Say(Boss.Unit, BarkLines.Pick(BarkLines.BossKill), 2f);

            var alive = new List<PartyMember>();
            foreach (var p in Party)
                if (!p.Unit.IsDead && p.AI != null) alive.Add(p);
            if (alive.Count > 0)
            {
                var who = alive[Random.Range(0, alive.Count)];
                Barks.Say(who.Unit, string.Format(BarkLines.Pick(BarkLines.DeathReaction), unit.DisplayName), 2.2f);
            }
        }

        public void OnBossDefeated()
        {
            if (State == EncounterState.Fighting) EndFight(true);
        }

        void EndFight(bool victory)
        {
            TimeController.SetSlowMo(false);
            FightDuration = CombatLog.FightTime;
            BossPercentAtEnd = Boss != null ? Boss.Unit.HealthPercent * 100f : 100f;
            NewBest = Familiarity.TrySetBest(RotgutBoss.BossId, victory ? 0f : BossPercentAtEnd);
            if (victory) Familiarity.AddKill(RotgutBoss.BossId);

            // Everyone learns from every pull, win or wipe.
            foreach (var p in Party) Familiarity.Increment(RotgutBoss.BossId, p.Profile.Name);
            Familiarity.Save();

            LootDrop = victory ? LootTable[Random.Range(0, LootTable.Length)] : null;

            // A parting line from someone in the party.
            var speakers = new List<PartyMember>();
            foreach (var p in Party)
                if (p.AI != null) speakers.Add(p);
            var speaker = speakers[Random.Range(0, speakers.Count)];
            EndQuoteBy = speaker.Profile.Name;
            if (victory) EndQuote = BarkLines.Pick(BarkLines.Victory);
            else if (speaker.Profile.Has(Trait.TiltProne)) EndQuote = BarkLines.Pick(BarkLines.WipeTilt);
            else EndQuote = BarkLines.Pick(BarkLines.Wipe);

            SetState(victory ? EncounterState.Victory : EncounterState.Wipe);
            if (!victory && Boss != null) Barks.Say(Boss.Unit, BarkLines.Pick(BarkLines.BossWipe), 4f, true);
        }
    }
}
