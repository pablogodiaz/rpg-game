using System;
using UnityEngine;

namespace ReadyCheck
{
    public class BossEntry
    {
        public string Id;
        public string Name;
        public string Location;
        public string Blurb;
        public ArenaTheme Theme;
        public Func<Transform, BossController> Spawn;
    }

    /// <summary>Every boss in the game, in progression order. Add new bosses here.</summary>
    public static class BossRoster
    {
        public static readonly BossEntry[] All =
        {
            new BossEntry
            {
                Id = RotgutBoss.Id, Name = RotgutBoss.Name, Location = "The Buffet Hall",
                Blurb = "Teaches STACK, SPREAD and INTERRUPT. Mind the grease fires.",
                Theme = ArenaTheme.BuffetHall, Spawn = RotgutBoss.Spawn,
            },
            new BossEntry
            {
                Id = KrakennaBoss.Id, Name = KrakennaBoss.Name, Location = "The Sunken Grotto",
                Blurb = "Eight arms, all breakable. FOCUS arms to free hugged friends and shrink the Ink Tsunami.",
                Theme = ArenaTheme.SunkenGrotto, Spawn = KrakennaBoss.Spawn,
            },
        };
    }
}
