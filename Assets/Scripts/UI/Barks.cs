using System.Collections.Generic;
using UnityEngine;

namespace ReadyCheck
{
    /// <summary>Speech bubbles above units. Most of the game's personality lives here.</summary>
    public class Barks : MonoBehaviour
    {
        public class Bubble
        {
            public Unit Unit;
            public string Text;
            public float ExpiresAt; // unscaled time
        }

        static readonly List<Bubble> active = new List<Bubble>();
        static readonly Dictionary<Unit, float> nextAllowed = new Dictionary<Unit, float>();

        public static IReadOnlyList<Bubble> Active => active;

        /// <param name="force">Ignore the per-unit cooldown (use for important lines).</param>
        public static void Say(Unit unit, string text, float duration = 2.4f, bool force = false)
        {
            if (unit == null || string.IsNullOrEmpty(text)) return;
            if (unit.IsDead && !force) return;
            float now = Time.unscaledTime;
            if (!force && nextAllowed.TryGetValue(unit, out var t) && now < t) return;
            active.RemoveAll(b => b.Unit == unit);
            active.Add(new Bubble { Unit = unit, Text = text, ExpiresAt = now + duration });
            nextAllowed[unit] = now + 1.5f;
        }

        public static void Clear()
        {
            active.Clear();
            nextAllowed.Clear();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            active.RemoveAll(b => b.Unit == null || now >= b.ExpiresAt);
        }
    }

    /// <summary>All the barks in one place, so writing can be iterated (and localized) easily.</summary>
    public static class BarkLines
    {
        public static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];

        public static readonly string[] Ready = { "Ready!", "Ready.", "Ready! (I think)", "Bio break— ok ready.", "Ready, boss." };
        public static readonly string[] ReadyBackseat = { "Wait, what's the strat?", "Ready. Are WE ready though?", "Ready. I'll call the mechanics. Kidding. Unless...?" };

        public static readonly string[] AckStack = { "On you!", "Stacking!", "Coming in!", "Group hug!" };
        public static readonly string[] AckSpread = { "Spreading!", "Personal space, got it.", "Going!", "Which left?" };
        public static readonly string[] AckKick = { "Kicking!", "I got the kick!", "On it!", "Mouth's closing!" };
        public static readonly string[] KickOnCooldown = { "My kick's on cooldown!", "Can't, cooldown!", "Already used it, sorry!" };
        public static readonly string[] Kicked = { "Kicked!", "Not today.", "No snacks for you!", "Interrupted!" };
        public static readonly string[] AckBurn = { "Popping everything!", "BURN!", "Big numbers time!", "Finally!" };
        public static readonly string[] AckDefensives = { "Shields up!", "Bracing!", "Defensives popped!" };
        public static readonly string[] Rally = { "For the loot!", "Rally to me!", "Hit him harder!", "For the guild!" };

        public static readonly string[] SelfRespond = { "I've got this one!", "Moving!", "Oh, I know this!" };
        public static readonly string[] SelfRespondFamiliar = { "I've seen this one!", "Not again, Rotgut.", "Like we practiced!", "Fool me once..." };

        public static readonly string[] FireEnthusiast = { "Ooh, warm.", "Is this... bad?", "Toasty!", "I'll move in a sec." };
        public static readonly string[] HealerPanic = { "Healing! HEALING!", "Stop taking damage!", "I'm doing my best!" };
        public static readonly string[] PulledAggro = { "Uh, why is he looking at me?", "TANK?!", "AGGRO! AGGRO!" };

        public static readonly string[] BackseatWrong =
        {
            "Everyone stack on the boss! ...no wait.",
            "Should we be DPSing the floor?",
            "I think we should Spread. Or Stack. One of those.",
            "Pro tip: don't die.",
            "In my old guild we'd have killed him by now.",
        };

        public static string BackseatCorrect(MechanicKind kind)
        {
            switch (kind)
            {
                case MechanicKind.BellyFlop: return "It's Belly Flop! STACK! ...see? I'm right sometimes.";
                case MechanicKind.Indigestion: return "Indigestion! SPREAD! Write that down.";
                case MechanicKind.SecondBreakfast: return "He's eating! Someone KICK! (Not me.)";
                default: return "Do the thing!";
            }
        }

        public static readonly string[] DeathReaction = { "RIP {0}.", "{0} is down!", "{0}, no!", "Battle rez {0}? Oh, we don't have that yet." };
        public static readonly string[] Wipe = { "Lag.", "That was the healer.", "That was NOT the healer.", "One more pull?", "My cat walked on the keyboard.", "We almost had him!" };
        public static readonly string[] WipeTilt = { "I can't heal stupid.", "I'm fine. This is fine.", "...I need a minute.", "Was that my fault? Don't answer." };
        public static readonly string[] Victory = { "GG!", "Screenshot! Everyone jump on him!", "Loot! Loot! Loot!", "Who's writing the guild post?" };

        public static readonly string[] BossPull = { "Is it lunch? It smells like lunch.", "Visitors! Did anyone bring snacks?", "Ah. Appetizers." };
        public static readonly string[] BossRetry = { "Back again? I've rearranged the furniture.", "You again! Did you bring a better healer?", "Round two. Or is it twelve?" };
        public static readonly string[] BossFlop = { "Incoming! Somebody catch me!", "CANNONBALL!", "Here comes the belly!" };
        public static readonly string[] BossIndigestion = { "Uh oh. Something I ate...", "That third goat was a mistake.", "Everybody... get back..." };
        public static readonly string[] BossBreakfast = { "Time for second breakfast.", "Snack break!", "One moment, I'm eating." };
        public static readonly string[] BossAte = { "Mmm. Crunchy.", "Delicious. I feel STRONGER.", "Compliments to the chef. Me." };
        public static readonly string[] BossInterrupted = { "I was EATING that!", "Rude!", "My sandwich!" };
        public static readonly string[] BossFire = { "Oops. Grease fire.", "Who left the fryer on?", "Mind the floor. It's hot." };
        public static readonly string[] BossKill = { "Crunchy.", "One down.", "Delicious." };
        public static readonly string[] BossWipe = { "Same time next week?", "Come back when you've practiced.", "Thanks for the snack." };
        public static readonly string[] BossDeath = { "Tell... the caterer... it was... fine...", "I should have... skipped... dessert..." };
    }
}
