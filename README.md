# Last Knights of Somerset

Unity 6.6 (2D URP) roguelike auto-battler. GitHub repo name: **DemonKnight**. C# namespace: `ArthurianAutobattler`.

King Arthur's warband is cornered in a gothic Somerset church. Scout, bribe, train, and fuse 52 pew-bound townsfolk before the demon horde breaches the churchyard. Merlin rewinds time between runs — **Grudge Memory** raises hire prices.

## Quick Start

1. Clone https://github.com/airizarry9938/DemonKnight and open **that folder** in Unity Hub (Unity 6 / 6000.x).
2. Import TMP Essentials if prompted. Wait for compile.
3. If Diagnose shows `cs1061-compat-api` / CompatApi lacks `ResolveLoop`: use the **hard fallback** below (jsDelivr `@main` is stale).
4. **Arthurian → Bootstrap CompatApi + Sync Tools (pinned)** → **Force Delete+Sync ALL Scripts**. Diagnose → `sync-pin-jsdelivr`.
5. Already have `ChurchBackground`? **Align Crowd Grid To Church Pews**. Brand-new only: **Build Complete Scene Hierarchy**.
6. Place HUD icons on `HUD_Bar` (Play keeps them; Codex = **Legends**).
7. Press Play on `CamelotMain`.

### Hard fallback (jsDelivr `@main` lags — pin commit `ba26256`)

**Delete** then download Save-As (do not rely on overwrite):

- https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@ba26256/Assets/Scripts/Systems/DemonKnightCompatApi.cs → `Assets/Scripts/Systems/DemonKnightCompatApi.cs`
- https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@ba26256/Assets/Editor/DemonKnightPackageVersion.cs → `Assets/Editor/DemonKnightPackageVersion.cs`
- https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@ba26256/Assets/Scripts/UI/SomersetChurchUI.cs → `Assets/Scripts/UI/SomersetChurchUI.cs`
- https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@ba26256/Assets/Editor/ArthurianSyncFromGitHub.cs → `Assets/Editor/ArthurianSyncFromGitHub.cs`

Then Unity compile → Bootstrap → Force Delete+Sync. **Do not** rebuild scene / replace whole project folder.

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
