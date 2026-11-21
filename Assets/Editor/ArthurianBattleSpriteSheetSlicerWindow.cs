#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Battle sprite sheet slicer: key out #1C2532, Point filter, grid slice with
/// bottom-center pivot (0.5, 0.0) so feet/tails sit on the cobblestone anchor.
/// Menu: DemonKnight → Battle Sprite Sheet Slicer
/// </summary>
public class ArthurianBattleSpriteSheetSlicerWindow : EditorWindow
{
    Texture2D sheet;
    string outputFolder = "Assets/Art/Battle/Sliced";
    Color keyColor = new Color(28f / 255f, 37f / 255f, 50f / 255f, 1f);
    float keyTolerance = 18f;
    bool pointFilter = true;
    int gridPreset; // 0=2x3, 1=1x3, 2=1x4, 3=custom
    int customCols = 2;
    int customRows = 3;
    string label = "BattleSheet";
    Vector2 scroll;

    // Owned only by this window — do not also register these paths on ArthurianMenuBootstrap.
    [MenuItem("Arthurian/Art Tools/Battle Sprite Sheet Slicer", false, 42)]
    [MenuItem("DemonKnight/Battle Sprite Sheet Slicer", false, 42)]
    public static void Open()
    {
        var win = GetWindow<ArthurianBattleSpriteSheetSlicerWindow>("Battle Sprite Slicer");
        win.minSize = new Vector2(480, 420);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("DemonKnight — Battle Sprite Sheet Slicer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Strips #1C2532, writes _Clean.png, FilterMode.Point, slices grid cells with pivot Bottom-Center (0.5, 0.0).\n" +
            "Presets: 2×3 · 1×3 · 1×4.",
            MessageType.Info);

        sheet = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet", sheet, typeof(Texture2D), false);
        label = EditorGUILayout.TextField("Output Label", label);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        keyColor = EditorGUILayout.ColorField("Key Color (#1C2532)", keyColor);
        keyTolerance = EditorGUILayout.Slider("Key Tolerance", keyTolerance, 0f, 64f);
        pointFilter = EditorGUILayout.Toggle("FilterMode.Point + uncompressed", pointFilter);

        EditorGUILayout.Space();
        gridPreset = GUILayout.Toolbar(gridPreset, new[] { "2×3", "1×3", "1×4", "Custom" });
        int cols = 2, rows = 3;
        switch (gridPreset)
        {
            case 0: cols = 2; rows = 3; break;
            case 1: cols = 1; rows = 3; break;
            case 2: cols = 1; rows = 4; break;
            default:
                customCols = EditorGUILayout.IntField("Columns", Mathf.Max(1, customCols));
                customRows = EditorGUILayout.IntField("Rows", Mathf.Max(1, customRows));
                cols = customCols;
                rows = customRows;
                break;
        }

        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(sheet == null);
        if (GUILayout.Button("Clean → Slice (Bottom-Center Pivot)", GUILayout.Height(40)))
            Run(cols, rows);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndScrollView();
    }

    void Run(int cols, int rows)
    {
        EnsureFolder(outputFolder);
        string srcPath = AssetDatabase.GetAssetPath(sheet);
        Texture2D readable = LoadReadable(sheet, srcPath);
        if (readable == null)
        {
            EditorUtility.DisplayDialog("Battle Slicer", "Could not read sheet pixels.", "OK");
            return;
        }

        Texture2D cleaned = KeyOut(readable, keyColor, keyTolerance);
        string safeLabel = string.IsNullOrWhiteSpace(label) ? "BattleSheet" : label.Trim();
        string cleanPath = $"{outputFolder}/{safeLabel}_Clean.png";
        File.WriteAllBytes(cleanPath, cleaned.EncodeToPNG());
        AssetDatabase.ImportAsset(cleanPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(cleanPath, cols, rows, pointFilter);
        AssetDatabase.ImportAsset(cleanPath, ImportAssetOptions.ForceUpdate);

        int count = 0;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(cleanPath))
            if (obj is Sprite) count++;

        EditorUtility.DisplayDialog(
            "Battle Slicer",
            $"Wrote {cleanPath}\n{count} sprites · pivot Bottom-Center (0.5, 0.0).\n" +
            "Assign battleSprite / portraitSprite on CharacterDefinition assets.",
            "OK");
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

        Texture2D copy;
        try
        {
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

        byte[] bytes = File.ReadAllBytes(assetPath);
        var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tmp.LoadImage(bytes);
        int w = tmp.width;
        int h = tmp.height;
        Object.DestroyImmediate(tmp);

        int cellW = Mathf.Max(1, w / Mathf.Max(1, cols));
        int cellH = Mathf.Max(1, h / Mathf.Max(1, rows));
        var metas = new List<SpriteMetaData>();
        int index = 0;
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
#pragma warning disable CS0618 // TextureImporter.spritesheet — still the reliable multi-slice write path in U6
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
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
