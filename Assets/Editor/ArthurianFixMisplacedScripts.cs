#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Assets/Editor clean: only Arthurian/Somerset editor tools are allowed.
/// Any gameplay script dropped into Editor (TownSplitUIController, GameLoopManager, …)
/// is moved to Scripts/ or deleted if Scripts/ already has it — stops CS0029 duplicate-type errors.
/// </summary>
[InitializeOnLoad]
public static class ArthurianFixMisplacedScripts
{
    private static readonly HashSet<string> AllowedEditorScripts = new HashSet<string>
    {
        "ArthurianAlignCrowdPews.cs",
        "ArthurianFixMisplacedScripts.cs",
        "ArthurianFixMissingScripts.cs",
        "ArthurianMenuBootstrap.cs",
        "ArthurianRepairChurchStage.cs",
        "ArthurianSceneBuilder.cs",
        "ArthurianSyncFromGitHub.cs",
        "DemonKnightPackageVersion.cs",
        "SomersetAssetStudioWindow.cs",
        // Art Tools (Arthurian* prefix also covered by IsAllowedEditorScript)
        "ArthurianModularSpriteSheetImporterWindow.cs",
        "ArthurianBattleSpriteSheetSlicerWindow.cs",
        "ArthurianCharacterCatalogSeeder.cs",
        "ArthurianCharacterDatabaseEditor.cs",
        // Legacy unprefixed Art Tools are NOT allowed — delete duplicates that cause
        // "menu item already exists" and keep wiping/conflicting with Arthurian* copies.
    };

    static bool IsAllowedEditorScript(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        if (AllowedEditorScripts.Contains(fileName)) return true;
        // Match the documented rule: Arthurian* / Somerset* / DemonKnight* stay in Editor.
        // Prevents Sync/old cleaner wipes of Art Tools again.
        if (fileName.StartsWith("Arthurian", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.StartsWith("Somerset", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.StartsWith("DemonKnight", System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Filename → path under Assets/. Keys must be unique (Dictionary ctor throws otherwise).
    /// </summary>
    private static readonly Dictionary<string, string> KnownGameplayTargets = BuildGameplayTargets();

    private static Dictionary<string, string> BuildGameplayTargets()
    {
        // Use indexer adds so a mistaken duplicate key never crashes the Editor domain reload.
        var map = new Dictionary<string, string>();
        void Add(string fileName, string assetsRelative)
        {
            if (map.ContainsKey(fileName))
            {
                Debug.LogWarning(
                    $"[DemonKnight] Duplicate KnownGameplayTargets key ignored: {fileName}");
                return;
            }
            map[fileName] = assetsRelative;
        }

        Add("ChurchStageUIController.cs", "Scripts/UI/ChurchStageUIController.cs");
        Add("SomersetPewLayout.cs", "Scripts/UI/SomersetPewLayout.cs");
        Add("SomersetChurchUI.cs", "Scripts/UI/SomersetChurchUI.cs");
        Add("ChurchPewLayout.cs", "Scripts/UI/ChurchPewLayout.cs");
        Add("PaperDollSeatView.cs", "Scripts/UI/PaperDollSeatView.cs");
        Add("PewSeatHoverHighlight.cs", "Scripts/UI/PewSeatHoverHighlight.cs");
        Add("MenuListSortBar.cs", "Scripts/UI/MenuListSortBar.cs");
        Add("TownSplitUIController.cs", "Scripts/UI/TownSplitUIController.cs");
        Add("LegendCodexUI.cs", "Scripts/UI/LegendCodexUI.cs");

        Add("GameLoopManager.cs", "Scripts/Systems/GameLoopManager.cs");
        Add("TownRNGGenerator.cs", "Scripts/Systems/TownRNGGenerator.cs");
        Add("TavernDiceMinigame.cs", "Scripts/Systems/TavernDiceMinigame.cs");
        // DiceManager / StageLayoutProfile / SpriteAnimClip / ModularSpriteLibrary /
        // ModularSpriteAnimator / StageParallaxController standalones REMOVED — embedded (CS0101).
        Add("CardFusionSystem.cs", "Scripts/Systems/CardFusionSystem.cs");
        Add("PerkDraftManager.cs", "Scripts/Systems/PerkDraftManager.cs");
        Add("DemonicPactManager.cs", "Scripts/Systems/DemonicPactManager.cs");
        Add("RelicManager.cs", "Scripts/Systems/RelicManager.cs");
        Add("SinRuneManager.cs", "Scripts/Systems/SinRuneManager.cs");

        Add("GameDataTypes.cs", "Scripts/Data/GameDataTypes.cs");
        Add("CharacterDefinition.cs", "Scripts/Data/Characters/CharacterDefinition.cs");
        Add("CharacterDatabase.cs", "Scripts/Data/Characters/CharacterDatabase.cs");
        Add("TownsfolkAppearance.cs", "Scripts/Data/TownsfolkAppearance.cs");
        Add("SomersetRunes.cs", "Scripts/Data/SomersetRunes.cs");
        Add("SomersetVisualDatabase.cs", "Scripts/Data/SomersetVisualDatabase.cs");
        Add("WaveBossHudView.cs", "Scripts/UI/WaveBossHudView.cs");
        Add("TownsfolkPortraitView.cs", "Scripts/UI/TownsfolkPortraitView.cs");
        Add("TownsfolkVisualApplier.cs", "Scripts/UI/TownsfolkVisualApplier.cs");

        Add("GrudgeSaveManager.cs", "Scripts/Persistence/GrudgeSaveManager.cs");
        Add("MerlinResurrectionManager.cs", "Scripts/Persistence/MerlinResurrectionManager.cs");

        Add("AutoBattlerEngine.cs", "Scripts/Combat/AutoBattlerEngine.cs");
        Add("BattlefieldVisualizer.cs", "Scripts/Combat/BattlefieldVisualizer.cs");
        Add("DemonHordeGenerator.cs", "Scripts/Combat/DemonHordeGenerator.cs");
        Add("FloatingCombatText.cs", "Scripts/Combat/FloatingCombatText.cs");
        Add("UnitVisualActor.cs", "Scripts/Combat/UnitVisualActor.cs");

        return map;
    }

    static ArthurianFixMisplacedScripts()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Arthurian/Fix Misplaced Scripts", false, 12)]
    public static void Run()
    {
        string assets = Application.dataPath;
        string editorDir = Path.Combine(assets, "Editor");
        if (!Directory.Exists(editorDir)) return;

        int fixedCount = 0;

        foreach (string path in Directory.GetFiles(editorDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(path);
            if (IsAllowedEditorScript(fileName)) continue;

            // Known gameplay → Scripts/ (delete Editor copy if Scripts already has it)
            if (KnownGameplayTargets.TryGetValue(fileName, out string toRel))
            {
                string to = Path.Combine(assets, toRel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (!File.Exists(to))
                {
                    File.Move(path, to);
                    string meta = path + ".meta";
                    if (File.Exists(meta)) File.Move(meta, to + ".meta");
                    Debug.Log($"[DemonKnight] Moved Editor/{fileName} → {toRel}");
                }
                else
                {
                    DeleteFileAndMeta(path);
                    Debug.Log($"[DemonKnight] Deleted Editor/{fileName} (duplicate of {toRel}) — this was causing CS0029.");
                }
                fixedCount++;
                continue;
            }

            // Anything else unexpected in Editor (NewMonoBehaviourScript, random pastes)
            DeleteFileAndMeta(path);
            Debug.LogWarning($"[DemonKnight] Deleted unexpected Editor script: {fileName}");
            fixedCount++;
        }

        // Also kill duplicate ChurchStageUIController.cs anywhere under Assets except the canonical path.
        fixedCount += RemoveDuplicateGameplayCopies(assets, "ChurchStageUIController.cs",
            Path.Combine(assets, "Scripts", "UI", "ChurchStageUIController.cs"));
        fixedCount += RemoveDuplicateGameplayCopies(assets, "SomersetChurchUI.cs",
            Path.Combine(assets, "Scripts", "UI", "SomersetChurchUI.cs"));
        fixedCount += RemoveDuplicateGameplayCopies(assets, "SomersetRunes.cs",
            Path.Combine(assets, "Scripts", "Data", "SomersetRunes.cs"));

        // CS0436: DemonKnightPackageVersion must NOT exist under Scripts/ (Editor-only type).
        string editorPv = Path.Combine(assets, "Editor", "DemonKnightPackageVersion.cs");
        foreach (string path in Directory.GetFiles(assets, "DemonKnightPackageVersion.cs", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".meta")) continue;
            string full = Path.GetFullPath(path);
            if (string.Equals(full, Path.GetFullPath(editorPv), System.StringComparison.OrdinalIgnoreCase))
                continue;
            DeleteFileAndMeta(full);
            Debug.LogWarning($"[DemonKnight] Deleted duplicate PackageVersion (CS0436): {full}");
            fixedCount++;
        }

        if (fixedCount > 0)
        {
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "DemonKnight",
                $"Cleaned {fixedCount} file(s) out of Assets/Editor/ (and duplicate gameplay copies).\n\n" +
                "Kept: Arthurian* / Somerset* / DemonKnight* Editor tools (incl. Art Tools).\n" +
                "Gameplay scripts belong under Assets/Scripts/.\n\n" +
                "Wait for compile.",
                "OK");
        }
    }

    /// <summary>
    /// If the same gameplay filename exists in more than one place, keep the canonical
    /// Scripts/ path and delete the extras (common after zip / hand-copy into Downloads).
    /// </summary>
    private static int RemoveDuplicateGameplayCopies(string assetsRoot, string fileName, string canonicalPath)
    {
        int removed = 0;
        string canonicalFull = Path.GetFullPath(canonicalPath);
        foreach (string path in Directory.GetFiles(assetsRoot, fileName, SearchOption.AllDirectories))
        {
            if (path.EndsWith(".meta")) continue;
            string full = Path.GetFullPath(path);
            if (string.Equals(full, canonicalFull, System.StringComparison.OrdinalIgnoreCase))
                continue;
            // Never touch PackageCache / something outside this project tree.
            if (full.IndexOf($"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase) < 0
                && full.IndexOf($"{Path.DirectorySeparatorChar}Scripts{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Still under Assets but weird folder — delete if canonical exists.
            }
            if (!File.Exists(canonicalFull))
            {
                // Canonical missing: move this copy into place instead of deleting.
                Directory.CreateDirectory(Path.GetDirectoryName(canonicalFull)!);
                File.Move(full, canonicalFull);
                string meta = full + ".meta";
                if (File.Exists(meta)) File.Move(meta, canonicalFull + ".meta");
                Debug.Log($"[DemonKnight] Restored canonical {fileName} from {full}");
                removed++;
                continue;
            }
            DeleteFileAndMeta(full);
            Debug.Log($"[DemonKnight] Deleted duplicate {fileName} at {full} (kept {canonicalFull})");
            removed++;
        }
        return removed;
    }

    private static void DeleteFileAndMeta(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        string meta = path + ".meta";
        if (File.Exists(meta)) File.Delete(meta);
    }
}
#endif
