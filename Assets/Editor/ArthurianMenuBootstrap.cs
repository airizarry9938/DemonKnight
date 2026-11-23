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
    public const string ArtToolsPinSha = "31ee456c2f85cbb7ba5c8ae7c4918ea7513b4ed1";

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
    /// <summary>Phase 2–5 standalones to DELETE when they redefine embeds. Profiles are real named MonoScripts (tip fix-gdt-profile-always-compile).</summary>
    public static readonly string[] Phase25StandaloneKillList =
    {
        "Assets/Scripts/Data/SpriteAnimClip.cs",
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
        // Auto-escape if still on breakglass / disk-tip-proof before reporting.
        try
        {
            if (DemonKnightTipEscape.NeedsEscape())
            {
                Debug.LogWarning("[DemonKnight] Diagnose: hosts stale — TipEscape now.");
                DemonKnightTipEscape.EscapeNow(force: true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] Diagnose TipEscape skip: " + ex.Message);
            try { EscapeHostsFromMainFallback(ex.Message); } catch { /* ignore */ }
        }

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
            $"Phase 2–5 StageLayoutProfile (named MonoScript): {(hasStageLayout ? "YES" : "MISSING — Force Sync StageLayoutProfile.cs")}\n" +
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

        string rebuild = ArthurianStageLayoutProfileEnsure.RebuildPewLibraryFromSplices();
        string wipe = "";
        try { wipe = DemonKnightPackageVersion.WipeModularPaperdollAssets(); } catch { /* ignore */ }
        string report =
            wipe + "\n" + rebuild + "\n" +
            "StageLayoutProfile is pew/battle SLOTS only — not required for sprite wipe.\n" +
            "Stop Play → New Game. Bind log: body=<spliced> outfit=NULL head=NULL.";
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

        string rebuild = ArthurianStageLayoutProfileEnsure.RebuildPewLibraryFromSplices();
        TownsfolkVisualApplier.SetLibrary(null);
        ArthurianStageLayoutProfileEnsure.EnsureAsset();

        art = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(artPath);
        var res = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resPath);
        int poses = art != null ? CountSprites(art.basePoses) : 0;
        int outfits = art != null ? CountSprites(art.outfits) : 0;
        int resPoses = res != null ? CountSprites(res.basePoses) : 0;
        int resOutfits = res != null ? CountSprites(res.outfits) : -1;

        string report =
            rebuild + "\n" +
            $"Source: {artPath}\n" +
            $"spliced sprites {poses} · outfits {outfits} (want 0)\n" +
            $"Runtime copy: {resPath} · Resources sprites={resPoses} outfits={resOutfits}\n" +
            (poses == 0
                ? "No Spliced/Sprite yet — pews are solid color (not Pose_/Outfit_ shapes).\nArchitect Tab 1 → Save Splices.\n"
                : "") +
            "Stop Play → New Game. Bind: body=<spliced> outfit=NULL head=NULL\n" +
            DemonKnightPackageVersion.WipeStatusForDiagnose();

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

    /// <summary>
    /// Precompile CS0246 belt: HardWrite named Profile MonoScripts from embeds even when
    /// TipEscape/Sync hosts are mid-upgrade. tip: fix-gdt-profile-always-compile
    /// </summary>
    [InitializeOnLoadMethod]
    static void DkHardWriteNamedProfilesOnLoad()
    {
        try
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            int n = HardWriteNamedProfilesFromEmbeds(root);
            if (n > 0)
            {
                Debug.Log("[DemonKnight] MenuBootstrap OnLoad HardWriteEmbed Profiles wrote " + n);
                EditorApplication.delayCall += () => AssetDatabase.Refresh();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] MenuBootstrap HardWriteEmbed OnLoad: " + ex.Message);
        }
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
    public const string BreakGlassTipSha = "fa8771cc844a15e218cddb9a42f7170f4f561fac";

    /// <summary>
    /// Lives in MenuBootstrap so skip-self Sync hosts still get a one-click upgrade.
    /// Break-glass FIRST when PackageVersion is tiny / Sync lacks LooksLikeHttpFailureBody —
    /// never trust stale Sync ForceOverwrite (lands 381B PackageVersion).
    /// </summary>
    [MenuItem("Arthurian/FORCE Upgrade Sync Hosts From main", false, 2)]
    public static void ForceUpgradeSyncHostsFromMain()
    {
        // NEVER call Sync.ForceUpgradeSyncHostsMenu — breakglass Sync is HardTip-first and
        // re-lands architect-breakglass-hosts / architect-disk-tip-proof forever.
        // Always TipEscape: API tip + raw@main only.
        if (!EditorUtility.DisplayDialog(
                "FORCE Upgrade → Tip Escape",
                "Pull PackageVersion / Sync / Bootstrap / Architect / TipEscape / PaperDoll\n"
                + "from GitHub tip of main (never HardTip).\n\n"
                + "Diagnose after compile must show: lore-boss-wave-order\n"
                + "(compiled Id == disk Id).",
                "Escape Now", "Cancel"))
            return;

        string report;
        try
        {
            report = DemonKnightTipEscape.EscapeNow(force: true);
        }
        catch (Exception ex)
        {
            // TipEscape type missing (pre-tip Bootstrap) — fall back to main-only host write.
            report = EscapeHostsFromMainFallback(ex.Message);
        }
        EditorUtility.DisplayDialog("FORCE Upgrade / Tip Escape", report, "OK");
    }

    /// <summary>When TipEscape.cs not compiled yet — still pull tip PackageVersion from raw@main.</summary>
    static string EscapeHostsFromMainFallback(string why)
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("TipEscape type missing (" + why + ") — main-only fallback.");
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string tip = null;
        try
        {
            using var wc = new System.Net.WebClient();
            wc.Headers.Add("User-Agent", "DemonKnight-FORCE-EscapeFallback");
            wc.Headers.Add("Accept", "application/vnd.github.v3+json");
            string json = wc.DownloadString("https://api.github.com/repos/airizarry9938/DemonKnight/commits/main");
            int colon = json.IndexOf(':');
            int q1 = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
            int q2 = q1 >= 0 ? json.IndexOf('"', q1 + 1) : -1;
            if (q1 >= 0 && q2 == q1 + 41) tip = json.Substring(q1 + 1, 40);
        }
        catch { /* ignore */ }
        if (string.IsNullOrEmpty(tip)) tip = "main";

        string root = Directory.GetParent(Application.dataPath)!.FullName;
        string[] rels =
        {
            "Assets/Editor/DemonKnightTipEscape.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            "Assets/Scripts/UI/SomersetPewLayout.cs",
            "Assets/Scripts/Data/GameDataTypes.cs",
            "Assets/Scripts/UI/SceneBackgroundApplier.cs",
            "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
            "Assets/Scripts/UI/SomersetChurchUI.cs",
            "Assets/Scripts/UI/TownSplitUIController.cs",
            "Assets/Editor/ArthurianSyncFromGitHub.cs",
            "Assets/Editor/ArthurianMenuBootstrap.cs",
            "Assets/Editor/ArthurianArtArchitectWindow.cs",
            "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
            "Assets/Scripts/UI/PaperDollSeatView.cs",
            "Assets/Scripts/UI/TownsfolkVisualApplier.cs",
        };
        int ok = 0;
        foreach (string rel in rels)
        {
            try
            {
                string bust = "t=" + DateTime.UtcNow.Ticks;
                string body = null;
                foreach (string sha in new[] { tip, "main" })
                {
                    string url = "https://raw.githubusercontent.com/airizarry9938/DemonKnight/"
                                 + sha + "/" + rel + "?" + bust;
                    try
                    {
                        using var wc = new System.Net.WebClient();
                        wc.Headers.Add("User-Agent", "DemonKnight-FORCE-EscapeFallback");
                        wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(
                            System.Net.Cache.RequestCacheLevel.BypassCache);
                        body = wc.DownloadString(url);
                        if (!string.IsNullOrEmpty(body) && body.Length > 80
                            && !body.TrimStart().StartsWith("404", StringComparison.Ordinal))
                            break;
                        body = null;
                    }
                    catch { body = null; }
                }
                if (string.IsNullOrEmpty(body)) throw new Exception("download failed");
                if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase)
                    && (body.IndexOf("architect-breakglass-hosts", StringComparison.Ordinal) >= 0
                        || body.IndexOf("architect-disk-tip-proof", StringComparison.Ordinal) >= 0)
                    && body.IndexOf("lore-boss-wave-order", StringComparison.Ordinal) < 0
                    && body.IndexOf("force-sync-appliers-hardfix", StringComparison.Ordinal) < 0)
                    throw new Exception("stale PackageVersion body");
                if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase)
                    && body.IndexOf("WipeModularPaperdollAssets", StringComparison.Ordinal) >= 0)
                    throw new Exception("Architect still calls PackageVersion.Wipe* (CS0117)");
                string dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                if (File.Exists(dest))
                {
                    try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }
                    try { File.Delete(dest); } catch { /* ignore */ }
                }
                File.WriteAllText(dest, body);
                ok++;
                log.AppendLine($"OK {rel} ({body.Length} B)");
            }
            catch (Exception ex)
            {
                log.AppendLine($"MISS {rel}: {ex.Message}");
            }
        }
        AssetDatabase.Refresh();
        log.AppendLine($"Wrote {ok}/{rels.Length}. Wait compile → click FORCE again if TipEscape menu missing.");
        log.AppendLine("Diagnose must show lore-boss-wave-order compiled=disk.");
        return log.ToString();
    }

    /// <summary>Andrew's reliable menu while Escape missing — tip/main only (never HardTip-first).</summary>
    [MenuItem("Arthurian/Break-Glass Pull Tip Hosts (hard SHA)", false, 2)]
    public static void BreakGlassPullTipHostsMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Break-Glass Pull Tip Hosts",
                "Overwrite from GitHub tip of main (never HardTip-first):\n\n"
                + "• TipEscape + PackageVersion + Sync + Bootstrap + Architect\n"
                + "• SomersetChurchUI + SomersetPewLayout (3-row / scale)\n"
                + "• PaperDoll + TownsfolkVisualApplier\n\n"
                + "After compile: Diagnose = lore-boss-wave-order\n"
                + "Architect title shows lore-boss-wave-order + church pew preview.",
                "Download Tip Now", "Cancel"))
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
                || id == "runtime-single-sprite" || id == "single-sprite-play-wipe"
                || id == "single-sprite-auto-wipe" || id == "spliced-sprite-escape"
                || id == "spliced-sprite-pews")
                return true;
            if (ArthurianSyncFromGitHub.IsStalePackageId(id)) return true;
            return false;
        }
        catch { return true; }
    }

    /// <summary>
    /// Atomic tip pull: TipEscape + PackageVersion + Sync + Bootstrap + Architect + StageLayout.
    /// API tip + raw@main FIRST — HardTip last and rejected if stale Id.
    /// </summary>
    public static string BreakGlassForceHostsFromHardTip()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string tip = TryResolveMainShaForBreakGlass() ?? "main";
        string[] hosts =
        {
            "Assets/Editor/DemonKnightTipEscape.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            // PewLayout BEFORE Architect so SeatShowsSprite exists if Architect still calls it.
            "Assets/Scripts/UI/SomersetPewLayout.cs",
            "Assets/Scripts/Data/GameDataTypes.cs",
            "Assets/Scripts/UI/SceneBackgroundApplier.cs",
            "Assets/Scripts/UI/ClassSpritePoolApplier.cs",
            "Assets/Scripts/UI/SomersetChurchUI.cs",
            "Assets/Scripts/UI/TownSplitUIController.cs",
            "Assets/Editor/ArthurianSyncFromGitHub.cs",
            "Assets/Editor/ArthurianMenuBootstrap.cs",
            "Assets/Editor/ArthurianArtArchitectWindow.cs",
            "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
            "Assets/Editor/ArthurianFixMisplacedScripts.cs",
            "Assets/Scripts/UI/PaperDollSeatView.cs",
            "Assets/Scripts/UI/TownsfolkVisualApplier.cs",
        };
        const string wantId = "lore-boss-wave-order";
        int ok = 0;
        var log = new System.Text.StringBuilder();
        log.AppendLine("Break-Glass atomic tip pull (tip/main FIRST, HardTip last)");
        log.AppendLine("API tip=" + tip + " want package=" + wantId);
        foreach (string rel in hosts)
        {
            try
            {
                string body = DownloadTipMainThenHardTip(rel, tip, BreakGlassTipSha);
                if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (body.Length < 500)
                        throw new Exception("PackageVersion still tiny " + body.Length + "B");
                    if (body.IndexOf("Id = \"" + wantId + "\"", StringComparison.Ordinal) < 0
                        && body.IndexOf(wantId, StringComparison.Ordinal) < 0)
                        throw new Exception("PackageVersion missing " + wantId);
                    if (body.IndexOf("Id = \"architect-breakglass-hosts\"", StringComparison.Ordinal) >= 0
                        || body.IndexOf("Id = \"architect-disk-tip-proof\"", StringComparison.Ordinal) >= 0)
                        throw new Exception("rejected stale PackageVersion Id");
                }
                if (rel.EndsWith("DemonKnightTipEscape.cs", StringComparison.OrdinalIgnoreCase)
                    && body.IndexOf("Escape to Tip", StringComparison.Ordinal) < 0)
                    throw new Exception("TipEscape body missing Escape menu");
                if (rel.EndsWith("ArthurianArtArchitectWindow.cs", StringComparison.OrdinalIgnoreCase)
                    && body.IndexOf("WipeModularPaperdollAssets", StringComparison.Ordinal) >= 0)
                    throw new Exception("Architect still calls PackageVersion.Wipe* (CS0117)");
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
        try { DemonKnightPackageVersion.NukeDuplicatePackageVersionOutsideEditor(); } catch { /* ignore */ }
        AssetDatabase.Refresh();
        string diskId = ReadPackageIdFromDisk() ?? "(unread)";
        long pvLen = 0;
        try
        {
            string pv = Path.Combine(projectRoot, "Assets", "Editor", "DemonKnightPackageVersion.cs");
            if (File.Exists(pv)) pvLen = new FileInfo(pv).Length;
        }
        catch { /* ignore */ }
        bool tipEscapeOnDisk = File.Exists(Path.Combine(projectRoot, "Assets", "Editor", "DemonKnightTipEscape.cs"));
        bool pewLayoutOnDisk = File.Exists(Path.Combine(projectRoot, "Assets", "Scripts", "UI", "SomersetPewLayout.cs"));
        log.AppendLine($"Wrote {ok}/{hosts.Length}. Package Id on disk: {diskId} ({pvLen} bytes)");
        log.AppendLine("TipEscape.cs on disk: " + (tipEscapeOnDisk ? "YES → Escape menu after compile" : "NO"));
        log.AppendLine("SomersetPewLayout.cs on disk: " + (pewLayoutOnDisk ? "YES (SeatShowsSprite)" : "NO"));
        log.AppendLine("Wait compile → Diagnose (" + wantId + ") → !!! Escape to Tip if still stale.");
        return log.ToString();
    }

    static string TryResolveMainShaForBreakGlass()
    {
        try
        {
            using var wc = new WebClient();
            wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
            wc.Headers.Add("User-Agent", "DemonKnight-BreakGlass/2.0");
            wc.Headers.Add("Accept", "application/vnd.github.v3+json");
            string json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/commits/main");
            int colon = json.IndexOf(':');
            int q1 = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
            int q2 = q1 >= 0 ? json.IndexOf('"', q1 + 1) : -1;
            if (q1 >= 0 && q2 == q1 + 41)
            {
                string sha = json.Substring(q1 + 1, 40);
                if (System.Text.RegularExpressions.Regex.IsMatch(sha, "^[0-9a-f]{40}$"))
                    return sha;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>API tip + main FIRST; HardTip last (and only if tip/main fail).</summary>
    static string DownloadTipMainThenHardTip(string rel, string tipSha, string hardTip)
    {
        string bust = "t=" + DateTime.UtcNow.Ticks + "&r=" + Guid.NewGuid().ToString("N");
        var shas = new List<string>();
        if (!string.IsNullOrEmpty(tipSha)) shas.Add(tipSha);
        shas.Add("main");
        if (!string.IsNullOrEmpty(hardTip)
            && !string.Equals(hardTip, tipSha, StringComparison.OrdinalIgnoreCase))
            shas.Add(hardTip);
        Exception last = null;
        foreach (string sha in shas)
        {
            string[] urls =
            {
                RawBase + sha + "/" + rel + "?" + bust,
                RawBase + sha + "/" + rel,
                JsDelivrBase + sha + "/" + rel + "?" + bust,
            };
            foreach (string url in urls)
            {
                try
                {
                    using var wc = new WebClient();
                    wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
                    wc.Headers.Add("User-Agent", "DemonKnight-BreakGlass/2.0");
                    wc.Headers.Add("Cache-Control", "no-cache, no-store");
                    string body = wc.DownloadString(url);
                    if (IsTinyHttpFailure(body) || body.Length < 80) throw new Exception("tiny/404");
                    // Never accept breakglass/disk-tip-proof PackageVersion from HardTip.
                    if (rel.EndsWith("DemonKnightPackageVersion.cs", StringComparison.OrdinalIgnoreCase)
                        && (body.IndexOf("Id = \"architect-breakglass-hosts\"", StringComparison.Ordinal) >= 0
                            || body.IndexOf("Id = \"architect-disk-tip-proof\"", StringComparison.Ordinal) >= 0)
                        && body.IndexOf("lore-boss-wave-order", StringComparison.Ordinal) < 0
                        && body.IndexOf("force-sync-appliers-hardfix", StringComparison.Ordinal) < 0)
                    {
                        last = new Exception("stale PackageVersion from " + sha);
                        continue;
                    }
                    return body;
                }
                catch (Exception ex) { last = ex; }
            }
        }
        throw last ?? new Exception("download failed " + rel);
    }

    static string DownloadHardTipOrMain(string rel, string tipSha)
        => DownloadTipMainThenHardTip(rel, tipSha, tipSha);

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

    /// <summary>
    /// Nuclear CS0246 belt: HardWrite five named Profile MonoScripts from embeds.
    /// tip: fix-gdt-profile-always-compile
    /// </summary>
    public static int HardWriteNamedProfilesFromEmbeds(string root = null, System.Text.StringBuilder log = null)
    {
        if (string.IsNullOrEmpty(root))
            root = Directory.GetParent(Application.dataPath)!.FullName;
        int wrote = 0;
        foreach (var (rel, b64) in NamedProfileEmbeds.All)
        {
            try
            {
                string dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                bool need = true;
                if (File.Exists(dest))
                {
                    string existing = File.ReadAllText(dest);
                    string className = Path.GetFileNameWithoutExtension(rel);
                    need = existing.Length < 200
                           || existing.IndexOf(NamedProfileEmbeds.TipId, StringComparison.Ordinal) < 0
                           || existing.IndexOf("class " + className, StringComparison.Ordinal) < 0;
                    if (!need && rel.EndsWith("StageLayoutProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("class SlotTransformConfig", StringComparison.Ordinal) >= 0
                               || existing.IndexOf("class StageParallaxLayer", StringComparison.Ordinal) >= 0;
                    if (!need && rel.EndsWith("SceneBackgroundProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("enum SceneBackgroundSlot", StringComparison.Ordinal) >= 0
                               || existing.IndexOf("class SceneBackgroundEntry", StringComparison.Ordinal) >= 0;
                    if (!need && rel.EndsWith("BattleGoodEvilSpritePoolProfile.cs", StringComparison.OrdinalIgnoreCase))
                        need = existing.IndexOf("class BattleSpriteSlotEntry", StringComparison.Ordinal) >= 0;
                }
                if (!need) continue;

                string body = NamedProfileEmbeds.Decode(b64);
                if (string.IsNullOrEmpty(body) || body.Length < 200)
                {
                    log?.AppendLine("MISS embed " + rel);
                    Debug.LogWarning("[DemonKnight] MenuBootstrap HardWrite miss " + rel);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                try
                {
                    if (File.Exists(dest))
                    {
                        File.SetAttributes(dest, FileAttributes.Normal);
                        File.Delete(dest);
                    }
                }
                catch { /* ignore */ }
                File.WriteAllText(dest, body);
                wrote++;
                Debug.Log("[DemonKnight] MenuBootstrap HardWriteEmbed " + rel + " (" + body.Length + " B)");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] MenuBootstrap HardWriteEmbed FAIL " + rel + ": " + ex.Message);
            }
        }
        return wrote;
    }


        // ===== NUCLEAR NAMED PROFILE EMBEDS (tip fix-gdt-profile-always-compile) =====
    // Full tip Profile MonoScript sources as base64. HardWrite BEFORE Force Delete / compile.
    // Helpers (SlotTransformConfig etc.) live in GameDataTypes — these files are SO-only.
    public static class NamedProfileEmbeds
    {
        public const string TipId = "fix-gdt-profile-always-compile";

        // Assets/Scripts/Data/StageLayoutProfile.cs (9102 bytes)
        const string B64_StageLayoutProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgU08gb25seSAoaGVscGVycyBsaXZlIGluIEdhbWVEYXRhVHlwZXMpCiAgICAvLy8gPHN1bW1hcnk+CiAg" +
            "ICAvLy8gTnVtYmVyZWQgU29tZXJzZXQgY2h1cmNoIHBld3MgKDUyKSwgcGxheWVyIGJhdHRsZSBzbG90cyAoNTIpLCBlbmVteSBz" +
            "bG90cyAoMjApLAogICAgLy8vIHBsdXMgcGFyYWxsYXggLyBhdG1vc3BoZXJlIHRvZ2dsZXMuIFJlc291cmNlcyBwYXRoOiBTdGFn" +
            "ZUxheW91dFByb2ZpbGUuCiAgICAvLy8gPC9zdW1tYXJ5PgogICAgW0NyZWF0ZUFzc2V0TWVudShmaWxlTmFtZSA9ICJTdGFnZUxh" +
            "eW91dFByb2ZpbGUiLCBtZW51TmFtZSA9ICJEZW1vbktuaWdodC9TdGFnZSBMYXlvdXQgUHJvZmlsZSIpXQogICAgcHVibGljIGNs" +
            "YXNzIFN0YWdlTGF5b3V0UHJvZmlsZSA6IFNjcmlwdGFibGVPYmplY3QKICAgIHsKICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5n" +
            "IFJlc291cmNlc1BhdGggPSAiU3RhZ2VMYXlvdXRQcm9maWxlIjsKICAgICAgICBwdWJsaWMgY29uc3QgaW50IENodXJjaFBld0Nv" +
            "dW50ID0gNTI7CiAgICAgICAgcHVibGljIGNvbnN0IGludCBQbGF5ZXJCYXR0bGVDb3VudCA9IDUyOwogICAgICAgIHB1YmxpYyBj" +
            "b25zdCBpbnQgRW5lbXlCYXR0bGVDb3VudCA9IDIwOwogICAgICAgIC8vLyA8c3VtbWFyeT5Hb3NzaXAgLyBwZXcgcm93IHNpemUg" +
            "KHBld1NlYXRJbmRleCAvIDEzKS48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIGNvbnN0IGludCBQZXdSb3dTaXplID0gMTM7Cgog" +
            "ICAgICAgIHB1YmxpYyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+IGNodXJjaFBld1Nsb3RzID0gbmV3IExpc3Q8U2xvdFRyYW5z" +
            "Zm9ybUNvbmZpZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTbG90VHJhbnNmb3JtQ29uZmlnPiBwbGF5ZXJCYXR0bGVTbG90cyA9" +
            "IG5ldyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U2xvdFRyYW5zZm9ybUNvbmZpZz4g" +
            "ZW5lbXlCYXR0bGVTbG90cyA9IG5ldyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U3Rh" +
            "Z2VQYXJhbGxheExheWVyPiBwYXJhbGxheExheWVycyA9IG5ldyBMaXN0PFN0YWdlUGFyYWxsYXhMYXllcj4oKTsKCiAgICAgICAg" +
            "cHVibGljIGJvb2wgZW5hYmxlUmFpbkZYOwogICAgICAgIHB1YmxpYyBib29sIGVuYWJsZUxpZ2h0bmluZ0ZsYXNoZXM7CiAgICAg" +
            "ICAgcHVibGljIGJvb2wgZW5hYmxlQ2FuZGxlRmxpY2tlciA9IHRydWU7CgogICAgICAgIC8vLyA8c3VtbWFyeT5UcnVlIHdoZW4g" +
            "dGhpcyBpcyBhIENyZWF0ZUluc3RhbmNlIHN0YW5kLWluIChub3QgYSBSZXNvdXJjZXMgYXNzZXQpLjwvc3VtbWFyeT4KICAgICAg" +
            "ICBbU3lzdGVtLk5vblNlcmlhbGl6ZWRdIHB1YmxpYyBib29sIGlzUnVudGltZVN0YW5kSW47CgogICAgICAgIHB1YmxpYyBzdGF0" +
            "aWMgU3RhZ2VMYXlvdXRQcm9maWxlIExvYWQoKQogICAgICAgIHsKICAgICAgICAgICAgU3RhZ2VMYXlvdXRQcm9maWxlIHAgPSBu" +
            "dWxsOwogICAgICAgICAgICB0cnkgeyBwID0gUmVzb3VyY2VzLkxvYWQ8U3RhZ2VMYXlvdXRQcm9maWxlPihSZXNvdXJjZXNQYXRo" +
            "KTsgfQogICAgICAgICAgICBjYXRjaCB7IC8qIG1pc3NpbmcgTW9ub1NjcmlwdCBHVUlEIGFmdGVyIGVtYmVkIOKAlCBmYWxsIHRo" +
            "cm91Z2ggKi8gfQogICAgICAgICAgICBpZiAocCAhPSBudWxsKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBwLkVuc3Vy" +
            "ZVNsb3RDb3VudHMoKTsKICAgICAgICAgICAgICAgIHAuaXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAgICAgICAg" +
            "cC5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAgICAgICAgICAgICAgcmV0dXJuIHA7CiAgICAgICAgICAgIH0K" +
            "ICAgICAgICAgICAgdHJ5CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBhbGwgPSBSZXNvdXJjZXMuTG9hZEFsbDxT" +
            "dGFnZUxheW91dFByb2ZpbGU+KCIiKTsKICAgICAgICAgICAgICAgIGlmIChhbGwgIT0gbnVsbCkKICAgICAgICAgICAgICAgIHsK" +
            "ICAgICAgICAgICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGFsbC5MZW5ndGg7IGkrKykKICAgICAgICAgICAgICAgICAg" +
            "ICB7CiAgICAgICAgICAgICAgICAgICAgICAgIGlmIChhbGxbaV0gPT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICAg" +
            "ICAgICAgIGFsbFtpXS5FbnN1cmVTbG90Q291bnRzKCk7CiAgICAgICAgICAgICAgICAgICAgICAgIGFsbFtpXS5pc1J1bnRpbWVT" +
            "dGFuZEluID0gZmFsc2U7CiAgICAgICAgICAgICAgICAgICAgICAgIGFsbFtpXS5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRo" +
            "cygpOwogICAgICAgICAgICAgICAgICAgICAgICByZXR1cm4gYWxsW2ldOwogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAg" +
            "ICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgICAgICBjYXRjaCB7IC8qIGJyb2tlbiBSZXNvdXJjZXMgYXNzZXQgKi8gfQog" +
            "ICAgICAgICAgICAvLyBSdW50aW1lIGZhbGxiYWNrIHdoZW4gdGhlIFJlc291cmNlcyBhc3NldCBoYXMgYSBzdGFsZSBzY3JpcHQg" +
            "R1VJRCAoRm9yY2UgU3luYyBlbWJlZCkuCiAgICAgICAgICAgIC8vIE5ldmVyIHdpcGUgYXV0aG9yZWQgUmVzb3VyY2VzIOKAlCBz" +
            "dGFuZC1pbiBpcyBsYXN0IHJlc29ydCBvbmx5LgogICAgICAgICAgICBwID0gQ3JlYXRlSW5zdGFuY2U8U3RhZ2VMYXlvdXRQcm9m" +
            "aWxlPigpOwogICAgICAgICAgICBwLm5hbWUgPSAiU3RhZ2VMYXlvdXRQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLmlz" +
            "UnVudGltZVN0YW5kSW4gPSB0cnVlOwogICAgICAgICAgICBwLkVuc3VyZVNsb3RDb3VudHMoKTsKICAgICAgICAgICAgcmV0dXJu" +
            "IHA7CiAgICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+CiAgICAgICAgLy8vIFJlc3RvcmUgTWlzc2luZyBwZXcvYmF0dGxl" +
            "IHNlYXQgc3ByaXRlcyBmcm9tIHBhdGggYmFja3Vwcy4KICAgICAgICAvLy8gV29ya3MgaW4gcGxheWVyIGJ1aWxkcyB2aWEgUmVz" +
            "b3VyY2VzLkxvYWQgLyBTdHJlYW1pbmdBc3NldHMgKG5vdCBFZGl0b3Itb25seSkuCiAgICAgICAgLy8vIE5ldmVyIGNsZWFycyBw" +
            "YXRoIGJhY2t1cHMgd2hlbiBzcHJpdGVzIGFyZSB0ZW1wb3JhcmlseSBudWxsLgogICAgICAgIC8vLyA8L3N1bW1hcnk+CiAgICAg" +
            "ICAgcHVibGljIGludCBSZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpCiAgICAgICAgewogICAgICAgICAgICBFbnN1cmVT" +
            "bG90Q291bnRzKCk7CiAgICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgbiArPSBSZW1hdGVyaWFsaXplU2xvdExpc3Qo" +
            "Y2h1cmNoUGV3U2xvdHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVTbG90TGlzdChwbGF5ZXJCYXR0bGVTbG90cyk7" +
            "CiAgICAgICAgICAgIG4gKz0gUmVtYXRlcmlhbGl6ZVNsb3RMaXN0KGVuZW15QmF0dGxlU2xvdHMpOwogICAgICAgICAgICByZXR1" +
            "cm4gbjsKICAgICAgICB9CgogICAgICAgIHN0YXRpYyBpbnQgUmVtYXRlcmlhbGl6ZVNsb3RMaXN0KExpc3Q8U2xvdFRyYW5zZm9y" +
            "bUNvbmZpZz4gc2xvdHMpCiAgICAgICAgewogICAgICAgICAgICBpZiAoc2xvdHMgPT0gbnVsbCkgcmV0dXJuIDA7CiAgICAgICAg" +
            "ICAgIGludCByZXN0b3JlZCA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgc2xvdHMuQ291bnQ7IGkrKykKICAg" +
            "ICAgICAgICAgewogICAgICAgICAgICAgICAgdmFyIHMgPSBzbG90c1tpXTsKICAgICAgICAgICAgICAgIGlmIChzID09IG51bGwg" +
            "fHwgcy5wcmV2aWV3T3JPdmVycmlkZVNwcml0ZSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChzdHJpbmcu" +
            "SXNOdWxsT3JFbXB0eShzLnByZXZpZXdBc3NldFBhdGgpKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIFNwcml0ZSBzcCA9IENs" +
            "YXNzU3ByaXRlUG9vbFByb2ZpbGUuVHJ5TG9hZFNwcml0ZUF0UGF0aChzLnByZXZpZXdBc3NldFBhdGgpOwogICAgICAgICAgICAg" +
            "ICAgaWYgKHNwID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgcy5wcmV2aWV3T3JPdmVycmlkZVNwcml0ZSA9IHNw" +
            "OwogICAgICAgICAgICAgICAgcmVzdG9yZWQrKzsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gcmVzdG9yZWQ7CiAg" +
            "ICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+UGFkIC8gdHJpbSBsaXN0cyB0byA1MiAvIDUyIC8gMjAgd2l0aCBkZWZhdWx0" +
            "IHRyYW5zZm9ybXMuPC9zdW1tYXJ5PgogICAgICAgIHB1YmxpYyB2b2lkIEVuc3VyZVNsb3RDb3VudHMoKQogICAgICAgIHsKICAg" +
            "ICAgICAgICAgY2h1cmNoUGV3U2xvdHMgPz89IG5ldyBMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+KCk7CiAgICAgICAgICAgIHBs" +
            "YXllckJhdHRsZVNsb3RzID8/PSBuZXcgTGlzdDxTbG90VHJhbnNmb3JtQ29uZmlnPigpOwogICAgICAgICAgICBlbmVteUJhdHRs" +
            "ZVNsb3RzID8/PSBuZXcgTGlzdDxTbG90VHJhbnNmb3JtQ29uZmlnPigpOwogICAgICAgICAgICBwYXJhbGxheExheWVycyA/Pz0g" +
            "bmV3IExpc3Q8U3RhZ2VQYXJhbGxheExheWVyPigpOwoKICAgICAgICAgICAgRmlsbFNsb3RzKGNodXJjaFBld1Nsb3RzLCBDaHVy" +
            "Y2hQZXdDb3VudCwgQ2h1cmNoUGV3RGVmYXVsdCk7CiAgICAgICAgICAgIEZpbGxTbG90cyhwbGF5ZXJCYXR0bGVTbG90cywgUGxh" +
            "eWVyQmF0dGxlQ291bnQsIFBsYXllckJhdHRsZURlZmF1bHQpOwogICAgICAgICAgICBGaWxsU2xvdHMoZW5lbXlCYXR0bGVTbG90" +
            "cywgRW5lbXlCYXR0bGVDb3VudCwgRW5lbXlCYXR0bGVEZWZhdWx0KTsKICAgICAgICB9CgogICAgICAgIHN0YXRpYyB2b2lkIEZp" +
            "bGxTbG90cyhMaXN0PFNsb3RUcmFuc2Zvcm1Db25maWc+IGxpc3QsIGludCBjb3VudCwgRnVuYzxpbnQsIFNsb3RUcmFuc2Zvcm1D" +
            "b25maWc+IGZhY3RvcnkpCiAgICAgICAgewogICAgICAgICAgICB3aGlsZSAobGlzdC5Db3VudCA8IGNvdW50KQogICAgICAgICAg" +
            "ICAgICAgbGlzdC5BZGQoZmFjdG9yeShsaXN0LkNvdW50KSk7CiAgICAgICAgICAgIGlmIChsaXN0LkNvdW50ID4gY291bnQpCiAg" +
            "ICAgICAgICAgICAgICBsaXN0LlJlbW92ZVJhbmdlKGNvdW50LCBsaXN0LkNvdW50IC0gY291bnQpOwogICAgICAgICAgICBmb3Ig" +
            "KGludCBpID0gMDsgaSA8IGxpc3QuQ291bnQ7IGkrKykKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgaWYgKGxpc3RbaV0g" +
            "PT0gbnVsbCkgbGlzdFtpXSA9IGZhY3RvcnkoaSk7CiAgICAgICAgICAgICAgICBsaXN0W2ldLnNsb3ROdW1iZXIgPSBpOwogICAg" +
            "ICAgICAgICB9CiAgICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+CiAgICAgICAgLy8vIFBsYXktbm9ybWFsaXplZCAw4oCT" +
            "MSBjaHVyY2ggc2VhdCAobWF0Y2hlcyBTb21lcnNldFBld0xheW91dCBkZWZhdWx0cykuCiAgICAgICAgLy8vIFdvcmxkLXNwYWNl" +
            "IGRlZmF1bHRzICgtNi4uNikgd2VyZSBpZ25vcmVkIGJ5IFBvcHVsYXRlUGV3cywgc28gU3RhZ2UgUGxhY2VyCiAgICAgICAgLy8v" +
            "IGVkaXRzIG5ldmVyIHNob3dlZCBpbiBQbGF5IHVudGlsIEFyY2hpdGVjdCByZS1zZWVkZWQg4oCUIGZpeGVkIGhlcmUuCiAgICAg" +
            "ICAgLy8vIDwvc3VtbWFyeT4KICAgICAgICBzdGF0aWMgU2xvdFRyYW5zZm9ybUNvbmZpZyBDaHVyY2hQZXdEZWZhdWx0KGludCBp" +
            "KQogICAgICAgIHsKICAgICAgICAgICAgVmVjdG9yMiBub3JtID0gRGVmYXVsdENodXJjaE5vcm1hbGl6ZWRQb3NpdGlvbihpKTsK" +
            "ICAgICAgICAgICAgcmV0dXJuIG5ldyBTbG90VHJhbnNmb3JtQ29uZmlnCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHNs" +
            "b3ROdW1iZXIgPSBpLAogICAgICAgICAgICAgICAgcG9zaXRpb24gPSBub3JtLAogICAgICAgICAgICAgICAgc2NhbGUgPSBWZWN0" +
            "b3IyLm9uZSwKICAgICAgICAgICAgICAgIGFzcGVjdE11bHRpcGxpZXIgPSBWZWN0b3IyLm9uZSwKICAgICAgICAgICAgICAgIHNv" +
            "cnRpbmdPcmRlciA9IC1NYXRoZi5Sb3VuZFRvSW50KG5vcm0ueSAqIDEwMGYpCiAgICAgICAgICAgIH07CiAgICAgICAgfQoKICAg" +
            "ICAgICAvLy8gPHN1bW1hcnk+Tm9ybWFsaXplZCAw4oCTMSBwZXcgc2VhdCBtYXRjaGluZyBTb21lcnNldFBld0xheW91dC5DcmVh" +
            "dGVEZWZhdWx0UGV3cyBjYXBhY2l0aWVzLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgc3RhdGljIFZlY3RvcjIgRGVmYXVsdENo" +
            "dXJjaE5vcm1hbGl6ZWRQb3NpdGlvbihpbnQgZ2xvYmFsU2VhdEluZGV4KQogICAgICAgIHsKICAgICAgICAgICAgLy8gTWlycm9y" +
            "IFNvbWVyc2V0UGV3TGF5b3V0LkNyZWF0ZURlZmF1bHRQZXdzICh2NCkg4oCUIGtlZXAgaW4gc3luYy4KICAgICAgICAgICAgZmxv" +
            "YXRbXVtdIGNvbFhCeVJvdyA9CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIG5ld1tdIHsgMC4yMjBmLCAwLjUwMGYsIDAu" +
            "NzgwZiB9LAogICAgICAgICAgICAgICAgbmV3W10geyAwLjIyNmYsIDAuNTAwZiwgMC43NzVmIH0sCiAgICAgICAgICAgICAgICBu" +
            "ZXdbXSB7IDAuMjMyZiwgMC41MDBmLCAwLjc3MGYgfSwKICAgICAgICAgICAgICAgIG5ld1tdIHsgMC4yMzhmLCAwLjUwMGYsIDAu" +
            "NzY1ZiB9CiAgICAgICAgICAgIH07CiAgICAgICAgICAgIGZsb2F0W10gcm93WSA9IHsgMC40MDBmLCAwLjU0NWYsIDAuNjkwZiwg" +
            "MC44MjBmIH07CiAgICAgICAgICAgIGZsb2F0W10gcm93U3ByZWFkID0geyAwLjE3NWYsIDAuMTYwZiwgMC4xNDVmLCAwLjEyMGYg" +
            "fTsKICAgICAgICAgICAgaW50WyxdIGNhcHMgPQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB7IDUsIDUsIDUgfSwKICAg" +
            "ICAgICAgICAgICAgIHsgNCwgNSwgNCB9LAogICAgICAgICAgICAgICAgeyA0LCA0LCA0IH0sCiAgICAgICAgICAgICAgICB7IDQs" +
            "IDQsIDQgfQogICAgICAgICAgICB9OwoKICAgICAgICAgICAgaW50IHJlbWFpbmluZyA9IE1hdGhmLkNsYW1wKGdsb2JhbFNlYXRJ" +
            "bmRleCwgMCwgQ2h1cmNoUGV3Q291bnQgLSAxKTsKICAgICAgICAgICAgZm9yIChpbnQgcm93ID0gMDsgcm93IDwgNDsgcm93Kysp" +
            "CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIGZvciAoaW50IGNvbCA9IDA7IGNvbCA8IDM7IGNvbCsrKQogICAgICAgICAg" +
            "ICAgICAgewogICAgICAgICAgICAgICAgICAgIGludCBjYXAgPSBNYXRoZi5NYXgoMSwgY2Fwc1tyb3csIGNvbF0pOwogICAgICAg" +
            "ICAgICAgICAgICAgIGlmIChyZW1haW5pbmcgPCBjYXApCiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAg" +
            "ICAgICBmbG9hdCB0ID0gY2FwID09IDEgPyAwLjVmIDogcmVtYWluaW5nIC8gKGZsb2F0KShjYXAgLSAxKTsKICAgICAgICAgICAg" +
            "ICAgICAgICAgICAgZmxvYXQgeCA9IGNvbFhCeVJvd1tyb3ddW2NvbF0KICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICsgTWF0aGYuTGVycCgtcm93U3ByZWFkW3Jvd10gKiAwLjVmLCByb3dTcHJlYWRbcm93XSAqIDAuNWYsIHQpOwogICAgICAgICAg" +
            "ICAgICAgICAgICAgICByZXR1cm4gbmV3IFZlY3RvcjIoeCwgcm93WVtyb3ddKTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAg" +
            "ICAgICAgICAgICAgICAgcmVtYWluaW5nIC09IGNhcDsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgICAg" +
            "ICByZXR1cm4gbmV3IFZlY3RvcjIoMC41ZiwgMC41Zik7CiAgICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+U3RhZ2dlcmVk" +
            "IGNvbHVtbnMgb2YgNiAoZnJvbnRsaW5lU2l6ZSA9IDYpIOKAlCBtYXRjaGVzIFBoYXNlIDMgYXV0by1hcnJhbmdlIG1hdGguPC9z" +
            "dW1tYXJ5PgogICAgICAgIHN0YXRpYyBTbG90VHJhbnNmb3JtQ29uZmlnIFBsYXllckJhdHRsZURlZmF1bHQoaW50IGkpCiAgICAg" +
            "ICAgewogICAgICAgICAgICBjb25zdCBpbnQgZnJvbnRsaW5lU2l6ZSA9IDY7CiAgICAgICAgICAgIGludCBjb2wgPSBpIC8gZnJv" +
            "bnRsaW5lU2l6ZTsKICAgICAgICAgICAgaW50IHJvdyA9IGkgJSBmcm9udGxpbmVTaXplOwogICAgICAgICAgICBmbG9hdCBzY2Fs" +
            "ZSA9IGNvbCA8PSAxID8gMWYgOiBNYXRoZi5MZXJwKDAuOTVmLCAwLjcwZiwgTWF0aGYuSW52ZXJzZUxlcnAoMmYsIDhmLCBjb2wp" +
            "KTsKICAgICAgICAgICAgZmxvYXQgeCA9IC0zLjVmICsgcm93ICogMS4xZjsKICAgICAgICAgICAgZmxvYXQgeSA9IDIuMGYgLSBj" +
            "b2wgKiAwLjg1ZjsKICAgICAgICAgICAgcmV0dXJuIG5ldyBTbG90VHJhbnNmb3JtQ29uZmlnCiAgICAgICAgICAgIHsKICAgICAg" +
            "ICAgICAgICAgIHNsb3ROdW1iZXIgPSBpLAogICAgICAgICAgICAgICAgcG9zaXRpb24gPSBuZXcgVmVjdG9yMih4LCB5KSwKICAg" +
            "ICAgICAgICAgICAgIHNjYWxlID0gbmV3IFZlY3RvcjIoc2NhbGUsIHNjYWxlKSwKICAgICAgICAgICAgICAgIGFzcGVjdE11bHRp" +
            "cGxpZXIgPSBWZWN0b3IyLm9uZSwKICAgICAgICAgICAgICAgIHNvcnRpbmdPcmRlciA9IE1hdGhmLlJvdW5kVG9JbnQoLXkgKiAx" +
            "MGYpCiAgICAgICAgICAgIH07CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMgU2xvdFRyYW5zZm9ybUNvbmZpZyBFbmVteUJhdHRs" +
            "ZURlZmF1bHQoaW50IGkpCiAgICAgICAgewogICAgICAgICAgICBpbnQgY29sID0gaSAvIDU7CiAgICAgICAgICAgIGludCByb3cg" +
            "PSBpICUgNTsKICAgICAgICAgICAgZmxvYXQgeCA9IDMuNWYgKyByb3cgKiAxLjBmOwogICAgICAgICAgICBmbG9hdCB5ID0gMS41" +
            "ZiAtIGNvbCAqIDEuMGY7CiAgICAgICAgICAgIHJldHVybiBuZXcgU2xvdFRyYW5zZm9ybUNvbmZpZwogICAgICAgICAgICB7CiAg" +
            "ICAgICAgICAgICAgICBzbG90TnVtYmVyID0gaSwKICAgICAgICAgICAgICAgIHBvc2l0aW9uID0gbmV3IFZlY3RvcjIoeCwgeSks" +
            "CiAgICAgICAgICAgICAgICBzY2FsZSA9IFZlY3RvcjIub25lLAogICAgICAgICAgICAgICAgYXNwZWN0TXVsdGlwbGllciA9IFZl" +
            "Y3RvcjIub25lLAogICAgICAgICAgICAgICAgc29ydGluZ09yZGVyID0gTWF0aGYuUm91bmRUb0ludCgteSAqIDEwZikKICAgICAg" +
            "ICAgICAgfTsKICAgICAgICB9CiAgICB9Cn0K"
            ;
        // Assets/Scripts/Data/SceneBackgroundProfile.cs (4643 bytes)
        const string B64_SceneBackgroundProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgU08gb25seSAoU2NlbmVCYWNrZ3JvdW5kU2xvdC9FbnRyeSBsaXZlIGluIEdhbWVEYXRhVHlwZXMpCiAg" +
            "ICAvLy8gPHN1bW1hcnk+CiAgICAvLy8gQXJjaGl0ZWN0LWFzc2lnbmVkIHNjZW5lL3NjcmVlbiBiYWNrZ3JvdW5kcyAoaW1hZ2Ug" +
            "b3IgdmlkZW8pLiBSZXNvdXJjZXMgcGF0aDogU2NlbmVCYWNrZ3JvdW5kUHJvZmlsZS4KICAgIC8vLyBBc3NpZ24tb25seSDigJQg" +
            "ZW1wdHkgc2xvdHMga2VlcCBlYWNoIHNjcmVlbidzIGJ1aWx0LWluIGRlZmF1bHQgYXJ0LgogICAgLy8vIDwvc3VtbWFyeT4KICAg" +
            "IFtDcmVhdGVBc3NldE1lbnUoZmlsZU5hbWUgPSAiU2NlbmVCYWNrZ3JvdW5kUHJvZmlsZSIsIG1lbnVOYW1lID0gIkRlbW9uS25p" +
            "Z2h0L1NjZW5lIEJhY2tncm91bmQgUHJvZmlsZSIpXQogICAgcHVibGljIGNsYXNzIFNjZW5lQmFja2dyb3VuZFByb2ZpbGUgOiBT" +
            "Y3JpcHRhYmxlT2JqZWN0CiAgICB7CiAgICAgICAgcHVibGljIGNvbnN0IHN0cmluZyBSZXNvdXJjZXNQYXRoID0gIlNjZW5lQmFj" +
            "a2dyb3VuZFByb2ZpbGUiOwoKICAgICAgICBwdWJsaWMgTGlzdDxTY2VuZUJhY2tncm91bmRFbnRyeT4gZW50cmllcyA9IG5ldyBM" +
            "aXN0PFNjZW5lQmFja2dyb3VuZEVudHJ5PigpOwoKICAgICAgICBwdWJsaWMgc3RhdGljIFNjZW5lQmFja2dyb3VuZFByb2ZpbGUg" +
            "TG9hZCgpCiAgICAgICAgewogICAgICAgICAgICBTY2VuZUJhY2tncm91bmRQcm9maWxlIHAgPSBudWxsOwogICAgICAgICAgICB0" +
            "cnkgeyBwID0gUmVzb3VyY2VzLkxvYWQ8U2NlbmVCYWNrZ3JvdW5kUHJvZmlsZT4oUmVzb3VyY2VzUGF0aCk7IH0KICAgICAgICAg" +
            "ICAgY2F0Y2ggeyAvKiBtaXNzaW5nIE1vbm9TY3JpcHQgR1VJRC9maWxlSUQgYWZ0ZXIgZW1iZWQg4oCUIGZhbGwgdGhyb3VnaCAq" +
            "LyB9CiAgICAgICAgICAgIGlmIChwICE9IG51bGwpCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHAuRW5zdXJlRGVmYXVs" +
            "dEVudHJpZXMoKTsKICAgICAgICAgICAgICAgIHAuUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAg" +
            "ICAgIHJldHVybiBwOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHRyeQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2" +
            "YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8U2NlbmVCYWNrZ3JvdW5kUHJvZmlsZT4oIiIpOwogICAgICAgICAgICAgICAgaWYg" +
            "KGFsbCAhPSBudWxsKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgYWxs" +
            "Lkxlbmd0aDsgaSsrKQogICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgaWYgKGFsbFtpXSA9PSBu" +
            "dWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgICAgICAgICAgYWxsW2ldLkVuc3VyZURlZmF1bHRFbnRyaWVzKCk7CiAgICAg" +
            "ICAgICAgICAgICAgICAgICAgIGFsbFtpXS5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAgICAgICAgICAgICAg" +
            "ICAgICAgICByZXR1cm4gYWxsW2ldOwogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAg" +
            "fQogICAgICAgICAgICBjYXRjaCB7IC8qIGJyb2tlbiBSZXNvdXJjZXMgYXNzZXQgLyBtaXNzaW5nIHNjcmlwdCBzcGFtIOKAlCBz" +
            "b2Z0IHN0YW5kLWluICovIH0KICAgICAgICAgICAgcCA9IENyZWF0ZUluc3RhbmNlPFNjZW5lQmFja2dyb3VuZFByb2ZpbGU+KCk7" +
            "CiAgICAgICAgICAgIHAubmFtZSA9ICJTY2VuZUJhY2tncm91bmRQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLkVuc3Vy" +
            "ZURlZmF1bHRFbnRyaWVzKCk7CiAgICAgICAgICAgIHJldHVybiBwOwogICAgICAgIH0KCiAgICAgICAgLy8vIDxzdW1tYXJ5Pgog" +
            "ICAgICAgIC8vLyBSZXN0b3JlIE1pc3NpbmcgaW1hZ2Ugc3ByaXRlcyBmcm9tIHBhdGggYmFja3VwcyAoUmVzb3VyY2VzIC8gU3Ry" +
            "ZWFtaW5nQXNzZXRzKS4KICAgICAgICAvLy8gdGlwOiBmaXgtY2xhc3MtcG9vbC1raW5kLWNzMDAxOQogICAgICAgIC8vLyA8L3N1" +
            "bW1hcnk+CiAgICAgICAgcHVibGljIGludCBSZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpCiAgICAgICAgewogICAgICAg" +
            "ICAgICBFbnN1cmVEZWZhdWx0RW50cmllcygpOwogICAgICAgICAgICBpZiAoZW50cmllcyA9PSBudWxsKSByZXR1cm4gMDsKICAg" +
            "ICAgICAgICAgaW50IHJlc3RvcmVkID0gMDsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBlbnRyaWVzLkNvdW50OyBp" +
            "KyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBlID0gZW50cmllc1tpXTsKICAgICAgICAgICAgICAgIGlmIChl" +
            "ID09IG51bGwgfHwgZS5pbWFnZVNwcml0ZSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChzdHJpbmcuSXNO" +
            "dWxsT3JFbXB0eShlLmltYWdlQXNzZXRQYXRoKSkgY29udGludWU7CiAgICAgICAgICAgICAgICBTcHJpdGUgc3AgPSBDbGFzc1Nw" +
            "cml0ZVBvb2xQcm9maWxlLlRyeUxvYWRTcHJpdGVBdFBhdGgoZS5pbWFnZUFzc2V0UGF0aCk7CiAgICAgICAgICAgICAgICBpZiAo" +
            "c3AgPT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICBlLmltYWdlU3ByaXRlID0gc3A7CiAgICAgICAgICAgICAgICBy" +
            "ZXN0b3JlZCsrOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiByZXN0b3JlZDsKICAgICAgICB9CgogICAgICAgIHB1" +
            "YmxpYyB2b2lkIEVuc3VyZURlZmF1bHRFbnRyaWVzKCkKICAgICAgICB7CiAgICAgICAgICAgIGVudHJpZXMgPz89IG5ldyBMaXN0" +
            "PFNjZW5lQmFja2dyb3VuZEVudHJ5PigpOwogICAgICAgICAgICBFbnN1cmVFbnRyeShTY2VuZUJhY2tncm91bmRTbG90Lk1haW5N" +
            "ZW51LCAiTWFpbiBNZW51Iik7CiAgICAgICAgICAgIEVuc3VyZUVudHJ5KFNjZW5lQmFja2dyb3VuZFNsb3QuU29tZXJzZXRDaHVy" +
            "Y2gsICJTb21lcnNldCBDaHVyY2giKTsKICAgICAgICAgICAgRW5zdXJlRW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xvdC5DaHVyY2h5" +
            "YXJkQmF0dGxlLCAiQ2h1cmNoeWFyZCBCYXR0bGUiKTsKICAgICAgICAgICAgRW5zdXJlRW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xv" +
            "dC5WaWN0b3J5T3ZlcmxheSwgIlZpY3RvcnkgT3ZlcmxheSIpOwogICAgICAgICAgICBFbnN1cmVFbnRyeShTY2VuZUJhY2tncm91" +
            "bmRTbG90Lk1lcmxpblJlc3VycmVjdGlvbiwgIk1lcmxpbiBSZXN1cnJlY3Rpb24iKTsKICAgICAgICAgICAgRW5zdXJlRW50cnko" +
            "U2NlbmVCYWNrZ3JvdW5kU2xvdC5MZWdlbmRDb2RleCwgIkxlZ2VuZHMgQ29kZXgiKTsKICAgICAgICB9CgogICAgICAgIHZvaWQg" +
            "RW5zdXJlRW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xvdCBzbG90LCBzdHJpbmcgbGFiZWwpCiAgICAgICAgewogICAgICAgICAgICBm" +
            "b3IgKGludCBpID0gMDsgaSA8IGVudHJpZXMuQ291bnQ7IGkrKykKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgaWYgKGVu" +
            "dHJpZXNbaV0gIT0gbnVsbCAmJiBlbnRyaWVzW2ldLnNsb3QgPT0gc2xvdCkKICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAg" +
            "ICAgICAgICBpZiAoc3RyaW5nLklzTnVsbE9yRW1wdHkoZW50cmllc1tpXS5kaXNwbGF5TmFtZSkpCiAgICAgICAgICAgICAgICAg" +
            "ICAgICAgIGVudHJpZXNbaV0uZGlzcGxheU5hbWUgPSBsYWJlbDsKICAgICAgICAgICAgICAgICAgICByZXR1cm47CiAgICAgICAg" +
            "ICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICAgICAgZW50cmllcy5BZGQobmV3IFNjZW5lQmFja2dyb3VuZEVudHJ5IHsg" +
            "c2xvdCA9IHNsb3QsIGRpc3BsYXlOYW1lID0gbGFiZWwgfSk7CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgU2NlbmVCYWNrZ3Jv" +
            "dW5kRW50cnkgR2V0RW50cnkoU2NlbmVCYWNrZ3JvdW5kU2xvdCBzbG90KQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlRGVm" +
            "YXVsdEVudHJpZXMoKTsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBlbnRyaWVzLkNvdW50OyBpKyspCiAgICAgICAg" +
            "ICAgIHsKICAgICAgICAgICAgICAgIGlmIChlbnRyaWVzW2ldICE9IG51bGwgJiYgZW50cmllc1tpXS5zbG90ID09IHNsb3QpCiAg" +
            "ICAgICAgICAgICAgICAgICAgcmV0dXJuIGVudHJpZXNbaV07CiAgICAgICAgICAgIH0KICAgICAgICAgICAgcmV0dXJuIG51bGw7" +
            "CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgYm9vbCBIYXNBc3NpZ25tZW50KFNjZW5lQmFja2dyb3VuZFNsb3Qgc2xvdCkKICAg" +
            "ICAgICB7CiAgICAgICAgICAgIHZhciBlID0gR2V0RW50cnkoc2xvdCk7CiAgICAgICAgICAgIHJldHVybiBlICE9IG51bGwgJiYg" +
            "KGUuaW1hZ2VTcHJpdGUgIT0gbnVsbCB8fCBlLnZpZGVvQ2xpcCAhPSBudWxsCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgIHx8ICFzdHJpbmcuSXNOdWxsT3JFbXB0eShlLmltYWdlQXNzZXRQYXRoKQogICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgICB8fCAhc3RyaW5nLklzTnVsbE9yRW1wdHkoZS52aWRlb0Fzc2V0UGF0aCkpOwogICAgICAgIH0KICAgIH0KfQo="
            ;
        // Assets/Scripts/Data/ClassSpritePoolProfile.cs (9070 bytes)
        const string B64_ClassSpritePoolProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgZm9yIFVuaXR5IFNPIGJpbmRpbmcKICAgIC8vLyA8c3VtbWFyeT4KICAgIC8vLyBBcmNoaXRlY3QgY2xh" +
            "c3Mgc3ByaXRlIHBvb2xzIChQZWFzYW50IC8gV2FycmlvciAvIEtuaWdodCkuIFJlc291cmNlcyBwYXRoOgogICAgLy8vIENsYXNz" +
            "U3ByaXRlUG9vbFByb2ZpbGUuIFN0YWdlTGF5b3V0UHJvZmlsZSBwZXcgc2xvdHMgc3RheSBwb3NpdGlvbi9zY2FsZSBndWlkZXMg" +
            "4oCUCiAgICAvLy8gY2h1cmNoL2NvbWJhdCB2aXN1YWxzIHBpY2sgZnJvbSB0aGVzZSBwb29scyBieSB1bml0IGNsYXNzLgogICAg" +
            "Ly8vIDwvc3VtbWFyeT4KICAgIFtDcmVhdGVBc3NldE1lbnUoZmlsZU5hbWUgPSAiQ2xhc3NTcHJpdGVQb29sUHJvZmlsZSIsIG1l" +
            "bnVOYW1lID0gIkRlbW9uS25pZ2h0L0NsYXNzIFNwcml0ZSBQb29sIFByb2ZpbGUiKV0KICAgIHB1YmxpYyBjbGFzcyBDbGFzc1Nw" +
            "cml0ZVBvb2xQcm9maWxlIDogU2NyaXB0YWJsZU9iamVjdAogICAgewogICAgICAgIHB1YmxpYyBjb25zdCBzdHJpbmcgUmVzb3Vy" +
            "Y2VzUGF0aCA9ICJDbGFzc1Nwcml0ZVBvb2xQcm9maWxlIjsKCiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiBwZWFzYW50U3By" +
            "aXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IHdhcnJpb3JTcHJpdGVzID0gbmV3" +
            "IExpc3Q8U3ByaXRlPigpOwogICAgICAgIHB1YmxpYyBMaXN0PFNwcml0ZT4ga25pZ2h0U3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0" +
            "ZT4oKTsKCiAgICAgICAgLy8vIDxzdW1tYXJ5PkVkaXRvciBwYXRoIGJhY2t1cHMgc28gbGlzdCBlbnRyaWVzIHN1cnZpdmUgcmVp" +
            "bXBvcnRzLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IHBlYXNhbnRBc3NldFBhdGhzID0gbmV3IExpc3Q8" +
            "c3RyaW5nPigpOwogICAgICAgIHB1YmxpYyBMaXN0PHN0cmluZz4gd2FycmlvckFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+" +
            "KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBrbmlnaHRBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAg" +
            "ICAgICAvLy8gPHN1bW1hcnk+VHJ1ZSB3aGVuIHRoaXMgaXMgYSBDcmVhdGVJbnN0YW5jZSBzdGFuZC1pbiAobm90IGEgUmVzb3Vy" +
            "Y2VzIGFzc2V0KS48L3N1bW1hcnk+CiAgICAgICAgW1N5c3RlbS5Ob25TZXJpYWxpemVkXSBwdWJsaWMgYm9vbCBpc1J1bnRpbWVT" +
            "dGFuZEluOwoKICAgICAgICBwdWJsaWMgc3RhdGljIENsYXNzU3ByaXRlUG9vbFByb2ZpbGUgTG9hZCgpCiAgICAgICAgewogICAg" +
            "ICAgICAgICBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlIHAgPSBudWxsOwogICAgICAgICAgICB0cnkgeyBwID0gUmVzb3VyY2VzLkxv" +
            "YWQ8Q2xhc3NTcHJpdGVQb29sUHJvZmlsZT4oUmVzb3VyY2VzUGF0aCk7IH0KICAgICAgICAgICAgY2F0Y2ggeyAvKiBtaXNzaW5n" +
            "IE1vbm9TY3JpcHQgR1VJRC9maWxlSUQgYWZ0ZXIgZW1iZWQg4oCUIGZhbGwgdGhyb3VnaCAqLyB9CiAgICAgICAgICAgIGlmIChw" +
            "ICE9IG51bGwpCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHAuRW5zdXJlTGlzdHMoKTsKICAgICAgICAgICAgICAgIHAu" +
            "aXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAgICAgICAgLy8gUGF0aHMgZmlsbGVkICsgU3ByaXRlIE1pc3Npbmcg" +
            "YWZ0ZXIgU3luYyDihpIgcmVtYXRlcmlhbGl6ZSBiZWZvcmUgQXBwbGllciBwaWNrcy4KICAgICAgICAgICAgICAgIHAuUmVtYXRl" +
            "cmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAgICAgIHJldHVybiBwOwogICAgICAgICAgICB9CiAgICAgICAg" +
            "ICAgIHRyeQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8Q2xhc3NTcHJp" +
            "dGVQb29sUHJvZmlsZT4oIiIpOwogICAgICAgICAgICAgICAgaWYgKGFsbCAhPSBudWxsKQogICAgICAgICAgICAgICAgewogICAg" +
            "ICAgICAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgYWxsLkxlbmd0aDsgaSsrKQogICAgICAgICAgICAgICAgICAgIHsK" +
            "ICAgICAgICAgICAgICAgICAgICAgICAgaWYgKGFsbFtpXSA9PSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgYWxsW2ldLkVuc3VyZUxpc3RzKCk7CiAgICAgICAgICAgICAgICAgICAgICAgIGFsbFtpXS5pc1J1bnRpbWVTdGFuZEluID0g" +
            "ZmFsc2U7CiAgICAgICAgICAgICAgICAgICAgICAgIGFsbFtpXS5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAg" +
            "ICAgICAgICAgICAgICAgICAgICByZXR1cm4gYWxsW2ldOwogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIH0K" +
            "ICAgICAgICAgICAgfQogICAgICAgICAgICBjYXRjaCB7IC8qIGJyb2tlbiBSZXNvdXJjZXMgYXNzZXQgLyBtaXNzaW5nIHNjcmlw" +
            "dCDigJQgc29mdCBzdGFuZC1pbiAqLyB9CiAgICAgICAgICAgIHAgPSBDcmVhdGVJbnN0YW5jZTxDbGFzc1Nwcml0ZVBvb2xQcm9m" +
            "aWxlPigpOwogICAgICAgICAgICBwLm5hbWUgPSAiQ2xhc3NTcHJpdGVQb29sUHJvZmlsZV9SdW50aW1lIjsKICAgICAgICAgICAg" +
            "cC5pc1J1bnRpbWVTdGFuZEluID0gdHJ1ZTsKICAgICAgICAgICAgcC5FbnN1cmVMaXN0cygpOwogICAgICAgICAgICByZXR1cm4g" +
            "cDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyB2b2lkIEVuc3VyZUxpc3RzKCkKICAgICAgICB7CiAgICAgICAgICAgIHBlYXNh" +
            "bnRTcHJpdGVzID8/PSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIHdhcnJpb3JTcHJpdGVzID8/PSBuZXcgTGlzdDxT" +
            "cHJpdGU+KCk7CiAgICAgICAgICAgIGtuaWdodFNwcml0ZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICAgICAgcGVh" +
            "c2FudEFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAgd2FycmlvckFzc2V0UGF0aHMgPz89IG5l" +
            "dyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAga25pZ2h0QXNzZXRQYXRocyA/Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwogICAg" +
            "ICAgIH0KCiAgICAgICAgLy8vIDxzdW1tYXJ5PgogICAgICAgIC8vLyBSZXN0b3JlIE1pc3Npbmcgc3ByaXRlIHJlZnMgZnJvbSBw" +
            "YXRoIGJhY2t1cHMgKGVkaXRvciBBc3NldERhdGFiYXNlKS4KICAgICAgICAvLy8gTmV2ZXIgd2lwZSBwYXRoIGJhY2t1cHMgd2hl" +
            "biBzcHJpdGVzIGFyZSB0ZW1wb3JhcmlseSBudWxsIOKAlCBQZXJzaXN0IHN5bmNzCiAgICAgICAgLy8vIHBhdGhzIG9ubHkgd2hl" +
            "biBhIGxpdmUgc3ByaXRlIGlzIHByZXNlbnQuCiAgICAgICAgLy8vIDwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgaW50IFJlbWF0" +
            "ZXJpYWxpemVTcHJpdGVzRnJvbVBhdGhzKCkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZUxpc3RzKCk7CiAgICAgICAgICAg" +
            "IGludCBuID0gMDsKICAgICAgICAgICAgbiArPSBSZW1hdGVyaWFsaXplTGlzdChwZWFzYW50U3ByaXRlcywgcGVhc2FudEFzc2V0" +
            "UGF0aHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVMaXN0KHdhcnJpb3JTcHJpdGVzLCB3YXJyaW9yQXNzZXRQYXRo" +
            "cyk7CiAgICAgICAgICAgIG4gKz0gUmVtYXRlcmlhbGl6ZUxpc3Qoa25pZ2h0U3ByaXRlcywga25pZ2h0QXNzZXRQYXRocyk7CiAg" +
            "ICAgICAgICAgIHJldHVybiBuOwogICAgICAgIH0KCiAgICAgICAgcHVibGljIHN0YXRpYyBpbnQgUmVtYXRlcmlhbGl6ZUxpc3Qo" +
            "TGlzdDxTcHJpdGU+IHNwcml0ZXMsIExpc3Q8c3RyaW5nPiBwYXRocykKICAgICAgICB7CiAgICAgICAgICAgIGlmIChzcHJpdGVz" +
            "ID09IG51bGwgfHwgcGF0aHMgPT0gbnVsbCkgcmV0dXJuIDA7CiAgICAgICAgICAgIGludCByZXN0b3JlZCA9IDA7CiAgICAgICAg" +
            "ICAgIGludCBjb3VudCA9IE1hdGhmLk1heChzcHJpdGVzLkNvdW50LCBwYXRocy5Db3VudCk7CiAgICAgICAgICAgIHdoaWxlIChz" +
            "cHJpdGVzLkNvdW50IDwgY291bnQpIHNwcml0ZXMuQWRkKG51bGwpOwogICAgICAgICAgICB3aGlsZSAocGF0aHMuQ291bnQgPCBj" +
            "b3VudCkgcGF0aHMuQWRkKCIiKTsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBjb3VudDsgaSsrKQogICAgICAgICAg" +
            "ICB7CiAgICAgICAgICAgICAgICBpZiAoc3ByaXRlc1tpXSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChz" +
            "dHJpbmcuSXNOdWxsT3JFbXB0eShwYXRoc1tpXSkpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgU3ByaXRlIHNwID0gVHJ5TG9h" +
            "ZFNwcml0ZUF0UGF0aChwYXRoc1tpXSk7CiAgICAgICAgICAgICAgICBpZiAoc3AgPT0gbnVsbCkgY29udGludWU7CiAgICAgICAg" +
            "ICAgICAgICBzcHJpdGVzW2ldID0gc3A7CiAgICAgICAgICAgICAgICByZXN0b3JlZCsrOwogICAgICAgICAgICB9CiAgICAgICAg" +
            "ICAgIHJldHVybiByZXN0b3JlZDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBzdGF0aWMgU3ByaXRlIFRyeUxvYWRTcHJpdGVB" +
            "dFBhdGgoc3RyaW5nIHBhdGgpCiAgICAgICAgewogICAgICAgICAgICBpZiAoc3RyaW5nLklzTnVsbE9yRW1wdHkocGF0aCkpIHJl" +
            "dHVybiBudWxsOwogICAgICAgICAgICBzdHJpbmcgbm9ybSA9IHBhdGguUmVwbGFjZSgiXFwiLCAiLyIpOwojaWYgVU5JVFlfRURJ" +
            "VE9SCiAgICAgICAgICAgIHRyeQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgc3AgPSBVbml0eUVkaXRvci5Bc3Nl" +
            "dERhdGFiYXNlLkxvYWRBc3NldEF0UGF0aDxTcHJpdGU+KG5vcm0pOwogICAgICAgICAgICAgICAgaWYgKHNwICE9IG51bGwpIHJl" +
            "dHVybiBzcDsKICAgICAgICAgICAgICAgIC8vIFRleHR1cmUgd2l0aCBtdWx0aXBsZSBzcHJpdGVzIOKAlCB0YWtlIGZpcnN0IHN1" +
            "Yi1hc3NldCBTcHJpdGUuCiAgICAgICAgICAgICAgICB2YXIgb2JqcyA9IFVuaXR5RWRpdG9yLkFzc2V0RGF0YWJhc2UuTG9hZEFs" +
            "bEFzc2V0c0F0UGF0aChub3JtKTsKICAgICAgICAgICAgICAgIGlmIChvYmpzICE9IG51bGwpCiAgICAgICAgICAgICAgICB7CiAg" +
            "ICAgICAgICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBvYmpzLkxlbmd0aDsgaSsrKQogICAgICAgICAgICAgICAgICAg" +
            "ICAgICBpZiAob2Jqc1tpXSBpcyBTcHJpdGUgcykgcmV0dXJuIHM7CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAg" +
            "ICAgICAgICAgY2F0Y2ggeyAvKiBpZ25vcmUgKi8gfQojZW5kaWYKICAgICAgICAgICAgLy8gUGxheWVyIGJ1aWxkczogUmVzb3Vy" +
            "Y2VzLkxvYWQgKHByb2ZpbGVzICsgcHJvbW90ZWQgQXJjaGl0ZWN0QXJ0IGNvcGllcykuCiAgICAgICAgICAgIHRyeQogICAgICAg" +
            "ICAgICB7CiAgICAgICAgICAgICAgICBpbnQgaWR4ID0gbm9ybS5JbmRleE9mKCJSZXNvdXJjZXMvIiwgU3lzdGVtLlN0cmluZ0Nv" +
            "bXBhcmlzb24uT3JkaW5hbElnbm9yZUNhc2UpOwogICAgICAgICAgICAgICAgaWYgKGlkeCA+PSAwKQogICAgICAgICAgICAgICAg" +
            "ewogICAgICAgICAgICAgICAgICAgIHN0cmluZyByZXMgPSBub3JtLlN1YnN0cmluZyhpZHggKyAiUmVzb3VyY2VzLyIuTGVuZ3Ro" +
            "KTsKICAgICAgICAgICAgICAgICAgICBpbnQgZG90ID0gcmVzLkxhc3RJbmRleE9mKCcuJyk7CiAgICAgICAgICAgICAgICAgICAg" +
            "aWYgKGRvdCA+IDApIHJlcyA9IHJlcy5TdWJzdHJpbmcoMCwgZG90KTsKICAgICAgICAgICAgICAgICAgICB2YXIgZnJvbVJlcyA9" +
            "IFJlc291cmNlcy5Mb2FkPFNwcml0ZT4ocmVzKTsKICAgICAgICAgICAgICAgICAgICBpZiAoZnJvbVJlcyAhPSBudWxsKSByZXR1" +
            "cm4gZnJvbVJlczsKICAgICAgICAgICAgICAgICAgICB2YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8U3ByaXRlPihyZXMpOwog" +
            "ICAgICAgICAgICAgICAgICAgIGlmIChhbGwgIT0gbnVsbCAmJiBhbGwuTGVuZ3RoID4gMCAmJiBhbGxbMF0gIT0gbnVsbCkgcmV0" +
            "dXJuIGFsbFswXTsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIC8vIEJhcmUgUmVzb3VyY2VzIGtleSAobm8gQXNz" +
            "ZXRzLyBwcmVmaXgpIOKAlCBBcmNoaXRlY3RBcnQvQm9keS9mb28KICAgICAgICAgICAgICAgIGlmICghbm9ybS5TdGFydHNXaXRo" +
            "KCJBc3NldHMvIiwgU3lzdGVtLlN0cmluZ0NvbXBhcmlzb24uT3JkaW5hbElnbm9yZUNhc2UpCiAgICAgICAgICAgICAgICAgICAg" +
            "JiYgbm9ybS5JbmRleE9mKCc6JykgPCAwKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIHN0cmluZyBrZXkg" +
            "PSBub3JtOwogICAgICAgICAgICAgICAgICAgIGludCBkb3QgPSBrZXkuTGFzdEluZGV4T2YoJy4nKTsKICAgICAgICAgICAgICAg" +
            "ICAgICBpZiAoZG90ID4gMCkga2V5ID0ga2V5LlN1YnN0cmluZygwLCBkb3QpOwogICAgICAgICAgICAgICAgICAgIHZhciBiYXJl" +
            "ID0gUmVzb3VyY2VzLkxvYWQ8U3ByaXRlPihrZXkpOwogICAgICAgICAgICAgICAgICAgIGlmIChiYXJlICE9IG51bGwpIHJldHVy" +
            "biBiYXJlOwogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAgICAgICAgIGNhdGNoIHsgLyogaWdub3JlICovIH0K" +
            "CiAgICAgICAgICAgIC8vIFN0cmVhbWluZ0Fzc2V0cyBmYWxsYmFjayAocG5nL2pwZyBjb3BpZWQgYnkgYnVpbGQgcHJlcGFyZSku" +
            "IFNvZnQtZmFpbCDigJQgbm8gd2lwZS4KICAgICAgICAgICAgdHJ5CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHN0cmlu" +
            "ZyBmaWxlTmFtZSA9IFN5c3RlbS5JTy5QYXRoLkdldEZpbGVOYW1lKG5vcm0pOwogICAgICAgICAgICAgICAgaWYgKCFzdHJpbmcu" +
            "SXNOdWxsT3JFbXB0eShmaWxlTmFtZSkpCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgc3RyaW5nIHNhID0g" +
            "U3lzdGVtLklPLlBhdGguQ29tYmluZShBcHBsaWNhdGlvbi5zdHJlYW1pbmdBc3NldHNQYXRoLCAiRGVtb25LbmlnaHRBcmNoaXRl" +
            "Y3QiLCBmaWxlTmFtZSk7CiAgICAgICAgICAgICAgICAgICAgaWYgKFN5c3RlbS5JTy5GaWxlLkV4aXN0cyhzYSkpCiAgICAgICAg" +
            "ICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgICAgICBieXRlW10gYnl0ZXMgPSBTeXN0ZW0uSU8uRmlsZS5SZWFkQWxs" +
            "Qnl0ZXMoc2EpOwogICAgICAgICAgICAgICAgICAgICAgICB2YXIgdGV4ID0gbmV3IFRleHR1cmUyRCgyLCAyLCBUZXh0dXJlRm9y" +
            "bWF0LlJHQkEzMiwgZmFsc2UpOwogICAgICAgICAgICAgICAgICAgICAgICBpZiAodGV4LkxvYWRJbWFnZShieXRlcykpCiAgICAg" +
            "ICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgICAgIHRleC5uYW1lID0gU3lzdGVtLklPLlBhdGgu" +
            "R2V0RmlsZU5hbWVXaXRob3V0RXh0ZW5zaW9uKGZpbGVOYW1lKTsKICAgICAgICAgICAgICAgICAgICAgICAgICAgIHJldHVybiBT" +
            "cHJpdGUuQ3JlYXRlKAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIHRleCwKICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgICAgICBuZXcgUmVjdCgwLCAwLCB0ZXgud2lkdGgsIHRleC5oZWlnaHQpLAogICAgICAgICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgIG5ldyBWZWN0b3IyKDAuNWYsIDAuNWYpLAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIDEwMGYpOwogICAgICAg" +
            "ICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAg" +
            "ICAgICAgICAgIGNhdGNoIHsgLyogaWdub3JlICovIH0KICAgICAgICAgICAgcmV0dXJuIG51bGw7CiAgICAgICAgfQoKICAgICAg" +
            "ICBwdWJsaWMgTGlzdDxTcHJpdGU+IEdldFBvb2woVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZUxp" +
            "c3RzKCk7CiAgICAgICAgICAgIHJldHVybiB0eXBlIHN3aXRjaAogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlw" +
            "ZS5QZWFzYW50ID0+IHBlYXNhbnRTcHJpdGVzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuV2FycmlvciA9PiB3YXJyaW9yU3By" +
            "aXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLktuaWdodCA9PiBrbmlnaHRTcHJpdGVzLAogICAgICAgICAgICAgICAgXyA9" +
            "PiBudWxsCiAgICAgICAgICAgIH07CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgaW50IENvdW50VXNhYmxlKFVuaXRUeXBlIHR5" +
            "cGUpCiAgICAgICAgewogICAgICAgICAgICB2YXIgcG9vbCA9IEdldFBvb2wodHlwZSk7CiAgICAgICAgICAgIGlmIChwb29sID09" +
            "IG51bGwpIHJldHVybiAwOwogICAgICAgICAgICBFbnN1cmVMaXN0cygpOwogICAgICAgICAgICBMaXN0PHN0cmluZz4gcGF0aHMg" +
            "PSB0eXBlIHN3aXRjaAogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlwZS5QZWFzYW50ID0+IHBlYXNhbnRBc3Nl" +
            "dFBhdGhzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuV2FycmlvciA9PiB3YXJyaW9yQXNzZXRQYXRocywKICAgICAgICAgICAg" +
            "ICAgIFVuaXRUeXBlLktuaWdodCA9PiBrbmlnaHRBc3NldFBhdGhzLAogICAgICAgICAgICAgICAgXyA9PiBudWxsCiAgICAgICAg" +
            "ICAgIH07CiAgICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgaW50IGNvdW50ID0gcG9vbC5Db3VudDsKICAgICAgICAg" +
            "ICAgaWYgKHBhdGhzICE9IG51bGwgJiYgcGF0aHMuQ291bnQgPiBjb3VudCkgY291bnQgPSBwYXRocy5Db3VudDsKICAgICAgICAg" +
            "ICAgZm9yIChpbnQgaSA9IDA7IGkgPCBjb3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBpZiAoaSA8IHBv" +
            "b2wuQ291bnQgJiYgcG9vbFtpXSAhPSBudWxsKSB7IG4rKzsgY29udGludWU7IH0KICAgICAgICAgICAgICAgIC8vIFBhdGggYmFj" +
            "a3VwIGNvdW50cyBhcyB1c2FibGUg4oCUIFJlbWF0ZXJpYWxpemUgcmVzdG9yZXMgdGhlIE1pc3NpbmcgU3ByaXRlIHJlZi4KICAg" +
            "ICAgICAgICAgICAgIGlmIChwYXRocyAhPSBudWxsICYmIGkgPCBwYXRocy5Db3VudCAmJiAhc3RyaW5nLklzTnVsbE9yRW1wdHko" +
            "cGF0aHNbaV0pKSBuKys7CiAgICAgICAgICAgIH0KICAgICAgICAgICAgcmV0dXJuIG47CiAgICAgICAgfQogICAgfQp9Cg=="
            ;
        // Assets/Scripts/Data/LegendPewPortraitProfile.cs (9587 bytes)
        const string B64_LegendPewPortraitProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgZm9yIFVuaXR5IFNPIGJpbmRpbmcKICAgIC8vLyA8c3VtbWFyeT5PbmUgTGVnZW5kIGZhY2UtcG9ydHJh" +
            "aXQgc2xvdCAoQXJjaGl0ZWN0IFRhYiA1KS48L3N1bW1hcnk+CiAgICBbU2VyaWFsaXphYmxlXQogICAgcHVibGljIGNsYXNzIExl" +
            "Z2VuZFBvcnRyYWl0RW50cnkKICAgIHsKICAgICAgICBwdWJsaWMgVW5pdFR5cGUgdW5pdFR5cGU7CiAgICAgICAgcHVibGljIHN0" +
            "cmluZyBkaXNwbGF5TmFtZTsKICAgICAgICBwdWJsaWMgU3ByaXRlIGZhY2VQb3J0cmFpdDsKICAgICAgICBwdWJsaWMgc3RyaW5n" +
            "IGFzc2V0UGF0aDsKICAgICAgICBbVG9vbHRpcCgiTWVybGluIGFuZCBvdGhlciBzb2Z0IHNsb3RzIHN0YXkgaW4gdGhlIGxpc3Qg" +
            "YnV0IGFyZSBub3QgcmVxdWlyZWQgZm9yIHBsYXlhYmxlIENvZGV4LiIpXQogICAgICAgIHB1YmxpYyBib29sIHNvZnRPcHRpb25h" +
            "bDsKICAgIH0KCiAgICAvLy8gPHN1bW1hcnk+CiAgICAvLy8gQXJjaGl0ZWN0IGxlZ2VuZCBmYWNlIHBvcnRyYWl0cyArIHRvd25z" +
            "Zm9sayBwZXcgRkFDRS1PTkxZIHByb2ZpbGUgcG9vbHMuCiAgICAvLy8gRGlzdGluY3QgZnJvbSBDbGFzc1Nwcml0ZVBvb2xQcm9m" +
            "aWxlIChmdWxsLWJvZHkgcGV3L2NvbWJhdCBhdmF0YXJzKS4gUmVzb3VyY2VzIHBhdGg6CiAgICAvLy8gTGVnZW5kUGV3UG9ydHJh" +
            "aXRQcm9maWxlLiBOZXZlciBhc3NpZ24gdGhlc2UgYXMgYm9keVNwcml0ZS4KICAgIC8vLyA8L3N1bW1hcnk+CiAgICBbQ3JlYXRl" +
            "QXNzZXRNZW51KGZpbGVOYW1lID0gIkxlZ2VuZFBld1BvcnRyYWl0UHJvZmlsZSIsIG1lbnVOYW1lID0gIkRlbW9uS25pZ2h0L0xl" +
            "Z2VuZCBQZXcgUG9ydHJhaXQgUHJvZmlsZSIpXQogICAgcHVibGljIGNsYXNzIExlZ2VuZFBld1BvcnRyYWl0UHJvZmlsZSA6IFNj" +
            "cmlwdGFibGVPYmplY3QKICAgIHsKICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5nIFJlc291cmNlc1BhdGggPSAiTGVnZW5kUGV3" +
            "UG9ydHJhaXRQcm9maWxlIjsKCiAgICAgICAgLy8vIDxzdW1tYXJ5PjE0IHBsYXlhYmxlIGxlZ2VuZHMgKyBzb2Z0IE1lcmxpbiBz" +
            "bG90IChsYXN0KS48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIHN0YXRpYyByZWFkb25seSBVbml0VHlwZVtdIENhbm9uaWNhbExl" +
            "Z2VuZFNsb3RzID0KICAgICAgICB7CiAgICAgICAgICAgIFVuaXRUeXBlLlNpcktheSwKICAgICAgICAgICAgVW5pdFR5cGUuU2ly" +
            "QmVkaXZlcmUsCiAgICAgICAgICAgIFVuaXRUeXBlLkxhZHlHdWluZXZlcmUsCiAgICAgICAgICAgIFVuaXRUeXBlLkFyY2hQcmll" +
            "c3RPZkdsYXN0b25idXJ5LAogICAgICAgICAgICBVbml0VHlwZS5TaXJHYXdhaW4sCiAgICAgICAgICAgIFVuaXRUeXBlLlNpclRy" +
            "aXN0YW4sCiAgICAgICAgICAgIFVuaXRUeXBlLlNpckxhbmNlbG90LAogICAgICAgICAgICBVbml0VHlwZS5LaW5nQXJ0aHVyLAog" +
            "ICAgICAgICAgICBVbml0VHlwZS5TaXJHYWxhaGFkLAogICAgICAgICAgICBVbml0VHlwZS5TaXJQZXJjaXZhbCwKICAgICAgICAg" +
            "ICAgVW5pdFR5cGUuTmltdWUsCiAgICAgICAgICAgIFVuaXRUeXBlLk1vcmRyZWQsCiAgICAgICAgICAgIFVuaXRUeXBlLk1vcmdh" +
            "bkxlRmF5LAogICAgICAgICAgICBVbml0VHlwZS5UaGVHcmVlbktuaWdodCwKICAgICAgICAgICAgVW5pdFR5cGUuTWVybGluLCAv" +
            "LyBzb2Z0IOKAlCBvdXQgb2YgcGxheWFibGUgQ29kZXggcG9vbAogICAgICAgIH07CgogICAgICAgIHB1YmxpYyBMaXN0PExlZ2Vu" +
            "ZFBvcnRyYWl0RW50cnk+IGxlZ2VuZFBvcnRyYWl0cyA9IG5ldyBMaXN0PExlZ2VuZFBvcnRyYWl0RW50cnk+KCk7CgogICAgICAg" +
            "IFtIZWFkZXIoIlRvd25zZm9sayBwZXcgRkFDRSBwcm9maWxlcyBvbmx5IChuZXZlciBib2R5U3ByaXRlIOKAlCBkaXN0aW5jdCBm" +
            "cm9tIFRhYiA0IENsYXNzU3ByaXRlUG9vbCkiKV0KICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IHBlYXNhbnRGYWNlUHJvZmls" +
            "ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiB3YXJyaW9yRmFjZVByb2ZpbGVzID0g" +
            "bmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgIHB1YmxpYyBMaXN0PFNwcml0ZT4ga25pZ2h0RmFjZVByb2ZpbGVzID0gbmV3IExp" +
            "c3Q8U3ByaXRlPigpOwoKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IHBlYXNhbnRGYWNlQXNzZXRQYXRocyA9IG5ldyBMaXN0" +
            "PHN0cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IHdhcnJpb3JGYWNlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0" +
            "cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IGtuaWdodEZhY2VBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5n" +
            "PigpOwoKICAgICAgICBbU3lzdGVtLk5vblNlcmlhbGl6ZWRdIHB1YmxpYyBib29sIGlzUnVudGltZVN0YW5kSW47CgogICAgICAg" +
            "IHB1YmxpYyBzdGF0aWMgTGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlIExvYWQoKQogICAgICAgIHsKICAgICAgICAgICAgTGVnZW5k" +
            "UGV3UG9ydHJhaXRQcm9maWxlIHAgPSBudWxsOwogICAgICAgICAgICB0cnkgeyBwID0gUmVzb3VyY2VzLkxvYWQ8TGVnZW5kUGV3" +
            "UG9ydHJhaXRQcm9maWxlPihSZXNvdXJjZXNQYXRoKTsgfQogICAgICAgICAgICBjYXRjaCB7IC8qIG1pc3NpbmcgTW9ub1Njcmlw" +
            "dCBHVUlEL2ZpbGVJRCBhZnRlciBlbWJlZCDigJQgZmFsbCB0aHJvdWdoICovIH0KICAgICAgICAgICAgaWYgKHAgIT0gbnVsbCkK" +
            "ICAgICAgICAgICAgewogICAgICAgICAgICAgICAgcC5FbnN1cmVTbG90c0FuZExpc3RzKCk7CiAgICAgICAgICAgICAgICBwLmlz" +
            "UnVudGltZVN0YW5kSW4gPSBmYWxzZTsKICAgICAgICAgICAgICAgIHAuUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsK" +
            "ICAgICAgICAgICAgICAgIHJldHVybiBwOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHRyeQogICAgICAgICAgICB7CiAgICAg" +
            "ICAgICAgICAgICB2YXIgYWxsID0gUmVzb3VyY2VzLkxvYWRBbGw8TGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlPigiIik7CiAgICAg" +
            "ICAgICAgICAgICBpZiAoYWxsICE9IG51bGwpCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgZm9yIChpbnQg" +
            "aSA9IDA7IGkgPCBhbGwuTGVuZ3RoOyBpKyspCiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgICAgICBp" +
            "ZiAoYWxsW2ldID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uRW5zdXJlU2xvdHNBbmRM" +
            "aXN0cygpOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uaXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAg" +
            "ICAgICAgICAgICAgICBhbGxbaV0uUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAgICAgICAgICAg" +
            "ICAgcmV0dXJuIGFsbFtpXTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAg" +
            "ICAgICAgICAgY2F0Y2ggeyAvKiBicm9rZW4gUmVzb3VyY2VzIGFzc2V0IC8gbWlzc2luZyBzY3JpcHQg4oCUIHNvZnQgc3RhbmQt" +
            "aW4gKi8gfQogICAgICAgICAgICBwID0gQ3JlYXRlSW5zdGFuY2U8TGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlPigpOwogICAgICAg" +
            "ICAgICBwLm5hbWUgPSAiTGVnZW5kUGV3UG9ydHJhaXRQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLmlzUnVudGltZVN0" +
            "YW5kSW4gPSB0cnVlOwogICAgICAgICAgICBwLkVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgcmV0dXJuIHA7CiAg" +
            "ICAgICAgfQoKICAgICAgICAvLy8gPHN1bW1hcnk+UmVzdG9yZSBNaXNzaW5nIGZhY2UvcG9vbCBzcHJpdGVzIGZyb20gcGF0aCBi" +
            "YWNrdXBzIChlZGl0b3IgKyBSZXNvdXJjZXMpLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgaW50IFJlbWF0ZXJpYWxpemVTcHJp" +
            "dGVzRnJvbVBhdGhzKCkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgaW50" +
            "IG4gPSAwOwogICAgICAgICAgICBuICs9IENsYXNzU3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3QocGVhc2FudEZh" +
            "Y2VQcm9maWxlcywgcGVhc2FudEZhY2VBc3NldFBhdGhzKTsKICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxl" +
            "LlJlbWF0ZXJpYWxpemVMaXN0KHdhcnJpb3JGYWNlUHJvZmlsZXMsIHdhcnJpb3JGYWNlQXNzZXRQYXRocyk7CiAgICAgICAgICAg" +
            "IG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJvZmlsZS5SZW1hdGVyaWFsaXplTGlzdChrbmlnaHRGYWNlUHJvZmlsZXMsIGtuaWdodEZh" +
            "Y2VBc3NldFBhdGhzKTsKICAgICAgICAgICAgaWYgKGxlZ2VuZFBvcnRyYWl0cyAhPSBudWxsKQogICAgICAgICAgICB7CiAgICAg" +
            "ICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGxlZ2VuZFBvcnRyYWl0cy5Db3VudDsgaSsrKQogICAgICAgICAgICAgICAg" +
            "ewogICAgICAgICAgICAgICAgICAgIHZhciBlID0gbGVnZW5kUG9ydHJhaXRzW2ldOwogICAgICAgICAgICAgICAgICAgIGlmIChl" +
            "ID09IG51bGwgfHwgZS5mYWNlUG9ydHJhaXQgIT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICAgICAgaWYgKHN0cmlu" +
            "Zy5Jc051bGxPckVtcHR5KGUuYXNzZXRQYXRoKSkgY29udGludWU7CiAgICAgICAgICAgICAgICAgICAgU3ByaXRlIHNwID0gQ2xh" +
            "c3NTcHJpdGVQb29sUHJvZmlsZS5UcnlMb2FkU3ByaXRlQXRQYXRoKGUuYXNzZXRQYXRoKTsKICAgICAgICAgICAgICAgICAgICBp" +
            "ZiAoc3AgPT0gbnVsbCkgY29udGludWU7CiAgICAgICAgICAgICAgICAgICAgZS5mYWNlUG9ydHJhaXQgPSBzcDsKICAgICAgICAg" +
            "ICAgICAgICAgICBuKys7CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICAgICAgcmV0dXJuIG47CiAgICAg" +
            "ICAgfQoKICAgICAgICBwdWJsaWMgdm9pZCBFbnN1cmVTbG90c0FuZExpc3RzKCkKICAgICAgICB7CiAgICAgICAgICAgIGxlZ2Vu" +
            "ZFBvcnRyYWl0cyA/Pz0gbmV3IExpc3Q8TGVnZW5kUG9ydHJhaXRFbnRyeT4oKTsKICAgICAgICAgICAgcGVhc2FudEZhY2VQcm9m" +
            "aWxlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICB3YXJyaW9yRmFjZVByb2ZpbGVzID8/PSBuZXcgTGlzdDxT" +
            "cHJpdGU+KCk7CiAgICAgICAgICAgIGtuaWdodEZhY2VQcm9maWxlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAg" +
            "ICBwZWFzYW50RmFjZUFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAgd2FycmlvckZhY2VBc3Nl" +
            "dFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgICAgIGtuaWdodEZhY2VBc3NldFBhdGhzID8/PSBuZXcgTGlz" +
            "dDxzdHJpbmc+KCk7CgogICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IENhbm9uaWNhbExlZ2VuZFNsb3RzLkxlbmd0aDsg" +
            "aSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlwZSB3YW50ID0gQ2Fub25pY2FsTGVnZW5kU2xvdHNbaV07" +
            "CiAgICAgICAgICAgICAgICBMZWdlbmRQb3J0cmFpdEVudHJ5IGZvdW5kID0gbnVsbDsKICAgICAgICAgICAgICAgIGZvciAoaW50" +
            "IGogPSAwOyBqIDwgbGVnZW5kUG9ydHJhaXRzLkNvdW50OyBqKyspCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAg" +
            "ICAgaWYgKGxlZ2VuZFBvcnRyYWl0c1tqXSAhPSBudWxsICYmIGxlZ2VuZFBvcnRyYWl0c1tqXS51bml0VHlwZSA9PSB3YW50KQog" +
            "ICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAgZm91bmQgPSBsZWdlbmRQb3J0cmFpdHNbal07CiAg" +
            "ICAgICAgICAgICAgICAgICAgICAgIGJyZWFrOwogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIH0KICAgICAg" +
            "ICAgICAgICAgIGlmIChmb3VuZCA9PSBudWxsKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGZvdW5kID0g" +
            "bmV3IExlZ2VuZFBvcnRyYWl0RW50cnkKICAgICAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgICAgIHVuaXRU" +
            "eXBlID0gd2FudCwKICAgICAgICAgICAgICAgICAgICAgICAgZGlzcGxheU5hbWUgPSBEZWZhdWx0RGlzcGxheU5hbWUod2FudCks" +
            "CiAgICAgICAgICAgICAgICAgICAgICAgIHNvZnRPcHRpb25hbCA9IHdhbnQgPT0gVW5pdFR5cGUuTWVybGluCiAgICAgICAgICAg" +
            "ICAgICAgICAgfTsKICAgICAgICAgICAgICAgICAgICBsZWdlbmRQb3J0cmFpdHMuQWRkKGZvdW5kKTsKICAgICAgICAgICAgICAg" +
            "IH0KICAgICAgICAgICAgICAgIGVsc2UKICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICBpZiAoc3RyaW5nLklz" +
            "TnVsbE9yRW1wdHkoZm91bmQuZGlzcGxheU5hbWUpKQogICAgICAgICAgICAgICAgICAgICAgICBmb3VuZC5kaXNwbGF5TmFtZSA9" +
            "IERlZmF1bHREaXNwbGF5TmFtZSh3YW50KTsKICAgICAgICAgICAgICAgICAgICBmb3VuZC5zb2Z0T3B0aW9uYWwgPSB3YW50ID09" +
            "IFVuaXRUeXBlLk1lcmxpbjsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgIH0KCiAgICAgICAgcHVibGlj" +
            "IHN0YXRpYyBzdHJpbmcgRGVmYXVsdERpc3BsYXlOYW1lKFVuaXRUeXBlIHQpID0+IHQgc3dpdGNoCiAgICAgICAgewogICAgICAg" +
            "ICAgICBVbml0VHlwZS5LaW5nQXJ0aHVyID0+ICJLaW5nIEFydGh1ciIsCiAgICAgICAgICAgIFVuaXRUeXBlLlNpckxhbmNlbG90" +
            "ID0+ICJTaXIgTGFuY2Vsb3QiLAogICAgICAgICAgICBVbml0VHlwZS5Nb3JnYW5MZUZheSA9PiAiTW9yZ2FuIGxlIEZheSIsCiAg" +
            "ICAgICAgICAgIFVuaXRUeXBlLkxhZHlPZlRoZUxha2UgPT4gIkxhZHkgb2YgdGhlIExha2UiLAogICAgICAgICAgICBVbml0VHlw" +
            "ZS5TaXJHYXdhaW4gPT4gIlNpciBHYXdhaW4iLAogICAgICAgICAgICBVbml0VHlwZS5TaXJHYWxhaGFkID0+ICJTaXIgR2FsYWhh" +
            "ZCIsCiAgICAgICAgICAgIFVuaXRUeXBlLlNpclRyaXN0YW4gPT4gIlNpciBUcmlzdGFuIiwKICAgICAgICAgICAgVW5pdFR5cGUu" +
            "U2lyUGVyY2l2YWwgPT4gIlNpciBQZXJjaXZhbCIsCiAgICAgICAgICAgIFVuaXRUeXBlLlNpcktheSA9PiAiU2lyIEtheSIsCiAg" +
            "ICAgICAgICAgIFVuaXRUeXBlLlNpckJlZGl2ZXJlID0+ICJTaXIgQmVkaXZlcmUiLAogICAgICAgICAgICBVbml0VHlwZS5MYWR5" +
            "R3VpbmV2ZXJlID0+ICJMYWR5IEd1aW5ldmVyZSIsCiAgICAgICAgICAgIFVuaXRUeXBlLkd1aW5ldmVyZSA9PiAiTGFkeSBHdWlu" +
            "ZXZlcmUiLAogICAgICAgICAgICBVbml0VHlwZS5BcmNoUHJpZXN0T2ZHbGFzdG9uYnVyeSA9PiAiQXJjaC1Qcmllc3Qgb2YgR2xh" +
            "c3RvbmJ1cnkiLAogICAgICAgICAgICBVbml0VHlwZS5Qcmllc3QgPT4gIkFyY2gtUHJpZXN0IG9mIEdsYXN0b25idXJ5IiwKICAg" +
            "ICAgICAgICAgVW5pdFR5cGUuVGhlR3JlZW5LbmlnaHQgPT4gIlRoZSBHcmVlbiBLbmlnaHQiLAogICAgICAgICAgICBVbml0VHlw" +
            "ZS5NZXJsaW4gPT4gIk1lcmxpbiAoc29mdCkiLAogICAgICAgICAgICBfID0+IHQuVG9TdHJpbmcoKQogICAgICAgIH07CgogICAg" +
            "ICAgIC8vLyA8c3VtbWFyeT5Ob3JtYWxpemUgYWxpYXNlcyBzbyBHdWluZXZlcmUvUHJpZXN0IG1hcCBvbnRvIGNhbm9uaWNhbCBz" +
            "bG90cy48L3N1bW1hcnk+CiAgICAgICAgcHVibGljIHN0YXRpYyBVbml0VHlwZSBOb3JtYWxpemVMZWdlbmRTbG90KFVuaXRUeXBl" +
            "IHR5cGUpID0+IHR5cGUgc3dpdGNoCiAgICAgICAgewogICAgICAgICAgICBVbml0VHlwZS5HdWluZXZlcmUgPT4gVW5pdFR5cGUu" +
            "TGFkeUd1aW5ldmVyZSwKICAgICAgICAgICAgVW5pdFR5cGUuUHJpZXN0ID0+IFVuaXRUeXBlLkFyY2hQcmllc3RPZkdsYXN0b25i" +
            "dXJ5LAogICAgICAgICAgICBfID0+IHR5cGUKICAgICAgICB9OwoKICAgICAgICBwdWJsaWMgTGVnZW5kUG9ydHJhaXRFbnRyeSBG" +
            "aW5kTGVnZW5kRW50cnkoVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsK" +
            "ICAgICAgICAgICAgVW5pdFR5cGUgd2FudCA9IE5vcm1hbGl6ZUxlZ2VuZFNsb3QodHlwZSk7CiAgICAgICAgICAgIGZvciAoaW50" +
            "IGkgPSAwOyBpIDwgbGVnZW5kUG9ydHJhaXRzLkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBl" +
            "ID0gbGVnZW5kUG9ydHJhaXRzW2ldOwogICAgICAgICAgICAgICAgaWYgKGUgIT0gbnVsbCAmJiBlLnVuaXRUeXBlID09IHdhbnQp" +
            "IHJldHVybiBlOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBudWxsOwogICAgICAgIH0KCiAgICAgICAgcHVibGlj" +
            "IFNwcml0ZSBHZXRMZWdlbmRGYWNlKFVuaXRUeXBlIHR5cGUpCiAgICAgICAgewogICAgICAgICAgICB2YXIgZSA9IEZpbmRMZWdl" +
            "bmRFbnRyeSh0eXBlKTsKICAgICAgICAgICAgcmV0dXJuIGUgIT0gbnVsbCA/IGUuZmFjZVBvcnRyYWl0IDogbnVsbDsKICAgICAg" +
            "ICB9CgogICAgICAgIHB1YmxpYyBMaXN0PFNwcml0ZT4gR2V0UGV3RmFjZVBvb2woVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAg" +
            "ICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgcmV0dXJuIHR5cGUgc3dpdGNoCiAgICAgICAgICAg" +
            "IHsKICAgICAgICAgICAgICAgIFVuaXRUeXBlLlBlYXNhbnQgPT4gcGVhc2FudEZhY2VQcm9maWxlcywKICAgICAgICAgICAgICAg" +
            "IFVuaXRUeXBlLldhcnJpb3IgPT4gd2FycmlvckZhY2VQcm9maWxlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLktuaWdodCA9" +
            "PiBrbmlnaHRGYWNlUHJvZmlsZXMsCiAgICAgICAgICAgICAgICBfID0+IG51bGwKICAgICAgICAgICAgfTsKICAgICAgICB9Cgog" +
            "ICAgICAgIHB1YmxpYyBpbnQgQ291bnRVc2FibGVQZXdGYWNlcyhVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAg" +
            "dmFyIHBvb2wgPSBHZXRQZXdGYWNlUG9vbCh0eXBlKTsKICAgICAgICAgICAgaWYgKHBvb2wgPT0gbnVsbCkgcmV0dXJuIDA7CiAg" +
            "ICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBwb29sLkNvdW50OyBpKyspCiAgICAg" +
            "ICAgICAgICAgICBpZiAocG9vbFtpXSAhPSBudWxsKSBuKys7CiAgICAgICAgICAgIHJldHVybiBuOwogICAgICAgIH0KCiAgICAg" +
            "ICAgcHVibGljIGludCBDb3VudEFzc2lnbmVkTGVnZW5kRmFjZXMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xvdHNB" +
            "bmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgbGVnZW5kUG9y" +
            "dHJhaXRzLkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBlID0gbGVnZW5kUG9ydHJhaXRzW2ld" +
            "OwogICAgICAgICAgICAgICAgaWYgKGUgIT0gbnVsbCAmJiBlLmZhY2VQb3J0cmFpdCAhPSBudWxsICYmICFlLnNvZnRPcHRpb25h" +
            "bCkgbisrOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBuOwogICAgICAgIH0KICAgIH0KfQo="
            ;
        // Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs (13776 bytes)
        const string B64_BattleGoodEvilSpritePoolProfile =
            "dXNpbmcgU3lzdGVtOwp1c2luZyBTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYzsKdXNpbmcgVW5pdHlFbmdpbmU7CgpuYW1lc3Bh" +
            "Y2UgQXJ0aHVyaWFuQXV0b2JhdHRsZXIKewogICAgLy8gdGlwOiBmaXgtZ2R0LXByb2ZpbGUtYWx3YXlzLWNvbXBpbGUg4oCUIG5h" +
            "bWVkIE1vbm9TY3JpcHQgU08gb25seSAoQmF0dGxlU3ByaXRlU2xvdEVudHJ5IGxpdmVzIGluIEdhbWVEYXRhVHlwZXMpCiAgICAv" +
            "Ly8gPHN1bW1hcnk+CiAgICAvLy8gQXJjaGl0ZWN0IFRhYiA2IOKAlCBBdXRvYmF0dGxlIEdvb2QgdnMgRXZpbCBiYXR0bGUgc3By" +
            "aXRlcyBmb3IgVW5pdFZpc3VhbEFjdG9yLgogICAgLy8vIERpc3RpbmN0IGZyb20gVGFiIDQgcGV3IEJPRFkgcG9vbHMgYW5kIFRh" +
            "YiA1IEZBQ0UgcG9ydHJhaXRzLiBSZXNvdXJjZXMgcGF0aDoKICAgIC8vLyBCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQcm9maWxl" +
            "LiB0aXA6IGJhdHRsZS1nb29kLWV2aWwtc3ByaXRlLXBvb2xzCiAgICAvLy8gPC9zdW1tYXJ5PgogICAgW0NyZWF0ZUFzc2V0TWVu" +
            "dShmaWxlTmFtZSA9ICJCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQcm9maWxlIiwgbWVudU5hbWUgPSAiRGVtb25LbmlnaHQvQmF0" +
            "dGxlIEdvb2QgRXZpbCBTcHJpdGUgUG9vbCBQcm9maWxlIildCiAgICBwdWJsaWMgY2xhc3MgQmF0dGxlR29vZEV2aWxTcHJpdGVQ" +
            "b29sUHJvZmlsZSA6IFNjcmlwdGFibGVPYmplY3QKICAgIHsKICAgICAgICBwdWJsaWMgY29uc3Qgc3RyaW5nIFJlc291cmNlc1Bh" +
            "dGggPSAiQmF0dGxlR29vZEV2aWxTcHJpdGVQb29sUHJvZmlsZSI7CgogICAgICAgIC8vLyA8c3VtbWFyeT4xNCBwbGF5YWJsZSBs" +
            "ZWdlbmRzICsgc29mdCBNZXJsaW4gKHNhbWUgb3JkZXIgYXMgVGFiIDUgQ2Fub25pY2FsTGVnZW5kU2xvdHMpLjwvc3VtbWFyeT4K" +
            "ICAgICAgICBwdWJsaWMgc3RhdGljIHJlYWRvbmx5IFVuaXRUeXBlW10gQ2Fub25pY2FsR29vZExlZ2VuZFNsb3RzID0gTGVnZW5k" +
            "UGV3UG9ydHJhaXRQcm9maWxlLkNhbm9uaWNhbExlZ2VuZFNsb3RzOwoKICAgICAgICAvLy8gPHN1bW1hcnk+V2F2ZSAz4oCTMTAr" +
            "IGJvc3NlcyAobG9yZS1ib3NzLXdhdmUtb3JkZXIpLjwvc3VtbWFyeT4KICAgICAgICBwdWJsaWMgc3RhdGljIHJlYWRvbmx5IFVu" +
            "aXRUeXBlW10gQ2Fub25pY2FsQm9zc1Nsb3RzID0KICAgICAgICB7CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NCbGFja0tuaWdo" +
            "dCwgICAvLyBXYXZlIDMKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc01lbGVhZ2FudCwgICAgIC8vIFdhdmUgNAogICAgICAgICAg" +
            "ICBVbml0VHlwZS5Cb3NzS2luZ0xvdCwgICAgICAgLy8gV2F2ZSA1CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NWb3J0aWdlcm4s" +
            "ICAgICAvLyBXYXZlIDYKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc0FjY29sb24sICAgICAgIC8vIFdhdmUgNwogICAgICAgICAg" +
            "ICBVbml0VHlwZS5Cb3NzUXVlc3RpbmdCZWFzdCwgLy8gV2F2ZSA4CiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NHcmVlbktuaWdo" +
            "dCwgICAvLyBXYXZlIDkKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc05pbXVlLCAgICAgICAgIC8vIFdhdmUgMTArCiAgICAgICAg" +
            "fTsKCiAgICAgICAgW0hlYWRlcigiR29vZCDigJQgdHJvb3AgYmF0dGxlIHBvb2xzIChtdWx0aS1zcHJpdGUpIildCiAgICAgICAg" +
            "cHVibGljIExpc3Q8U3ByaXRlPiBwZWFzYW50QmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJs" +
            "aWMgTGlzdDxTcHJpdGU+IHdhcnJpb3JCYXR0bGVTcHJpdGVzID0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgIHB1YmxpYyBM" +
            "aXN0PFNwcml0ZT4ga25pZ2h0QmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxT" +
            "cHJpdGU+IHByaWVzdEJhdHRsZVNwcml0ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CgogICAgICAgIHB1YmxpYyBMaXN0PHN0cmlu" +
            "Zz4gcGVhc2FudEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5n" +
            "PiB3YXJyaW9yQmF0dGxlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+" +
            "IGtuaWdodEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBw" +
            "cmllc3RCYXR0bGVBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICBbSGVhZGVyKCJHb29kIOKAlCBwbGF5" +
            "YWJsZSBMZWdlbmQgYmF0dGxlIHNsb3RzIChzaW5nbGUgc3ByaXRlIGVhY2gpIildCiAgICAgICAgcHVibGljIExpc3Q8QmF0dGxl" +
            "U3ByaXRlU2xvdEVudHJ5PiBnb29kTGVnZW5kU2xvdHMgPSBuZXcgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7CgogICAg" +
            "ICAgIFtIZWFkZXIoIkV2aWwg4oCUIHdhdmUtdW5pdCBiYXR0bGUgcG9vbHMiKV0KICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+" +
            "IGRlbW9uSW1wQmF0dGxlU3ByaXRlcyA9IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IGhl" +
            "bGxob3VuZEJhdHRsZVNwcml0ZXMgPSBuZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgcHVibGljIExpc3Q8U3ByaXRlPiBkcmVh" +
            "ZExvcmRCYXR0bGVTcHJpdGVzID0gbmV3IExpc3Q8U3ByaXRlPigpOwoKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IGRlbW9u" +
            "SW1wQmF0dGxlQXNzZXRQYXRocyA9IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICBwdWJsaWMgTGlzdDxzdHJpbmc+IGhlbGxo" +
            "b3VuZEJhdHRsZUFzc2V0UGF0aHMgPSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgcHVibGljIExpc3Q8c3RyaW5nPiBkcmVh" +
            "ZExvcmRCYXR0bGVBc3NldFBhdGhzID0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICBbSGVhZGVyKCJFdmlsIOKAlCBXYXZl" +
            "IDPigJMxMCsgYm9zcyBiYXR0bGUgc2xvdHMiKV0KICAgICAgICBwdWJsaWMgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+IGV2" +
            "aWxCb3NzU2xvdHMgPSBuZXcgTGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7CgogICAgICAgIFtTeXN0ZW0uTm9uU2VyaWFs" +
            "aXplZF0gcHVibGljIGJvb2wgaXNSdW50aW1lU3RhbmRJbjsKCiAgICAgICAgcHVibGljIHN0YXRpYyBCYXR0bGVHb29kRXZpbFNw" +
            "cml0ZVBvb2xQcm9maWxlIExvYWQoKQogICAgICAgIHsKICAgICAgICAgICAgQmF0dGxlR29vZEV2aWxTcHJpdGVQb29sUHJvZmls" +
            "ZSBwID0gbnVsbDsKICAgICAgICAgICAgdHJ5IHsgcCA9IFJlc291cmNlcy5Mb2FkPEJhdHRsZUdvb2RFdmlsU3ByaXRlUG9vbFBy" +
            "b2ZpbGU+KFJlc291cmNlc1BhdGgpOyB9CiAgICAgICAgICAgIGNhdGNoIHsgLyogbWlzc2luZyBNb25vU2NyaXB0IEdVSUQvZmls" +
            "ZUlEIGFmdGVyIGVtYmVkIOKAlCBmYWxsIHRocm91Z2ggKi8gfQogICAgICAgICAgICBpZiAocCAhPSBudWxsKQogICAgICAgICAg" +
            "ICB7CiAgICAgICAgICAgICAgICBwLkVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgICAgIHAuaXNSdW50aW1lU3Rh" +
            "bmRJbiA9IGZhbHNlOwogICAgICAgICAgICAgICAgcC5SZW1hdGVyaWFsaXplU3ByaXRlc0Zyb21QYXRocygpOwogICAgICAgICAg" +
            "ICAgICAgcmV0dXJuIHA7CiAgICAgICAgICAgIH0KICAgICAgICAgICAgdHJ5CiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAg" +
            "IHZhciBhbGwgPSBSZXNvdXJjZXMuTG9hZEFsbDxCYXR0bGVHb29kRXZpbFNwcml0ZVBvb2xQcm9maWxlPigiIik7CiAgICAgICAg" +
            "ICAgICAgICBpZiAoYWxsICE9IG51bGwpCiAgICAgICAgICAgICAgICB7CiAgICAgICAgICAgICAgICAgICAgZm9yIChpbnQgaSA9" +
            "IDA7IGkgPCBhbGwuTGVuZ3RoOyBpKyspCiAgICAgICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgICAgICBpZiAo" +
            "YWxsW2ldID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uRW5zdXJlU2xvdHNBbmRMaXN0" +
            "cygpOwogICAgICAgICAgICAgICAgICAgICAgICBhbGxbaV0uaXNSdW50aW1lU3RhbmRJbiA9IGZhbHNlOwogICAgICAgICAgICAg" +
            "ICAgICAgICAgICBhbGxbaV0uUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKTsKICAgICAgICAgICAgICAgICAgICAgICAg" +
            "cmV0dXJuIGFsbFtpXTsKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAg" +
            "ICAgICAgY2F0Y2ggeyAvKiBicm9rZW4gUmVzb3VyY2VzIGFzc2V0ICovIH0KICAgICAgICAgICAgcCA9IENyZWF0ZUluc3RhbmNl" +
            "PEJhdHRsZUdvb2RFdmlsU3ByaXRlUG9vbFByb2ZpbGU+KCk7CiAgICAgICAgICAgIHAubmFtZSA9ICJCYXR0bGVHb29kRXZpbFNw" +
            "cml0ZVBvb2xQcm9maWxlX1J1bnRpbWUiOwogICAgICAgICAgICBwLmlzUnVudGltZVN0YW5kSW4gPSB0cnVlOwogICAgICAgICAg" +
            "ICBwLkVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAgICAgICAgcmV0dXJuIHA7CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMg" +
            "dm9pZCBFbnN1cmVTbG90c0FuZExpc3RzKCkKICAgICAgICB7CiAgICAgICAgICAgIHBlYXNhbnRCYXR0bGVTcHJpdGVzID8/PSBu" +
            "ZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIHdhcnJpb3JCYXR0bGVTcHJpdGVzID8/PSBuZXcgTGlzdDxTcHJpdGU+KCk7" +
            "CiAgICAgICAgICAgIGtuaWdodEJhdHRsZVNwcml0ZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4oKTsKICAgICAgICAgICAgcHJpZXN0" +
            "QmF0dGxlU3ByaXRlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICBwZWFzYW50QmF0dGxlQXNzZXRQYXRocyA/" +
            "Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwogICAgICAgICAgICB3YXJyaW9yQmF0dGxlQXNzZXRQYXRocyA/Pz0gbmV3IExpc3Q8c3Ry" +
            "aW5nPigpOwogICAgICAgICAgICBrbmlnaHRCYXR0bGVBc3NldFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAg" +
            "ICAgIHByaWVzdEJhdHRsZUFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKCiAgICAgICAgICAgIGRlbW9uSW1wQmF0" +
            "dGxlU3ByaXRlcyA/Pz0gbmV3IExpc3Q8U3ByaXRlPigpOwogICAgICAgICAgICBoZWxsaG91bmRCYXR0bGVTcHJpdGVzID8/PSBu" +
            "ZXcgTGlzdDxTcHJpdGU+KCk7CiAgICAgICAgICAgIGRyZWFkTG9yZEJhdHRsZVNwcml0ZXMgPz89IG5ldyBMaXN0PFNwcml0ZT4o" +
            "KTsKICAgICAgICAgICAgZGVtb25JbXBCYXR0bGVBc3NldFBhdGhzID8/PSBuZXcgTGlzdDxzdHJpbmc+KCk7CiAgICAgICAgICAg" +
            "IGhlbGxob3VuZEJhdHRsZUFzc2V0UGF0aHMgPz89IG5ldyBMaXN0PHN0cmluZz4oKTsKICAgICAgICAgICAgZHJlYWRMb3JkQmF0" +
            "dGxlQXNzZXRQYXRocyA/Pz0gbmV3IExpc3Q8c3RyaW5nPigpOwoKICAgICAgICAgICAgZ29vZExlZ2VuZFNsb3RzID8/PSBuZXcg" +
            "TGlzdDxCYXR0bGVTcHJpdGVTbG90RW50cnk+KCk7CiAgICAgICAgICAgIGV2aWxCb3NzU2xvdHMgPz89IG5ldyBMaXN0PEJhdHRs" +
            "ZVNwcml0ZVNsb3RFbnRyeT4oKTsKCiAgICAgICAgICAgIEVuc3VyZVNsb3RMaXN0KGdvb2RMZWdlbmRTbG90cywgQ2Fub25pY2Fs" +
            "R29vZExlZ2VuZFNsb3RzLCBzb2Z0TWVybGluOiB0cnVlKTsKICAgICAgICAgICAgRW5zdXJlU2xvdExpc3QoZXZpbEJvc3NTbG90" +
            "cywgQ2Fub25pY2FsQm9zc1Nsb3RzLCBzb2Z0TWVybGluOiBmYWxzZSk7CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMgdm9pZCBF" +
            "bnN1cmVTbG90TGlzdChMaXN0PEJhdHRsZVNwcml0ZVNsb3RFbnRyeT4gbGlzdCwgVW5pdFR5cGVbXSB3YW50VHlwZXMsIGJvb2wg" +
            "c29mdE1lcmxpbikKICAgICAgICB7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgd2FudFR5cGVzLkxlbmd0aDsgaSsr" +
            "KQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICBVbml0VHlwZSB3YW50ID0gd2FudFR5cGVzW2ldOwogICAgICAgICAgICAg" +
            "ICAgQmF0dGxlU3ByaXRlU2xvdEVudHJ5IGZvdW5kID0gbnVsbDsKICAgICAgICAgICAgICAgIGZvciAoaW50IGogPSAwOyBqIDwg" +
            "bGlzdC5Db3VudDsgaisrKQogICAgICAgICAgICAgICAgewogICAgICAgICAgICAgICAgICAgIGlmIChsaXN0W2pdICE9IG51bGwg" +
            "JiYgbGlzdFtqXS51bml0VHlwZSA9PSB3YW50KQogICAgICAgICAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgICAgICAgICAg" +
            "Zm91bmQgPSBsaXN0W2pdOwogICAgICAgICAgICAgICAgICAgICAgICBicmVhazsKICAgICAgICAgICAgICAgICAgICB9CiAgICAg" +
            "ICAgICAgICAgICB9CiAgICAgICAgICAgICAgICBpZiAoZm91bmQgPT0gbnVsbCkKICAgICAgICAgICAgICAgIHsKICAgICAgICAg" +
            "ICAgICAgICAgICBmb3VuZCA9IG5ldyBCYXR0bGVTcHJpdGVTbG90RW50cnkKICAgICAgICAgICAgICAgICAgICB7CiAgICAgICAg" +
            "ICAgICAgICAgICAgICAgIHVuaXRUeXBlID0gd2FudCwKICAgICAgICAgICAgICAgICAgICAgICAgZGlzcGxheU5hbWUgPSBEZWZh" +
            "dWx0QmF0dGxlRGlzcGxheU5hbWUod2FudCksCiAgICAgICAgICAgICAgICAgICAgICAgIHNvZnRPcHRpb25hbCA9IHNvZnRNZXJs" +
            "aW4gJiYgd2FudCA9PSBVbml0VHlwZS5NZXJsaW4KICAgICAgICAgICAgICAgICAgICB9OwogICAgICAgICAgICAgICAgICAgIGxp" +
            "c3QuQWRkKGZvdW5kKTsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIGVsc2UKICAgICAgICAgICAgICAgIHsKICAg" +
            "ICAgICAgICAgICAgICAgICBpZiAoc3RyaW5nLklzTnVsbE9yRW1wdHkoZm91bmQuZGlzcGxheU5hbWUpKQogICAgICAgICAgICAg" +
            "ICAgICAgICAgICBmb3VuZC5kaXNwbGF5TmFtZSA9IERlZmF1bHRCYXR0bGVEaXNwbGF5TmFtZSh3YW50KTsKICAgICAgICAgICAg" +
            "ICAgICAgICBpZiAoc29mdE1lcmxpbikKICAgICAgICAgICAgICAgICAgICAgICAgZm91bmQuc29mdE9wdGlvbmFsID0gd2FudCA9" +
            "PSBVbml0VHlwZS5NZXJsaW47CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgICB9CgogICAgICAgIHB1Ymxp" +
            "YyBzdGF0aWMgc3RyaW5nIERlZmF1bHRCYXR0bGVEaXNwbGF5TmFtZShVbml0VHlwZSB0KSA9PiB0IHN3aXRjaAogICAgICAgIHsK" +
            "ICAgICAgICAgICAgVW5pdFR5cGUuS2luZ0FydGh1ciA9PiAiS2luZyBBcnRodXIiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJM" +
            "YW5jZWxvdCA9PiAiU2lyIExhbmNlbG90IiwKICAgICAgICAgICAgVW5pdFR5cGUuTW9yZ2FuTGVGYXkgPT4gIk1vcmdhbiBsZSBG" +
            "YXkiLAogICAgICAgICAgICBVbml0VHlwZS5MYWR5T2ZUaGVMYWtlID0+ICJMYWR5IG9mIHRoZSBMYWtlIiwKICAgICAgICAgICAg" +
            "VW5pdFR5cGUuU2lyR2F3YWluID0+ICJTaXIgR2F3YWluIiwKICAgICAgICAgICAgVW5pdFR5cGUuU2lyR2FsYWhhZCA9PiAiU2ly" +
            "IEdhbGFoYWQiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJUcmlzdGFuID0+ICJTaXIgVHJpc3RhbiIsCiAgICAgICAgICAgIFVu" +
            "aXRUeXBlLlNpclBlcmNpdmFsID0+ICJTaXIgUGVyY2l2YWwiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJLYXkgPT4gIlNpciBL" +
            "YXkiLAogICAgICAgICAgICBVbml0VHlwZS5TaXJCZWRpdmVyZSA9PiAiU2lyIEJlZGl2ZXJlIiwKICAgICAgICAgICAgVW5pdFR5" +
            "cGUuTGFkeUd1aW5ldmVyZSA9PiAiTGFkeSBHdWluZXZlcmUiLAogICAgICAgICAgICBVbml0VHlwZS5HdWluZXZlcmUgPT4gIkxh" +
            "ZHkgR3VpbmV2ZXJlIiwKICAgICAgICAgICAgVW5pdFR5cGUuQXJjaFByaWVzdE9mR2xhc3RvbmJ1cnkgPT4gIkFyY2gtUHJpZXN0" +
            "IG9mIEdsYXN0b25idXJ5IiwKICAgICAgICAgICAgVW5pdFR5cGUuUHJpZXN0ID0+ICJQcmllc3QiLAogICAgICAgICAgICBVbml0" +
            "VHlwZS5UaGVHcmVlbktuaWdodCA9PiAiVGhlIEdyZWVuIEtuaWdodCIsCiAgICAgICAgICAgIFVuaXRUeXBlLk1lcmxpbiA9PiAi" +
            "TWVybGluIChzb2Z0KSIsCiAgICAgICAgICAgIFVuaXRUeXBlLkRlbW9uSW1wID0+ICJEZW1vbiBJbXAiLAogICAgICAgICAgICBV" +
            "bml0VHlwZS5EZW1vbkhlbGxob3VuZCA9PiAiSGVsbGhvdW5kIiwKICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25EcmVhZExvcmQg" +
            "PT4gIkRyZWFkIExvcmQiLAogICAgICAgICAgICBVbml0VHlwZS5Cb3NzQmxhY2tLbmlnaHQgPT4gIkJsYWNrIEtuaWdodCAoVzMp" +
            "IiwKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc01lbGVhZ2FudCA9PiAiTWVsZWFnYW50IChXNCkiLAogICAgICAgICAgICBVbml0" +
            "VHlwZS5Cb3NzS2luZ0xvdCA9PiAiS2luZyBMb3QgKFc1KSIsCiAgICAgICAgICAgIFVuaXRUeXBlLkJvc3NWb3J0aWdlcm4gPT4g" +
            "IlZvcnRpZ2VybiAoVzYpIiwKICAgICAgICAgICAgVW5pdFR5cGUuQm9zc0FjY29sb24gPT4gIkFjY29sb24gKFc3KSIsCiAgICAg" +
            "ICAgICAgIFVuaXRUeXBlLkJvc3NRdWVzdGluZ0JlYXN0ID0+ICJRdWVzdGluZyBCZWFzdCAoVzgpIiwKICAgICAgICAgICAgVW5p" +
            "dFR5cGUuQm9zc0dyZWVuS25pZ2h0ID0+ICJHcmVlbiBLbmlnaHQgQm9zcyAoVzkpIiwKICAgICAgICAgICAgVW5pdFR5cGUuQm9z" +
            "c05pbXVlID0+ICJOaW11ZSAoVzEwKykiLAogICAgICAgICAgICBfID0+IHQuVG9TdHJpbmcoKQogICAgICAgIH07CgogICAgICAg" +
            "IHB1YmxpYyBpbnQgUmVtYXRlcmlhbGl6ZVNwcml0ZXNGcm9tUGF0aHMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xv" +
            "dHNBbmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJvZmls" +
            "ZS5SZW1hdGVyaWFsaXplTGlzdChwZWFzYW50QmF0dGxlU3ByaXRlcywgcGVhc2FudEJhdHRsZUFzc2V0UGF0aHMpOwogICAgICAg" +
            "ICAgICBuICs9IENsYXNzU3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3Qod2FycmlvckJhdHRsZVNwcml0ZXMsIHdh" +
            "cnJpb3JCYXR0bGVBc3NldFBhdGhzKTsKICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlLlJlbWF0ZXJpYWxp" +
            "emVMaXN0KGtuaWdodEJhdHRsZVNwcml0ZXMsIGtuaWdodEJhdHRsZUFzc2V0UGF0aHMpOwogICAgICAgICAgICBuICs9IENsYXNz" +
            "U3ByaXRlUG9vbFByb2ZpbGUuUmVtYXRlcmlhbGl6ZUxpc3QocHJpZXN0QmF0dGxlU3ByaXRlcywgcHJpZXN0QmF0dGxlQXNzZXRQ" +
            "YXRocyk7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJvZmlsZS5SZW1hdGVyaWFsaXplTGlzdChkZW1vbkltcEJh" +
            "dHRsZVNwcml0ZXMsIGRlbW9uSW1wQmF0dGxlQXNzZXRQYXRocyk7CiAgICAgICAgICAgIG4gKz0gQ2xhc3NTcHJpdGVQb29sUHJv" +
            "ZmlsZS5SZW1hdGVyaWFsaXplTGlzdChoZWxsaG91bmRCYXR0bGVTcHJpdGVzLCBoZWxsaG91bmRCYXR0bGVBc3NldFBhdGhzKTsK" +
            "ICAgICAgICAgICAgbiArPSBDbGFzc1Nwcml0ZVBvb2xQcm9maWxlLlJlbWF0ZXJpYWxpemVMaXN0KGRyZWFkTG9yZEJhdHRsZVNw" +
            "cml0ZXMsIGRyZWFkTG9yZEJhdHRsZUFzc2V0UGF0aHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVTbG90cyhnb29k" +
            "TGVnZW5kU2xvdHMpOwogICAgICAgICAgICBuICs9IFJlbWF0ZXJpYWxpemVTbG90cyhldmlsQm9zc1Nsb3RzKTsKICAgICAgICAg" +
            "ICAgcmV0dXJuIG47CiAgICAgICAgfQoKICAgICAgICBzdGF0aWMgaW50IFJlbWF0ZXJpYWxpemVTbG90cyhMaXN0PEJhdHRsZVNw" +
            "cml0ZVNsb3RFbnRyeT4gc2xvdHMpCiAgICAgICAgewogICAgICAgICAgICBpZiAoc2xvdHMgPT0gbnVsbCkgcmV0dXJuIDA7CiAg" +
            "ICAgICAgICAgIGludCBuID0gMDsKICAgICAgICAgICAgZm9yIChpbnQgaSA9IDA7IGkgPCBzbG90cy5Db3VudDsgaSsrKQogICAg" +
            "ICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IHNsb3RzW2ldOwogICAgICAgICAgICAgICAgaWYgKGUgPT0gbnVsbCB8" +
            "fCBlLmJhdHRsZVNwcml0ZSAhPSBudWxsKSBjb250aW51ZTsKICAgICAgICAgICAgICAgIGlmIChzdHJpbmcuSXNOdWxsT3JFbXB0" +
            "eShlLmFzc2V0UGF0aCkpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgU3ByaXRlIHNwID0gQ2xhc3NTcHJpdGVQb29sUHJvZmls" +
            "ZS5UcnlMb2FkU3ByaXRlQXRQYXRoKGUuYXNzZXRQYXRoKTsKICAgICAgICAgICAgICAgIGlmIChzcCA9PSBudWxsKSBjb250aW51" +
            "ZTsKICAgICAgICAgICAgICAgIGUuYmF0dGxlU3ByaXRlID0gc3A7CiAgICAgICAgICAgICAgICBuKys7CiAgICAgICAgICAgIH0K" +
            "ICAgICAgICAgICAgcmV0dXJuIG47CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgTGlzdDxTcHJpdGU+IEdldFRyb29wT3JXYXZl" +
            "UG9vbChVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xvdHNBbmRMaXN0cygpOwogICAgICAgICAg" +
            "ICByZXR1cm4gdHlwZSBzd2l0Y2gKICAgICAgICAgICAgewogICAgICAgICAgICAgICAgVW5pdFR5cGUuUGVhc2FudCA9PiBwZWFz" +
            "YW50QmF0dGxlU3ByaXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLldhcnJpb3IgPT4gd2FycmlvckJhdHRsZVNwcml0ZXMs" +
            "CiAgICAgICAgICAgICAgICBVbml0VHlwZS5LbmlnaHQgPT4ga25pZ2h0QmF0dGxlU3ByaXRlcywKICAgICAgICAgICAgICAgIFVu" +
            "aXRUeXBlLlByaWVzdCA9PiBwcmllc3RCYXR0bGVTcHJpdGVzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25JbXAgPT4g" +
            "ZGVtb25JbXBCYXR0bGVTcHJpdGVzLAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25IZWxsaG91bmQgPT4gaGVsbGhvdW5k" +
            "QmF0dGxlU3ByaXRlcywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLkRlbW9uRHJlYWRMb3JkID0+IGRyZWFkTG9yZEJhdHRsZVNw" +
            "cml0ZXMsCiAgICAgICAgICAgICAgICBfID0+IG51bGwKICAgICAgICAgICAgfTsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBM" +
            "aXN0PHN0cmluZz4gR2V0VHJvb3BPcldhdmVQYXRocyhVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJl" +
            "U2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICByZXR1cm4gdHlwZSBzd2l0Y2gKICAgICAgICAgICAgewogICAgICAgICAgICAg" +
            "ICAgVW5pdFR5cGUuUGVhc2FudCA9PiBwZWFzYW50QmF0dGxlQXNzZXRQYXRocywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLldh" +
            "cnJpb3IgPT4gd2FycmlvckJhdHRsZUFzc2V0UGF0aHMsCiAgICAgICAgICAgICAgICBVbml0VHlwZS5LbmlnaHQgPT4ga25pZ2h0" +
            "QmF0dGxlQXNzZXRQYXRocywKICAgICAgICAgICAgICAgIFVuaXRUeXBlLlByaWVzdCA9PiBwcmllc3RCYXR0bGVBc3NldFBhdGhz" +
            "LAogICAgICAgICAgICAgICAgVW5pdFR5cGUuRGVtb25JbXAgPT4gZGVtb25JbXBCYXR0bGVBc3NldFBhdGhzLAogICAgICAgICAg" +
            "ICAgICAgVW5pdFR5cGUuRGVtb25IZWxsaG91bmQgPT4gaGVsbGhvdW5kQmF0dGxlQXNzZXRQYXRocywKICAgICAgICAgICAgICAg" +
            "IFVuaXRUeXBlLkRlbW9uRHJlYWRMb3JkID0+IGRyZWFkTG9yZEJhdHRsZUFzc2V0UGF0aHMsCiAgICAgICAgICAgICAgICBfID0+" +
            "IG51bGwKICAgICAgICAgICAgfTsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBCYXR0bGVTcHJpdGVTbG90RW50cnkgRmluZExl" +
            "Z2VuZFNsb3QoVW5pdFR5cGUgdHlwZSkKICAgICAgICB7CiAgICAgICAgICAgIEVuc3VyZVNsb3RzQW5kTGlzdHMoKTsKICAgICAg" +
            "ICAgICAgLy8gTGFkeUd1aW5ldmVyZSAvIEd1aW5ldmVyZSBhbGlhcwogICAgICAgICAgICBmb3IgKGludCBpID0gMDsgaSA8IGdv" +
            "b2RMZWdlbmRTbG90cy5Db3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGdvb2RMZWdlbmRT" +
            "bG90c1tpXTsKICAgICAgICAgICAgICAgIGlmIChlID09IG51bGwpIGNvbnRpbnVlOwogICAgICAgICAgICAgICAgaWYgKGUudW5p" +
            "dFR5cGUgPT0gdHlwZSkgcmV0dXJuIGU7CiAgICAgICAgICAgICAgICBpZiAoKHR5cGUgPT0gVW5pdFR5cGUuR3VpbmV2ZXJlIHx8" +
            "IHR5cGUgPT0gVW5pdFR5cGUuTGFkeUd1aW5ldmVyZSkKICAgICAgICAgICAgICAgICAgICAmJiAoZS51bml0VHlwZSA9PSBVbml0" +
            "VHlwZS5HdWluZXZlcmUgfHwgZS51bml0VHlwZSA9PSBVbml0VHlwZS5MYWR5R3VpbmV2ZXJlKSkKICAgICAgICAgICAgICAgICAg" +
            "ICByZXR1cm4gZTsKICAgICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbnVsbDsKICAgICAgICB9CgogICAgICAgIHB1Ymxp" +
            "YyBCYXR0bGVTcHJpdGVTbG90RW50cnkgRmluZEJvc3NTbG90KFVuaXRUeXBlIHR5cGUpCiAgICAgICAgewogICAgICAgICAgICBF" +
            "bnN1cmVTbG90c0FuZExpc3RzKCk7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgZXZpbEJvc3NTbG90cy5Db3VudDsg" +
            "aSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGV2aWxCb3NzU2xvdHNbaV07CiAgICAgICAgICAgICAg" +
            "ICBpZiAoZSAhPSBudWxsICYmIGUudW5pdFR5cGUgPT0gdHlwZSkgcmV0dXJuIGU7CiAgICAgICAgICAgIH0KICAgICAgICAgICAg" +
            "cmV0dXJuIG51bGw7CiAgICAgICAgfQoKICAgICAgICBwdWJsaWMgU3ByaXRlIEdldExlZ2VuZEJhdHRsZVNwcml0ZShVbml0VHlw" +
            "ZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIGUgPSBGaW5kTGVnZW5kU2xvdCh0eXBlKTsKICAgICAgICAgICAgcmV0" +
            "dXJuIGUgIT0gbnVsbCA/IGUuYmF0dGxlU3ByaXRlIDogbnVsbDsKICAgICAgICB9CgogICAgICAgIHB1YmxpYyBTcHJpdGUgR2V0" +
            "Qm9zc0JhdHRsZVNwcml0ZShVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIGUgPSBGaW5kQm9zc1Nsb3Qo" +
            "dHlwZSk7CiAgICAgICAgICAgIHJldHVybiBlICE9IG51bGwgPyBlLmJhdHRsZVNwcml0ZSA6IG51bGw7CiAgICAgICAgfQoKICAg" +
            "ICAgICBwdWJsaWMgaW50IENvdW50VXNhYmxlUG9vbChVbml0VHlwZSB0eXBlKQogICAgICAgIHsKICAgICAgICAgICAgdmFyIHBv" +
            "b2wgPSBHZXRUcm9vcE9yV2F2ZVBvb2wodHlwZSk7CiAgICAgICAgICAgIGlmIChwb29sID09IG51bGwpIHJldHVybiAwOwogICAg" +
            "ICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgcG9vbC5Db3VudDsgaSsrKQogICAgICAg" +
            "ICAgICAgICAgaWYgKHBvb2xbaV0gIT0gbnVsbCkgbisrOwogICAgICAgICAgICByZXR1cm4gbjsKICAgICAgICB9CgogICAgICAg" +
            "IHB1YmxpYyBpbnQgQ291bnRBc3NpZ25lZExlZ2VuZEJhdHRsZXMoKQogICAgICAgIHsKICAgICAgICAgICAgRW5zdXJlU2xvdHNB" +
            "bmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBpIDwgZ29vZExlZ2Vu" +
            "ZFNsb3RzLkNvdW50OyBpKyspCiAgICAgICAgICAgIHsKICAgICAgICAgICAgICAgIHZhciBlID0gZ29vZExlZ2VuZFNsb3RzW2ld" +
            "OwogICAgICAgICAgICAgICAgaWYgKGUgPT0gbnVsbCB8fCBlLnNvZnRPcHRpb25hbCkgY29udGludWU7CiAgICAgICAgICAgICAg" +
            "ICBpZiAoZS5iYXR0bGVTcHJpdGUgIT0gbnVsbCkgbisrOwogICAgICAgICAgICB9CiAgICAgICAgICAgIHJldHVybiBuOwogICAg" +
            "ICAgIH0KCiAgICAgICAgcHVibGljIGludCBDb3VudEFzc2lnbmVkQm9zc0JhdHRsZXMoKQogICAgICAgIHsKICAgICAgICAgICAg" +
            "RW5zdXJlU2xvdHNBbmRMaXN0cygpOwogICAgICAgICAgICBpbnQgbiA9IDA7CiAgICAgICAgICAgIGZvciAoaW50IGkgPSAwOyBp" +
            "IDwgZXZpbEJvc3NTbG90cy5Db3VudDsgaSsrKQogICAgICAgICAgICB7CiAgICAgICAgICAgICAgICB2YXIgZSA9IGV2aWxCb3Nz" +
            "U2xvdHNbaV07CiAgICAgICAgICAgICAgICBpZiAoZSAhPSBudWxsICYmIGUuYmF0dGxlU3ByaXRlICE9IG51bGwpIG4rKzsKICAg" +
            "ICAgICAgICAgfQogICAgICAgICAgICByZXR1cm4gbjsKICAgICAgICB9CiAgICB9Cn0K"
            ;

        public static readonly (string rel, string b64)[] All =
        {
            ("Assets/Scripts/Data/StageLayoutProfile.cs", B64_StageLayoutProfile),
            ("Assets/Scripts/Data/SceneBackgroundProfile.cs", B64_SceneBackgroundProfile),
            ("Assets/Scripts/Data/ClassSpritePoolProfile.cs", B64_ClassSpritePoolProfile),
            ("Assets/Scripts/Data/LegendPewPortraitProfile.cs", B64_LegendPewPortraitProfile),
            ("Assets/Scripts/Data/BattleGoodEvilSpritePoolProfile.cs", B64_BattleGoodEvilSpritePoolProfile),
        };

        public static string Decode(string b64)
        {
            if (string.IsNullOrEmpty(b64)) return null;
            try
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            }
            catch { return null; }
        }
    }

}
#endif
