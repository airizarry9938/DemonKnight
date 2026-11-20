#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ArthurianAutobattler;

/// <summary>
/// Keys out slate backgrounds, slices pose/outfit/head-prop sheets, and assigns
/// sprites into a ModularSpriteLibrary. Menu: DemonKnight → Modular Sprite Sheet Importer
/// </summary>
public class ModularSpriteSheetImporterWindow : EditorWindow
{
    const string DefaultLibraryPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
    const string DefaultOutputFolder = "Assets/Art/Modular/Sliced";

    Texture2D posesSheet;
    Texture2D outfitsSheet;
    Texture2D headsPropsSheet;
    ModularSpriteLibrary targetLibrary;
    string outputFolder = DefaultOutputFolder;
    Color keyColor = new Color(28f / 255f, 37f / 255f, 50f / 255f, 1f); // #1C2532
    float keyTolerance = 18f;
    bool pointFilter = true;
    Vector2 scroll;

    [MenuItem("DemonKnight/Modular Sprite Sheet Importer")]
    public static void Open()
    {
        var win = GetWindow<ModularSpriteSheetImporterWindow>("Modular Sprite Importer");
        win.minSize = new Vector2(520, 480);
    }

    void OnEnable()
    {
        if (targetLibrary == null)
            targetLibrary = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(DefaultLibraryPath);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("DemonKnight — Modular Townsfolk Sheets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drop three sheets:\n" +
            "• Poses 1×4 (grayscale bodies)\n" +
            "• Outfits 2×6 (pre-colored, transparent skin)\n" +
            "• Heads & Props 2×6 (row0 hair / row1 helms+props — adjustable)\n\n" +
            "Importer keys out #1C2532, writes _Clean.png, Point filter, slices cells, assigns ModularSpriteLibrary.",
            MessageType.Info);

        posesSheet = (Texture2D)EditorGUILayout.ObjectField("Poses 1×4", posesSheet, typeof(Texture2D), false);
        outfitsSheet = (Texture2D)EditorGUILayout.ObjectField("Outfits 2×6", outfitsSheet, typeof(Texture2D), false);
        headsPropsSheet = (Texture2D)EditorGUILayout.ObjectField("Heads & Props 2×6", headsPropsSheet, typeof(Texture2D), false);

        EditorGUILayout.Space();
        targetLibrary = (ModularSpriteLibrary)EditorGUILayout.ObjectField(
            "Target Library", targetLibrary, typeof(ModularSpriteLibrary), false);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        keyColor = EditorGUILayout.ColorField("Key Color (#1C2532)", keyColor);
        keyTolerance = EditorGUILayout.Slider("Key Tolerance", keyTolerance, 0f, 64f);
        pointFilter = EditorGUILayout.Toggle("FilterMode.Point + uncompressed", pointFilter);

        EditorGUILayout.Space();
        if (GUILayout.Button("Create Library Asset If Missing", GUILayout.Height(28)))
            EnsureLibraryAsset();

        EditorGUI.BeginDisabledGroup(posesSheet == null && outfitsSheet == null && headsPropsSheet == null);
        if (GUILayout.Button("Import → Clean → Slice → Assign Library", GUILayout.Height(40)))
            RunImport();
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndScrollView();
    }

    void EnsureLibraryAsset()
    {
        if (targetLibrary != null) return;
        string dir = Path.GetDirectoryName(DefaultLibraryPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            EnsureFolder(dir);
        targetLibrary = CreateInstance<ModularSpriteLibrary>();
        targetLibrary.EnsureSizedArrays();
        AssetDatabase.CreateAsset(targetLibrary, DefaultLibraryPath);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(targetLibrary);
        Debug.Log("[DemonKnight] Created " + DefaultLibraryPath);
    }

    void RunImport()
    {
        EnsureLibraryAsset();
        if (targetLibrary == null)
        {
            EditorUtility.DisplayDialog("Importer", "Assign or create a ModularSpriteLibrary first.", "OK");
            return;
        }

        EnsureFolder(outputFolder);
        targetLibrary.EnsureSizedArrays();
        Undo.RecordObject(targetLibrary, "Import Modular Sprites");

        if (posesSheet != null)
        {
            var sprites = ProcessSheet(posesSheet, "Poses", cols: 4, rows: 1, outputFolder);
            for (int i = 0; i < 4 && i < sprites.Count; i++)
                targetLibrary.basePoses[i] = sprites[i];
        }

        if (outfitsSheet != null)
        {
            var sprites = ProcessSheet(outfitsSheet, "Outfits", cols: 6, rows: 2, outputFolder);
            for (int i = 0; i < 6 && i < sprites.Count; i++)
                targetLibrary.outfits[i] = sprites[i];
        }

        if (headsPropsSheet != null)
        {
            // 2×6 = 12 cells: first 6 → heads (row0 hair, row1 often helms), next/overlap props.
            var sprites = ProcessSheet(headsPropsSheet, "HeadsProps", cols: 6, rows: 2, outputFolder);
            var heads = new List<Sprite>();
            var isHair = new List<bool>();
            var props = new List<Sprite> { null }; // index 0 = None
            for (int i = 0; i < sprites.Count; i++)
            {
                // Row-major: 0..5 row0 (hair), 6..11 row1 (helms / props).
                if (i < 6)
                {
                    heads.Add(sprites[i]);
                    isHair.Add(true);
                }
                else if (i < 9)
                {
                    heads.Add(sprites[i]);
                    isHair.Add(false); // helms
                }
                else
                {
                    props.Add(sprites[i]);
                }
            }
            targetLibrary.AssignHeadsFromSprites(heads, isHair);
            targetLibrary.props = props.ToArray();
        }

        EditorUtility.SetDirty(targetLibrary);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Also drop a Resources copy path hint.
        string resourcesDir = "Assets/Resources/Art/Modular";
        EnsureFolder(resourcesDir);
        string resourcesAsset = resourcesDir + "/ModularSpriteLibrary.asset";
        if (AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resourcesAsset) == null)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(targetLibrary), resourcesAsset);
            AssetDatabase.SaveAssets();
        }

        EditorUtility.DisplayDialog(
            "Modular Import Complete",
            $"Assigned sprites into {AssetDatabase.GetAssetPath(targetLibrary)}.\n" +
            "Resources copy (if new): Assets/Resources/Art/Modular/ModularSpriteLibrary.asset\n" +
            "Assign TownRNGGenerator.modularLibrary or rely on Resources.Load.",
            "OK");
    }

    List<Sprite> ProcessSheet(Texture2D source, string label, int cols, int rows, string folder)
    {
        var result = new List<Sprite>();
        if (source == null) return result;

        string srcPath = AssetDatabase.GetAssetPath(source);
        Texture2D readable = LoadReadable(source, srcPath);
        if (readable == null)
        {
            Debug.LogError("[DemonKnight] Could not read pixels from " + srcPath);
            return result;
        }

        Texture2D cleaned = KeyOut(readable, keyColor, keyTolerance);
        string cleanPath = $"{folder}/{label}_Clean.png";
        EnsureFolder(folder);
        File.WriteAllBytes(cleanPath, cleaned.EncodeToPNG());
        AssetDatabase.ImportAsset(cleanPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(cleanPath, cols, rows, pointFilter);
        AssetDatabase.ImportAsset(cleanPath, ImportAssetOptions.ForceUpdate);

        var assets = AssetDatabase.LoadAllAssetsAtPath(cleanPath);
        // Prefer sliced sprites in grid order by name suffix _0, _1...
        var ordered = new List<(int idx, Sprite sp)>();
        foreach (var obj in assets)
        {
            if (obj is Sprite sp)
            {
                int idx = ParseTrailingIndex(sp.name);
                ordered.Add((idx >= 0 ? idx : ordered.Count, sp));
            }
        }
        ordered.Sort((a, b) => a.idx.CompareTo(b.idx));
        foreach (var o in ordered) result.Add(o.sp);

        if (result.Count == 0)
        {
            // Single sprite fallback.
            var single = AssetDatabase.LoadAssetAtPath<Sprite>(cleanPath);
            if (single != null) result.Add(single);
        }

        Debug.Log($"[DemonKnight] Sliced {label}: {result.Count} sprites → {cleanPath}");
        return result;
    }

    static int ParseTrailingIndex(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        int us = name.LastIndexOf('_');
        if (us < 0) return -1;
        if (int.TryParse(name.Substring(us + 1), out int n)) return n;
        return -1;
    }

    static Texture2D LoadReadable(Texture2D source, string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        bool wasReadable = false;
        TextureImporterCompression wasCompression = TextureImporterCompression.Uncompressed;
        FilterMode wasFilter = FilterMode.Bilinear;
        if (importer != null)
        {
            wasReadable = importer.isReadable;
            wasCompression = importer.textureCompression;
            wasFilter = importer.filterMode;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Texture2D copy = null;
        try
        {
            // Read via RenderTexture to survive non-readable edge cases.
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
        finally
        {
            if (importer != null)
            {
                importer.isReadable = wasReadable;
                importer.textureCompression = wasCompression;
                importer.filterMode = wasFilter;
                importer.SaveAndReimport();
            }
        }
        return copy;
    }

    static Texture2D KeyOut(Texture2D src, Color key, float tolerance)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] pixels = src.GetPixels32();
        byte kr = (byte)Mathf.RoundToInt(key.r * 255f);
        byte kg = (byte)Mathf.RoundToInt(key.g * 255f);
        byte kb = (byte)Mathf.RoundToInt(key.b * 255f);
        float tol = Mathf.Max(0f, tolerance);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            float dist = Mathf.Abs(p.r - kr) + Mathf.Abs(p.g - kg) + Mathf.Abs(p.b - kb);
            if (dist <= tol * 3f)
                pixels[i] = new Color32(0, 0, 0, 0);
        }
        dst.SetPixels32(pixels);
        dst.Apply();
        return dst;
    }

    static void ConfigureImporter(string assetPath, int cols, int rows, bool point)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;

        Texture2D preview = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        // After write, re-read dimensions from file.
        byte[] bytes = File.ReadAllBytes(assetPath);
        var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tmp.LoadImage(bytes);
        int w = tmp.width;
        int h = tmp.height;
        UnityEngine.Object.DestroyImmediate(tmp);

        int cellW = Mathf.Max(1, w / Mathf.Max(1, cols));
        int cellH = Mathf.Max(1, h / Mathf.Max(1, rows));
        var metas = new List<SpriteMetaData>();
        int index = 0;
        // Unity sprite sheet: row 0 is bottom. Artist sheets usually top-first — flip rows.
        for (int row = 0; row < rows; row++)
        {
            int y = (rows - 1 - row) * cellH;
            for (int col = 0; col < cols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = Path.GetFileNameWithoutExtension(assetPath) + "_" + index,
                    rect = new Rect(col * cellW, y, cellW, cellH),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0.0f)
                });
                index++;
            }
        }
        importer.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    static void EnsureFolder(string path)
    {
        path = path.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
