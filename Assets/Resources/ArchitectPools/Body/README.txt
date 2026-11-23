Architect named pool profiles (tip: fix-architect-save-load-pool-ux)

Folders:
  Body/   — Tab 4 Class Sprites / Unit Pools (BODY) snapshots
  Face/   — Tab 5 Legend / Pew Portraits (FACE) snapshots
  Battle/ — Tab 6 Battle Good / Evil snapshots

Live Play profiles stay at Resources root:
  ClassSpritePoolProfile.asset
  LegendPewPortraitProfile.asset
  BattleGoodEvilSpritePoolProfile.asset

Save Pool Profile → writes a named .asset here + SoftPromote Persist on the live profile.
Load Pool Profile → copies the named snapshot into the live profile + SoftPromote Persist.
Appliers always Resources.Load the live root profile (never ArchitectPools snapshots).
