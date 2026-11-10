using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// The human-controlled raid leader: movement, Leader Bolt, Dodge, Rally, Quick Calls and the Raid Wheel.
    /// Holding the wheel slows time to 20% so calls can be made under pressure without a full pause.
    /// </summary>
    public class LeaderController : MonoBehaviour
    {
        public static readonly RaidCall[] WheelCalls =
        {
            RaidCall.Stack, RaidCall.Spread, RaidCall.Interrupt,
            RaidCall.Burn, RaidCall.Defensives, RaidCall.AsYouWere,
        };

        public const float AttackInterval = 0.5f;
        public const float AttackRange = 12f;
        public const float AttackDamage = 24f;
        public const float DodgeCooldown = 5f;
        public const float DodgeDuration = 0.22f;
        public const float RallyCooldown = 90f;

        public float DodgeReadyAt { get; private set; }
        public float RallyReadyAt { get; private set; }
        public bool WheelOpen { get; private set; }
        public int WheelSelection { get; private set; } = -1;

        PartyMember m;
        float attackReadyAt;
        float dodgeEndsAt;
        Vector3 dodgeDir = Vector3.forward;
        Vector3 lastMoveDir = Vector3.forward;
        Vector2 wheelPointerOrigin;

        void Awake()
        {
            m = GetComponent<PartyMember>();
        }

        void OnDisable()
        {
            if (WheelOpen) CloseWheel(false);
        }

        void Update()
        {
            var enc = EncounterManager.Instance;
            var input = GameInput.Instance;
            if (enc == null || input == null) return;

            if (m.Unit.IsDead)
            {
                if (WheelOpen) CloseWheel(false);
                m.Unit.Invulnerable = false;
                return;
            }

            bool fighting = enc.State == EncounterState.Fighting;
            bool canMove = fighting || enc.State == EncounterState.PrePull;

            // Movement (and dodge)
            var stick = input.Move.ReadValue<Vector2>();
            var dir = CameraRelative(stick);
            if (Time.time < dodgeEndsAt)
            {
                m.Move(dodgeDir, 3.4f);
            }
            else
            {
                m.Unit.Invulnerable = false;
                if (canMove) m.Move(dir);
                if (dir.sqrMagnitude > 0.04f) lastMoveDir = dir.normalized;
            }

            if (!fighting)
            {
                if (WheelOpen) CloseWheel(false);
                return;
            }

            if (input.Dodge.WasPressedThisFrame() && Time.time >= DodgeReadyAt)
            {
                dodgeDir = lastMoveDir;
                dodgeEndsAt = Time.time + DodgeDuration;
                DodgeReadyAt = Time.time + DodgeCooldown;
                m.Unit.Invulnerable = true;
            }

            if (input.Attack.IsPressed() && Time.time >= attackReadyAt) TryAttack(enc);
            if (input.Rally.WasPressedThisFrame()) TryRally(enc);

            // Quick Calls (D-pad / number keys)
            var cmd = CommandSystem.Instance;
            if (input.CallStack.WasPressedThisFrame()) cmd.Issue(RaidCall.Stack);
            if (input.CallSpread.WasPressedThisFrame()) cmd.Issue(RaidCall.Spread);
            if (input.CallInterrupt.WasPressedThisFrame()) cmd.Issue(RaidCall.Interrupt);
            if (input.CallBurn.WasPressedThisFrame()) cmd.Issue(RaidCall.Burn);
            if (input.CallDefensives.WasPressedThisFrame()) cmd.Issue(RaidCall.Defensives);
            if (input.CallAsYouWere.WasPressedThisFrame()) cmd.Issue(RaidCall.AsYouWere);

            // Raid Wheel (hold)
            if (input.Wheel.IsPressed())
            {
                if (!WheelOpen) OpenWheel(input);
                UpdateWheelSelection(input);
            }
            else if (WheelOpen)
            {
                CloseWheel(true);
            }
        }

        static Vector3 CameraRelative(Vector2 stick)
        {
            var cam = CameraRig.MainCam;
            if (cam == null) return new Vector3(stick.x, 0f, stick.y);
            // Top-down camera: "forward" on the stick is screen-up.
            var forward = cam.transform.up;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            var right = cam.transform.right;
            right.y = 0f;
            right.Normalize();
            return right * stick.x + forward * stick.y;
        }

        void TryAttack(EncounterManager enc)
        {
            var boss = enc.Boss;
            if (boss == null || boss.Unit.IsDead) return;
            if (m.DistanceToBossEdge(boss) > AttackRange) return;
            attackReadyAt = Time.time + AttackInterval;
            m.FireAt(boss.Unit, AttackDamage, new Color(1f, 0.9f, 0.35f), "Leader Bolt");
        }

        void TryRally(EncounterManager enc)
        {
            if (Time.time < RallyReadyAt)
            {
                Barks.Say(m.Unit, $"Rally is on cooldown ({RallyReadyAt - Time.time:0}s).", 1.5f, true);
                return;
            }
            RallyReadyAt = Time.time + RallyCooldown;
            foreach (var member in enc.Party)
                if (!member.Unit.IsDead)
                    member.Unit.AddBuff("rally", "RALLY", BuffKind.DamageDone, 1.3f, 12f);
            CombatLog.RecordCall("Rally");
            CommandSystem.Instance.Shout("RALLY! (+30% damage)", new Color(1f, 0.85f, 0.3f));
            Barks.Say(m.Unit, BarkLines.Pick(BarkLines.Rally), 2f, true);
        }

        void OpenWheel(GameInput input)
        {
            WheelOpen = true;
            WheelSelection = -1;
            wheelPointerOrigin = input.PointerPosition ?? Vector2.zero;
            TimeController.SetSlowMo(true);
        }

        void UpdateWheelSelection(GameInput input)
        {
            var dir = Vector2.zero;
            var stick = input.WheelSelect.ReadValue<Vector2>();
            if (stick.magnitude > 0.5f)
            {
                dir = stick;
            }
            else if (!input.UsingGamepad && input.PointerPosition.HasValue)
            {
                var delta = input.PointerPosition.Value - wheelPointerOrigin;
                if (delta.magnitude > 30f) dir = delta;
            }

            if (dir == Vector2.zero) return;
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; // 0 = up, clockwise
            if (angle < 0f) angle += 360f;
            float slice = 360f / WheelCalls.Length;
            WheelSelection = Mathf.RoundToInt(angle / slice) % WheelCalls.Length;
        }

        void CloseWheel(bool issueSelection)
        {
            WheelOpen = false;
            TimeController.SetSlowMo(false);
            if (issueSelection && WheelSelection >= 0 && CommandSystem.Instance != null)
                CommandSystem.Instance.Issue(WheelCalls[WheelSelection]);
            WheelSelection = -1;
        }
    }
}
