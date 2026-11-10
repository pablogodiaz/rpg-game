using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    public enum EncounterState { PrePull, Countdown, Fighting, Wipe, Victory }

    /// <summary>
    /// Runs one boss encounter: spawns the party and the selected boss, Ready Check → countdown → pull,
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
        public BossController Boss { get; private set; }
        public int SelectedBoss { get; private set; }
        public BossEntry SelectedEntry => BossRoster.All[SelectedBoss];

        /// <summary>The leader's FOCUS target (skull marker). Damage dealers attack it instead of the boss.</summary>
        public Unit FocusTarget { get; private set; }

        public void SetFocus(Unit target) => FocusTarget = target;
        public void ClearFocus() => FocusTarget = null;

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
        const string SelectedBossKey = "ReadyCheck.selectedBoss";

        void Awake()
        {
            Instance = this;
            SelectedBoss = Mathf.Clamp(PlayerPrefs.GetInt(SelectedBossKey, 0), 0, BossRoster.All.Length - 1);
        }

        void SelectNextBoss()
        {
            SelectedBoss = (SelectedBoss + 1) % BossRoster.All.Length;
            PlayerPrefs.SetInt(SelectedBossKey, SelectedBoss);
            PlayerPrefs.Save();
            Build();
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
            FX.Clear();
            Root = new GameObject("Encounter").transform;
            CombatLog.Reset();
            if (CommandSystem.Instance != null) CommandSystem.Instance.ResetState();

            var entry = SelectedEntry;
            Arena.SetTheme(entry.Theme);
            Boss = entry.Spawn(Root);

            var profiles = Roster.StarterParty();
            for (int i = 0; i < profiles.Count; i++)
            {
                var start = new Vector3((i - (profiles.Count - 1) / 2f) * 1.4f, 0f, -9f);
                Party.Add(SpawnMember(profiles[i], i, start));
            }
            Leader = Party[0];

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
            Telegraph.Dangers.Clear();
            FocusTarget = null;
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
            member.Familiarity = Familiarity.Get(Boss.BossId, profile.Name);
            member.SetupAbilities();

            var visual = UnitVisual.Create(unit, PixelArt.Character(profile), PixelArt.Foot(profile.Secondary, false),
                0.3f, 0.22f, 10, 0f);
            visual.AimDirection = () => AimAt(member);
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

        /// <summary>Party members face whatever they are attacking while fighting, otherwise the way they walk.</summary>
        Vector3 AimAt(PartyMember member)
        {
            if (State != EncounterState.Fighting || Boss == null || Boss.Unit.IsDead) return Vector3.zero;
            var target = member.AttackTarget;
            return target != null ? target.Position - member.Unit.Position : Vector3.zero;
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
                        Familiarity.ResetBoss(Boss.BossId, names);
                        Build();
                    }
                    else if (input.NextBoss.WasPressedThisFrame())
                    {
                        SelectNextBoss();
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
            if (Familiarity.Attempts(Boss.BossId) > 0)
                Barks.Say(Boss.Unit, BarkLines.Pick(Boss.RetryLines), 3f, true);
        }

        void Pull()
        {
            SetState(EncounterState.Fighting);
            AttemptNumber = Familiarity.IncrementAttempts(Boss.BossId);
            Familiarity.Save();
            CombatLog.Reset();
            PartyMember tank = null;
            foreach (var p in Party)
                if (p.Profile.Role == Role.Tank) tank = p;
            foreach (var p in Party) p.ResetForPull();
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
                Barks.Say(Boss.Unit, BarkLines.Pick(Boss.KillLines), 2f);

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
            NewBest = Familiarity.TrySetBest(Boss.BossId, victory ? 0f : BossPercentAtEnd);
            if (victory) Familiarity.AddKill(Boss.BossId);

            // Everyone learns from every pull, win or wipe.
            foreach (var p in Party) Familiarity.Increment(Boss.BossId, p.Profile.Name);
            Familiarity.Save();

            var loot = Boss.LootTable;
            LootDrop = victory ? loot[Random.Range(0, loot.Length)] : null;

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
            if (!victory && Boss != null) Barks.Say(Boss.Unit, BarkLines.Pick(Boss.WipeLines), 4f, true);
        }
    }
}
