#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Always-available menu entries that do not depend on TextMeshPro or gameplay scripts.
/// Art Tools live HERE so they show under Arthurian even when Force Sync still pins old main
/// (importers are downloaded on demand from the package tip SHA).
/// </summary>
public static class ArthurianMenuBootstrap
{
    /// <summary>
    /// Package tip with Modular/Battle importers + Art Tools aliases.
    /// Update when shipping a new Sync pin (Force Sync prefers this over main).
    /// </summary>
    public const string ArtToolsPinSha = "43252d0";

    const string Repo = "airizarry9938/DemonKnight";

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
        bool hasModularImporter = TypeExists("ModularSpriteSheetImporterWindow");
        bool hasBattleSlicer = TypeExists("BattleSpriteSheetSlicerWindow");

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
        string modularImporterPath = Path.Combine(editorDir, "ModularSpriteSheetImporterWindow.cs");
        string battleSlicerPath = Path.Combine(editorDir, "BattleSpriteSheetSlicerWindow.cs");
        bool alignFileOnDisk = File.Exists(alignPath);
        bool syncOnDisk = File.Exists(syncPath);
        bool menuBootOnDisk = File.Exists(menuBootPath);
        bool modularImporterOnDisk = File.Exists(modularImporterPath);
        bool battleSlicerOnDisk = File.Exists(battleSlicerPath);
        bool hasSyncType = TypeExists("ArthurianSyncFromGitHub");
        string builderStamp = File.Exists(builderPath)
            ? File.GetLastWriteTimeUtc(builderPath).ToString("u") + " UTC"
            : "MISSING";

        bool looksLikeFirstBuild = !hasPews || packageId.StartsWith("(no version");

        string msg =
            "DemonKnight / Arthurian diagnose\n\n" +
            $"Package: {packageId} ({packageGit})\n" +
            $"Art Tools pin: {ArtToolsPinSha}\n" +
            $"Assets path:\n{Application.dataPath}\n" +
            $"Sync menus on disk: {(syncOnDisk ? "YES" : "NO")}\n" +
            $"Sync menus compiled: {(hasSyncType ? "YES" : "NO")}\n" +
            $"Diagnose host on disk: {(menuBootOnDisk ? "YES" : "NO")}\n" +
            $"Align script on disk: {(alignFileOnDisk ? "YES" : "NO")}\n" +
            $"Align menu compiled: {(hasAlignType ? "YES" : "NO")}\n" +
            $"Art importer on disk: {(modularImporterOnDisk ? "YES" : "NO")}\n" +
            $"Art importer compiled: {(hasModularImporter ? "YES" : "NO")}\n" +
            $"Battle slicer on disk: {(battleSlicerOnDisk ? "YES" : "NO")}\n" +
            $"Battle slicer compiled: {(hasBattleSlicer ? "YES" : "NO")}\n" +
            $"ArthurianSceneBuilder.cs mtime: {builderStamp}\n\n" +
            $"TextMeshPro loaded: {(hasTmp ? "YES" : "NO")}\n" +
            $"Gameplay scripts: {(hasGameplay ? "YES" : "NO")}\n" +
            $"Church pew layout: {(hasPews ? "YES" : "NO")}\n" +
            $"Scene builder: {(hasBuilder ? "YES" : "NO")}\n\n";

        if (!hasModularImporter || !hasBattleSlicer)
        {
            msg +=
                "ART TOOLS NOT COMPILED YET.\n" +
                "Use: Arthurian → Art Tools → Install Art Importers Now\n" +
                "(Force Sync from main alone will NOT get them until that PR merges.)\n";
        }
        else
        {
            msg +=
                "Art tools ready.\n" +
                "• Arthurian → Art Tools → Modular Sprite Sheet Importer\n" +
                "• Arthurian → Art Tools → Battle Sprite Sheet Slicer\n";
        }

        Debug.Log(msg);
        EditorUtility.DisplayDialog("Arthurian Diagnose", msg, "OK");
    }

    [MenuItem("Arthurian/Art Tools/Install Art Importers Now", false, 40)]
    public static void InstallArtImportersNow()
    {
        if (!EditorUtility.DisplayDialog(
                "Install Art Importers",
                "Download Art Tools Editor scripts AND their runtime data scripts from pin "
                + ArtToolsPinSha + "?\n\n"
                + "Fixes CS0246 (CharacterDefinition / ModularSpriteLibrary missing).",
                "Download", "Cancel"))
            return;

        string[] rels =
        {
            // Editor tools
            "Assets/Editor/ModularSpriteSheetImporterWindow.cs",
            "Assets/Editor/BattleSpriteSheetSlicerWindow.cs",
            "Assets/Editor/CharacterCatalogSeeder.cs",
            "Assets/Editor/CharacterDatabaseEditor.cs",
            "Assets/Editor/DemonKnightPackageVersion.cs",
            "Assets/Editor/ArthurianMenuBootstrap.cs",
            // Runtime data the Editor tools reference (CS0246 without these)
            "Assets/Scripts/Data/ModularSpriteLibrary.cs",
            "Assets/Scripts/Data/TownsfolkAppearance.cs",
            "Assets/Scripts/Data/Characters/CharacterDefinition.cs",
            "Assets/Scripts/Data/Characters/CharacterDatabase.cs",
            "Assets/Scripts/UI/TownsfolkVisualApplier.cs",
            "Assets/Scripts/UI/TownsfolkPortraitView.cs",
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
            $"Installed {ok}/{rels.Length} @ {ArtToolsPinSha}.\n"
            + "Wait for compile, then open:\n"
            + "Arthurian → Art Tools → Modular Sprite Sheet Importer\n\n"
            + (string.IsNullOrEmpty(errors) ? "" : "Errors:\n" + errors),
            "OK");
    }

    [MenuItem("Arthurian/Art Tools/Modular Sprite Sheet Importer", false, 41)]
    public static void OpenModularImporter()
    {
        if (!TryOpenEditorWindow("ModularSpriteSheetImporterWindow"))
        {
            if (EditorUtility.DisplayDialog(
                    "Art Tools",
                    "Modular importer script is not compiled yet.\nDownload it now?",
                    "Install", "Cancel"))
            {
                InstallArtImportersNow();
            }
        }
    }

    [MenuItem("Arthurian/Art Tools/Battle Sprite Sheet Slicer", false, 42)]
    public static void OpenBattleSlicer()
    {
        if (!TryOpenEditorWindow("BattleSpriteSheetSlicerWindow"))
        {
            if (EditorUtility.DisplayDialog(
                    "Art Tools",
                    "Battle slicer script is not compiled yet.\nDownload it now?",
                    "Install", "Cancel"))
            {
                InstallArtImportersNow();
            }
        }
    }

    [MenuItem("Arthurian/Art Tools/Seed Default Character Definitions", false, 43)]
    public static void SeedCharacters()
    {
        var t = FindType("CharacterCatalogSeeder");
        var m = t?.GetMethod("SeedAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (m == null)
        {
            if (EditorUtility.DisplayDialog(
                    "Art Tools",
                    "Character seeder not compiled yet.\nDownload Art Importers now?",
                    "Install", "Cancel"))
                InstallArtImportersNow();
            return;
        }
        m.Invoke(null, null);
    }

    [MenuItem("Arthurian/Art Tools/Auto-Collect Character Database", false, 44)]
    public static void AutoCollectCharacters()
    {
        var t = FindType("CharacterDatabaseEditor");
        var m = t?.GetMethod("MenuAutoCollect", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (m == null)
        {
            if (EditorUtility.DisplayDialog(
                    "Art Tools",
                    "CharacterDatabase editor not compiled yet.\nDownload Art Importers now?",
                    "Install", "Cancel"))
                InstallArtImportersNow();
            return;
        }
        m.Invoke(null, null);
    }

    static bool TryOpenEditorWindow(string typeName)
    {
        var t = FindType(typeName);
        if (t == null) return false;
        EditorWindow.GetWindow(t);
        return true;
    }

    static Type FindType(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(typeName, false);
                if (t != null) return t;
            }
            catch { /* ignore */ }
        }
        return null;
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
            "Art tools: Arthurian → Art Tools → Install Art Importers Now\n"
            + "(Force Sync from main will not bring Art Tools until that PR merges.)\n\n"
            + "Then: Modular Sprite Sheet Importer / Battle Sprite Sheet Slicer.\n\n"
            + "Project Assets path:\n" + Application.dataPath,
            "OK");
        Debug.Log("[DemonKnight] Assets path: " + Application.dataPath);
    }
}
#endif
