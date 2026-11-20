# Character Definitions (plug-and-play)

Drop new **Character Definition** assets here (or create via `Create → DemonKnight → Character Definition`).

## Folders
- `Legends/` — fusion products (Priest, Arthur, Mordred, …)
- `Bosses/` — wave bosses (`bossWaveNumber` 3–10)
- `Demons/` — horde spawn units (`powerCost`)

## After adding art / new characters
1. **DemonKnight → Battle Sprite Sheet Slicer** — key `#1C2532`, pivot **Bottom-Center (0.5, 0.0)**
2. Assign `portraitSprite` + `battleSprite` (+ `visualScale` for bosses)
3. **DemonKnight → Characters → Seed Default Character Definitions** (first time)  
   or **Auto-Collect All Character Definitions** on the `CharacterDatabase` asset
4. Diagnose must show package id `character-registry-battle-sprites`

`Resources/Data/CharacterDatabase` is the runtime registry CardFusion / Horde / Wave HUD / Codex read.
