# DemonKnight

Unity 6.6 (2D URP) roguelike auto-battler set in Camelot: recruit townsfolk under a ticking clock, fuse Arthurian legends, then auto-fight escalating demon hordes. Merlin rewinds time between runs — survivors remember dying, and their hire prices rise (**Grudge Memory**).

C# code lives in the `ArthurianAutobattler` namespace so scripts compile cleanly as one package inside the **DemonKnight** Unity project.

## Requirements

- Unity **6000.x / 6.6** (2D URP template recommended)
- TextMeshPro (included with Unity UI)

## Quick Start

1. Clone https://github.com/airizarry9938/DemonKnight and open **that folder** in Unity Hub (not an older empty local project).
2. Use Unity **6.0 / 6.6**. Let packages import; wait until the bottom-right spinner finishes.
3. If prompted, import **TextMeshPro Essential Resources** (`Window → TextMeshPro → Import TMP Essential Resources`).
4. Top menu bar → **Arthurian → Diagnose Project** (always available). Fix any Console errors it reports.
5. Top menu bar → **Arthurian → Build Complete Scene Hierarchy**.
6. Assign portrait sprites on `TownRNGGenerator` and hero sprites on `CardFusionSystem`, then play `CamelotMain`.

If you do **not** see an **Arthurian** menu at all, Unity is almost certainly open on the wrong folder (no `Assets/Editor` scripts) or scripts have not finished importing.

## How to Play

| Phase | What you do |
| --- | --- |
| **Town (timer)** | Scout intel (10 / 50 / 100g), train Tank or Ranged (15g), mediate rivalries (40g), buy a tavern round (50g), gamble at dice, hire fighters, fuse legends in the Codex. |
| **Auto-Battle** | Frontline of 6. Tanks soak demon aggro. Wave 2–5 spawn corrupted legend bosses; Wave 6+ is **Nimue**. |
| **Perk Draft** | After each win (before Nimue), pick 1 of 3 run perks. Town timer shrinks by 30s each draft (floor 180s). |
| **Resurrection** | Spend Arcane Legacy Points in the Royal Supply Bank, then enter the next timeline. |

Starting purse is **75g** + bank bonuses. If every citizen costs more than you can afford, **Camelot Abandoned** ends the run.

## Script Map (`ArthurianAutobattler` namespace)

```
Assets/Scripts/
  Data/           GameDataTypes.cs
  Persistence/    GrudgeSaveManager.cs, MerlinResurrectionManager.cs
  Systems/        GameLoopManager.cs, TownRNGGenerator.cs, CardFusionSystem.cs,
                  TavernDiceMinigame.cs, PerkDraftManager.cs
  Combat/         DemonHordeGenerator.cs, AutoBattlerEngine.cs,
                  BattlefieldVisualizer.cs, UnitVisualActor.cs, FloatingCombatText.cs
  UI/             TownSplitUIController.cs, LegendCodexUI.cs
Assets/Editor/    ArthurianSceneBuilder.cs
```

## Persistence

- **Grudges:** staged in memory during a run; written to `Application.persistentDataPath/grudge_memory.json` only when a full run ends.
- **Royal Bank:** `PlayerPrefs` (`LegacyPoints`, `BankGoldLvl`, `BankDiscountLvl`, `BankRationsLvl`).

## Design Notes

- Town pool is always **52** persistent IDs (12 Knights / 15 Warriors / 25 Peasants); stats & traits re-roll each run.
- Attack speed is clamped to **1.0–2.0** APS for humans in battle.
- Demons are `isUnorganized` (random targeting); human backline focuses lowest HP.
- King Arthur present → trait rivalry (hatedTrait) nerfs are nullified.
