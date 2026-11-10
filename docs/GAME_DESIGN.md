# Game Design Doc (draft v0.1)

> Working title: **Ready Check**. Other options: *Pull Timer*, *Wipe & Learn*, *The Raid Leader*.
> This is a living document. Items marked **[OPEN]** are decisions still to make.

---

## 1. Pitch

You lead a raid party through dungeons and boss fights. You fight in the battle yourself, but your
real weapon is your voice. You call out mechanics, assign targets, place markers and decide when to
burn cooldowns. Your party starts as three scrappy recruits and grows into a full raid of
specialists. Each one has a role, a personality, and their own memory of every boss they've wiped on.

**The fantasy:** being the person on voice chat who says *"Spread for bombs... tanks swap NOW... burn
it, burn it!"* and watching the boss go down because of the calls you made.

**Pillars**
1. **Command is the core verb.** Every boss is a puzzle solved through calls, not just button mashing.
2. **Roles matter.** Tank, healer and DPS play differently and need different orders.
3. **Wiping is progress.** Failed attempts teach your party. Each try goes better.
4. **Controller-first.** Every command has to be fast to give on a gamepad. Mouse and keyboard is a bonus.

---

## 2. Core gameplay

### 2.1 Perspective & feel
- **Top-down / 3/4 isometric camera.** Ground telegraphs (circles, cones, lines) need to be easy to
  read, it works well with a controller, and it's cheap enough to run on Switch.
- **Real-time with slow-mo while commanding.** Opening a command wheel slows time to about 20% instead of pausing it.
  An optional "Tactical Pause" setting gives full pause, for accessibility or an easy mode. **[OPEN]**

### 2.2 The Leader (you)
You control one character directly. It has two kits:

| Kit | What it is |
|---|---|
| **Role kit** | You pick a role (Tank / Healer / DPS / Support) and play it like any member. |
| **Leader kit** | Shared across all roles: Rally (raid-wide damage buff, long cooldown), Battle Rez (limited charges), Ready Check (pre-pull buff), Markers. |

Your role changes how you play. As a **healer** you see everyone's health bars and command from the back.
As a **tank** you're in the boss's face and have to call things while you're under pressure. That gives
the game a lot of replay value.

### 2.3 Calls (the command system)
Calls are the heart of the game. There are three layers, from fastest to most detailed:

**1. Quick Calls (D-pad, one press)**: four customizable slots, e.g.
- Up: **Stack** (gather on leader/marker)
- Down: **Spread**
- Left: **Interrupt!** (nearest interrupter kicks current cast)
- Right: **Burn / Cooldowns** (all DPS pop offensive cooldowns)

**2. Raid Wheel (hold LB)**: global calls on a radial menu:
Stack · Spread · Fall Back · Move to Marker · Focus Target · Hold DPS · Defensives · Dispel

**3. Role Wheel (hold RB → pick group → pick order)**: targeted orders:
- *Tanks:* Taunt / Swap / Pick up adds / Face boss away
- *Healers:* Focus tank / Raid heal / Save mana / Use big cooldown
- *DPS:* Switch to adds / Stay on boss / Soak / Kite
- *Individual:* Later on, you can pick a specific member from the wheel for a precise call ("Mira, take the left soak").

**Markers**: point at enemies or the ground and drop raid symbols (Skull, X, Square, Star...).
Markers are the bridge between calls and positioning: "Tanks to Square", "Kill order: Skull then X".

> Design rule: any call must take **≤ 2 inputs and ≤ 1 second** on a controller. If it doesn't, it's too complex
> or it belongs in pre-fight planning.

### 2.4 Party AI: how members respond
Each member runs on a priority stack:

```
Leader Call  >  Pre-fight Tactics  >  Role default behavior
```

- **Role default**: tanks hold aggro, healers heal the lowest HP, DPS hits the current target.
- **Pre-fight Tactics** (set in the planning screen, simple rules in the style of FFXII gambits):
  e.g. "Healer: keep the main tank above 70% first", "DPS: always switch to Totems".
- **Leader Call**: overrides both for a few seconds, or until done.

**Awareness stat (key mechanic):** Each member has an *Awareness* value. With low awareness they react late or not
at all unless you make the call. With high awareness they handle known mechanics on their own.
This is how the game's difficulty grows along with your raid:
- Early: you call *everything*.
- Late: veterans handle the basics and you focus on the hard, big-picture calls.

**Boss Familiarity:** Each member has a hidden familiarity score per boss. It goes up with every attempt,
win or wipe. After five wipes on a boss your team visibly dodges its slam better. This makes
progression raiding work in a single-player game: failure isn't wasted time.

### 2.5 Combat roles

| Role | Job | Signature mechanic | Example subclasses |
|---|---|---|---|
| **Tank** | Hold aggro, take hits, position the boss | Threat meter, active mitigation, tank swaps | Guardian (shield), Juggernaut (self-heal), Evasion tank |
| **Healer** | Keep the raid alive, manage mana | Mana economy, triage, big cooldowns | Cleric (direct), Druid (heal over time), Bard (shields) |
| **Melee DPS** | High damage, must manage positioning | Behind-the-boss bonus, cleave risk | Duelist, Berserker |
| **Ranged DPS** | Safe damage, handles ranged mechanics | Cast times (have to stop casting to move) | Mage, Hunter |
| **Support** | Buffs, debuffs, interrupts, crowd control | Limited-charge utilities | Chronomancer, Warchanter |

Shared role duties are spread across subclasses on purpose. Not every class can interrupt, dispel or battle rez,
so **raid composition is a real choice**.

---

## 3. Progression

### 3.1 Party size curve
Party size is also a **tutorial pacing tool**. Each step up adds a new command layer.

| Stage | Size | What it teaches |
|---|---|---|
| Act 1 | 3 (you + tank + healer) | Basic calls, individual orders |
| Act 2 | 5 (dungeon group) | Full holy trinity, interrupts, markers |
| Act 3 | 10 (raid) | Role groups, split mechanics, tank swaps |
| Act 4+ | 10 active + bench of ~20 | Comp building per boss, two squads (A/B team calls) |

Recommend capping **active party at 10** for readability and Switch performance. Grow the *roster*
(bench) instead of the active party. **[OPEN]**

### 3.2 Loops
- **Moment (seconds):** read the telegraph → make the call → party reacts.
- **Encounter (minutes):** trash pulls → boss → loot.
- **Session (an hour):** clear or attempt a dungeon, go back to the Guild Hall.
- **Meta (the whole game):** grow the roster, gear up, unlock harder dungeons and Heroic/Mythic versions.

### 3.3 The Guild Hall (hub)
- **Recruitment:** applications board, tavern, members rescued during dungeons, trial runs. Recruits arrive with
  traits and starting Awareness.
- **Roster & Bench:** choose who raids. Benched members still gain a little XP. Some don't like being benched...
- **Planning Table:** set the comp, Tactics presets and opening marker positions per boss. Watch replays of
  past wipes (a "death log" showing what killed whom).
- **Loot distribution:** *you* decide who gets each drop. Options: Need/Greed roll, DKP points, or
  Loot Council (you choose). Choices affect **Morale**. Give the healer's trinket to the mage and expect complaints.
- **Training grounds:** spend gold/time to raise Awareness or teach a member a specific mechanic.

### 3.4 Member personalities (traits)
Light and funny, with a mechanical effect:
- *Fire Enthusiast*: slow to leave ground effects. *Clutch*: stronger below 20% boss HP.
- *Tilt-Prone*: loses morale after each wipe. *Calm Voice*: raises nearby members' Awareness.
- *Loot Goblin*: big morale swing from loot decisions. *Veteran*: starts with high Awareness, asks for more pay.

### 3.5 Rival guilds (optional meta, recommended)
Other guilds race you through the same content. A news board posts "Iron Wolves defeat the Lich King's Uncle
(World First!)". It gives the campaign a sense of time and competition. It can also be used for story:
recruits poached by rivals, rival guild leaders as recurring characters.

---

## 4. Boss design

Each boss is built around **one primary call** it teaches, then remixes old ones.

| # | Boss (placeholder) | Teaches | Key mechanic |
|---|---|---|---|
| 1 | Rotgut the Hungry | Move out of bad | Fire puddles, simple tank & spank |
| 2 | Sister Vell | **Interrupt** | Casts a raid-wipe spell every 20s, must be kicked |
| 3 | The Twin Hounds | **Markers / Kill order** | Two bosses, kill within 10s of each other |
| 4 | Gravemaw | **Tank Swap** | Stacking debuff on the active tank |
| 5 | The Conductor | **Spread / Stack** | Alternates between chain lightning (spread) and a meteor (stack) |
| 6 | Hive Queen | **Switch to adds** | Add waves heal the boss if they reach it |
| 7 | The Clockwork Council | Everything | 3 bosses, rotating abilities, burn phase with an enrage timer |

**Boss rules**
- Every dangerous attack has a **clear telegraph** (visual and audio) with enough lead time to make a call.
- Phases change at HP thresholds. Wipes show "Best attempt: 34%" to show progress.
- Difficulty modes add mechanics instead of just more HP (Normal → Heroic → Mythic).

---

## 5. Controls

### Gamepad (primary)
| Input | Action |
|---|---|
| Left stick | Move leader |
| Right stick | Aim / soft-target |
| A / B / X / Y | Leader role abilities |
| RT | Basic attack / primary |
| LT (hold) | Target lock / cycle targets |
| **LB (hold)** | **Raid Wheel** (slow-mo) |
| **RB (hold)** | **Role Wheel** (slow-mo) |
| D-pad | Quick Calls (4 slots) |
| R3 | Drop marker at aim point |
| Start | Pause / Tactics |
| Select | Raid frames / damage meter toggle |

### Mouse & keyboard
WASD move, mouse aim, 1-5 abilities, Q/E hold for wheels, F1-F4 quick calls, Ctrl+click to place markers.

Every action must be rebindable. Use one input-action layer from day 1 so both schemes stay equal.

---

## 6. UI / UX
- **Raid frames** (health bars for the party) in a corner, by role, colored by health, with debuff icons.
- **Boss cast bar** big and central, with an interruptible indicator (a shield icon when it can be interrupted).
- **Call feedback:** when you call, members visibly answer (speech bubble/bark: "On it!", "Kicking!",
  "Can't, on cooldown!"). This is important for game feel and clarity.
- **Post-fight report:** damage/healing meters, deaths and their causes, which calls were made and when.

---

## 7. Technical direction

### Engine
| Option | Pros | Cons |
|---|---|---|
| **Godot 4** (recommended for a solo/small team) | Free, light, good 2D/3D-lite, good gamepad support | Console export goes through third-party porters (e.g. W4 Games), so plan for that cost |
| **Unity** | Official Switch support after Nintendo dev registration, huge ecosystem | Licensing history, heavier |
| **Unreal** | Top-tier 3D | Overkill here, harder on Switch |

If Switch is a real goal: keep scripting console-friendly (GDScript in Godot is the safest bet), avoid
heavy shaders, and cap entity counts. Switch 2 gives more headroom, but target the original Switch's
budget if possible.

### Architecture notes
- **Data-driven content:** classes, abilities, bosses and traits defined in data files (resources/JSON) so
  content can be added without code changes.
- **AI:** Utility AI per member (score possible actions), with a **Command layer** on top that injects
  high-priority goals. Awareness = reaction delay + chance to self-respond to known mechanics.
- **Boss scripting:** timeline + phase state machine. Each mechanic emits a `MechanicIncoming` event that
  the AI (and the familiarity system) can listen to.
- **Input:** one abstraction layer, with no direct key checks in gameplay code.

---

## 8. Scope & milestones

**MVP / Vertical Slice** (prove the core is fun):
- 1 dungeon, 3 trash packs, **3 bosses** (Rotgut, Sister Vell, Gravemaw)
- Party of 5: Leader + Tank + Healer + 2 DPS (fixed members)
- Quick Calls + Raid Wheel (6 calls) + Skull/X markers
- Awareness + Boss Familiarity
- Gamepad and keyboard support
- Simple post-fight report

**Question the slice must answer:** *Is making calls fun, and does a wipe → learn → kill loop feel good?*

Then: Guild Hall and loot → recruitment and traits → 10-person raid and Role Wheel → campaign content.

---

## 9. Open questions
1. **Pause:** slow-mo only, or full tactical pause available?
2. **Leader:** always a fighter, or allow a pure "commander" camera mode?
3. **Tone:** earnest dark fantasy, or affectionate MMO parody (loot drama, "stands in fire" jokes)?
4. **Visual style:** 2D pixel art, 2.5D, or low-poly 3D?
5. **Max party size:** 10? 20?
6. **Co-op:** could a second player be a party member (couch co-op on Switch)?
7. **Permadeath/injuries** for members, or soft consequences only?
