DemonKnight — fight-scene VIDEO background (drop path)

WHERE TO PUT THE CLIP (pick one; first match wins at runtime)

  1) Inspector (best)
       Managers → BattlefieldVisualizer → Battle Background Video
       Assign your VideoClip asset.

  2) Project import + Resources auto-load
       Assets/Art/Backgrounds/BattlefieldLoop.mp4
       AND copy/duplicate to:
       Assets/Resources/Art/Backgrounds/BattlefieldLoop.mp4
       (Resources path used by code: Art/Backgrounds/BattlefieldLoop — no extension)

  3) StreamingAssets (builds / URL playback)
       Assets/StreamingAssets/BattlefieldLoop.mp4

STILL IMAGE FALLBACK (if no video yet)

  Assets/Art/Backgrounds/Battlefield.jpg  (or .png) → Sprite on BattleBackground
  Same paths documented in BATTLEFIELD-ART.txt / BattlefieldVisualizer XML.

HOW IT PLAYS

  March → PrepareBattlefield → ApplyVideoBackground
  VideoPlayer on BattlefieldVisualizer renders into a RenderTexture
  displayed on BattlefieldArena/BattleVideoBackground (world Quad).
  Loops, no audio. Still sprite dims behind the video.

NO CLIP YET?

  Game still runs. Console logs this drop path. Brown/tint still background shows.
  Boss waves also show a bottom dialog bar (BattleBossDialogUI) on enter.

FORMAT TIPS

  .mp4 (H.264) recommended. Keep resolution ~1280×720 for the default RT.
  Loop-friendly clip (seamless end→start) looks best.
