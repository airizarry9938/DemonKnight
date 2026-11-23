Architect named profiles (tip: fix-stage-placer-profiles)

Folders:
  Stage/  — Tab 2 Stage Placer & Parallax (StageLayoutProfile) snapshots
  Body/   — Tab 4 Class Sprites / Unit Pools (BODY) snapshots
  Face/   — Tab 5 Legend / Pew Portraits (FACE) snapshots
  Battle/ — Tab 6 Battle Good / Evil snapshots

Live Play profiles stay at Resources root:
  StageLayoutProfile.asset
  ClassSpritePoolProfile.asset
  LegendPewPortraitProfile.asset
  BattleGoodEvilSpritePoolProfile.asset

Save Profile / Save Pool Profile → writes a named .asset here + SoftPromote Persist on the live profile.
Load Profile / Load Pool Profile → copies the named snapshot into the live profile + SoftPromote Persist.
Appliers / Stage Load always Resources.Load the live root profile (never ArchitectPools snapshots).
