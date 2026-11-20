#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-visible Arthurian menu: places 52 seat boxes on ChurchBackground pews.
/// Intentionally avoids TMPro and hard gameplay usings so it still shows when
/// ArthurianSceneBuilder fails to compile.
/// </summary>
[InitializeOnLoad]
public static class ArthurianAlignCrowdPews
{
    static ArthurianAlignCrowdPews()
    {
        // Old first-build SceneBuilder still uses enableWordWrapping — patch on load.
        EditorApplication.delayCall += PatchObsoleteTmpWrapping;
    }

    [MenuItem("Arthurian/Fix Obsolete TMP Warning", false, 11)]
    public static void PatchObsoleteTmpWrapping()
    {
        string path = Path.Combine(Application.dataPath, "Editor", "ArthurianSceneBuilder.cs");
        if (!File.Exists(path)) return;

        string text = File.ReadAllText(path);
        if (!text.Contains("enableWordWrapping")) return;

        string patched = text
            .Replace("tmp.enableWordWrapping = true;", "tmp.textWrappingMode = TextWrappingModes.Normal;")
            .Replace("enableWordWrapping = true;", "textWrappingMode = TextWrappingModes.Normal;");

        if (patched == text) return;

        File.WriteAllText(path, patched);
        AssetDatabase.Refresh();
        Debug.Log("[DemonKnight] Patched obsolete enableWordWrapping → textWrappingMode in ArthurianSceneBuilder.cs");
    }

    private const int TotalSeats = 52;
    private const int PewColumns = 3;
    private const int PewRows = 4;
    private const int PewCount = 12;

    // Capacities matching SomersetPewLayout defaults (sum = 52).
    private static readonly int[] Caps = { 5, 5, 5, 4, 5, 4, 4, 4, 4, 4, 4, 4 };

    [MenuItem("Arthurian/Align Crowd Grid To Church Pews", false, 10)]
    public static void Align()
    {
        var townCanvas = GameObject.Find("TownCanvas");
        if (townCanvas == null)
        {
            EditorUtility.DisplayDialog(
                "Last Knights of Somerset",
                "No TownCanvas in the open scene.\nOpen your town scene (CamelotMain) first.",
                "OK");
            return;
        }

        Transform bgTf = FindChurchBackground(townCanvas.transform);
        if (bgTf == null)
        {
            EditorUtility.DisplayDialog(
                "Last Knights of Somerset",
                "No ChurchBackground under TownCanvas.\n" +
                "Select TownCanvas → Right-click → UI → Image → rename to ChurchBackground,\n" +
                "stretch full-screen, assign the church sprite, then run this again.",
                "OK");
            return;
        }

        bgTf.SetAsFirstSibling();
        var bgRt = bgTf as RectTransform;
        if (bgRt == null) bgRt = bgTf.gameObject.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var bgImage = bgTf.GetComponent<Image>();
        if (bgImage == null) bgImage = bgTf.gameObject.AddComponent<Image>();
        bgImage.preserveAspect = true;
        if (bgImage.sprite == null)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Backgrounds/SomersetChurchInterior.jpg");
            if (sprite != null) bgImage.sprite = sprite;
        }

        Transform seatTf = bgTf.Find("PewSeatLayer");
        if (seatTf == null)
        {
            var seatGo = new GameObject("PewSeatLayer", typeof(RectTransform));
            seatGo.transform.SetParent(bgTf, false);
            seatTf = seatGo.transform;
        }

        var seatRt = seatTf as RectTransform;
        seatRt.anchorMin = Vector2.zero;
        seatRt.anchorMax = Vector2.one;
        seatRt.offsetMin = Vector2.zero;
        seatRt.offsetMax = Vector2.zero;

        var grid = seatTf.GetComponent<GridLayoutGroup>();
        if (grid != null) grid.enabled = false;

        // Wire gameplay components when those scripts compiled; otherwise still drop seat boxes.
        Component pewLayout = EnsureComponent(townCanvas, "ArthurianAutobattler.SomersetPewLayout");
        if (pewLayout != null)
        {
            SetField(pewLayout, "backgroundRect", bgRt);
            Invoke(pewLayout, "ResetToChurchArtDefaults");
        }

        Component churchUI = EnsureComponent(townCanvas, "ArthurianAutobattler.ChurchStageUIController");
        if (churchUI != null)
        {
            SetField(churchUI, "churchBackgroundImage", bgImage);
            SetField(churchUI, "pewLayout", pewLayout);
            SetField(churchUI, "pewSeatLayer", seatRt);
            if (GetField(churchUI, "paperDollSeatPrefab") == null)
            {
                SetField(churchUI, "paperDollSeatPrefab",
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PaperDollSeat.prefab"));
            }
        }

        Component townUI = EnsureComponent(townCanvas, "ArthurianAutobattler.TownSplitUIController");
        GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/RecruitAvatarButton.prefab");
        if (townUI != null)
        {
            SetField(townUI, "pewLayout", pewLayout);
            SetField(townUI, "townGridContainer", seatRt);
            if (GetField(townUI, "recruitAvatarButtonPrefab") == null)
                SetField(townUI, "recruitAvatarButtonPrefab", buttonPrefab);
            buttonPrefab = GetField(townUI, "recruitAvatarButtonPrefab") as GameObject ?? buttonPrefab;
        }

        HideLegacyCrowd(townCanvas.transform);

        Component loop = FindComponentInScene("ArthurianAutobattler.GameLoopManager");
        if (loop != null)
        {
            SetField(loop, "townUI", townUI);
            SetField(loop, "churchUI", churchUI);
            SetField(loop, "townCanvasObj", townCanvas);
        }

        PreviewSeatBoxes(seatRt, bgRt, bgImage, buttonPrefab);

        EditorUtility.SetDirty(townCanvas);
        EditorSceneManager.MarkSceneDirty(townCanvas.scene);

        bool gameplayOk = pewLayout != null && churchUI != null;
        EditorUtility.DisplayDialog(
            "Last Knights of Somerset",
            "52 seat boxes placed on ChurchBackground pews.\n\n" +
            "• PewSeatLayer is under ChurchBackground\n" +
            "• Empty boxes are dim gold (filled at Play when scripts compile)\n" +
            (gameplayOk
                ? "• SomersetPewLayout wired — nudge anchors on TownCanvas if a row is off"
                : "• Gameplay scripts not compiled yet — boxes still placed; fix Console red errors, then Align again"),
            "OK");
    }

    [MenuItem("Arthurian/Align Crowd Grid To Church Pews", true)]
    public static bool AlignValidate() => true;

    private static Transform FindChurchBackground(Transform townCanvas)
    {
        Transform bg = townCanvas.Find("ChurchBackground");
        if (bg != null) return bg;
        foreach (Transform child in townCanvas)
        {
            if (child.name.IndexOf("ChurchBackground", StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
            if (child.name.IndexOf("church", StringComparison.OrdinalIgnoreCase) >= 0
                && child.GetComponent<Image>() != null)
                return child;
        }
        return null;
    }

    private static void HideLegacyCrowd(Transform root)
    {
        string[] names = { "Panel_TownCrowd", "Panel_TownCrowd_Legacy", "CamelotCrowd" };
        foreach (string name in names)
        {
            Transform found = FindDeep(root, name);
            if (found != null) found.gameObject.SetActive(false);
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var hit = FindDeep(child, name);
            if (hit != null) return hit;
        }
        return null;
    }

    private static void PreviewSeatBoxes(
        RectTransform seatLayer, RectTransform bgRt, Image bgImage, GameObject buttonPrefab)
    {
        while (seatLayer.childCount > 0)
            UnityEngine.Object.DestroyImmediate(seatLayer.GetChild(0).gameObject);

        GetContentNormalizedRange(bgRt, bgImage, out Vector2 cMin, out Vector2 cMax);

        for (int seat = 0; seat < TotalSeats; seat++)
        {
            Vector2 norm = GetNormalizedSeatPosition(seat);
            float scale = GetSeatScale(seat);

            GameObject go;
            if (buttonPrefab != null)
                go = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, seatLayer);
            else
            {
                go = new GameObject($"Seat_{seat:00}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(seatLayer, false);
            }

            go.name = $"Seat_{seat:00}_Empty";
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            // Anchor-based placement (same as runtime) so Play Mode doesn't stack seats.
            Vector2 anchor = new Vector2(
                Mathf.Lerp(cMin.x, cMax.x, norm.x),
                Mathf.Lerp(cMin.y, cMax.y, norm.y));
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.12f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(48f, 64f) * scale;

            var img = go.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = null;
                img.color = new Color(0.85f, 0.75f, 0.45f, 0.55f);
            }

            var btn = go.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
    }

    private static void GetContentNormalizedRange(
        RectTransform bgRt, Image img, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.one;
        if (bgRt == null) return;

        Rect full = bgRt.rect;
        if (full.width <= 0.01f || full.height <= 0.01f) return;

        Rect content = full;
        if (img != null && img.preserveAspect && img.sprite != null)
        {
            float spriteAspect = img.sprite.rect.width / Mathf.Max(1f, img.sprite.rect.height);
            float rectAspect = full.width / full.height;
            if (spriteAspect > rectAspect)
            {
                float h = full.width / spriteAspect;
                float y = full.y + (full.height - h) * 0.5f;
                content = new Rect(full.x, y, full.width, h);
            }
            else
            {
                float w = full.height * spriteAspect;
                float x = full.x + (full.width - w) * 0.5f;
                content = new Rect(x, full.y, w, full.height);
            }
        }

        min = new Vector2(
            (content.xMin - full.xMin) / full.width,
            (content.yMin - full.yMin) / full.height);
        max = new Vector2(
            (content.xMax - full.xMin) / full.width,
            (content.yMax - full.yMin) / full.height);
    }

    private static Vector2 GetNormalizedSeatPosition(int globalSeatIndex)
    {
        int pew = GetPewIndex(globalSeatIndex);
        int slot = GetSlotInPew(globalSeatIndex);
        int row = pew / PewColumns;
        int col = pew % PewColumns;
        int cap = Mathf.Max(1, Caps[pew]);

        float[][] colXByRow =
        {
            new[] { 0.213f, 0.500f, 0.787f },
            new[] { 0.220f, 0.500f, 0.780f },
            new[] { 0.228f, 0.500f, 0.773f },
            new[] { 0.235f, 0.500f, 0.768f }
        };
        // Match SomersetPewLayout — upper 4 pew bands; camera-front pews stay empty.
        float[] rowY = { 0.375f, 0.490f, 0.600f, 0.715f };
        float[] rowSpread = { 0.175f, 0.160f, 0.145f, 0.130f };

        float t = cap == 1 ? 0.5f : slot / (float)(cap - 1);
        float x = colXByRow[row][col] + Mathf.Lerp(-rowSpread[row] * 0.5f, rowSpread[row] * 0.5f, t);
        return new Vector2(x, rowY[row]);
    }

    private static float GetSeatScale(int globalSeatIndex)
    {
        int row = GetPewIndex(globalSeatIndex) / PewColumns;
        float[] rowScale = { 1.00f, 0.88f, 0.76f, 0.66f };
        return rowScale[Mathf.Clamp(row, 0, PewRows - 1)];
    }

    private static int GetPewIndex(int globalSeatIndex)
    {
        int remaining = Mathf.Clamp(globalSeatIndex, 0, TotalSeats - 1);
        for (int p = 0; p < PewCount; p++)
        {
            if (remaining < Caps[p]) return p;
            remaining -= Caps[p];
        }
        return PewCount - 1;
    }

    private static int GetSlotInPew(int globalSeatIndex)
    {
        int remaining = Mathf.Clamp(globalSeatIndex, 0, TotalSeats - 1);
        for (int p = 0; p < PewCount; p++)
        {
            if (remaining < Caps[p]) return remaining;
            remaining -= Caps[p];
        }
        return 0;
    }

    private static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    private static Component EnsureComponent(GameObject go, string fullTypeName)
    {
        Type t = FindType(fullTypeName);
        if (t == null) return null;
        var existing = go.GetComponent(t);
        if (existing != null) return existing;
        return go.AddComponent(t);
    }

    private static Component FindComponentInScene(string fullTypeName)
    {
        Type t = FindType(fullTypeName);
        if (t == null) return null;
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType(t) as Component;
#else
        return UnityEngine.Object.FindObjectOfType(t) as Component;
#endif
    }

    private static void SetField(object target, string name, object value)
    {
        if (target == null) return;
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var field = target.GetType().GetField(name, flags);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }
        var prop = target.GetType().GetProperty(name, flags);
        if (prop != null && prop.CanWrite) prop.SetValue(target, value);
    }

    private static object GetField(object target, string name)
    {
        if (target == null) return null;
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var field = target.GetType().GetField(name, flags);
        if (field != null) return field.GetValue(target);
        var prop = target.GetType().GetProperty(name, flags);
        return prop != null && prop.CanRead ? prop.GetValue(target) : null;
    }

    private static void Invoke(object target, string methodName)
    {
        if (target == null) return;
        var method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(target, null);
    }
}
#endif
