Architect named Stage Layout profiles (tip: fix-stage-placer-profiles)

Folder:
  Stage/ — Tab 2 Stage Placer & Parallax snapshots (StageLayoutProfile)

Live Play profile stays at Resources root:
  StageLayoutProfile.asset

Save Profile → writes a named .asset here + SoftPromote Persist on the live profile.
Load Profile → copies seats, layout prefs (hitbox/sprite/gap), parallax layers & FX into the live profile + SoftPromote Persist.
Play always Resources.Load the live root profile (never ArchitectPools/Stage snapshots).
