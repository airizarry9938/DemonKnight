# Last Knights of Somerset

Unity 6.6 (2D URP) roguelike auto-battler. GitHub repo name: **DemonKnight**. C# namespace: `ArthurianAutobattler`.

King Arthur's warband is cornered in a gothic Somerset church. Scout, bribe, train, and fuse 52 pew-bound townsfolk before the demon horde breaches the churchyard. Merlin rewinds time between runs — **Grudge Memory** raises hire prices.

## Quick Start

1. Clone https://github.com/airizarry9938/DemonKnight and open **that folder** in Unity Hub (Unity 6 / 6000.x).
2. Import TMP Essentials if prompted. Wait for compile.
3. Top menu → **Arthurian → Sync All Scripts From GitHub**, then **Arthurian → Diagnose Project**. It must say package `hud-preserve-authored`.
4. Already have `ChurchBackground` on `TownCanvas`? Use **Arthurian → Align Crowd Grid To Church Pews** (keeps your scene).  
   Brand-new scene only: **Arthurian → Build Complete Scene Hierarchy** (this *replaces* the scene).
5. Place HUD icons in the Editor on `HUD_Bar`. **Play keeps those RectTransforms** (Codex label shows **Legends**). Only enable `forceEqualHudSlots` on `SomersetChurchUI` if you want the old equal-slot auto layout.
6. Optional: **Somerset → Asset & Dialogue Studio** for paper-doll sprites / dialogue.
7. Press Play on `CamelotMain`. After Merlin’s bank screen, **Enter Next Timeline** starts the next night in-place.

### After merging HUD layout fix

1. `git pull` (or Sync) — **do not** rebuild the scene unless you want a fresh hierarchy.
2. Keep your edited `HUD_Bar` positions; Play should match the Editor.
3. Diagnose → `hud-preserve-authored`. Console: `HUD layout preserved (editor RectTransforms)`.

### Hotkeys (church stage)
| Key | Action |
| --- | --- |
| `T` | Tavern Dice modal |
| `C` | Legend Codex |
| `R` | Warband Formation |
| `B` | Buy Tavern Round (50g) |
| `Esc` | Close modals |

## Systems Included

- 52 persistent pew seats + 3-layer paper-doll tells  
- Bank upgrades + uncapped Runes (Gossip / Gambler / Blood-Oath / Peasant's Crown)  
- Mid-run Relics, Demonic Pacts, Sin Runes (Jealousy / Lust)  
- 10 Arthurian fusions + 5 corrupted bosses (Nimue finale)  
- Staged grudge JSON committed only on full run end  

## Script Map

```
Assets/Scripts/
  Data/         GameDataTypes.cs, SomersetVisualDatabase.cs
  Persistence/  GrudgeSaveManager.cs, MerlinResurrectionManager.cs
  Systems/      GameLoop, DemonKnightCompatApi, TownRNG, Fusion, Dice, Perks,
                RelicManager, DemonicPactManager, SinRuneManager
  Combat/       Horde, AutoBattler, Visualizer, UnitVisualActor, FloatingText
  UI/           SomersetChurchUI, ChurchStageUIController, PaperDollSeatView,
                TownSplitUIController, LegendCodexUI, MainMenuUI
Assets/Editor/  ArthurianSceneBuilder, ArthurianMenuBootstrap, SomersetAssetStudioWindow
```
