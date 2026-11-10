using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// First-iteration HUD drawn with IMGUI. It's quick to change and needs no assets.
    /// Planned replacement: a pixel-art UI Toolkit/uGUI layer (see design doc, UI section).
    /// Everything is laid out on a 720p-tall virtual canvas and scaled to the screen.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        const float RefHeight = 720f;

        float scale = 1f;
        float vw = 1280f;
        float vh = RefHeight;

        GUIStyle label, labelRight, labelCenter, small, smallCenter, smallRight, big, title, bubble, wheelItem, floating, chip, hint;
        bool stylesReady;

        static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.82f);
        static readonly Color PanelLight = new Color(0.16f, 0.14f, 0.22f, 0.9f);
        static readonly Color BarBg = new Color(0.12f, 0.1f, 0.14f, 1f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
        static readonly Color Muted = new Color(0.7f, 0.68f, 0.78f);

        void EnsureStyles()
        {
            if (stylesReady) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false, wordWrap = false };
            label.normal.textColor = Color.white;
            labelRight = new GUIStyle(label) { alignment = TextAnchor.UpperRight };
            labelCenter = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(label) { fontSize = 11 };
            smallCenter = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            smallRight = new GUIStyle(small) { alignment = TextAnchor.UpperRight };
            big = new GUIStyle(labelCenter) { fontSize = 34, fontStyle = FontStyle.Bold };
            title = new GUIStyle(labelCenter) { fontSize = 22, fontStyle = FontStyle.Bold };
            bubble = new GUIStyle(labelCenter) { fontSize = 12, wordWrap = true };
            bubble.normal.textColor = new Color(0.1f, 0.08f, 0.12f);
            wheelItem = new GUIStyle(labelCenter) { fontSize = 15, fontStyle = FontStyle.Bold };
            floating = new GUIStyle(labelCenter) { fontStyle = FontStyle.Bold };
            chip = new GUIStyle(smallCenter) { fontSize = 9, fontStyle = FontStyle.Bold };
            hint = new GUIStyle(labelCenter) { fontSize = 13, wordWrap = true };
            stylesReady = true;
        }

        // ----- Drawing helpers ---------------------------------------------------------------------

        static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Bar(Rect r, float fraction, Color fg, Color bg)
        {
            Box(r, bg);
            Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), fg);
        }

        static void Text(Rect r, string text, GUIStyle style, Color? color = null)
        {
            var original = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = color ?? original;
            GUI.Label(r, text, style);
            style.normal.textColor = original;
        }

        bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            gui = Vector2.zero;
            var cam = CameraRig.MainCam;
            if (cam == null) return false;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return false;
            gui = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            return true;
        }

        static Color RoleColor(Role role)
        {
            switch (role)
            {
                case Role.Tank: return new Color(0.75f, 0.55f, 0.35f);
                case Role.Healer: return new Color(0.4f, 0.9f, 0.5f);
                case Role.Melee: return new Color(0.95f, 0.4f, 0.35f);
                case Role.Ranged: return new Color(0.45f, 0.6f, 1f);
                default: return Color.white;
            }
        }

        static string CallGlyph(GameInput input, RaidCall call)
        {
            switch (call)
            {
                case RaidCall.Stack: return input.Glyph("D-Up", "1");
                case RaidCall.Spread: return input.Glyph("D-Down", "2");
                case RaidCall.Interrupt: return input.Glyph("D-Left", "3");
                case RaidCall.Burn: return input.Glyph("D-Right", "4");
                case RaidCall.Defensives: return input.Glyph("LB wheel", "5");
                case RaidCall.AsYouWere: return input.Glyph("LB wheel", "6");
                case RaidCall.Focus: return input.Glyph("RB", "7");
                default: return "";
            }
        }

        // ----- Main --------------------------------------------------------------------------------

        void OnGUI()
        {
            var enc = EncounterManager.Instance;
            var input = GameInput.Instance;
            if (enc == null || input == null) return;
            EnsureStyles();

            scale = Screen.height / RefHeight;
            vw = Screen.width / scale;
            vh = RefHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawWorldMarkers(enc);
            DrawBossParts(enc);
            DrawFloatingText();
            DrawBarks();
            if (enc.Boss != null) DrawBossFrame(enc);
            DrawRaidFrames(enc);

            if (enc.State == EncounterState.Fighting)
            {
                DrawActionBar(enc, input);
                DrawHint(enc, input);
            }

            DrawShout();

            var leader = enc.Leader != null ? enc.Leader.GetComponent<LeaderController>() : null;
            if (leader != null && leader.WheelOpen) DrawWheel(leader, input);

            switch (enc.State)
            {
                case EncounterState.PrePull: DrawPrePull(enc, input); break;
                case EncounterState.Countdown: DrawCountdown(enc); break;
                case EncounterState.Wipe:
                case EncounterState.Victory: DrawReport(enc, input); break;
            }

            if (!GameInput.BackendEnabled)
            {
                var warn = new Rect((vw - 760f) / 2f, vh / 2f - 40f, 760f, 80f);
                Box(warn, new Color(0.5f, 0.05f, 0.05f, 0.95f));
                Text(new Rect(warn.x + 10, warn.y + 10, warn.width - 20, 24), "Input System backend is disabled, so controls won't work.", labelCenter);
                Text(new Rect(warn.x + 10, warn.y + 40, warn.width - 20, 24),
                    "Edit > Project Settings > Player > Other Settings > Active Input Handling = Input System Package (New), then restart Unity.", smallCenter);
            }

            Text(new Rect(vw - 260, vh - 22, 250, 20),
                $"{input.Glyph("Select", "H")}: hints {(enc.ShowHints ? "on" : "off")}", smallRight, Muted);
        }

        // ----- World-space labels ------------------------------------------------------------------

        void DrawWorldMarkers(EncounterManager enc)
        {
            var cmd = CommandSystem.Instance;
            if (cmd == null || enc.Boss == null || enc.State != EncounterState.Fighting) return;
            if (Time.time < cmd.LeaderSpreadUntil && enc.Leader != null && !enc.Leader.Unit.IsDead)
            {
                var p = CommandSystem.SpreadPoint(enc.Boss, cmd.LeaderSpreadAngle);
                if (WorldToGui(p, out var g))
                {
                    var r = new Rect(g.x - 30, g.y - 10, 60, 20);
                    Box(r, new Color(0.7f, 0.35f, 1f, 0.75f));
                    Text(r, "YOU", smallCenter);
                }
            }
        }

        /// <summary>Health bars and names over extra targets (Krakenna's arms), the FOCUS skull, and grip meters.</summary>
        void DrawBossParts(EncounterManager enc)
        {
            var boss = enc.Boss;
            if (boss == null || enc.State == EncounterState.Wipe || enc.State == EncounterState.Victory) return;
            foreach (var u in boss.ExtraTargets)
            {
                if (u == null || u.IsDead) continue;
                if (!WorldToGui(u.Position, out var g)) continue;
                bool focused = enc.FocusTarget == u;
                var arm = u.GetComponent<KrakenArm>();
                bool hugging = arm != null && arm.Grabbed != null;
                // Keep it readable: bars only on arms that matter right now, names only when focused or hugging.
                if (!focused && !hugging && u.HealthPercent >= 0.999f) continue;
                const float w = 58f;
                var bar = new Rect(g.x - w / 2f, g.y - 30f, w, 6f);
                if (focused) Box(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), Gold);
                Bar(bar, u.HealthPercent, new Color(0.9f, 0.35f, 0.6f), new Color(0.1f, 0.05f, 0.08f, 0.9f));
                if (focused || hugging)
                    Text(new Rect(g.x - 60f, g.y - 46f, 120f, 16f), u.DisplayName, chip, focused ? Gold : new Color(1f, 0.85f, 0.92f));

                if (arm != null && arm.Grabbed != null)
                {
                    var grip = new Rect(g.x - w / 2f, g.y - 22f, w, 5f);
                    Bar(grip, arm.GripFraction, new Color(0.5f, 1f, 0.6f), new Color(0.1f, 0.1f, 0.1f, 0.9f));
                    Text(new Rect(g.x - 80f, g.y - 18f, 160f, 16f),
                        $"HUGGING {arm.Grabbed.Profile.Name.ToUpperInvariant()}  {arm.GrabTimeLeft:0.0}s", chip, new Color(1f, 0.6f, 0.75f));
                }
            }

            var focus = enc.FocusTarget;
            if (focus != null && !focus.IsDead && WorldToGui(focus.Position, out var fg))
            {
                float bob = Mathf.Sin(Time.unscaledTime * 5f) * 3f;
                var tex = PixelArt.Skull().texture;
                float size = 11f * 2.6f;
                GUI.DrawTexture(new Rect(fg.x - size / 2f, fg.y - 52f - size + bob, size, size * 12f / 11f), tex);
            }
        }

        /// <summary>Damage numbers and ability callouts rising off units.</summary>
        void DrawFloatingText()
        {
            float now = Time.unscaledTime;
            foreach (var ft in FX.Texts)
            {
                float age = now - ft.Born;
                float t = Mathf.Clamp01(age / ft.Life);
                var world = ft.World + Vector3.forward * (age * 0.9f) + Vector3.right * (ft.DriftX * age);
                if (!WorldToGui(world, out var g)) continue;
                float alpha = t < 0.7f ? 1f : (1f - t) / 0.3f;
                float pop = ft.Pop && age < 0.12f ? Mathf.Lerp(1.6f, 1f, age / 0.12f) : 1f;
                floating.fontSize = Mathf.RoundToInt(ft.Size * pop);
                var r = new Rect(g.x - 120f, g.y - 16f, 240f, 32f);
                Text(r, ft.Text, floating, new Color(ft.Color.r, ft.Color.g, ft.Color.b, alpha));
            }
        }

        void DrawBarks()
        {
            foreach (var b in Barks.Active)
            {
                if (b.Unit == null) continue;
                float above = b.Unit.Role == Role.Boss ? 2.6f : 0.95f;
                if (!WorldToGui(b.Unit.Position + Vector3.forward * above, out var g)) continue;
                var content = new GUIContent(b.Text);
                float w = Mathf.Min(190f, bubble.CalcSize(content).x + 14f);
                float h = bubble.CalcHeight(content, w - 10f) + 8f;
                var r = new Rect(g.x - w / 2f, g.y - h, w, h);
                Box(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(0.1f, 0.08f, 0.12f, 0.9f));
                Box(r, b.Unit.Role == Role.Boss ? new Color(1f, 0.88f, 0.75f, 0.95f) : new Color(0.97f, 0.96f, 0.92f, 0.95f));
                GUI.Label(new Rect(r.x + 5, r.y + 4, r.width - 10, r.height - 8), b.Text, bubble);
            }
        }

        // ----- Boss frame --------------------------------------------------------------------------

        void DrawBossFrame(EncounterManager enc)
        {
            var boss = enc.Boss;
            float w = 520f, x = (vw - w) / 2f, y = 10f;
            Box(new Rect(x - 8, y - 4, w + 16, boss.IsCasting ? 82 : 58), Panel);

            string name = boss.BossName + (boss.PhaseLabel != null ? "   - " + boss.PhaseLabel : "");
            Text(new Rect(x, y, w, 20), name, label, boss.Enraged ? new Color(1f, 0.4f, 0.3f) : Color.white);

            string right = "";
            if (boss.StatusText != null) right += boss.StatusText + "    ";
            if (enc.State == EncounterState.Fighting)
                right += boss.Enraged ? "ENRAGED" : $"Enrage in {CombatLog.FormatTime(boss.EnrageTime - boss.FightTime)}";
            Text(new Rect(x, y, w, 20), right, labelRight, Gold);

            var hpRect = new Rect(x, y + 22, w, 18);
            Bar(hpRect, boss.Unit.HealthPercent, new Color(0.78f, 0.2f, 0.22f), BarBg);
            Text(hpRect, $"{boss.Unit.HealthPercent * 100f:0.0}%   ({boss.Unit.Health:0} / {boss.Unit.MaxHealth:0})", smallCenter);

            if (boss.IsCasting)
            {
                float cw = 360f, cx = (vw - cw) / 2f;
                var castRect = new Rect(cx, y + 48, cw, 22);
                var col = boss.CastInterruptible ? new Color(1f, 0.85f, 0.25f) : new Color(0.6f, 0.58f, 0.66f);
                Bar(castRect, boss.CastFraction, col, BarBg);
                string castText = boss.CastInterruptible ? $"{boss.CastName}   [ INTERRUPTIBLE ]" : boss.CastName;
                float remaining = Mathf.Max(0f, boss.CastEndsAt - Time.time);
                Text(castRect, $"{castText}   {remaining:0.0}s", smallCenter, Color.black);
            }
        }

        // ----- Raid frames -------------------------------------------------------------------------

        void DrawRaidFrames(EncounterManager enc)
        {
            float x = 12f, y = 96f, w = 230f, h = 60f;
            Box(new Rect(x - 6, y - 24, w + 12, enc.Party.Count * (h + 6) + 28), Panel);
            Text(new Rect(x, y - 22, w, 20), "RAID", small, Muted);

            foreach (var p in enc.Party)
            {
                var u = p.Unit;
                var r = new Rect(x, y, w, h);
                Box(r, PanelLight);
                Box(new Rect(x, y, 4, h), RoleColor(p.Profile.Role));

                Text(new Rect(x + 8, y + 1, w - 10, 18), $"{p.Profile.Name}  ({p.Profile.ClassTitle})", small,
                    p.IsLeader ? Gold : Color.white);

                // Tags: orders, buffs, statuses
                var tags = new List<string>();
                if (p.AI != null && p.AI.OrderLabel != null) tags.Add(p.AI.OrderLabel);
                foreach (var buff in u.Buffs) tags.Add(buff.Label);
                var puddle = u.IsDead ? null : FirePuddle.At(u.Position, -0.1f);
                if (puddle != null) tags.Add(puddle.Label);
                if (enc.Boss != null && !u.IsDead && enc.Boss.IsMarked(u)) tags.Add("MARKED");
                if (p.IsHeld) tags.Add("GRABBED");
                Text(new Rect(x + 8, y + 1, w - 12, 18), string.Join(" ", tags), smallRight, Gold);

                var hpRect = new Rect(x + 8, y + 19, w - 16, 13);
                var hpColor = Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.35f), u.HealthPercent);
                Bar(hpRect, u.HealthPercent, hpColor, BarBg);
                if (u.Absorb > 0f)
                    Box(new Rect(hpRect.x, hpRect.yMax - 4, hpRect.width * Mathf.Clamp01(u.Absorb / u.MaxHealth), 4),
                        new Color(0.55f, 0.9f, 1f, 0.95f));
                Text(hpRect, u.IsDead ? $"DEAD ({u.DeathCause})" : $"{u.Health:0} / {u.MaxHealth:0}", smallCenter);

                if (p.CastName != null && !u.IsDead)
                    Bar(new Rect(x + 8, y + 34, w - 16, 3), p.CastFraction, new Color(0.9f, 0.9f, 0.5f), BarBg);

                if (p.Profile.CanInterrupt && !u.IsDead)
                    Text(new Rect(x + 8, y + 33, w - 12, 14),
                        p.CanInterruptNow ? "kick ready" : $"kick {p.InterruptReadyAt - Time.time:0}s", small,
                        p.CanInterruptNow ? new Color(1f, 0.95f, 0.4f) : Muted);
                Text(new Rect(x + 8, y + 33, w - 12, 14),
                    p.IsLeader ? "" : $"awareness {p.Profile.Awareness * 100f:0}  fam {p.Familiarity}", smallRight, Muted);

                DrawAbilityChips(p, x + 8, y + 47);

                if (u.IsDead) Box(r, new Color(0f, 0f, 0f, 0.45f));
                y += h + 6f;
            }
        }

        /// <summary>Small cooldown chips for each signature ability. They flash white when used.</summary>
        void DrawAbilityChips(PartyMember p, float x, float y)
        {
            const float cw = 42f, chh = 11f;
            foreach (var a in p.Abilities)
            {
                var r = new Rect(x, y, cw, chh);
                var c = a.Color;
                Box(r, new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 1f));
                float frac = a.Charges == 0 ? 0f : a.Ready ? 1f : 1f - a.Remaining / a.Cooldown;
                Box(new Rect(x, y, cw * frac, chh), a.Ready ? c : new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f));
                if (Time.time - a.LastUsedAt < 0.35f) Box(r, new Color(1f, 1f, 1f, 0.8f));
                Text(r, a.Short, chip, a.Ready ? Color.black : Color.white);
                x += cw + 3f;
            }
        }

        // ----- Action bar --------------------------------------------------------------------------

        void DrawActionBar(EncounterManager enc, GameInput input)
        {
            var cmd = CommandSystem.Instance;
            var leader = enc.Leader.GetComponent<LeaderController>();
            float bw = 96f, bh = 44f, gap = 6f;
            var calls = LeaderController.WheelCalls;
            float total = calls.Length * (bw + gap) - gap;
            float x = (vw - total) / 2f, y = vh - bh - 34f;

            Box(new Rect(x - 8, y - 22, total + 16, bh + 54), Panel);
            Text(new Rect(x, y - 20, total, 18), "CALLS", small, Muted);

            foreach (var call in calls)
            {
                var r = new Rect(x, y, bw, bh);
                Box(r, PanelLight);
                Box(new Rect(x, y, bw, 3), RaidCallInfo.Color(call));
                Text(new Rect(x, y + 5, bw, 18), RaidCallInfo.Name(call), smallCenter);
                Text(new Rect(x, y + 23, bw, 16), CallGlyph(input, call), smallCenter, Muted);
                float remaining = cmd.Remaining(call);
                if (remaining > 0f)
                {
                    float frac = remaining / RaidCallInfo.Cooldown(call);
                    Box(new Rect(x, y + bh * (1f - frac), bw, bh * frac), new Color(0f, 0f, 0f, 0.6f));
                    Text(r, $"{remaining:0}", title);
                }
                x += bw + gap;
            }

            // Leader abilities row
            var lc = enc.Leader;
            float ax = (vw - total) / 2f, ay = y + bh + 4f, ah = 24f;
            DrawLeaderSlot(new Rect(ax, ay, bw, ah), $"Attack {input.Glyph("RT", "LMB")}", new Color(1f, 0.9f, 0.35f), 0f, 1f, -1);
            ax += bw + gap;
            DrawLeaderSlot(new Rect(ax, ay, bw, ah), $"Dodge {input.Glyph("A", "Spc")}", new Color(0.85f, 0.85f, 0.95f),
                Mathf.Max(0f, leader.DodgeReadyAt - Time.time), LeaderController.DodgeCooldown, -1);
            ax += bw + gap;
            foreach (var a in lc.Abilities)
            {
                string key = a is PiercingLance ? input.Glyph("X", "E") : a is RallyAbility ? input.Glyph("Y", "R") : input.Glyph("B", "F");
                string name = a is PiercingLance ? "Lance" : a.Name;
                DrawLeaderSlot(new Rect(ax, ay, bw, ah), $"{name} {key}", a.Color, a.Remaining, a.Cooldown, a.Charges, a.LastUsedAt);
                ax += bw + gap;
            }
            DrawLeaderSlot(new Rect(ax, ay, bw, ah), $"Wheel {input.Glyph("LB", "Q")}", new Color(0.8f, 0.7f, 1f), 0f, 1f, -1);
        }

        void DrawLeaderSlot(Rect r, string text, Color color, float remaining, float cooldown, int charges, float lastUsed = -100f)
        {
            Box(r, PanelLight);
            Box(new Rect(r.x, r.y, r.width, 2), color);
            bool empty = charges == 0;
            if (remaining > 0f && cooldown > 0f)
            {
                float frac = remaining / cooldown;
                Box(new Rect(r.x, r.y + r.height * (1f - frac), r.width, r.height * frac), new Color(0f, 0f, 0f, 0.6f));
            }
            if (empty) Box(r, new Color(0f, 0f, 0f, 0.6f));
            if (Time.time - lastUsed < 0.3f) Box(r, new Color(1f, 1f, 1f, 0.6f));
            string label = remaining > 0f ? $"{text}  {remaining:0}s" : empty ? $"{text}  (used)" : text;
            Text(r, label, smallCenter, remaining > 0f || empty ? Muted : Color.white);
        }

        // ----- Mechanic hints ----------------------------------------------------------------------

        void DrawHint(EncounterManager enc, GameInput input)
        {
            var e = enc.ActiveHint;
            if (!enc.ShowHints || e == null) return;
            if (string.IsNullOrEmpty(e.Hint)) return;
            string text = e.Hint.Replace("{call}", CallGlyph(input, e.SuggestedCall));
            float w = Mathf.Min(vw - 40f, 880f), x = (vw - w) / 2f, y = vh - 160f;
            var col = RaidCallInfo.Color(e.SuggestedCall);
            Box(new Rect(x, y, w, 40), new Color(0.05f, 0.04f, 0.08f, 0.88f));
            Box(new Rect(x, y, 4, 40), col);
            Text(new Rect(x + 10, y + 2, w - 20, 36), text, hint, col);
        }

        void DrawShout()
        {
            var cmd = CommandSystem.Instance;
            if (cmd == null || cmd.LastShout == null) return;
            float age = Time.unscaledTime - cmd.LastShoutAt;
            if (age > 1.6f) return;
            float alpha = Mathf.Clamp01(1.6f - age);
            var c = cmd.LastShoutColor;
            Text(new Rect(0, 128, vw, 44), cmd.LastShout, big, new Color(c.r, c.g, c.b, alpha));
        }

        // ----- Raid Wheel --------------------------------------------------------------------------

        void DrawWheel(LeaderController leader, GameInput input)
        {
            Box(new Rect(0, 0, vw, vh), new Color(0.02f, 0.0f, 0.05f, 0.35f));
            var cmd = CommandSystem.Instance;
            var center = new Vector2(vw / 2f, vh / 2f);
            const float radius = 150f;
            var calls = LeaderController.WheelCalls;

            Text(new Rect(center.x - 120, center.y - 30, 240, 24), "RAID WHEEL", title, Gold);
            Text(new Rect(center.x - 120, center.y - 4, 240, 20), "slow-mo  -  release to call", smallCenter, Muted);
            Text(new Rect(center.x - 150, center.y + 14, 300, 20),
                input.Glyph("aim with right stick", "move the mouse toward a call"), smallCenter, Muted);

            for (int i = 0; i < calls.Length; i++)
            {
                float a = i * Mathf.PI * 2f / calls.Length;
                var pos = center + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius;
                var r = new Rect(pos.x - 78, pos.y - 26, 156, 52);
                bool selected = leader.WheelSelection == i;
                Box(r, selected ? new Color(0.35f, 0.28f, 0.12f, 0.95f) : new Color(0.08f, 0.07f, 0.12f, 0.92f));
                Box(new Rect(r.x, r.y, r.width, 3), RaidCallInfo.Color(calls[i]));
                if (selected)
                {
                    Box(new Rect(r.x - 2, r.y - 2, r.width + 4, 2), Gold);
                    Box(new Rect(r.x - 2, r.yMax, r.width + 4, 2), Gold);
                }
                Text(new Rect(r.x, r.y + 6, r.width, 20), RaidCallInfo.Name(calls[i]), wheelItem, selected ? Gold : Color.white);
                float remaining = cmd.Remaining(calls[i]);
                string sub = remaining > 0f ? $"cooldown {remaining:0}s" : RaidCallInfo.Description(calls[i]);
                Text(new Rect(r.x, r.y + 28, r.width, 18), sub, smallCenter, Muted);
            }
        }

        // ----- Screens -----------------------------------------------------------------------------

        void DrawPrePull(EncounterManager enc, GameInput input)
        {
            float w = 700f, h = 480f, x = (vw - w) / 2f, y = (vh - h) / 2f + 20f;
            Box(new Rect(x, y, w, h), Panel);
            Box(new Rect(x, y, w, 3), Gold);
            Text(new Rect(x, y + 10, w, 30), "READY CHECK", title, Gold);

            var boss = enc.Boss;
            var entry = enc.SelectedEntry;
            int attempts = Familiarity.Attempts(boss.BossId);
            float best = Familiarity.BestPercent(boss.BossId);
            int kills = Familiarity.Kills(boss.BossId);
            string progress = attempts == 0
                ? "First pull. Nobody knows this fight yet, so you'll have to call everything."
                : $"Attempts: {attempts}    Best: {(kills > 0 ? "KILLED" : best.ToString("0.0") + "% boss HP")}    Kills: {kills}";
            string selector = BossRoster.All.Length > 1
                ? $"<  {boss.BossName}  >    ({enc.SelectedBoss + 1}/{BossRoster.All.Length})   [{input.Glyph("RB", "Tab")}] switch boss"
                : boss.BossName;
            Text(new Rect(x, y + 38, w, 20), selector, labelCenter, new Color(1f, 0.75f, 0.85f));
            Text(new Rect(x, y + 58, w, 18), $"{entry.Location}: {entry.Blurb}", smallCenter, Muted);
            Text(new Rect(x, y + 74, w, 18), progress, smallCenter, Muted);

            float ly = y + 98f;
            Text(new Rect(x + 20, ly, w - 40, 20), "YOUR PARTY", small, Muted);
            ly += 18f;
            foreach (var p in enc.Party)
            {
                string traits = p.Profile.Traits.Count > 0
                    ? string.Join(", ", p.Profile.Traits.ConvertAll(MemberProfile.TraitName))
                    : "-";
                string line = p.IsLeader
                    ? $"{p.Profile.Name} - {p.Profile.ClassTitle}"
                    : $"{p.Profile.Name} - {p.Profile.ClassTitle} ({MemberProfile.RoleName(p.Profile.Role)})   traits: {traits}   familiarity: {p.Familiarity}";
                Text(new Rect(x + 20, ly, w - 40, 18), line, small, p.IsLeader ? Gold : Color.white);
                ly += 15f;
                var names = new List<string>();
                foreach (var a in p.Abilities) names.Add(a.Name);
                if (p.Profile.CanInterrupt) names.Add(p.Profile.InterruptName + " (interrupt)");
                Text(new Rect(x + 34, ly, w - 54, 18), "abilities: " + string.Join(", ", names), small, Muted);
                ly += 17f;
            }

            ly += 10f;
            Text(new Rect(x + 20, ly, w - 40, 20), "CONTROLS", small, Muted);
            ly += 18f;
            string[] controls = input.UsingGamepad
                ? new[]
                {
                    "Left stick: move   RT: attack   A: dodge (wriggle when grabbed)   X: Lance   Y: Rally   B: Battle Rez",
                    "D-pad: Quick Calls (Up Stack, Down Spread, Left Interrupt, Right Burn)   RB: FOCUS (skull)",
                    "Hold LB: Raid Wheel (slow-mo), aim with right stick, release to call",
                }
                : new[]
                {
                    "WASD: move   LMB / J: attack   Space: dodge (wriggle when grabbed)   E: Lance   R: Rally   F: Battle Rez",
                    "1-7: calls (Stack, Spread, Interrupt, Burn, Defensives, As You Were, FOCUS)",
                    "Hold Q or RMB: Raid Wheel (slow-mo), move the mouse toward a call, release to call",
                };
            foreach (var c in controls)
            {
                Text(new Rect(x + 20, ly, w - 40, 18), c, small);
                ly += 17f;
            }

            Text(new Rect(x, y + h - 52, w, 22),
                $"Press [{input.Glyph("Start / A", "Enter / Space")}] to start the Ready Check and pull",
                labelCenter, Gold);
            Text(new Rect(x, y + h - 28, w, 18), "[F9] reset this boss's progress (attempts and familiarity)", smallCenter, Muted);
        }

        void DrawCountdown(EncounterManager enc)
        {
            float remaining = 3f - (Time.unscaledTime - enc.StateEnteredAt);
            int n = Mathf.Clamp(Mathf.CeilToInt(remaining), 1, 3);
            Text(new Rect(0, vh / 2f - 60, vw, 60), n.ToString(), big, Gold);
            Text(new Rect(0, vh / 2f - 10, vw, 24), "Pulling in...", labelCenter, Muted);
        }

        void DrawReport(EncounterManager enc, GameInput input)
        {
            bool victory = enc.State == EncounterState.Victory;
            float w = 660f, h = 450f, x = (vw - w) / 2f, y = (vh - h) / 2f + 30f;
            Box(new Rect(x, y, w, h), Panel);
            var accent = victory ? Gold : new Color(1f, 0.35f, 0.3f);
            Box(new Rect(x, y, w, 3), accent);
            Text(new Rect(x, y + 10, w, 30), victory ? "VICTORY!" : "WIPE", title, accent);

            string summary = victory
                ? $"{enc.Boss.BossName} defeated in {CombatLog.FormatTime(enc.FightDuration)} on attempt #{enc.AttemptNumber}!"
                : $"{enc.Boss.BossName} at {enc.BossPercentAtEnd:0.0}% after {CombatLog.FormatTime(enc.FightDuration)}" +
                  (enc.NewBest ? "   NEW BEST!" : $"   (best: {Familiarity.BestPercent(enc.Boss.BossId):0.0}%)");
            Text(new Rect(x, y + 40, w, 20), summary, labelCenter);

            // Table
            float ty = y + 72f;
            float c0 = x + 24, c1 = x + 230, c2 = x + 320, c3 = x + 410;
            Text(new Rect(c0, ty, 200, 18), "MEMBER", small, Muted);
            Text(new Rect(c1, ty, 90, 18), "DAMAGE", small, Muted);
            Text(new Rect(c2, ty, 90, 18), "HEALING", small, Muted);
            Text(new Rect(c3, ty, 240, 18), "DIED TO", small, Muted);
            ty += 18f;

            Unit topDamage = null, mostFire = null, flashiest = null;
            float topDmg = -1f;
            int fire = 0, flashes = 0;
            foreach (var p in enc.Party)
            {
                var s = CombatLog.Get(p.Unit);
                if (s.Damage > topDmg) { topDmg = s.Damage; topDamage = p.Unit; }
                if (s.FireTicks > fire) { fire = s.FireTicks; mostFire = p.Unit; }
                if (!p.IsLeader && s.AbilitiesUsed > flashes) { flashes = s.AbilitiesUsed; flashiest = p.Unit; }
                Text(new Rect(c0, ty, 200, 18), $"{p.Profile.Name} ({p.Profile.ClassTitle})", small, p.IsLeader ? Gold : Color.white);
                Text(new Rect(c1, ty, 90, 18), $"{s.Damage:0}", small);
                Text(new Rect(c2, ty, 90, 18), $"{s.Healing:0}", small);
                Text(new Rect(c3, ty, 240, 18), s.DeathCause ?? "-", small);
                ty += 17f;
            }

            ty += 10f;
            Text(new Rect(c0, ty, w - 48, 18), "AWARDS", small, Muted);
            ty += 17f;
            if (topDamage != null) { Text(new Rect(c0, ty, w - 48, 18), $"Top Damage: {topDamage.DisplayName} ({topDmg:0})", small); ty += 16f; }
            if (flashiest != null) { Text(new Rect(c0, ty, w - 48, 18), $"Flashiest: {flashiest.DisplayName} ({flashes} abilities)", small); ty += 16f; }
            if (mostFire != null) { Text(new Rect(c0, ty, w - 48, 18), $"Most Time Standing In Bad: {mostFire.DisplayName} ({fire} ticks)", small); ty += 16f; }
            if (CombatLog.DeathOrder.Count > 0) { Text(new Rect(c0, ty, w - 48, 18), $"First To Die: {CombatLog.DeathOrder[0].DisplayName}", small); ty += 16f; }

            ty += 8f;
            int calls = CombatLog.Calls.Count;
            string recent = calls > 0 ? string.Join(", ", CombatLog.Calls.GetRange(Mathf.Max(0, calls - 5), Mathf.Min(5, calls))) : "none. Bold strategy.";
            Text(new Rect(c0, ty, w - 48, 18), $"Calls made: {calls}   (last: {recent})", small);
            ty += 18f;
            Text(new Rect(c0, ty, w - 48, 18), $"Your party is learning: everyone gained +1 familiarity with {enc.Boss.BossName}.", small, new Color(0.5f, 0.9f, 1f));
            ty += 18f;

            if (victory && enc.LootDrop != null)
            {
                Text(new Rect(c0, ty, w - 48, 18), $"LOOT: {enc.LootDrop}  (loot council arrives in a later build)", small, Gold);
                ty += 18f;
            }

            Text(new Rect(x, y + h - 58, w, 20), $"{enc.EndQuoteBy}: \"{enc.EndQuote}\"", labelCenter, Muted);
            if (Time.unscaledTime - enc.StateEnteredAt > 1.5f)
                Text(new Rect(x, y + h - 32, w, 22), $"One more pull?  [{input.Glyph("Start / A", "Enter / Space")}]", labelCenter, Gold);
        }
    }
}
