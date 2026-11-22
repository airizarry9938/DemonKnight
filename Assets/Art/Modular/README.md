# Modular Townsfolk Sprites

1. Open **Arthurian → Art Tools → Modular Sprite Sheet Importer**
   (If pews show empty boxes: **Arthurian → Art Tools → Verify & Sync Modular Library**, then New Game.)
2. Assign sheets: Poses 1×4, Outfits 2×6, Heads & Props 2×6
3. Import keys out `#1C2532`, writes `Sliced/*_Clean.png`, Point filter, assigns `ModularSpriteLibrary.asset`
4. Runtime loads `Resources/Art/Modular/ModularSpriteLibrary` (importer copies there)

Pew seats + dossier portraits share `TownsfolkAppearance` indices.
