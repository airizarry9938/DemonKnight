#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using ArthurianAutobattler;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Always-available menu entries that do not depend on TextMeshPro or gameplay scripts.
/// Art Tools → Install lives HERE. The actual importer / slicer / seeder windows register
/// their own Arthurian → Art Tools menus once they compile — do NOT duplicate those paths
/// here or Unity runs both handlers (Install dialog + window) and menus flicker on refresh.
/// </summary>
public static class ArthurianMenuBootstrap
{
    /// <summary>
    /// Package tip with Modular/Battle importers + Art Tools.
    /// Update when shipping a new Sync pin (Force Sync prefers this over main).
    /// </summary>
    public const string ArtToolsPinSha = "a35193efd12fb0d95789893b4cd9ae670614c339";

    /// <summary>
    /// Hard pin for Phase 2–5 editor helpers (absolute raw URLs). Updated each ship.
    /// Standalone ModularSpriteLibrary / StageLayout / Dice / Anim / Parallax are NEVER
    /// downloaded — types live in embeds; leftover standalones are deleted (CS0101).
    /// </summary>
    public const string Phase25FetchPinSha = "6e31cd9d28444881bbcbdcd3676720e4fd61ddd6";

    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    const string JsDelivrBase = "https://cdn.jsdelivr.net/gh/" + Repo + "@";

    /// <summary>Phase 2–5 standalones to DELETE (types embedded elsewhere). CS0101 kill list.</summary>
    public static readonly string[] Phase25StandaloneKillList =
    {
        "Assets/Scripts/Data/SpriteAnimClip.cs",
        "Assets/Scripts/Data/StageLayoutProfile.cs",
        "Assets/Scripts/Data/ModularSpriteLibrary.cs",
        "Assets/Scripts/UI/ModularSpriteAnimator.cs",
        "Assets/Scripts/Systems/StageParallaxController.cs",
        "Assets/Scripts/Systems/DiceManager.cs",
    };

    /// <summary>Editor helpers only — never the six embedded-type standalones.</summary>
    public static readonly string[] Phase25ScriptPaths =
    {
        "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
        "Assets/Editor/ArthurianArtArchitectWindow.cs",
    };

    [MenuItem("Arthurian/Diagnose Project", false, 1)]
    public static void DiagnoseProject()
    {
        bool hasTmp = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro") != null
                      || Type.GetType("TMPro.TextMeshProUGUI, Unity.ugui") != null;

        if (!hasTmp)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType("TMPro.TextMeshProUGUI", false) != null)
                    {
                        hasTmp = true;
                        break;
                    }
                }
                catch { /* ignore */ }
            }
        }

        bool hasGameplay = TypeExists("ArthurianAutobattler.GameLoopManager");
        bool hasPews = TypeExists("ArthurianAutobattler.SomersetPewLayout");
        bool hasBuilder = TypeExists("ArthurianSceneBuilder");
        bool hasAlignType = TypeExists("ArthurianAlignCrowdPews");
        bool hasModularImporter = TypeExists("ArthurianModularSpriteSheetImporterWindow");
        bool hasBattleSlicer = TypeExists("ArthurianBattleSpriteSheetSlicerWindow");
        bool hasSeeder = TypeExists("ArthurianCharacterCatalogSeeder");
        bool hasDbEditor = TypeExists("ArthurianCharacterDatabaseEditor");
        bool hasStageLayout = TypeExists("ArthurianAutobattler.StageLayoutProfile");
        bool hasParallax = TypeExists("ArthurianAutobattler.StageParallaxController");
        bool hasModAnim = TypeExists("ArthurianAutobattler.ModularSpriteAnimator");
        bool hasDiceMgr = TypeExists("ArthurianAutobattler.DiceManager");

        string packageId = "(no version stamp — old first build)";
        string packageGit = "?";
        var verType = Type.GetType("DemonKnightPackageVersion")
                      ?? Type.GetType("DemonKnightPackageVersion, Assembly-CSharp-Editor");
        if (verType == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    verType = asm.GetType("DemonKnightPackageVersion", false);
                    if (verType != null) break;
                }
                catch { /* ignore */ }
            }
        }
        if (verType != null)
        {
            packageId = verType.GetField("Id")?.GetValue(null) as string ?? packageId;
            packageGit = verType.GetField("GitHint")?.GetValue(null) as string ?? packageGit;
        }

        string packageIdOnDisk = ReadPackageIdFromDisk() ?? "(missing DemonKnightPackageVersion.cs on disk)";
        bool packageStale = !string.Equals(packageId, packageIdOnDisk, StringComparison.Ordinal);

        string editorDir = Path.Combine(Application.dataPath, "Editor");
        string alignPath = Path.Combine(editorDir, "ArthurianAlignCrowdPews.cs");
        string builderPath = Path.Combine(editorDir, "ArthurianSceneBuilder.cs");
        string syncPath = Path.Combine(editorDir, "ArthurianSyncFromGitHub.cs");
        string menuBootPath = Path.Combine(editorDir, "ArthurianMenuBootstrap.cs");
        string modularImporterPath = Path.Combine(editorDir, "ArthurianModularSpriteSheetImporterWindow.cs");
        string battleSlicerPath = Path.Combine(editorDir, "ArthurianBattleSpriteSheetSlicerWindow.cs");
        // Common mistake: extract zip-with-Assets/Editor/ INTO Assets/Editor → nested path.
        string nestedModular = Path.Combine(editorDir, "Assets", "Editor", "ArthurianModularSpriteSheetImporterWindow.cs");
        string nestedBattle = Path.Combine(editorDir, "Assets", "Editor", "ArthurianBattleSpriteSheetSlicerWindow.cs");
        bool alignFileOnDisk = File.Exists(alignPath);
        bool syncOnDisk = File.Exists(syncPath);
        bool menuBootOnDisk = File.Exists(menuBootPath);
        bool modularImporterOnDisk = File.Exists(modularImporterPath);
        bool battleSlicerOnDisk = File.Exists(battleSlicerPath);
        bool nestedZipExtract = File.Exists(nestedModular) || File.Exists(nestedBattle);
        bool hasSyncType = TypeExists("ArthurianSyncFromGitHub");
        string builderStamp = File.Exists(builderPath)
            ? File.GetLastWriteTimeUtc(builderPath).ToString("u") + " UTC"
            : "MISSING";

        string strayHits = "";
        try
        {
            if (Directory.Exists(editorDir))
            {
                foreach (var hit in Directory.GetFiles(editorDir, "*Importer*.cs", SearchOption.AllDirectories))
                    strayHits += "  " + hit + "\n";
                foreach (var hit in Directory.GetFiles(editorDir, "*Slicer*.cs", SearchOption.AllDirectories))
                    strayHits += "  " + hit + "\n";
            }
        }
        catch { /* ignore */ }

        string msg =
            "DemonKnight / Arthurian diagnose\n\n" +
            $"Package (compiled): {packageId} ({packageGit})\n" +
            $"Package Id on disk: {packageIdOnDisk}"
            + (packageStale
                ? "\n⚠ STALE COMPILE — disk ≠ compiled. Run Arthurian → FORCE Upgrade Sync Hosts From main, wait for compile."
                : "") + "\n" +
            ArthurianSyncFromGitHub.ArchitectTipMarkersReport() + "\n" +
            PackageVersionDiskReport() + "\n" +
            DemonKnightPackageVersion.WipeStatusForDiagnose() + "\n" +
            $"Art Tools pin: {ArtToolsPinSha}\n" +
            $"Clear Paperdoll menu on disk: {(menuBootOnDisk && File.Exists(menuBootPath) && File.ReadAllText(menuBootPath).IndexOf("Clear Paperdoll Library", StringComparison.Ordinal) >= 0 ? "YES" : "NO (auto-wipe OnLoad/Force Sync still runs)")}\n" +
            $"Assets path:\n{Application.dataPath}\n" +
            $"Expected importer path:\n{modularImporterPath}\n" +
            $"Sync menus on disk: {(syncOnDisk ? "YES" : "NO")}\n" +
            $"Sync menus compiled: {(hasSyncType ? "YES" : "NO")}\n" +
            $"Diagnose host on disk: {(menuBootOnDisk ? "YES" : "NO")}\n" +
            $"Align script on disk: {(alignFileOnDisk ? "YES" : "NO")}\n" +
            $"Align menu compiled: {(hasAlignType ? "YES" : "NO")}\n" +
            $"Art importer on disk: {(modularImporterOnDisk ? "YES" : "NO")}\n" +
            $"Art importer compiled: {(hasModularImporter ? "YES" : "NO")}\n" +
            $"Battle slicer on disk: {(battleSlicerOnDisk ? "YES" : "NO")}\n" +
            $"Battle slicer compiled: {(hasBattleSlicer ? "YES" : "NO")}\n" +
            $"Seeder compiled: {(hasSeeder ? "YES" : "NO")}\n" +
            $"DB editor compiled: {(hasDbEditor ? "YES" : "NO")}\n" +
            $"Nested zip extract?: {(nestedZipExtract ? "YES — files under Assets/Editor/Assets/Editor" : "NO")}\n" +
            $"ArthurianSceneBuilder.cs mtime: {builderStamp}\n\n" +
            $"TextMeshPro loaded: {(hasTmp ? "YES" : "NO")}\n" +
            $"Gameplay scripts: {(hasGameplay ? "YES" : "NO")}\n" +
            $"Church pew layout: {(hasPews ? "YES" : "NO")}\n" +
            $"Scene builder: {(hasBuilder ? "YES" : "NO")}\n" +
            $"Phase 2–5 StageLayoutProfile (embed): {(hasStageLayout ? "YES" : "MISSING — Force Sync GameDataTypes.cs")}\n" +
            $"Phase 4 StageParallaxController (embed): {(hasParallax ? "YES" : "MISSING — Force Sync GameLoopManager.cs")}\n" +
            $"Phase 4 ModularSpriteAnimator (embed): {(hasModAnim ? "YES" : "MISSING — Force Sync PaperDollSeatView.cs")}\n" +
            $"Phase 5 DiceManager (embed): {(hasDiceMgr ? "YES" : "MISSING — Force Sync TavernDiceMinigame.cs")}\n\n";

        if (!hasStageLayout || !hasParallax || !hasModAnim || !hasDiceMgr)
        {
            msg +=
                "CS0103 FIX: Force Sync hosts that embed these types (GameDataTypes / PaperDoll / GameLoop / TavernDice).\n" +
                "CS0101 FIX: Arthurian → Delete Phase 2–5 Duplicate Standalones\n" +
                "(or manually delete Assets/Scripts/Data/ModularSpriteLibrary.cs then recompile)\n\n";
        }

        if (!string.IsNullOrEmpty(strayHits))
            msg += "Found under Editor (search):\n" + strayHits + "\n";

        if (hasModularImporter && hasBattleSlicer)
        {
            msg +=
                "Art tools READY — do NOT Install again.\n" +
                "Open the windows (not Install):\n" +
                "• Arthurian → Art & Stage Architect (Tab 1 · Sprite)\n" +
                "• Arthurian → Art Tools → Battle Sprite Sheet Slicer\n" +
                "DemonKnight menu is an optional alias for the same windows.\n";
        }
        else if (nestedZipExtract)
        {
            msg +=
                "ART TOOLS ZIP EXTRACTED ONE FOLDER TOO DEEP.\n" +
                "Files are in Assets/Editor/Assets/Editor/ — Unity ignores that.\n" +
                "Use: Arthurian → Art Tools → Fix Nested Art Tools Zip Extract\n" +
                "(moves the .cs files up into Assets/Editor/).\n";
        }
        else if (modularImporterOnDisk || battleSlicerOnDisk)
        {
            msg +=
                "Art tool SCRIPTS ARE ON DISK but NOT COMPILED.\n" +
                "Do NOT click Install again — that only re-downloads and flicker menus.\n\n" +
                "FIX: Window → General → Console → clear every red CS error.\n" +
                "When the bottom-right spinner stops and Console is clean, the\n" +
                "Modular / Battle menus open the real windows.\n";
        }
        else
        {
            msg +=
                "ART TOOLS NOT AT EXPECTED PATH.\n" +
                "Put these 4 files DIRECTLY in Assets/Editor/ (same folder as this Diagnose script):\n" +
                "• ArthurianModularSpriteSheetImporterWindow.cs\n" +
                "• ArthurianBattleSpriteSheetSlicerWindow.cs\n" +
                "• ArthurianCharacterCatalogSeeder.cs\n" +
                "• ArthurianCharacterDatabaseEditor.cs\n" +
                "Flat zip: docs/DemonKnight-ArtTools-Editor.zip (files at zip root).\n";
        }

        Debug.Log(msg);
        EditorUtility.DisplayDialog("Arthurian Diagnose", msg, "OK");
    }

    /// <summary>
    /// Creates/fills ModularSpriteLibrary under Art + copies to Resources so Play mode pews work.
    /// Empty poses (posesArray=4 posesFilled=0) get placeholder sprites when no sheets imported.
    /// </summary>
    [MenuItem("Arthurian/Art Tools/Verify & Sync Modular Library", false, 38)]
    public static void VerifyAndSyncModularLibrary()
    {
        string report = VerifyAndSyncModularLibraryInternal(showDialog: true);
        Debug.Log("[DemonKnight] " + report);
    }

    [MenuItem("Arthurian/Art Tools/Clear Paperdoll Library → Single Sprite", false, 37)]
    public static void ClearPaperdollLibraryMenu()
    {
        string report = ClearPaperdollLibraryInternal(showDialog: true);
        Debug.Log("[DemonKnight] " + report);
    }

    [MenuItem("Arthurian/Art Tools/Save Sprite Library → Resources", false, 36)]
    public static void SaveSpriteLibraryToResourcesMenu()
    {
        string report = VerifyAndSyncModularLibraryInternal(showDialog: true);
        Debug.Log("[DemonKnight] Save Sprite Library: " + report);
    }

    /// <summary>Wipe Outfit/Head/Prop + legacy Pose clips, then copy library → Resources.</summary>
    public static string ClearPaperdollLibraryInternal(bool showDialog)
    {
        const string artPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
        const string resPath = "Assets/Resources/Art/Modular/ModularSpriteLibrary.asset";
        EnsureFolderPath("Assets/Art/Modular");
        EnsureFolderPath("Assets/Resources/Art/Modular");

        var art = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(artPath);
        if (art == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:ModularSpriteLibrary");
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                art = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(p);
                if (art != null) break;
            }
        }
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<ModularSpriteLibrary>();
            art.EnsureSizedArrays();
            AssetDatabase.CreateAsset(art, artPath);
        }

        // Prefer PackageVersion SerializedObject wipe (works even mid-upgrade).
        try { DemonKnightPackageVersion.WipeModularPaperdollAssets(); } catch { /* ignore */ }
        int cleared = art.ClearPaperdollLayersKeepSprites();
        // Drop legacy Prepared_Pose sheets from basePoses when Spliced/Sprite exports exist — else keep / placeholder.
        TryImportSplicedSpritesIntoBasePoses(art);
        if (CountSprites(art.basePoses) == 0)
        {
            art.EnsurePlaceholderSpritesIfEmpty();
            BakePlaceholderSpritesIntoProject(art, "Assets/Art/Modular/Placeholders");
        }
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();

        string syncReport = VerifyAndSyncModularLibraryInternal(showDialog: false);
        string report =
            $"Cleared paperdoll residue entries≈{cleared}.\n" +
            $"outfits/heads/props emptied; anim clips kept=Sprite-only.\n" +
            syncReport +
            "\nStageLayoutProfile is pew/battle SLOTS only — not required for sprite wipe.\n" +
            "Stop Play → New Game. Bind log must show outfit=NULL head=NULL.";
        if (showDialog)
            EditorUtility.DisplayDialog("Paperdoll Library Cleared", report, "OK");
        return report;
    }

    /// <summary>Callable from Force Sync without a dialog. Also strips paperdoll residue.</summary>
    public static string VerifyAndSyncModularLibraryInternal(bool showDialog)
    {
        const string artPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
        const string resPath = "Assets/Resources/Art/Modular/ModularSpriteLibrary.asset";

        EnsureFolderPath("Assets/Art/Modular");
        EnsureFolderPath("Assets/Resources/Art/Modular");

        var art = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(artPath);
        if (art == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:ModularSpriteLibrary");
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Replace('\\', '/').IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                art = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(p);
                if (art != null) { Debug.Log("[DemonKnight] Using library at " + p); break; }
            }
        }

        if (art == null)
        {
            art = ScriptableObject.CreateInstance<ModularSpriteLibrary>();
            art.EnsureSizedArrays();
            AssetDatabase.CreateAsset(art, artPath);
            Debug.Log("[DemonKnight] Created empty ModularSpriteLibrary at " + artPath);
        }

        art.EnsureSizedArrays();
        int wiped = 0;
        if (art.HasPaperdollResidue())
            wiped = art.ClearPaperdollLayersKeepSprites();
        TryImportSplicedSpritesIntoBasePoses(art);

        bool usedPlaceholders = CountSprites(art.basePoses) == 0;
        if (usedPlaceholders)
        {
            art.EnsurePlaceholderSpritesIfEmpty();
            // Persist placeholders as textures under the library so Resources copy keeps refs.
            BakePlaceholderSpritesIntoProject(art, "Assets/Art/Modular/Placeholders");
        }
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();

        int poses = CountSprites(art.basePoses);
        int outfits = CountSprites(art.outfits);
        int heads = 0;
        if (art.heads != null)
            for (int i = 0; i < art.heads.Length; i++)
                if (art.heads[i] != null && art.heads[i].sprite != null) heads++;
        string artAssetPath = AssetDatabase.GetAssetPath(art);

        if (AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resPath) != null)
            AssetDatabase.DeleteAsset(resPath);
        AssetDatabase.CopyAsset(artAssetPath, resPath);
        // Also copy placeholder textures folder if present.
        if (AssetDatabase.IsValidFolder("Assets/Art/Modular/Placeholders"))
        {
            EnsureFolderPath("Assets/Resources/Art/Modular/Placeholders");
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Modular/Placeholders" });
            foreach (string g in texGuids)
            {
                string src = AssetDatabase.GUIDToAssetPath(g);
                string name = Path.GetFileName(src);
                string dst = "Assets/Resources/Art/Modular/Placeholders/" + name;
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dst) != null)
                    AssetDatabase.DeleteAsset(dst);
                AssetDatabase.CopyAsset(src, dst);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TownsfolkVisualApplier.SetLibrary(null);
        ArthurianStageLayoutProfileEnsure.EnsureAsset();

        var res = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resPath);
        if (res != null)
        {
            res.EnsureSizedArrays();
            if (res.HasPaperdollResidue())
                res.ClearPaperdollLayersKeepSprites();
            if (CountSprites(res.basePoses) == 0)
            {
                // CopyAsset can drop ephemeral refs — re-seed + bake under Resources.
                res.EnsurePlaceholderSpritesIfEmpty();
                BakePlaceholderSpritesIntoProject(res, "Assets/Resources/Art/Modular/Placeholders");
            }
            EditorUtility.SetDirty(res);
            AssetDatabase.SaveAssets();
        }
        int resPoses = res != null ? CountSprites(res.basePoses) : 0;
        int resOutfits = res != null ? CountSprites(res.outfits) : -1;

        string report =
            $"Source: {artAssetPath}\n" +
            $"sprites(basePoses) {poses} · outfits {outfits} (want 0) · heads {heads} (want 0)\n" +
            (wiped > 0 ? $"Wiped paperdoll residue≈{wiped}\n" : "") +
            $"Runtime copy: {resPath}\n" +
            $"Resources sprites filled: {resPoses} · outfits: {resOutfits}\n" +
            (usedPlaceholders
                ? "Seeded PLACEHOLDER sprites (no Spliced/Sprite exports yet).\nArchitect → Save Splices (Category Sprite) when ready.\n"
                : "") +
            "Stop Play → Main Menu → New Game to rebuild pews.\n" +
            "Bind log must show: body=… outfit=NULL head=NULL";

        if (showDialog)
            EditorUtility.DisplayDialog("Modular Library Synced", report, "OK");
        return report;
    }

    /// <summary>Pull PNG/sprites from Assets/Art/Spliced/Sprite into basePoses when present.</summary>
    static void TryImportSplicedSpritesIntoBasePoses(ModularSpriteLibrary lib)
    {
        if (lib == null) return;
        const string folder = "Assets/Art/Spliced/Sprite";
        if (!AssetDatabase.IsValidFolder(folder)) return;
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        if (guids == null || guids.Length == 0) return;

        var sprites = new List<Sprite>();
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) sprites.Add(sp);
        }
        if (sprites.Count == 0) return;

        // Prefer Spliced/Sprite over legacy Prepared_Pose sheets.
        bool legacyOnly = CountSprites(lib.basePoses) > 0;
        if (legacyOnly)
        {
            bool allLegacy = true;
            for (int i = 0; i < lib.basePoses.Length; i++)
            {
                var sp = lib.basePoses[i];
                if (sp == null) continue;
                string n = sp.name ?? "";
                if (n.IndexOf("Prepared_Pose", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Prepared_Outfit", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("DK_Sprite_", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("DK_Pose_", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    allLegacy = false;
                    break;
                }
            }
            if (!allLegacy) return; // already has real spliced refs
        }

        lib.basePoses = sprites.ToArray();
        EditorUtility.SetDirty(lib);
        Debug.Log($"[DemonKnight] Imported {sprites.Count} sprite(s) from {folder} → basePoses.");
    }

    static void EnsureFolderPath(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
        string[] parts = folder.Replace('\\', '/').Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    /// <summary>Write runtime placeholder sprites to disk so Resources copy keeps valid refs.</summary>
    static void BakePlaceholderSpritesIntoProject(ModularSpriteLibrary lib, string folder)
    {
        if (lib == null) return;
        EnsureFolderPath(folder);
        void Bake(ref Sprite sp, string fileName)
        {
            if (sp == null || sp.texture == null) return;
            string path = folder + "/" + fileName + ".png";
            File.WriteAllBytes(path, sp.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.alphaIsTransparency = true;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(0.5f, 0f);
                settings.alphaIsTransparency = true;
                settings.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (loaded != null) sp = loaded;
        }

        // Single-sprite: only bake basePoses (never re-materialize Outfit/Head/Prop placeholders).
        if (lib.basePoses != null)
            for (int i = 0; i < lib.basePoses.Length; i++)
                Bake(ref lib.basePoses[i], $"Sprite_{i}");
    }

    [InitializeOnLoadMethod]
    static void DkAutoVerifyModularOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                const string resPath = "Assets/Resources/Art/Modular/ModularSpriteLibrary.asset";
                var res = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resPath);
                int filled = res != null ? CountSprites(res.basePoses) : 0;
                bool paperdoll = res != null && res.HasPaperdollResidue();
                if (res == null || filled == 0 || paperdoll)
                {
                    Debug.Log(paperdoll
                        ? "[DemonKnight] Auto Clear Paperdoll → Single Sprite (Resources still has Outfit/Head/Pose clips)."
                        : "[DemonKnight] Auto Verify & Sync Modular Library (Resources missing/empty).");
                    if (paperdoll)
                        ClearPaperdollLibraryInternal(showDialog: false);
                    else
                        VerifyAndSyncModularLibraryInternal(showDialog: false);
                }
                DedupeArtToolScripts();
                ForceArchitectTipIfStale();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] Auto modular verify skipped: " + ex.Message);
            }
        };
    }

    /// <summary>
    /// Chicken-egg: PackageVersion can tip while Architect stays paperdoll because old Sync
    /// ValidateCritical required "Paper-Doll". After Bootstrap compiles, force Architect from tip.
    /// </summary>
    public static void ForceArchitectTipIfStale()
    {
        const string rel = "Assets/Editor/ArthurianArtArchitectWindow.cs";
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            ArthurianSyncFromGitHub.NukeNestedArchitectCopies(projectRoot);
            string disk = File.Exists(dest) ? File.ReadAllText(dest) : "";
            bool tipShape = ArthurianSyncFromGitHub.IsTipArchitectBody(disk);
            bool packageMatch = disk.IndexOf(DemonKnightPackageVersion.Id, StringComparison.Ordinal) >= 0
                                && !ArthurianSyncFromGitHub.IsStaleArchitectUiBuild(disk);
            if (tipShape && packageMatch) return;

            Debug.LogWarning(
                "[DemonKnight] Stale Art Architect on disk (paperdoll and/or package Id≠"
                + DemonKnightPackageVersion.Id + ") — force tip overwrite.");
            ArthurianSyncFromGitHub.NukeStaleArchitectAndPaperDollEditors(projectRoot);
            ArthurianSyncFromGitHub.CloseOpenArchitectWindows();
            var errors = new List<string>();
            int n = ArthurianSyncFromGitHub.ForceOverwriteSyncHostsFromMain(errors);
            AssetDatabase.Refresh();
            string after = File.Exists(dest) ? File.ReadAllText(dest) : "";
            Debug.Log("[DemonKnight] " + ArthurianSyncFromGitHub.ArchitectTipMarkersReport());
            if (ArthurianSyncFromGitHub.IsTipArchitectBody(after)
                && after.IndexOf(DemonKnightPackageVersion.Id, StringComparison.Ordinal) >= 0)
                Debug.Log($"[DemonKnight] Architect tip landed via host overwrite ({n} hosts). Re-open Art & Stage Architect.");
            else
                Debug.LogError("[DemonKnight] Architect still stale after host overwrite. Run Arthurian → FORCE Upgrade Sync Hosts From main, wait compile, Force Sync again.\n"
                               + string.Join("\n", errors));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] ForceArchitectTipIfStale: " + ex.Message);
        }
    }

    /// <summary>Delete nested/duplicate Art Tools / Architect .cs that cause MenuItem already exists.</summary>
    public static void DedupeArtToolScripts()
    {
        string editorDir = Path.Combine(Application.dataPath, "Editor");
        string[] names =
        {
            "ArthurianModularSpriteSheetImporterWindow.cs",
            "ArthurianBattleSpriteSheetSlicerWindow.cs",
            "ArthurianArtArchitectWindow.cs",
        };
        try
        {
            foreach (string name in names)
            {
                string canonical = Path.Combine(editorDir, name);
                foreach (var hit in Directory.GetFiles(editorDir, name, SearchOption.AllDirectories))
                {
                    if (string.Equals(hit, canonical, StringComparison.OrdinalIgnoreCase)) continue;
                    TryDeleteEditorScript(hit);
                }
            }
            // Also sweep nested under Assets outside Editor (zip extract).
            ArthurianSyncFromGitHub.NukeNestedArchitectCopies(null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] Art Tools dedupe: " + ex.Message);
        }
    }

    static void TryDeleteEditorScript(string fullPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string rel = "Assets" + fullPath.Substring(Application.dataPath.Length).Replace('\\', '/');
        Debug.LogWarning("[DemonKnight] Removing duplicate Art Tools script: " + rel);
        if (!AssetDatabase.DeleteAsset(rel))
        {
            try
            {
                File.SetAttributes(fullPath, FileAttributes.Normal);
                File.Delete(fullPath);
                if (File.Exists(fullPath + ".meta")) File.Delete(fullPath + ".meta");
            }
            catch { /* ignore */ }
        }
    }

    static int CountSprites(Sprite[] arr)
    {
        if (arr == null) return 0;
        int n = 0;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] != null) n++;
        return n;
    }

    /// <summary>
    /// Moves Art Tools .cs files from Assets/Editor/Assets/Editor/ up into Assets/Editor/
    /// (happens when the nested zip was extracted into the Editor folder).
    /// </summary>
    [MenuItem("Arthurian/Art Tools/Fix Nested Art Tools Zip Extract", false, 39)]
    public static void FixNestedArtToolsZipExtract()
    {
        string editorDir = Path.Combine(Application.dataPath, "Editor");
        string nestedDir = Path.Combine(editorDir, "Assets", "Editor");
        string[] names =
        {
            "ArthurianModularSpriteSheetImporterWindow.cs",
            "ArthurianBattleSpriteSheetSlicerWindow.cs",
            "ArthurianCharacterCatalogSeeder.cs",
            "ArthurianCharacterDatabaseEditor.cs",
        };

        int moved = 0;
        var log = "";
        if (!Directory.Exists(nestedDir))
        {
            // Also search one level of subfolders for the four files.
            nestedDir = null;
            try
            {
                foreach (var dir in Directory.GetDirectories(editorDir, "*", SearchOption.AllDirectories))
                {
                    if (File.Exists(Path.Combine(dir, names[0])))
                    {
                        nestedDir = dir;
                        break;
                    }
                }
            }
            catch { /* ignore */ }
        }

        if (string.IsNullOrEmpty(nestedDir) || !Directory.Exists(nestedDir))
        {
            EditorUtility.DisplayDialog(
                "Art Tools Zip Fix",
                "No nested ArthurianModularSpriteSheetImporterWindow.cs found under Assets/Editor/.\n\n" +
                "In File Explorer open Assets\\Editor and confirm the 4 .cs files sit NEXT TO\n" +
                "ArthurianMenuBootstrap.cs (not in a subfolder).",
                "OK");
            EditorUtility.RevealInFinder(editorDir);
            return;
        }

        foreach (var name in names)
        {
            string src = Path.Combine(nestedDir, name);
            string dest = Path.Combine(editorDir, name);
            if (!File.Exists(src)) continue;
            try
            {
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(src, dest);
                moved++;
                log += "Moved " + name + "\n";
            }
            catch (Exception ex)
            {
                log += name + ": " + ex.Message + "\n";
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Art Tools Zip Fix",
            $"Moved {moved}/4 into Assets/Editor/.\n\n{log}\nWait for compile, then Diagnose again.",
            "OK");
        EditorUtility.RevealInFinder(editorDir);
    }

    /// <summary>
    /// One-time download of importer scripts. Does NOT open the windows.
    /// After a green compile, use Modular / Battle menu items (owned by those scripts).
    /// </summary>
    [MenuItem("Arthurian/Art Tools/Install Art Importers Now", false, 40)]
    public static void InstallArtImportersNow()
    {
        bool modularCompiled = TypeExists("ArthurianModularSpriteSheetImporterWindow");
        bool battleCompiled = TypeExists("ArthurianBattleSpriteSheetSlicerWindow");
        if (modularCompiled && battleCompiled)
        {
            EditorUtility.DisplayDialog(
                "Art Tools Already Installed",
                "Importer scripts are already compiled.\n\n" +
                "Do NOT Install again.\n\n" +
                "Open:\n" +
                "• Arthurian → Art & Stage Architect (Tab 1 · Sprite)\n" +
                "• Arthurian → Art Tools → Battle Sprite Sheet Slicer\n\n" +
                "If menus look wrong, run Arthurian → Diagnose Project.",
                "OK");
            return;
        }

        string editorDir = Path.Combine(Application.dataPath, "Editor");
        bool modularOnDisk = File.Exists(Path.Combine(editorDir, "ArthurianModularSpriteSheetImporterWindow.cs"));
        bool battleOnDisk = File.Exists(Path.Combine(editorDir, "ArthurianBattleSpriteSheetSlicerWindow.cs"));
        if ((modularOnDisk || battleOnDisk) && (!modularCompiled || !battleCompiled))
        {
            bool force = EditorUtility.DisplayDialog(
                "Scripts On Disk — Compile Broken",
                "Art importer files are already in Assets/Editor but Unity cannot compile them.\n\n" +
                "Re-Install will NOT fix this and will make the DemonKnight menu flicker while scripts reload.\n\n" +
                "Open Window → General → Console and fix red errors first.\n\n" +
                "Only force re-download if you deleted files on purpose.",
                "Force Re-Download", "Cancel — Fix Console");
            if (!force) return;
        }
        else if (!EditorUtility.DisplayDialog(
                     "Install Art Importers",
                     "Download Art Tools Editor scripts AND their runtime data scripts from pin "
                     + ArtToolsPinSha + "?\n\n"
                     + "One-time step. After compile, open Modular / Battle — not this Install item.",
                     "Download", "Cancel"))
        {
            return;
        }

        // Do NOT download ArthurianMenuBootstrap.cs here — that overwrote this file from a
        // stale pin and re-introduced duplicate MenuItems that always popped Install.
        string[] rels =
        {
            "Assets/Editor/ArthurianModularSpriteSheetImporterWindow.cs",
            "Assets/Editor/ArthurianBattleSpriteSheetSlicerWindow.cs",
            "Assets/Editor/ArthurianCharacterCatalogSeeder.cs",
            "Assets/Editor/ArthurianCharacterDatabaseEditor.cs",
            // PackageVersion is NOT in this list — Install must not overwrite a newer Diagnose marker.
            "Assets/Scripts/Data/GameDataTypes.cs",
            // ModularSpriteLibrary.cs standalone REMOVED — type embedded in GameDataTypes.cs (CS0101).
            "Assets/Scripts/Data/TownsfolkAppearance.cs",
            "Assets/Scripts/Data/Characters/CharacterDefinition.cs",
            "Assets/Scripts/Data/Characters/CharacterDatabase.cs",
            "Assets/Scripts/UI/TownsfolkVisualApplier.cs",
            "Assets/Scripts/UI/TownsfolkPortraitView.cs",
            "Assets/Scripts/UI/PaperDollSeatView.cs",
        };

        int ok = 0;
        var errors = "";
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            for (int i = 0; i < rels.Length; i++)
            {
                string rel = rels[i];
                EditorUtility.DisplayProgressBar("Art Tools Install", rel, (float)i / rels.Length);
                try
                {
                    string body = DownloadRel(rel, ArtToolsPinSha);
                    string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.WriteAllText(dest, body);
                    ok++;
                    Debug.Log("[DemonKnight] Installed " + rel + " @ " + ArtToolsPinSha);
                }
                catch (Exception ex)
                {
                    errors += rel + ": " + ex.Message + "\n";
                    Debug.LogError("[DemonKnight] Install failed " + rel + ": " + ex.Message);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Art Tools Install",
            $"Installed {ok}/{rels.Length} @ {ArtToolsPinSha}.\n\n"
            + "1) Wait for the bottom-right compile spinner to finish.\n"
            + "2) Console must show 0 red errors.\n"
            + "3) Then open (NOT Install):\n"
            + "   Arthurian → Art & Stage Architect (Tab 1 · Sprite)\n"
            + "   Arthurian → Art Tools → Battle Sprite Sheet Slicer\n\n"
            + "DemonKnight menu may blink once during compile — that is normal.\n"
            + (string.IsNullOrEmpty(errors) ? "" : "Errors:\n" + errors),
            "OK");
    }

    static string DownloadRel(string rel, string sha)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks;
        string[] urls =
        {
            "https://cdn.jsdelivr.net/gh/" + Repo + "@" + sha + "/" + rel + "?" + bust,
            "https://raw.githubusercontent.com/" + Repo + "/" + sha + "/" + rel + "?" + bust,
        };
        Exception last = null;
        foreach (string url in urls)
        {
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "DemonKnight-ArtTools/" + sha);
                    wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(
                        System.Net.Cache.RequestCacheLevel.BypassCache);
                    string body = wc.DownloadString(url);
                    if (!string.IsNullOrEmpty(body) && body.Length > 32)
                        return body;
                }
            }
            catch (Exception ex) { last = ex; }
        }
        throw last ?? new Exception("Download failed for " + rel);
    }

    private static bool TypeExists(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (asm.GetType(fullName, false) != null) return true;
            }
            catch { /* ignore */ }
        }
        return false;
    }

    [MenuItem("Arthurian/Open Setup Guide", false, 2)]
    public static void OpenSetupGuide()
    {
        EditorUtility.RevealInFinder(Application.dataPath);
        EditorUtility.DisplayDialog(
            "DemonKnight Setup",
            "1) Arthurian → FORCE Upgrade Sync Hosts From main\n"
            + "   (or Bootstrap CompatApi + Sync Tools).\n"
            + "2) Wait for compile. Diagnose Package Id on disk must match compiled.\n"
            + "3) Arthurian → Force Delete+Sync ALL Scripts.\n"
            + "4) If CS0101: manually delete Assets/Scripts/Data/ModularSpriteLibrary.cs then recompile.\n"
            + "5) Diagnose must show force-upgrade-sync-hosts (or newer).\n\n"
            + "Project Assets path:\n" + Application.dataPath,
            "OK");
        Debug.Log("[DemonKnight] Assets path: " + Application.dataPath);
    }

    /// <summary>
    /// Known-good tip with LooksLikeHttpFailureBody + OnLoad PackageVersion (≥500B).
    /// Update when shipping break-glass tips. Used when Sync tip-resolve is stuck.
    /// </summary>
    public const string BreakGlassTipSha = "a35193efd12fb0d95789893b4cd9ae670614c339";

    /// <summary>
    /// Lives in MenuBootstrap so skip-self Sync hosts still get a one-click upgrade.
    /// Break-glass FIRST when PackageVersion is tiny / Sync lacks LooksLikeHttpFailureBody —
    /// never trust stale Sync ForceOverwrite (lands 381B PackageVersion).
    /// </summary>
    [MenuItem("Arthurian/FORCE Upgrade Sync Hosts From main", false, 2)]
    public static void ForceUpgradeSyncHostsFromMain()
    {
        if (NeedsBreakGlassHostPull())
        {
            if (!EditorUtility.DisplayDialog(
                    "FORCE Upgrade (Break-Glass)",
                    "Local Sync/PackageVersion looks stuck (tiny PackageVersion or old 404 check).\n\n"
                    + "Will download Sync / Bootstrap / PackageVersion / Architect from hard tip SHA:\n"
                    + BreakGlassTipSha + "\n(+ raw@main fallback).\n\n"
                    + "Does NOT use stale Sync tip-resolve.",
                    "Break-Glass Now", "Cancel"))
                return;
            string report = BreakGlassForceHostsFromHardTip();
            EditorUtility.DisplayDialog("Break-Glass Hosts", report, "OK");
            return;
        }

        // Prefer Sync host implementation when present (single code path).
        try
        {
            var syncType = Type.GetType("ArthurianSyncFromGitHub")
                           ?? Type.GetType("ArthurianSyncFromGitHub, Assembly-CSharp-Editor");
            var mi = syncType?.GetMethod("ForceUpgradeSyncHostsMenu",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (mi != null)
            {
                mi.Invoke(null, null);
                return;
            }
        }
        catch { /* fall through */ }

        string report2 = BreakGlassForceHostsFromHardTip();
        EditorUtility.DisplayDialog("Break-Glass Hosts", report2, "OK");
    }

    /// <summary>Menu for Andrew when FORCE still stuck — same as break-glass path.</summary>
    [MenuItem("Arthurian/Break-Glass Pull Tip Hosts (hard SHA)", false, 2)]
    public static void BreakGlassPullTipHostsMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Break-Glass Pull Tip Hosts",
                "Force-download Sync + Bootstrap + PackageVersion + Architect from:\n"
                + "raw.githubusercontent.com/…/" + BreakGlassTipSha + "/…\n"
                + "and raw@main. Bypasses Sync tip-resolve entirely.",
                "Download Now", "Cancel"))
            return;
        EditorUtility.DisplayDialog("Break-Glass Hosts", BreakGlassForceHostsFromHardTip(), "OK");
    }

    static bool NeedsBreakGlassHostPull()
    {
        try
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string pv = Path.Combine(root, "Assets", "Editor", "DemonKnightPackageVersion.cs");
            if (!File.Exists(pv)) return true;
            long len = new FileInfo(pv).Length;
            if (len < 500) return true; // 381B stuck tip
            string sync = Path.Combine(root, "Assets", "Editor", "ArthurianSyncFromGitHub.cs");
            if (!File.Exists(sync)) return true;
            string body = File.ReadAllText(sync);
            if (body.IndexOf("LooksLikeHttpFailureBody", StringComparison.Ordinal) < 0) return true;
            if (body.IndexOf("body.Contains(\"404: Not Found\")", StringComparison.Ordinal) >= 0
                && body.IndexOf("LooksLikeHttpFailureBody", StringComparison.Ordinal) < 0)
                return true;
            string id = ReadPackageIdFromDisk() ?? "";
            if (id == "architect-force-tip-ui" || id == "single-sprite-no-paperdoll"
                || id == "architect-breakglass-hosts" || id == "architect-disk-tip-proof"
                || id == "runtime-single-sprite" || id == "single-sprite-play-wipe")
                return true;
            if (ArthurianSyncFromGitHub.IsStalePackageId(id)) return true;
            return false;
        }
        catch { return true; }
    }

    /// <summary>Download hosts by hard SHA URL — no Sync validate, length-gated 404 only.</summary>
    public static string BreakGlassForceHostsFromHardTip()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string[] hosts =
        {
            "Assets/Editor/ArthurianSyncFromGitHub.cs",
            "Assets/Editor/ArthurianMenuBootstrap.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            "Assets/Editor/ArthurianArtArchitectWindow.cs",
        };
        int ok = 0;
        var log = new System.Text.StringBuilder();
        log.AppendLine("Break-glass tip SHA: " + BreakGlassTipSha);
        foreach (string rel in hosts)
        {
            try
            {
                string body = DownloadHardTipOrMain(rel, BreakGlassTipSha);
                if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase)
                    && body.Length < 500)
                    throw new Exception("PackageVersion still tiny " + body.Length + "B");
                string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                if (File.Exists(dest))
                {
                    try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }
                    try { File.Delete(dest); } catch { /* ignore */ }
                }
                File.WriteAllText(dest, body);
                ok++;
                log.AppendLine($"OK {rel} ({body.Length} bytes)");
                Debug.Log($"[DemonKnight] Break-glass wrote {rel} ({body.Length} bytes)");
            }
            catch (Exception ex)
            {
                log.AppendLine($"FAIL {rel}: {ex.Message}");
                Debug.LogError($"[DemonKnight] Break-glass FAIL {rel}: {ex.Message}");
            }
        }
        AssetDatabase.Refresh();
        string diskId = ReadPackageIdFromDisk() ?? "(unread)";
        long pvLen = 0;
        try
        {
            string pv = Path.Combine(projectRoot, "Assets", "Editor", "DemonKnightPackageVersion.cs");
            if (File.Exists(pv)) pvLen = new FileInfo(pv).Length;
        }
        catch { /* ignore */ }
        log.AppendLine($"Wrote {ok}/{hosts.Length}. Package Id on disk: {diskId} ({pvLen} bytes)");
        log.AppendLine("Wait compile → Diagnose → reopen Art & Stage Architect.");
        return log.ToString();
    }

    static string DownloadHardTipOrMain(string rel, string tipSha)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks + "&r=" + Guid.NewGuid().ToString("N");
        string[] urls =
        {
            RawBase + tipSha + "/" + rel + "?" + bust,
            RawBase + tipSha + "/" + rel,
            RawBase + "main/" + rel + "?" + bust,
            RawBase + "main/" + rel,
            JsDelivrBase + tipSha + "/" + rel + "?" + bust,
            JsDelivrBase + "main/" + rel + "?" + bust,
        };
        Exception last = null;
        foreach (string url in urls)
        {
            try
            {
                using var wc = new WebClient();
                wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
                wc.Headers.Add("User-Agent", "DemonKnight-BreakGlass/1.0");
                wc.Headers.Add("Cache-Control", "no-cache, no-store");
                string body = wc.DownloadString(url);
                if (IsTinyHttpFailure(body) || body.Length < 80) throw new Exception("tiny/404");
                return body;
            }
            catch (Exception ex) { last = ex; }
        }
        throw last ?? new Exception("download failed " + rel);
    }

    static bool IsTinyHttpFailure(string body)
    {
        if (string.IsNullOrEmpty(body)) return true;
        if (body.Length >= 512) return false;
        string t = body.TrimStart();
        return t.StartsWith("404", StringComparison.Ordinal)
               || t.IndexOf("404: Not Found", StringComparison.OrdinalIgnoreCase) >= 0
               || t.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    static string ReadPackageIdFromDisk()
    {
        try
        {
            string path = Path.Combine(Application.dataPath, "Editor", "DemonKnightPackageVersion.cs");
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path);
            int i = text.IndexOf("public const string Id", StringComparison.Ordinal);
            if (i < 0) return null;
            int q1 = text.IndexOf('"', i);
            int q2 = q1 >= 0 ? text.IndexOf('"', q1 + 1) : -1;
            return (q1 >= 0 && q2 > q1) ? text.Substring(q1 + 1, q2 - q1 - 1) : null;
        }
        catch { return null; }
    }

    /// <summary>Diagnose proof: on-disk PackageVersion byte length + Id (catch 381B stuck tip).</summary>
    public static string PackageVersionDiskReport()
    {
        try
        {
            string path = Path.Combine(Application.dataPath, "Editor", "DemonKnightPackageVersion.cs");
            if (!File.Exists(path))
                return "PackageVersion on disk: MISSING (0 bytes) Id=(none)";
            long len = new FileInfo(path).Length;
            string id = ReadPackageIdFromDisk() ?? "(unparsed)";
            string tipHint = len < 500 ? " ⚠ TINY — stale tip SHA / need break-glass ≥500B" : "";
            return $"PackageVersion on disk: {len} bytes Id={id}{tipHint}";
        }
        catch (Exception ex)
        {
            return "PackageVersion on disk: ERROR " + ex.Message;
        }
    }

    static string DownloadRawMainOnly(string rel)
    {
        Exception last = null;
        string tip = null;
        try
        {
            using var wc = new WebClient();
            wc.Headers.Add("User-Agent", "DemonKnight-ForceUpgradeHosts/1.0");
            string json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/commits/main");
            int i = json.IndexOf("\"sha\"", StringComparison.Ordinal);
            if (i >= 0)
            {
                int q1 = json.IndexOf('"', i + 5);
                int q2 = q1 >= 0 ? json.IndexOf('"', q1 + 1) : -1;
                // API returns "sha": "..." — find value after colon
                int colon = json.IndexOf(':', i);
                q1 = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
                q2 = q1 >= 0 ? json.IndexOf('"', q1 + 1) : -1;
                if (q1 >= 0 && q2 > q1 + 30)
                    tip = json.Substring(q1 + 1, q2 - q1 - 1);
            }
        }
        catch { /* tip optional */ }

        for (int attempt = 0; attempt < 4; attempt++)
        {
            string bust = "t=" + DateTime.UtcNow.Ticks + "&r=" + Guid.NewGuid().ToString("N");
            var urls = new List<string>();
            if (!string.IsNullOrEmpty(tip))
            {
                urls.Add(RawBase + tip + "/" + rel + "?" + bust);
                urls.Add(RawBase + tip + "/" + rel);
                urls.Add(JsDelivrBase + tip + "/" + rel + "?" + bust);
            }
            urls.Add(RawBase + "main/" + rel + "?" + bust);
            urls.Add(RawBase + "main/" + rel);
            urls.Add(JsDelivrBase + "main/" + rel + "?" + bust);
            foreach (string url in urls)
            {
                try
                {
                    using var wc = new WebClient();
                    wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
                    wc.Headers.Add("User-Agent", "DemonKnight-ForceUpgradeHosts/1.0");
                    wc.Headers.Add("Cache-Control", "no-cache, no-store");
                    string body = wc.DownloadString(url);
                    // Do NOT body.Contains("404: Not Found") — Sync/Bootstrap source contains that
                    // literal and would false-positive reject a good download.
                    if (!ArthurianSyncFromGitHub.LooksLikeHttpFailureBody(body) && body.Length > 80)
                    {
                        // Reject stuck legacy package when tip CDN returns stale @main.
                        if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase)
                            && body.Contains("sync-keep-editor-menus")
                            && !body.Contains("force-upgrade-sync-hosts"))
                            throw new Exception("stale PackageVersion sync-keep-editor-menus");
                        return body;
                    }
                }
                catch (Exception ex) { last = ex; }
            }
        }
        throw last ?? new Exception("raw tip/main failed for " + rel);
    }

    /// <summary>
    /// DA-59 CS0101 fix: DELETE Phase 2–5 standalones that redefine embedded types.
    /// Never re-download ModularSpriteLibrary / StageLayout / Dice / Anim / Parallax .cs.
    /// </summary>
    [MenuItem("Arthurian/Delete Phase 2–5 Duplicate Standalones", false, 4)]
    [MenuItem("Arthurian/Stub Phase 2–5 Duplicate Standalones", false, 5)]
    [MenuItem("Arthurian/Fetch Missing Phase 2–5 Scripts", false, 6)]
    public static void FetchMissingPhase25Scripts()
    {
        if (!EditorUtility.DisplayDialog(
                "Delete Phase 2–5 Duplicates",
                "DELETE standalone Phase 2–5 .cs files that contain public class\n"
                + "(CS0101 against embeds in GameDataTypes / PaperDollSeatView / GameLoopManager / TavernDice).\n\n"
                + "Also fetches Art Architect editor helpers if missing.\n"
                + "If CS0101 persists: manually delete Assets/Scripts/Data/ModularSpriteLibrary.cs then recompile.",
                "Delete Now", "Cancel"))
            return;

        int ok = WritePhase25StubsInternal(out string report);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            ok >= 0 ? "Phase 2–5 Delete Done" : "Phase 2–5 Delete Incomplete",
            report, "OK");
    }

    /// <summary>Legacy name — now deletes standalones (never re-downloads type bodies).</summary>
    public static int FetchPhase25ScriptsInternal(out string report)
    {
        return WritePhase25StubsInternal(out report);
    }

    /// <summary>DELETE Phase 2–5 standalones with public class; fetch editor helpers only.</summary>
    public static int WritePhase25StubsInternal(out string report)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        var errors = new List<string>();
        int ok = 0;

        try
        {
            for (int i = 0; i < Phase25StandaloneKillList.Length; i++)
            {
                string rel = Phase25StandaloneKillList[i];
                EditorUtility.DisplayProgressBar("Phase 2–5 Delete", rel, (float)i / Phase25StandaloneKillList.Length);
                try
                {
                    string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(dest)) continue;
                    string cur = File.ReadAllText(dest);
                    bool hasType = cur.IndexOf("public class ", StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("public partial class ", StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class ModularSpriteLibrary", StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class StageLayoutProfile", StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class SpriteAnimClip", StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class ModularSpriteAnimator", StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class StageParallaxController", StringComparison.Ordinal) >= 0
                                   || cur.IndexOf("class DiceManager", StringComparison.Ordinal) >= 0;
                    if (!hasType)
                    {
                        // Comment-only stub — still delete so Sync never leaves a ghost path.
                        hasType = true;
                    }
                    try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }
                    File.Delete(dest);
                    string meta = dest + ".meta";
                    if (File.Exists(meta))
                    {
                        try { File.SetAttributes(meta, FileAttributes.Normal); } catch { /* ignore */ }
                        try { File.Delete(meta); } catch { /* ignore */ }
                    }
                    ok++;
                    Debug.Log("[DemonKnight] Phase 2–5 DELETED " + rel);
                }
                catch (Exception ex)
                {
                    errors.Add($"{rel}: {ex.Message}");
                }
            }

            // Editor helpers only (not embedded) by absolute URL.
            foreach (string rel in Phase25ScriptPaths)
            {
                try
                {
                    string body = DownloadPhase25Absolute(rel, Phase25FetchPinSha, out string source);
                    if (string.IsNullOrEmpty(body) || body.Length < 40) continue;
                    if (body.IndexOf("public class ", StringComparison.Ordinal) < 0) continue;
                    string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.WriteAllText(dest, body);
                    ok++;
                    Debug.Log($"[DemonKnight] Phase 2–5 fetched editor helper {rel} via {source}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{rel}: {ex.Message}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        report = $"Deleted Phase 2–5 standalones / fetched editor helpers ({ok} ops).\n"
                 + "Types live in GameDataTypes / PaperDoll / GameLoop / TavernDice.\n"
                 + "If CS0101 remains: manually delete Assets/Scripts/Data/ModularSpriteLibrary.cs then recompile.\n";
        if (errors.Count > 0)
            report += "\nFailures:\n" + string.Join("\n", errors);
        else
            report += "\nWait for compile — CS0101 duplicates should be gone.";
        return ok;
    }

    static string DownloadPhase25Absolute(string rel, string pinSha, out string source)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks;
        string[] urls =
        {
            RawBase + pinSha + "/" + rel + "?" + bust,
            RawBase + pinSha + "/" + rel,
            RawBase + "main/" + rel + "?" + bust,
            RawBase + "main/" + rel,
            JsDelivrBase + pinSha + "/" + rel + "?" + bust,
            JsDelivrBase + pinSha + "/" + rel,
            JsDelivrBase + "main/" + rel,
        };
        Exception last = null;
        foreach (string url in urls)
        {
            try
            {
                using var wc = new WebClient();
                wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
                wc.Headers.Add("User-Agent", "DemonKnight-Phase25-Fetch/1.0");
                wc.Headers.Add("Cache-Control", "no-cache, no-store");
                string body = wc.DownloadString(url);
                if (!ArthurianSyncFromGitHub.LooksLikeHttpFailureBody(body) && body.Length > 40)
                {
                    source = url;
                    return body;
                }
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }
        throw last ?? new Exception("all absolute URLs failed for " + rel);
    }
}
#endif
