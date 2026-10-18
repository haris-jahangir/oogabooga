# AfterLife

A 2D horror side-scroller. You wake up in a burning warehouse on Pier 3. When the lights flicker, the shadow comes: stand in the light to survive. Find the door code, outrun the fire, climb to the roof, and face the shadow at the end.

## How to play

**Goal**
1. Level 1: use the light switch, find the four clues, enter the code on the keypad by the door.
2. Level 2: climb to the roof before the rising fire reaches you.
3. Outside, the shadow is waiting. Choose: take its hand, or walk toward the light.

**Survive**
- When the lights flicker, the shadow is coming: get into a pool of lamp light and stay there until it leaves.
- Don't touch the fire. It spreads, and it burns away the safe light pools as it goes.
- If you die, choose **TRY AGAIN** to restart the level.

**Controls**

| Action | Keys |
| --- | --- |
| Move | A / D or arrow keys |
| Jump | Space |
| Dash | Left Shift (also in mid-air) |
| Use (switch, clue, keypad) | E |
| Keypad | Number keys, Backspace, Enter; Esc to step away |
| Menus and dialogue | Enter / Space / E to confirm, W / S or arrows to choose, mouse works too |

## Running the project

1. Install **Unity 6000.3.26f1** (Unity 6) through Unity Hub.
2. Clone the repo and open the folder in Unity Hub (**Add project from disk**).
3. Open `Assets/Scenes/MainMenu` and press **Play**.

Scenes in build order: `MainMenu`, `Scene1` (Level 1), `Scene2` (Level 2), `Story/Scenes/Ending`.

**Building:** File > Build Profiles > Windows (or Linux) > Build. The scene list is already set.

**Editor tools** (Tools > Oogabooga): *Build Scene 2 Climb*, *Place Spot Lights*, and *Story > Wire Story Into Game*, which re-applies the story, character art and scene links to the levels after a merge conflict.

## Credits

**Team**
- Haris Jahangir (KibroPixen): programming, levels, VFX, main menu
- Shayan (S16): narrative and level design, story and cutscenes, character animation set-up, game icon
- Fatimah Adeel: character art and animation frames, clue art

Built with Unity 6, Claude Code and the Unity MCP.

**Art**
- Player character (idle, walk, jump, scare), clue pictures: drawn by the team's artist
- Warehouse sprites (scaffolding, pipes, crates, doors), cursor: made by the team
- Shadow, ending backdrops (docks, shadow's view, hospital), light switch, clue props, game icon: made by the team, from the artist's drawings (generator scripts in `ArtSource/Story`)

**Fonts** (licence files in `Assets/Story/Resources/Fonts`)
- [Creepster](https://fonts.google.com/specimen/Creepster) by Font Diner, Inc. SIL Open Font License 1.1
- [Stardos Stencil](https://fonts.google.com/specimen/Stardos+Stencil) by Vernon Adams. SIL Open Font License 1.1
- [Special Elite](https://fonts.google.com/specimen/Special+Elite) by Brian J. Bonislawsky (Astigmatic). Apache License 2.0
eeded]**
