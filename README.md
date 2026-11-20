# Last Knights of Somerset

Unity 6.6 (2D URP) roguelike auto-battler. GitHub repo name: **DemonKnight**. C# namespace: `ArthurianAutobattler`.

King Arthur's warband is cornered in a gothic Somerset church. Scout, bribe, train, and fuse 52 pew-bound townsfolk before the demon horde breaches the churchyard. Merlin rewinds time between runs — **Grudge Memory** raises hire prices.

## Quick Start

1. Clone https://github.com/airizarry9938/DemonKnight and open **that folder** in Unity Hub (Unity 6 / 6000.x).
2. Import TMP Essentials if prompted. Wait for compile.
3. If Diagnose shows `cs1061-compat-api` / CompatApi lacks `ResolveLoop`: use the **hard fallback** below (never use jsDelivr `@main` — it lags).
4. **Arthurian → Bootstrap CompatApi + Sync Tools (pinned)** → **Force Delete+Sync ALL Scripts**. Diagnose → `fix-fusion-serialize-intel-costs`.
   If Bootstrap / Force Delete+Sync menus are missing: overwrite `ArthurianSyncFromGitHub.cs` + `ArthurianMenuBootstrap.cs` from jsDelivr (pinned SHA), Refresh, then Bootstrap.
   Must include `Assets/Scripts/Combat/BattleBossDialogUI.cs` (CS0103 without it).
5. Already have `ChurchBackground`? **Align Crowd Grid To Church Pews**. Brand-new only: **Build Complete Scene Hierarchy**.
6. Place HUD icons on `HUD_Bar` (Play keeps them; Codex = **Legends**). Menu/Esc sits far-left; authored positions preserved (`forceEqualHudSlots` off).
7. Press Play on `CamelotMain`.

### Locked run rules (Andrew)

1. **Fusion dies** → leaf pew units still take the **death hire penalty** (+15).
2. **Combat XP** — no invented XP on unfuse; only perk/reward grants.
3. **Mordred** — fuse/Legend only; **never returns to a pew** (must fuse again next run).
4. **Per wave:** stacks / combat perks / win−10·loss+15 hire / taxes / **Mediate rivalries** / wave changes.
5. **Runes** (bank / awarded / purchased) **persist across waves** in the run.

**Mediate:** **100g** — cancels rivalries for **this wave** so formerly incompatible units can recruit together. Expires when the wave ends.

**Victory stars:** each wave win, units returning to pews gain a ★ (stacks). Each star → **−5g** hire (stacks with win −10 / loss +15 grudge). Gold star icon + count on paper-doll pews; persisted in run autosave.

**Initial hire bands:** Peasants **10–20g**, Warriors **50–100g**, Knights **200–400g** (higher ATK/HP → higher within band). Starting royal gold **200g**.

**Buy Tavern Round (50g):** −10g hire for **all** units until the wave ends. HUD night label shows **Night N · Wave M (Boss/Horde)**.

**New Game:** always **200g**; wipes **all** bank runes, sin runes, relics, run perks, and pacts (every scene manager + PlayerPrefs). Modifiers accumulate only during the run. Continue restores saved gold only (no bank re-add).

**Peasant hire floor:** final hire never below **10g** after Loyalist / Crown / Tavern (Warrior ≥50, Knight ≥200).

### Hard fallback (jsDelivr `@main` lags — pin commit)

After this PR merges, pin the **merge commit SHA** (or tip of `main`). Until then, use the PR branch tip SHA from the Sync steps doc, or floor:

**Warband formation diagram:** Camelot Crowd shows BACK (Ranged) / FRONT (Tank) boxes with unit `#N` that move when you pick Tank or Ranged.

**AutoBattle formation:** demons (and hit-targeting) strike **FRONT** (Tank/Unassigned) first; **BACK** (Ranged) is only hit after the front is wiped.

**Perk / rune hire math:** dossier Hire line lists every add-on/subtraction (Writ, Crown, Blood-Oath, Jealousy, ★, Tavern, Loyalist ÷2, floor) from the same breakdown as Recruit (Ng).

**Formation vs type:** melee (Warrior/Knight/melee legends) in **BACK** cannot attack — only ranged types (Peasant, Priest, Merlin, Morgan, Lady) fight from BACK. Warband shows **NO REACH**.

**Boss dialog + video bg:** boss waves open a bottom dialog bar; fight scene can loop `Assets/Art/Backgrounds/BattlefieldLoop.mp4` (VideoPlayer → RenderTexture). See `Assets/Art/Backgrounds/README-BATTLE-VIDEO.txt`.

**Balance V2:** start 250g; Train Ranged = crossbow; Mordred persists; bosses 3–10+; new legends; survivor bounty +10; no death/loss +15 hire.

**Intel:** Scout (Free, click to reveal HP/ATK) · Investigate **20g** · Full Report **30g** (was Dossier).

**Floor (Sync tools that pin commits):** `d4c45c9` (prefer tip of `main` after merge)

**Delete** then download Save-As (do not rely on overwrite):

- `https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@<SHA>/Assets/Scripts/Systems/DemonKnightCompatApi.cs` → `Assets/Scripts/Systems/DemonKnightCompatApi.cs`
- `https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@<SHA>/Assets/Editor/DemonKnightPackageVersion.cs` → `Assets/Editor/DemonKnightPackageVersion.cs`
- `https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@<SHA>/Assets/Scripts/UI/SomersetChurchUI.cs` → `Assets/Scripts/UI/SomersetChurchUI.cs`
- `https://cdn.jsdelivr.net/gh/airizarry9938/DemonKnight@<SHA>/Assets/Editor/ArthurianSyncFromGitHub.cs` → `Assets/Editor/ArthurianSyncFromGitHub.cs`

Then Unity compile → Bootstrap → Force Delete+Sync. **Do not** rebuild scene / replace whole project folder. No Align/Build required for this UX package if church + HUD already exist.

### Hotkeys (church stage)
| Key | Action |
| --- | --- |
| `T` | Tavern Dice modal (toggle) |
| `C` | Legends codex (toggle) |
| `R` | Warband Formation (toggle) |
| `B` | Buy Tavern Round (50g) |
| `Esc` | Close floating modal, else toggle main menu (same as HUD Esc button) |

### Autobattle pacing

`AutoBattlerEngine.combatPace` (default **0.38**) scales combat dt; `attackResolvePause` (default **0.12s**) adds a beat after each strike. Raise pace toward `1` for faster fights.

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
