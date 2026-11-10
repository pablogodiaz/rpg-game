using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    public enum ClassKind { Leader, Guardian, Cleric, Duelist, Mage }

    public enum Trait
    {
        /// <summary>Slow to leave ground effects. "Ooh, warm."</summary>
        FireEnthusiast,
        /// <summary>+30% damage while the boss is below 20% health.</summary>
        Clutch,
        /// <summary>Makes their own calls in chat. Usually wrong. Occasionally right, and won't let you forget it.</summary>
        BackseatLeader,
        /// <summary>Takes wipes personally. (Morale system comes in a later iteration.)</summary>
        TiltProne,
    }

    /// <summary>
    /// Data for one party member. Plain data for now; this moves to ScriptableObject assets once
    /// recruitment and the Guild Hall exist.
    /// </summary>
    [Serializable]
    public class MemberProfile
    {
        public string Name;
        public ClassKind Class;
        public Role Role;
        public float MaxHealth = 1100f;

        /// <summary>0..1. How well this member reacts to mechanics without being told.</summary>
        [Range(0f, 1f)] public float Awareness = 0.4f;
        public List<Trait> Traits = new List<Trait>();

        public bool CanInterrupt;
        public float InterruptCooldown = 15f;
        /// <summary>Distance from the boss's edge needed to interrupt.</summary>
        public float InterruptRange = 1.2f;

        public Color32 Skin = new Color32(238, 196, 160, 255);
        public Color32 Hair = new Color32(90, 56, 40, 255);
        public Color32 Primary = new Color32(80, 90, 160, 255);
        public Color32 Secondary = new Color32(50, 50, 70, 255);
        public Color32 Accent = new Color32(240, 200, 70, 255);

        public bool Has(Trait t) => Traits.Contains(t);

        public string ClassTitle
        {
            get
            {
                switch (Class)
                {
                    case ClassKind.Leader: return "Raid Leader";
                    case ClassKind.Guardian: return "Guardian";
                    case ClassKind.Cleric: return "Cleric";
                    case ClassKind.Duelist: return "Duelist";
                    case ClassKind.Mage: return "Mage";
                    default: return Class.ToString();
                }
            }
        }

        public static string RoleName(Role role)
        {
            switch (role)
            {
                case Role.Tank: return "Tank";
                case Role.Healer: return "Healer";
                case Role.Melee: return "Melee DPS";
                case Role.Ranged: return "Ranged DPS";
                default: return role.ToString();
            }
        }

        public static string TraitName(Trait t)
        {
            switch (t)
            {
                case Trait.FireEnthusiast: return "Fire Enthusiast";
                case Trait.Clutch: return "Clutch";
                case Trait.BackseatLeader: return "Backseat Leader";
                case Trait.TiltProne: return "Tilt-Prone";
                default: return t.ToString();
            }
        }
    }

    public static class Roster
    {
        /// <summary>The fixed 5-person party for the first vertical slice.</summary>
        public static List<MemberProfile> StarterParty()
        {
            return new List<MemberProfile>
            {
                new MemberProfile
                {
                    Name = "You", Class = ClassKind.Leader, Role = Role.Ranged, MaxHealth = 1150f, Awareness = 1f,
                    Hair = new Color32(60, 40, 30, 255), Primary = new Color32(64, 84, 170, 255),
                    Secondary = new Color32(46, 46, 78, 255), Accent = new Color32(244, 204, 64, 255),
                },
                new MemberProfile
                {
                    Name = "Grenn", Class = ClassKind.Guardian, Role = Role.Tank, MaxHealth = 2200f, Awareness = 0.35f,
                    Traits = new List<Trait> { Trait.FireEnthusiast },
                    Skin = new Color32(214, 170, 130, 255), Hair = new Color32(160, 70, 40, 255),
                    Primary = new Color32(150, 60, 50, 255), Secondary = new Color32(80, 60, 50, 255),
                    Accent = new Color32(240, 210, 90, 255),
                },
                new MemberProfile
                {
                    Name = "Pip", Class = ClassKind.Cleric, Role = Role.Healer, MaxHealth = 1100f, Awareness = 0.45f,
                    Traits = new List<Trait> { Trait.TiltProne },
                    Hair = new Color32(240, 220, 150, 255), Primary = new Color32(230, 230, 240, 255),
                    Secondary = new Color32(170, 170, 200, 255), Accent = new Color32(110, 220, 255, 255),
                },
                new MemberProfile
                {
                    Name = "Vex", Class = ClassKind.Duelist, Role = Role.Melee, MaxHealth = 1100f, Awareness = 0.5f,
                    Traits = new List<Trait> { Trait.Clutch },
                    CanInterrupt = true, InterruptCooldown = 15f, InterruptRange = 1.3f,
                    Skin = new Color32(176, 124, 92, 255), Hair = new Color32(30, 30, 40, 255),
                    Primary = new Color32(60, 70, 60, 255), Secondary = new Color32(40, 44, 40, 255),
                    Accent = new Color32(200, 60, 60, 255),
                },
                new MemberProfile
                {
                    Name = "Moira", Class = ClassKind.Mage, Role = Role.Ranged, MaxHealth = 1050f, Awareness = 0.3f,
                    Traits = new List<Trait> { Trait.BackseatLeader },
                    CanInterrupt = true, InterruptCooldown = 24f, InterruptRange = 10f,
                    Hair = new Color32(200, 80, 160, 255), Primary = new Color32(120, 60, 170, 255),
                    Secondary = new Color32(70, 40, 100, 255), Accent = new Color32(90, 70, 200, 255),
                },
            };
        }
    }
}
