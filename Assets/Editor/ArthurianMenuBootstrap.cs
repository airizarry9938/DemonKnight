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
    public const string ArtToolsPinSha = "a58b1f60bb9eca4e00f0aebbef63d576b468c873";

    /// <summary>
    /// Hard pin for Phase 2–5 emergency fetch (absolute raw URLs). Updated each ship.
    /// Force Sync often skips upgrading ArthurianSyncFromGitHub — this menu lives here so
    /// a normal Sync of MenuBootstrap still gives Andrew a one-shot download.
    /// </summary>
    public const string Phase25FetchPinSha = "62c5a33cd9a9295386ea62929effa7cc9eb23af5";

    const string Repo = "airizarry9938/DemonKnight";
    const string RawBase = "https://raw.githubusercontent.com/" + Repo + "/";
    const string JsDelivrBase = "https://cdn.jsdelivr.net/gh/" + Repo + "@";

    /// <summary>Master Spec Phase 2–5 files that Force Sync missed when Sync host was stale.</summary>
    public static readonly string[] Phase25ScriptPaths =
    {
        "Assets/Scripts/Data/SpriteAnimClip.cs",
        "Assets/Scripts/Data/StageLayoutProfile.cs",
        "Assets/Scripts/Data/ModularSpriteLibrary.cs",
        "Assets/Scripts/UI/ModularSpriteAnimator.cs",
        "Assets/Scripts/Systems/StageParallaxController.cs",
        "Assets/Scripts/Systems/DiceManager.cs",
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
            $"Package: {packageId} ({packageGit})\n" +
            $"Art Tools pin: {ArtToolsPinSha}\n" +
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
            $"Phase 2–5 StageLayoutProfile: {(hasStageLayout ? "YES" : "MISSING — run Fetch Missing Phase 2–5 Scripts")}\n" +
            $"Phase 4 StageParallaxController: {(hasParallax ? "YES" : "MISSING")}\n" +
            $"Phase 4 ModularSpriteAnimator: {(hasModAnim ? "YES" : "MISSING")}\n" +
            $"Phase 5 DiceManager: {(hasDiceMgr ? "YES" : "MISSING")}\n\n";

        if (!hasStageLayout || !hasParallax || !hasModAnim || !hasDiceMgr)
        {
            msg +=
                "CS0103 FIX: Arthurian → Fetch Missing Phase 2–5 Scripts\n" +
                "(downloads StageLayoutProfile / ModularSpriteAnimator / StageParallax / DiceManager by absolute raw URL)\n\n";
        }

        if (!string.IsNullOrEmpty(strayHits))
            msg += "Found under Editor (search):\n" + strayHits + "\n";

        if (hasModularImporter && hasBattleSlicer)
        {
            msg +=
                "Art tools READY — do NOT Install again.\n" +
                "Open the windows (not Install):\n" +
                "• Arthurian → Art Tools → Modular Sprite Sheet Importer\n" +
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
    /// Copies filled Art/Modular library into Resources so Play mode pews stop showing boxes.
    /// </summary>
    [MenuItem("Arthurian/Art Tools/Verify & Sync Modular Library", false, 38)]
    public static void VerifyAndSyncModularLibrary()
    {
        const string artPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
        const string resPath = "Assets/Resources/Art/Modular/ModularSpriteLibrary.asset";

        var art = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(artPath);
        if (art == null)
        {
            // Search any ModularSpriteLibrary under Assets/Art
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
            EditorUtility.DisplayDialog(
                "Modular Library",
                "No ModularSpriteLibrary found under Assets/Art.\n\n" +
                "Open Modular Sprite Sheet Importer → Create Library → Import sheets first.",
                "OK");
            return;
        }

        int poses = CountSprites(art.basePoses);
        int outfits = CountSprites(art.outfits);
        int heads = art.heads != null ? art.heads.Length : 0;

        string artAssetPath = AssetDatabase.GetAssetPath(art);
        string resDir = "Assets/Resources/Art/Modular";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Art"))
            AssetDatabase.CreateFolder("Assets/Resources", "Art");
        if (!AssetDatabase.IsValidFolder(resDir))
            AssetDatabase.CreateFolder("Assets/Resources/Art", "Modular");

        if (AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resPath) != null)
            AssetDatabase.DeleteAsset(resPath);
        AssetDatabase.CopyAsset(artAssetPath, resPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TownsfolkVisualApplier.SetLibrary(null);

        var res = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resPath);
        int resPoses = res != null ? CountSprites(res.basePoses) : 0;

        EditorUtility.DisplayDialog(
            "Modular Library Synced",
            $"Source: {artAssetPath}\n" +
            $"poses {poses}/4 · outfits {outfits}/6 · heads {heads}\n\n" +
            $"Runtime copy: {resPath}\n" +
            $"Resources poses filled: {resPoses}\n\n" +
            (poses == 0
                ? "SOURCE LIBRARY HAS NO POSE SPRITES — re-run Import with Poses sheet assigned.\n"
                : "Stop Play → Main Menu → New Game to rebuild pews.\n"),
            "OK");
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
                "• Arthurian → Art Tools → Modular Sprite Sheet Importer\n" +
                "• Arthurian → Art Tools → Battle Sprite Sheet Slicer\n\n" +
                "If those still look like Install, run Arthurian → Diagnose Project.",
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
            "Assets/Scripts/Data/ModularSpriteLibrary.cs",
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
            + "   Arthurian → Art Tools → Modular Sprite Sheet Importer\n"
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
            "1) Arthurian → Bootstrap CompatApi + Sync Tools (pinned).\n"
            + "2) Arthurian → Fetch Missing Phase 2–5 Scripts (if CS0103 StageLayout/Dice/Anim).\n"
            + "3) Arthurian → Force Delete+Sync ALL Scripts.\n"
            + "4) Diagnose must show sync-phase25-fetch-menu (or newer).\n\n"
            + "Project Assets path:\n" + Application.dataPath,
            "OK");
        Debug.Log("[DemonKnight] Assets path: " + Application.dataPath);
    }

    /// <summary>
    /// DA-59 hard fix: download ONLY Master Spec Phase 2–5 scripts via absolute raw URLs.
    /// Does not depend on ArthurianSyncFromGitHub ScriptPaths (stale Sync host skips those files).
    /// </summary>
    [MenuItem("Arthurian/Stub Phase 2–5 Duplicate Standalones", false, 4)]
    [MenuItem("Arthurian/Fetch Missing Phase 2–5 Scripts", false, 5)]
    public static void FetchMissingPhase25Scripts()
    {
        if (!EditorUtility.DisplayDialog(
                "Stub Phase 2–5 Duplicates",
                "Write type-definition-free stubs for Phase 2–5 standalones so they no longer\n"
                + "CS0101 against embeds in GameDataTypes / PaperDollSeatView / GameLoopManager / TavernDice.\n\n"
                + "Also fetches Art Architect editor helpers if missing.\n"
                + "Use when Force Sync re-downloaded full ModularSpriteLibrary.cs (etc.).",
                "Stub Now", "Cancel"))
            return;

        int ok = WritePhase25StubsInternal(out string report);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            ok > 0 ? "Phase 2–5 Stubs OK" : "Phase 2–5 Stubs Incomplete",
            report, "OK");
    }

    /// <summary>
    /// DA-59 CS0101 fix: NEVER re-download full Phase 2–5 standalones (old pins still have
    /// public class bodies). Always write type-definition-free stubs — real types are embedded.
    /// </summary>
    public static int FetchPhase25ScriptsInternal(out string report)
    {
        return WritePhase25StubsInternal(out report);
    }

    /// <summary>Hardcoded stubs for Phase 2–5 standalones (embedded elsewhere).</summary>
    public static int WritePhase25StubsInternal(out string report)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        var errors = new List<string>();
        int ok = 0;
        var map = new (string rel, string host, string typeName)[]
        {
            ("Assets/Scripts/Data/SpriteAnimClip.cs", "GameDataTypes.cs", "SpriteAnimClip"),
            ("Assets/Scripts/Data/StageLayoutProfile.cs", "GameDataTypes.cs", "StageLayoutProfile"),
            ("Assets/Scripts/Data/ModularSpriteLibrary.cs", "GameDataTypes.cs", "ModularSpriteLibrary"),
            ("Assets/Scripts/UI/ModularSpriteAnimator.cs", "PaperDollSeatView.cs", "ModularSpriteAnimator"),
            ("Assets/Scripts/Systems/StageParallaxController.cs", "GameLoopManager.cs", "StageParallaxController"),
            ("Assets/Scripts/Systems/DiceManager.cs", "TavernDiceMinigame.cs", "DiceManager"),
            // Art tools stay downloadable; not type-duplicated in embeds.
        };

        try
        {
            for (int i = 0; i < map.Length; i++)
            {
                var e = map[i];
                EditorUtility.DisplayProgressBar("Phase 2–5 Stubs", e.rel, (float)i / map.Length);
                try
                {
                    string dest = Path.Combine(projectRoot, e.rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    string stub =
                        "// EMBEDDED in " + e.host + " — DA-59 nuclear Sync fix (CS0101 if type redefined here).\n"
                        + "// Real type: ArthurianAutobattler." + e.typeName + "\n"
                        + "// Force Sync may still download this stub; keep it type-definition-free.\n";
                    if (File.Exists(dest))
                    {
                        try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }
                    }
                    File.WriteAllText(dest, stub);
                    ok++;
                    Debug.Log("[DemonKnight] Phase 2–5 stubbed " + e.rel);
                }
                catch (Exception ex)
                {
                    errors.Add($"{e.rel}: {ex.Message}");
                }
            }

            // Still try to fetch Art Architect helpers (not embedded) by absolute URL.
            foreach (string rel in new[]
                     {
                         "Assets/Editor/ArthurianStageLayoutProfileEnsure.cs",
                         "Assets/Editor/ArthurianArtArchitectWindow.cs",
                     })
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

        report = $"Stubbed/fetched Phase 2–5 paths ({ok} writes). Types live in GameDataTypes / PaperDoll / GameLoop / TavernDice.\n";
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
                if (!string.IsNullOrEmpty(body) && body.Length > 40
                    && body.IndexOf("404: Not Found", StringComparison.OrdinalIgnoreCase) < 0)
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
