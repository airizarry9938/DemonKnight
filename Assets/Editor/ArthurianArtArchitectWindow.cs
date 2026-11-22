#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ArthurianAutobattler;
using UnityEditor;
using UnityEngine;
#pragma warning disable CS0618 // TextureImporter.spritesheet — splice export still uses SpriteMetaData API

/// <summary>
/// Master Spec Phase 3 — Art &amp; Stage Architect.
/// Menu: Arthurian → Art &amp; Stage Architect
/// Tabs: Sheet Splicer &amp; Animation Builder | Numbered Stage Placer
/// Single sprite category only (no paperdoll / multi-layer body·outfit·head).
/// </summary>
public class ArthurianArtArchitectWindow : EditorWindow
{
    /// <summary>Visible in window title/header — must match DemonKnightPackageVersion.Id tip.</summary>
    public const string UiBuildId = "spliced-sprite-escape";

    const string DefaultLibraryPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
    const string DefaultSliceFolder = "Assets/Art/Modular/ArchitectSliced";
    const string SplicedRootFolder = "Assets/Art/Spliced";
    const string SpriteCategory = "Sprite";
    const string DefaultExportFolder = SplicedRootFolder + "/" + SpriteCategory;
    const string StageProfilePath = "Assets/Resources/StageLayoutProfile.asset";

    enum ArchitectTab { Splicer, StagePlacer }
    enum StageViewMode { SomersetChurch, ChurchyardBattle }
    enum BattleSlotSide { Player, Enemy }
    enum CropToolMode { ManualBox, GridCrop, AutoIsland }
    enum BoxPivotMode { BottomCenter, Center, Custom }

    ArchitectTab tab;
    Vector2 scroll;

    // ── Tab 1: Splicer ─────────────────────────────────────────────────────
    Texture2D sheetTexture;
    ModularSpriteLibrary targetLibrary;
    Color keyColor = new Color(28f / 255f, 37f / 255f, 50f / 255f, 1f);
    float keyTolerance = 18f;
    int cellWidth = 64;
    int cellHeight = 64;
    readonly List<Sprite> croppedFrames = new List<Sprite>();
    readonly List<bool> frameSelected = new List<bool>();
    string clipName = SpriteAnimClip.Idle;
    float clipFps = 6f;
    bool clipLoop = true;
    string sliceOutputFolder = DefaultSliceFolder;
    string targetExportFolder = DefaultExportFolder;

    // Save Splices chroma key (#FF00FF) + optional corner detect + magenta defringe
    static readonly Color MagentaChromaKey = new Color(1f, 0f, 1f, 1f); // #FF00FF
    Color saveChromaKey = MagentaChromaKey;
    bool autoDetectCornerColor;
    float colorTolerance = 0.18f; // 0.01–0.50
    bool defringeMagentaEdges = true;

    // Interactive Manual Box Splicer (default crop tool)
    CropToolMode cropTool = CropToolMode.ManualBox;
    readonly List<RectInt> manualBoxes = new List<RectInt>();
    int selectedBox = -1;
    BoxPivotMode boxPivot = BoxPivotMode.BottomCenter;
    Vector2 customPivot = new Vector2(0.5f, 0f);
    float sheetZoom = 1f;
    Vector2 sheetPan;
    bool sheetDraggingPan;
    bool boxDrawing;
    bool boxMoving;
    bool boxResizing;
    int resizeHandle = -1; // 0..7: NW N NE E SE S SW W
    Vector2Int boxDragStartTex;
    RectInt boxDragOrig;
    const float HandlePx = 8f;
    static Texture2D _checkerTex;

    // ── Tab 2: Stage Placer ────────────────────────────────────────────────
    StageLayoutProfile stageProfile;
    StageViewMode stageView = StageViewMode.SomersetChurch;
    BattleSlotSide battleSide = BattleSlotSide.Player;
    int selectedSlot;
    bool draggingSlot;
    Vector2 stagePan;
    float stageZoom = 28f;
    Vector2 parallaxScroll;
    Vector2 paletteScroll;
    readonly List<Sprite> splicedPalette = new List<Sprite>();
    Sprite paletteSelectedSprite;
    double lastPaletteRefresh;

    // Single menu path only — do not also register under DemonKnight/.
    [MenuItem("Arthurian/Art & Stage Architect", false, 35)]
    public static void Open()
    {
        // Drop cached/stale EditorWindow instances so tip type + layout always show.
        CloseAllArchitectWindows();
        string title = "Art & Stage Architect · " + UiBuildId;
        var win = CreateInstance<ArthurianArtArchitectWindow>();
        win.titleContent = new GUIContent(title);
        win.minSize = new Vector2(780, 560);
        win.Show();
        win.Focus();
        Debug.Log($"[DemonKnight] Opened Art & Stage Architect ({UiBuildId}) — Category: Sprite, 2 tabs.");
    }

    public static void CloseAllArchitectWindows()
    {
        var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (var w in windows)
        {
            if (w == null) continue;
            if (w is ArthurianArtArchitectWindow
                || (w.titleContent?.text != null
                    && w.titleContent.text.IndexOf("Art & Stage Architect", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                try { w.Close(); } catch { /* ignore */ }
            }
        }
    }

    void OnEnable()
    {
        wantsMouseMove = true;
        titleContent = new GUIContent("Art & Stage Architect · " + UiBuildId);
        if (targetLibrary == null)
            targetLibrary = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(DefaultLibraryPath)
                            ?? ModularSpriteLibrary.Load();
        if (stageProfile == null)
            stageProfile = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(StageProfilePath)
                           ?? StageLayoutProfile.Load();
        stageProfile?.EnsureSlotCounts();
        SyncDefaultExportFolder();
    }

    void OnGUI()
    {
        DrawToolbar();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case ArchitectTab.Splicer: DrawSplicerTab(); break;
            case ArchitectTab.StagePlacer: DrawStagePlacerTab(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(tab == ArchitectTab.Splicer, "1 · Sheet Splicer & Animation", EditorStyles.toolbarButton))
            tab = ArchitectTab.Splicer;
        if (GUILayout.Toggle(tab == ArchitectTab.StagePlacer, "2 · Stage Placer & Parallax", EditorStyles.toolbarButton))
            tab = ArchitectTab.StagePlacer;
        GUILayout.FlexibleSpace();
        GUILayout.Label(UiBuildId, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            $"Art & Stage Architect  ·  {UiBuildId}  ·  Category: {SpriteCategory} (no paperdoll)",
            EditorStyles.boldLabel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 1 — Sheet Splicer & Animation Builder (+ Interactive Manual Box Splicer)
    // ═══════════════════════════════════════════════════════════════════════

    void DrawSplicerTab()
    {
        EditorGUILayout.HelpBox(
            "Drop a sprite sheet → Auto-Prepare → crop with Manual Box (default), Grid, or Auto-Island → " +
            "Save Splices / Create Animation Clip (single Sprite category). Manual: LMB drag green boxes; select gold + handles.",
            MessageType.Info);

        sheetTexture = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet", sheetTexture, typeof(Texture2D), false);
        EditorGUILayout.LabelField("Category", SpriteCategory);
        EditorGUILayout.HelpBox(
            "One sprite category only — crop/export full sprites (no body / outfit / head layers).",
            MessageType.None);
        if (string.IsNullOrEmpty(targetExportFolder))
            SyncDefaultExportFolder();
        targetLibrary = (ModularSpriteLibrary)EditorGUILayout.ObjectField(
            "ModularSpriteLibrary", targetLibrary, typeof(ModularSpriteLibrary), false);
        targetExportFolder = EditorGUILayout.TextField("Target Export Folder", targetExportFolder);
        EditorGUILayout.LabelField($"Default: {DefaultExportFolder}", EditorStyles.miniLabel);
        sliceOutputFolder = EditorGUILayout.TextField("Working Slice Folder", sliceOutputFolder);
        keyColor = EditorGUILayout.ColorField("Key Color", keyColor);
        keyTolerance = EditorGUILayout.Slider("Key Tolerance", keyTolerance, 0f, 64f);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(cropTool == CropToolMode.ManualBox, "Manual Box Crop", EditorStyles.toolbarButton))
            cropTool = CropToolMode.ManualBox;
        if (GUILayout.Toggle(cropTool == CropToolMode.GridCrop, "Grid Crop", EditorStyles.toolbarButton))
            cropTool = CropToolMode.GridCrop;
        if (GUILayout.Toggle(cropTool == CropToolMode.AutoIsland, "Auto-Island Crop", EditorStyles.toolbarButton))
            cropTool = CropToolMode.AutoIsland;
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(sheetTexture == null);
        if (GUILayout.Button("Auto-Prepare Sheet", GUILayout.Height(28)))
            AutoPrepareSheet();
        EditorGUI.EndDisabledGroup();

        if (cropTool == CropToolMode.ManualBox)
            DrawManualBoxSplicer();
        else if (cropTool == CropToolMode.GridCrop)
            DrawGridCropControls();
        else
            DrawIslandCropControls();

        DrawCroppedFramePicker();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Create Animation Clip", EditorStyles.boldLabel);
        clipName = EditorGUILayout.TextField("Clip Name (Idle/Attack/Hit/Death)", clipName);
        clipFps = EditorGUILayout.Slider("FPS", clipFps, 1f, 24f);
        clipLoop = EditorGUILayout.Toggle("Loop", clipLoop);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Save Splices — Chroma Key", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        saveChromaKey = EditorGUILayout.ColorField("Chroma Key", saveChromaKey);
        if (GUILayout.Button("#FF00FF", GUILayout.Width(70)))
            saveChromaKey = MagentaChromaKey;
        EditorGUILayout.EndHorizontal();
        autoDetectCornerColor = EditorGUILayout.ToggleLeft(
            "Auto-Detect Corner Color (overrides key from sheet/frame corners)", autoDetectCornerColor);
        colorTolerance = EditorGUILayout.Slider("Color Tolerance", colorTolerance, 0.01f, 0.50f);
        defringeMagentaEdges = EditorGUILayout.ToggleLeft(
            "Defringe Magenta Edges (after key → Color.clear)", defringeMagentaEdges);

        int selectedCount = CountSelectedFrames();
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(selectedCount == 0);
        if (GUILayout.Button($"Save Splices to Folder ({selectedCount})", GUILayout.Height(36)))
            SaveSplicesToFolder();
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(selectedCount == 0 || targetLibrary == null);
        if (GUILayout.Button($"Create Animation Clip ({selectedCount} frames) → Register Library", GUILayout.Height(36)))
            CreateAndRegisterClip();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Play library (single Sprite)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Sprite Library → Resources", GUILayout.Height(28)))
        {
            EnsureLibraryAsset();
            if (targetLibrary != null && targetLibrary.HasPaperdollResidue())
                targetLibrary.ClearPaperdollLayersKeepSprites();
            EditorUtility.SetDirty(targetLibrary);
            AssetDatabase.SaveAssets();
            SyncLibraryToResources();
            ArthurianMenuBootstrap.VerifyAndSyncModularLibraryInternal(showDialog: false);
            EditorUtility.DisplayDialog(
                "Sprite Library Saved",
                "Copied ModularSpriteLibrary → Resources (play reads this).\n" +
                "Outfit/Head/Prop cleared. Stop Play → New Game.\n" +
                "Bind log must show outfit=NULL head=NULL.\n\n" +
                "StageLayoutProfile is pew/battle slots only — not required for sprites.",
                "OK");
        }
        if (GUILayout.Button("Clear Paperdoll Layers", GUILayout.Height(28)))
        {
            string report = ArthurianMenuBootstrap.ClearPaperdollLibraryInternal(showDialog: false);
            EnsureLibraryAsset();
            targetLibrary = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(DefaultLibraryPath);
            EditorUtility.DisplayDialog("Paperdoll Cleared", report, "OK");
        }
        EditorGUILayout.EndHorizontal();

        if (targetLibrary != null && targetLibrary.animClips != null && targetLibrary.animClips.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Registered clips: {targetLibrary.animClips.Count}", EditorStyles.miniLabel);
            for (int i = 0; i < Mathf.Min(12, targetLibrary.animClips.Count); i++)
            {
                var e = targetLibrary.animClips[i];
                if (e?.clip == null) continue;
                EditorGUILayout.LabelField(
                    $"  [{e.category}] {e.clip.clipName} · {e.clip.FrameCount}f @ {e.clip.framesPerSecond:0.#}fps " +
                    (e.clip.loop ? "loop" : "once"),
                    EditorStyles.miniLabel);
            }
        }
    }

    void DrawGridCropControls()
    {
        EditorGUILayout.BeginHorizontal();
        cellWidth = Mathf.Max(4, EditorGUILayout.IntField("Cell Width", cellWidth));
        cellHeight = Mathf.Max(4, EditorGUILayout.IntField("Cell Height", cellHeight));
        EditorGUILayout.EndHorizontal();
        EditorGUI.BeginDisabledGroup(sheetTexture == null);
        if (GUILayout.Button("Grid Crop", GUILayout.Height(32)))
            RunGridCrop();
        EditorGUI.EndDisabledGroup();
    }

    void DrawIslandCropControls()
    {
        EditorGUI.BeginDisabledGroup(sheetTexture == null);
        if (GUILayout.Button("Auto-Island Crop", GUILayout.Height(32)))
            RunIslandCrop();
        EditorGUI.EndDisabledGroup();
    }

    void DrawManualBoxSplicer()
    {
        Event ke = Event.current;
        if (ke.type == EventType.KeyDown
            && (ke.keyCode == KeyCode.Delete || ke.keyCode == KeyCode.Backspace)
            && selectedBox >= 0
            && !(EditorGUIUtility.editingTextField))
        {
            RemoveSelectedBox();
            ke.Use();
        }

        EditorGUILayout.LabelField("Interactive Manual Box Splicer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scroll-wheel zoom · MMB/Alt+LMB pan · LMB drag empty area = new green box · " +
            "Click box = gold select · drag body move · 8×8 corner/edge handles resize · " +
            "Delete/Backspace removes selection.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        sheetZoom = EditorGUILayout.Slider("Zoom", sheetZoom, 0.25f, 8f);
        if (GUILayout.Button("Fit", GUILayout.Width(40)))
        {
            sheetZoom = 1f;
            sheetPan = Vector2.zero;
        }
        EditorGUILayout.EndHorizontal();

        DrawManualBoxCanvas();

        // Selection inspector: X/Y/W/H + Pivot
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Boxes: {manualBoxes.Count}", GUILayout.Width(80));
        if (GUILayout.Button("Remove Selected", GUILayout.Width(120)))
            RemoveSelectedBox();
        if (GUILayout.Button("Clear All", GUILayout.Width(80)))
        {
            manualBoxes.Clear();
            selectedBox = -1;
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        boxPivot = (BoxPivotMode)EditorGUILayout.EnumPopup("Pivot", boxPivot);
        if (boxPivot == BoxPivotMode.Custom)
            customPivot = EditorGUILayout.Vector2Field("Custom Pivot (0–1)", customPivot);

        if (selectedBox >= 0 && selectedBox < manualBoxes.Count)
        {
            var r = manualBoxes[selectedBox];
            EditorGUI.BeginChangeCheck();
            int x = EditorGUILayout.IntField("X", r.x);
            int y = EditorGUILayout.IntField("Y", r.y);
            int w = Mathf.Max(1, EditorGUILayout.IntField("W", r.width));
            int h = Mathf.Max(1, EditorGUILayout.IntField("H", r.height));
            if (EditorGUI.EndChangeCheck() && sheetTexture != null)
            {
                r = ClampBox(new RectInt(x, y, w, h), sheetTexture.width, sheetTexture.height);
                manualBoxes[selectedBox] = r;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No box selected — drag on the preview to draw one.", MessageType.None);
        }

        EditorGUI.BeginDisabledGroup(sheetTexture == null || manualBoxes.Count == 0);
        if (GUILayout.Button("Slice Manual Boxes → Point Sprites", GUILayout.Height(34)))
            SliceManualBoxes();
        EditorGUI.EndDisabledGroup();
    }

    void DrawManualBoxCanvas()
    {
        float viewH = Mathf.Clamp(position.height * 0.42f, 280f, 520f);
        Rect canvas = GUILayoutUtility.GetRect(10, viewH, GUILayout.ExpandWidth(true), GUILayout.MinHeight(260f));
        DrawCheckerboard(canvas);

        if (sheetTexture == null)
        {
            GUI.Label(canvas, "Assign a Sprite Sheet to preview.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Event e = Event.current;
        // Zoom with scroll wheel over canvas
        if (e.type == EventType.ScrollWheel && canvas.Contains(e.mousePosition))
        {
            float old = sheetZoom;
            sheetZoom = Mathf.Clamp(sheetZoom * (e.delta.y > 0 ? 0.9f : 1.1f), 0.25f, 8f);
            // Zoom toward cursor
            Vector2 local = e.mousePosition - canvas.center - sheetPan;
            sheetPan += local * (1f - sheetZoom / old);
            e.Use();
            Repaint();
        }

        Rect texRect = SheetDrawRect(canvas);
        GUI.DrawTexture(texRect, sheetTexture, ScaleMode.StretchToFill, true);

        // Draw boxes (texture y-up → GUI y-down)
        for (int i = 0; i < manualBoxes.Count; i++)
        {
            Rect gr = TexToGui(manualBoxes[i], texRect);
            bool sel = i == selectedBox;
            Color fill = sel ? new Color(1f, 0.85f, 0.15f, 0.18f) : new Color(0.2f, 0.95f, 0.35f, 0.12f);
            Color edge = sel ? new Color(1f, 0.85f, 0.15f, 1f) : new Color(0.25f, 0.95f, 0.4f, 1f);
            EditorGUI.DrawRect(gr, fill);
            DrawRectOutline(gr, edge, sel ? 2f : 1.5f);
            GUI.Label(new Rect(gr.x + 2f, gr.y + 1f, 40f, 14f), i.ToString(), EditorStyles.miniBoldLabel);
            if (sel)
                DrawBoxHandles(gr);
        }

        // Live rubber-band while drawing
        if (boxDrawing && e.type == EventType.Repaint)
        {
            Vector2Int cur = GuiToTex(e.mousePosition, texRect, sheetTexture.width, sheetTexture.height);
            RectInt live = ClampBox(RectFromCorners(boxDragStartTex, cur), sheetTexture.width, sheetTexture.height);
            DrawRectOutline(TexToGui(live, texRect), new Color(0.3f, 1f, 0.45f, 0.9f), 1.5f);
        }

        HandleManualBoxInput(canvas, texRect, e);
    }

    void HandleManualBoxInput(Rect canvas, Rect texRect, Event e)
    {
        if (!canvas.Contains(e.mousePosition) && e.type != EventType.MouseDrag && e.type != EventType.MouseUp)
            return;

        // Pan: MMB or Alt+LMB
        if (e.type == EventType.MouseDown && (e.button == 2 || (e.button == 0 && e.alt)))
        {
            sheetDraggingPan = true;
            e.Use();
        }
        if (sheetDraggingPan && e.type == EventType.MouseDrag)
        {
            sheetPan += e.delta;
            e.Use();
            Repaint();
        }
        if (sheetDraggingPan && e.type == EventType.MouseUp)
        {
            sheetDraggingPan = false;
            e.Use();
        }
        if (e.alt) return;

        Vector2Int texPt = GuiToTex(e.mousePosition, texRect, sheetTexture.width, sheetTexture.height);

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // Resize handle on selection?
            if (selectedBox >= 0 && selectedBox < manualBoxes.Count)
            {
                Rect gr = TexToGui(manualBoxes[selectedBox], texRect);
                int h = HitHandle(gr, e.mousePosition);
                if (h >= 0)
                {
                    boxResizing = true;
                    resizeHandle = h;
                    boxDragOrig = manualBoxes[selectedBox];
                    boxDragStartTex = texPt;
                    e.Use();
                    Repaint();
                    return;
                }
            }

            // Hit existing box?
            int hit = HitBox(texPt);
            if (hit >= 0)
            {
                selectedBox = hit;
                boxMoving = true;
                boxDragOrig = manualBoxes[hit];
                boxDragStartTex = texPt;
                e.Use();
                Repaint();
                return;
            }

            // Start new box
            selectedBox = -1;
            boxDrawing = true;
            boxDragStartTex = texPt;
            e.Use();
            Repaint();
        }

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            if (boxResizing && selectedBox >= 0)
            {
                manualBoxes[selectedBox] = ResizeBox(boxDragOrig, resizeHandle, texPt,
                    sheetTexture.width, sheetTexture.height);
                e.Use();
                Repaint();
            }
            else if (boxMoving && selectedBox >= 0)
            {
                int dx = texPt.x - boxDragStartTex.x;
                int dy = texPt.y - boxDragStartTex.y;
                var r = boxDragOrig;
                r.x += dx;
                r.y += dy;
                manualBoxes[selectedBox] = ClampBox(r, sheetTexture.width, sheetTexture.height);
                e.Use();
                Repaint();
            }
            else if (boxDrawing)
            {
                e.Use();
                Repaint();
            }
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (boxDrawing)
            {
                RectInt live = ClampBox(RectFromCorners(boxDragStartTex, texPt),
                    sheetTexture.width, sheetTexture.height);
                if (live.width >= 2 && live.height >= 2)
                {
                    manualBoxes.Add(live);
                    selectedBox = manualBoxes.Count - 1;
                }
                boxDrawing = false;
                e.Use();
                Repaint();
            }
            if (boxMoving || boxResizing)
            {
                boxMoving = false;
                boxResizing = false;
                resizeHandle = -1;
                e.Use();
            }
        }
    }

    Rect SheetDrawRect(Rect canvas)
    {
        float tw = sheetTexture.width * sheetZoom;
        float th = sheetTexture.height * sheetZoom;
        return new Rect(
            canvas.center.x - tw * 0.5f + sheetPan.x,
            canvas.center.y - th * 0.5f + sheetPan.y,
            tw, th);
    }

    Rect TexToGui(RectInt tex, Rect drawRect)
    {
        float ux = drawRect.width / Mathf.Max(1f, sheetTexture.width);
        float uy = drawRect.height / Mathf.Max(1f, sheetTexture.height);
        float x = drawRect.x + tex.x * ux;
        float y = drawRect.y + (sheetTexture.height - tex.y - tex.height) * uy;
        return new Rect(x, y, tex.width * ux, tex.height * uy);
    }

    Vector2Int GuiToTex(Vector2 gui, Rect drawRect, int texW, int texH)
    {
        float ux = drawRect.width / Mathf.Max(1f, texW);
        float uy = drawRect.height / Mathf.Max(1f, texH);
        int x = Mathf.RoundToInt((gui.x - drawRect.x) / ux);
        int yFromTop = Mathf.RoundToInt((gui.y - drawRect.y) / uy);
        int y = texH - yFromTop;
        return new Vector2Int(Mathf.Clamp(x, 0, texW), Mathf.Clamp(y, 0, texH));
    }

    void DrawBoxHandles(Rect gr)
    {
        for (int i = 0; i < 8; i++)
        {
            Rect h = HandleRect(gr, i);
            EditorGUI.DrawRect(h, new Color(1f, 0.9f, 0.2f, 1f));
            DrawRectOutline(h, Color.black, 1f);
        }
    }

    static Rect HandleRect(Rect gr, int handle)
    {
        float hx = HandlePx, hy = HandlePx;
        Vector2 c = gr.center;
        switch (handle)
        {
            case 0: return new Rect(gr.xMin - hx * 0.5f, gr.yMin - hy * 0.5f, hx, hy); // NW
            case 1: return new Rect(c.x - hx * 0.5f, gr.yMin - hy * 0.5f, hx, hy); // N
            case 2: return new Rect(gr.xMax - hx * 0.5f, gr.yMin - hy * 0.5f, hx, hy); // NE
            case 3: return new Rect(gr.xMax - hx * 0.5f, c.y - hy * 0.5f, hx, hy); // E
            case 4: return new Rect(gr.xMax - hx * 0.5f, gr.yMax - hy * 0.5f, hx, hy); // SE
            case 5: return new Rect(c.x - hx * 0.5f, gr.yMax - hy * 0.5f, hx, hy); // S
            case 6: return new Rect(gr.xMin - hx * 0.5f, gr.yMax - hy * 0.5f, hx, hy); // SW
            default: return new Rect(gr.xMin - hx * 0.5f, c.y - hy * 0.5f, hx, hy); // W
        }
    }

    static int HitHandle(Rect gr, Vector2 mouse)
    {
        for (int i = 0; i < 8; i++)
            if (HandleRect(gr, i).Contains(mouse)) return i;
        return -1;
    }

    int HitBox(Vector2Int texPt)
    {
        for (int i = manualBoxes.Count - 1; i >= 0; i--)
            if (manualBoxes[i].Contains(new Vector2Int(texPt.x, texPt.y)))
                return i;
        return -1;
    }

    void RemoveSelectedBox()
    {
        if (selectedBox < 0 || selectedBox >= manualBoxes.Count) return;
        manualBoxes.RemoveAt(selectedBox);
        if (selectedBox >= manualBoxes.Count) selectedBox = manualBoxes.Count - 1;
        Repaint();
    }

    static RectInt RectFromCorners(Vector2Int a, Vector2Int b)
    {
        int x0 = Mathf.Min(a.x, b.x);
        int y0 = Mathf.Min(a.y, b.y);
        int x1 = Mathf.Max(a.x, b.x);
        int y1 = Mathf.Max(a.y, b.y);
        return new RectInt(x0, y0, Mathf.Max(1, x1 - x0), Mathf.Max(1, y1 - y0));
    }

    static RectInt ClampBox(RectInt r, int texW, int texH)
    {
        r.width = Mathf.Max(1, r.width);
        r.height = Mathf.Max(1, r.height);
        r.x = Mathf.Clamp(r.x, 0, Mathf.Max(0, texW - r.width));
        r.y = Mathf.Clamp(r.y, 0, Mathf.Max(0, texH - r.height));
        if (r.xMax > texW) r.width = texW - r.x;
        if (r.yMax > texH) r.height = texH - r.y;
        return r;
    }

    static RectInt ResizeBox(RectInt orig, int handle, Vector2Int texPt, int texW, int texH)
    {
        // Handles in GUI space: 0=NW 1=N 2=NE 3=E 4=SE 5=S 6=SW 7=W
        // Texture y-up: GUI-N pulls tex.yMax, GUI-S pulls tex.yMin.
        int x0 = orig.xMin, y0 = orig.yMin, x1 = orig.xMax, y1 = orig.yMax;
        switch (handle)
        {
            case 0: x0 = texPt.x; y1 = texPt.y; break;
            case 1: y1 = texPt.y; break;
            case 2: x1 = texPt.x; y1 = texPt.y; break;
            case 3: x1 = texPt.x; break;
            case 4: x1 = texPt.x; y0 = texPt.y; break;
            case 5: y0 = texPt.y; break;
            case 6: x0 = texPt.x; y0 = texPt.y; break;
            case 7: x0 = texPt.x; break;
        }
        return ClampBox(RectFromCorners(new Vector2Int(x0, y0), new Vector2Int(x1, y1)), texW, texH);
    }

    void SliceManualBoxes()
    {
        if (sheetTexture == null || manualBoxes.Count == 0) return;
        string path = AssetDatabase.GetAssetPath(sheetTexture);
        Texture2D readable = LoadReadableCopy(sheetTexture, path);
        if (readable == null)
        {
            EditorUtility.DisplayDialog("Slice Manual Boxes", "Could not read sheet pixels (isReadable?).", "OK");
            return;
        }
        EnsureFolder(sliceOutputFolder);
        string sheetPath = $"{sliceOutputFolder}/{sheetTexture.name}_ManualBoxes.png";
        File.WriteAllBytes(sheetPath, readable.EncodeToPNG());
        AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceUpdate);
        ConfigureManualBoxImporter(sheetPath, new List<RectInt>(manualBoxes), boxPivot, customPivot);
        AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceUpdate);
        LoadFramesFromAsset(sheetPath);
        Debug.Log($"[DemonKnight] Slice Manual Boxes → {croppedFrames.Count} Point sprites @ {sheetPath}");
        EditorUtility.DisplayDialog(
            "Slice Manual Boxes",
            $"Created {croppedFrames.Count} Point sprites from {manualBoxes.Count} boxes.\n"
            + "Select frames below → Create Animation Clip.",
            "OK");
    }

    static void ConfigureManualBoxImporter(
        string assetPath, List<RectInt> boxes, BoxPivotMode pivotMode, Vector2 custom)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;

        SpriteAlignment align;
        Vector2 pivot;
        switch (pivotMode)
        {
            case BoxPivotMode.BottomCenter:
                align = SpriteAlignment.BottomCenter;
                pivot = new Vector2(0.5f, 0f);
                break;
            case BoxPivotMode.Center:
                align = SpriteAlignment.Center;
                pivot = new Vector2(0.5f, 0.5f);
                break;
            default:
                align = SpriteAlignment.Custom;
                pivot = new Vector2(Mathf.Clamp01(custom.x), Mathf.Clamp01(custom.y));
                break;
        }

        var metas = new SpriteMetaData[boxes.Count];
        for (int i = 0; i < boxes.Count; i++)
        {
            var r = boxes[i];
            metas[i] = new SpriteMetaData
            {
                name = Path.GetFileNameWithoutExtension(assetPath) + "_" + i,
                rect = new Rect(r.x, r.y, r.width, r.height),
                alignment = (int)align,
                pivot = pivot
            };
        }
#pragma warning disable CS0618 // TextureImporter.spritesheet — keep until ISpriteEditorDataProvider migrate
        importer.spritesheet = metas;
#pragma warning restore CS0618
        importer.SaveAndReimport();
    }

    static void DrawCheckerboard(Rect rect)
    {
        if (_checkerTex == null)
        {
            _checkerTex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            _checkerTex.SetPixels32(new[]
            {
                new Color32(48, 48, 52, 255), new Color32(62, 62, 68, 255),
                new Color32(62, 62, 68, 255), new Color32(48, 48, 52, 255)
            });
            _checkerTex.Apply();
        }
        GUI.DrawTextureWithTexCoords(rect, _checkerTex,
            new Rect(0, 0, rect.width / 16f, rect.height / 16f));
    }

    void DrawCroppedFramePicker()
    {
        if (croppedFrames.Count == 0)
        {
            EditorGUILayout.HelpBox("No cropped frames yet.", MessageType.None);
            return;
        }
        EditorGUILayout.LabelField($"Cropped frames ({croppedFrames.Count}) — click to select", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All", GUILayout.Width(90)))
            for (int i = 0; i < frameSelected.Count; i++) frameSelected[i] = true;
        if (GUILayout.Button("Select None", GUILayout.Width(90)))
            for (int i = 0; i < frameSelected.Count; i++) frameSelected[i] = false;
        EditorGUILayout.EndHorizontal();

        const float thumb = 56f;
        int cols = Mathf.Max(1, Mathf.FloorToInt((position.width - 40f) / (thumb + 6f)));
        int row = 0;
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < croppedFrames.Count; i++)
        {
            if (i > 0 && i % cols == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                row++;
            }
            var sp = croppedFrames[i];
            Rect r = GUILayoutUtility.GetRect(thumb, thumb, GUILayout.Width(thumb), GUILayout.Height(thumb));
            if (sp != null)
                DrawSprite(r, sp, Color.white);
            else
                EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.2f));
            if (frameSelected[i])
                DrawRectOutline(r, new Color(1f, 0.85f, 0.2f, 1f), 2f);
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                frameSelected[i] = !frameSelected[i];
                Event.current.Use();
                Repaint();
            }
            GUI.Label(new Rect(r.x, r.yMax - 14f, r.width, 14f), i.ToString(), EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();
    }

    void AutoPrepareSheet()
    {
        if (sheetTexture == null) return;
        string path = AssetDatabase.GetAssetPath(sheetTexture);
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("Auto-Prepare", "Sheet must be a project asset.", "OK");
            return;
        }
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = true;
        importer.isReadable = true;
        var settings = new TextureImporterPlatformSettings
        {
            name = "DefaultTexturePlatform",
            overridden = true,
            format = TextureImporterFormat.RGBA32,
            maxTextureSize = 8192,
            textureCompression = TextureImporterCompression.Uncompressed
        };
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();

        // Key-out solid background into a _Prepared copy when possible.
        Texture2D readable = LoadReadableCopy(sheetTexture, path);
        if (readable != null)
        {
            Texture2D cleaned = KeyOut(readable, keyColor, keyTolerance);
            EnsureFolder(sliceOutputFolder);
            string outPath = $"{sliceOutputFolder}/{sheetTexture.name}_Prepared.png";
            File.WriteAllBytes(outPath, cleaned.EncodeToPNG());
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            ConfigurePreparedImporter(outPath);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            sheetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            Debug.Log($"[DemonKnight] Auto-Prepare → {outPath}");
        }
        else
        {
            Debug.Log("[DemonKnight] Auto-Prepare importer settings applied (no pixel key-out).");
        }
    }

    void RunGridCrop()
    {
        if (sheetTexture == null) return;
        string path = AssetDatabase.GetAssetPath(sheetTexture);
        Texture2D readable = LoadReadableCopy(sheetTexture, path);
        if (readable == null)
        {
            EditorUtility.DisplayDialog("Grid Crop", "Could not read sheet pixels (isReadable?).", "OK");
            return;
        }
        int cols = Mathf.Max(1, readable.width / cellWidth);
        int rows = Mathf.Max(1, readable.height / cellHeight);
        EnsureFolder(sliceOutputFolder);
        string cleanPath = $"{sliceOutputFolder}/{sheetTexture.name}_Grid.png";
        File.WriteAllBytes(cleanPath, readable.EncodeToPNG());
        AssetDatabase.ImportAsset(cleanPath, ImportAssetOptions.ForceUpdate);
        ConfigureGridImporter(cleanPath, cols, rows, cellWidth, cellHeight);
        AssetDatabase.ImportAsset(cleanPath, ImportAssetOptions.ForceUpdate);
        LoadFramesFromAsset(cleanPath);
        Debug.Log($"[DemonKnight] Grid Crop {cols}×{rows} → {croppedFrames.Count} frames");
    }

    void RunIslandCrop()
    {
        if (sheetTexture == null) return;
        string path = AssetDatabase.GetAssetPath(sheetTexture);
        Texture2D readable = LoadReadableCopy(sheetTexture, path);
        if (readable == null)
        {
            EditorUtility.DisplayDialog("Island Crop", "Could not read sheet pixels.", "OK");
            return;
        }
        Texture2D keyed = KeyOut(readable, keyColor, keyTolerance);
        var islands = FindOpaqueIslands(keyed);
        if (islands.Count == 0)
        {
            EditorUtility.DisplayDialog("Island Crop", "No opaque islands found.", "OK");
            return;
        }
        EnsureFolder(sliceOutputFolder);
        string sheetPath = $"{sliceOutputFolder}/{sheetTexture.name}_Islands.png";
        File.WriteAllBytes(sheetPath, keyed.EncodeToPNG());
        AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceUpdate);
        ConfigureIslandImporter(sheetPath, islands);
        AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceUpdate);
        LoadFramesFromAsset(sheetPath);
        Debug.Log($"[DemonKnight] Auto-Island Crop → {croppedFrames.Count} islands");
    }

    void LoadFramesFromAsset(string assetPath)
    {
        croppedFrames.Clear();
        frameSelected.Clear();
        var ordered = new List<(int idx, Sprite sp)>();
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (obj is Sprite sp)
            {
                int idx = ParseTrailingIndex(sp.name);
                ordered.Add((idx >= 0 ? idx : ordered.Count, sp));
            }
        }
        ordered.Sort((a, b) => a.idx.CompareTo(b.idx));
        foreach (var o in ordered)
        {
            croppedFrames.Add(o.sp);
            frameSelected.Add(true);
        }
        if (croppedFrames.Count == 0)
        {
            var single = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (single != null)
            {
                croppedFrames.Add(single);
                frameSelected.Add(true);
            }
        }
        Repaint();
    }

    int CountSelectedFrames()
    {
        int n = 0;
        for (int i = 0; i < frameSelected.Count; i++)
            if (frameSelected[i] && i < croppedFrames.Count && croppedFrames[i] != null) n++;
        return n;
    }

    void SyncDefaultExportFolder()
    {
        string cur = (targetExportFolder ?? "").Replace('\\', '/').TrimEnd('/');
        // Migrate old paperdoll category folders → single Sprite export root.
        if (string.IsNullOrEmpty(cur) || cur == SplicedRootFolder || IsLegacyCategoryExportFolder(cur))
            targetExportFolder = DefaultExportFolder;
    }

    static bool IsLegacyCategoryExportFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string n = path.Replace('\\', '/').TrimEnd('/');
        string[] legacy = { "Pose", "Outfit", "Head", "Weapon", "EnemyBoss", SpriteCategory };
        foreach (string c in legacy)
            if (string.Equals(n, $"{SplicedRootFolder}/{c}", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    void SaveSplicesToFolder()
    {
        SyncDefaultExportFolder();
        string folder = string.IsNullOrWhiteSpace(targetExportFolder)
            ? DefaultExportFolder
            : targetExportFolder.Replace('\\', '/').TrimEnd('/');
        EnsureFolder(folder);

        var frames = new List<Sprite>();
        for (int i = 0; i < croppedFrames.Count; i++)
        {
            if (i < frameSelected.Count && frameSelected[i] && croppedFrames[i] != null)
                frames.Add(croppedFrames[i]);
        }
        if (frames.Count == 0)
        {
            EditorUtility.DisplayDialog("Save Splices", "Select at least one cropped frame first.", "OK");
            return;
        }

        var saved = new List<Sprite>();
        string baseName = sheetTexture != null ? sheetTexture.name : "splice";
        for (int i = 0; i < frames.Count; i++)
        {
            Sprite src = frames[i];
            string assetPath = $"{folder}/{baseName}_{SpriteCategory}_{i:00}.png";
            Sprite written = WriteSpritePngPointBottomCenter(src, assetPath);
            if (written != null) saved.Add(written);
        }

        if (saved.Count == 0)
        {
            EditorUtility.DisplayDialog("Save Splices", "Failed to write PNGs (readable texture?).", "OK");
            return;
        }

        // Refresh croppedFrames to the exported Point/Bottom-Center sprites.
        croppedFrames.Clear();
        frameSelected.Clear();
        foreach (var sp in saved)
        {
            croppedFrames.Add(sp);
            frameSelected.Add(true);
        }

        RegisterSpritesIntoLibrary(saved);
        RefreshSplicedPalette(force: true);
        Debug.Log($"[DemonKnight] Saved {saved.Count} splices → {folder} + ModularSpriteLibrary");
        string keyDesc = autoDetectCornerColor ? "Auto-Corner" : ColorUtility.ToHtmlStringRGB(saveChromaKey);
        EditorUtility.DisplayDialog(
            "Save Splices to Folder",
            $"Wrote {saved.Count} PNG(s)\n"
            + $"Chroma {keyDesc} tol={colorTolerance:0.00}"
            + (defringeMagentaEdges ? " + magenta defringe" : "") + "\n"
            + "Importer: Sprite / alphaIsTransparency / FromInput / Point / Uncompressed / Bottom-Center\n"
            + $"→ {folder}\nRegistered into ModularSpriteLibrary.",
            "OK");
    }

    Sprite WriteSpritePngPointBottomCenter(Sprite src, string assetPath)
    {
        if (src == null || src.texture == null) return null;
        Texture2D readable = ExtractSpriteTexture(src);
        if (readable == null) return null;

        Color key = saveChromaKey;
        if (autoDetectCornerColor)
            key = DetectCornerKeyColor(readable);
        // Always also punch pure #FF00FF when keying (magenta sheets).
        ApplyChromaKeyClearAndDefringe(readable, key, colorTolerance, defringeMagentaEdges);

        File.WriteAllBytes(assetPath, readable.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(readable);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        ConfigureSingleSpritePointBottomCenter(assetPath);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    /// <summary>Sample four corners; pick the most saturated / modal corner as chroma key.</summary>
    static Color DetectCornerKeyColor(Texture2D tex)
    {
        if (tex == null) return MagentaChromaKey;
        int w = tex.width, h = tex.height;
        if (w < 1 || h < 1) return MagentaChromaKey;
        Color c0 = tex.GetPixel(0, 0);
        Color c1 = tex.GetPixel(w - 1, 0);
        Color c2 = tex.GetPixel(0, h - 1);
        Color c3 = tex.GetPixel(w - 1, h - 1);
        // Prefer the corner closest to magenta, else average of similar corners.
        Color[] corners = { c0, c1, c2, c3 };
        Color best = c0;
        float bestMagenta = -1f;
        for (int i = 0; i < corners.Length; i++)
        {
            Color c = corners[i];
            float mag = (c.r + c.b) * 0.5f - c.g; // magenta-ness
            if (mag > bestMagenta)
            {
                bestMagenta = mag;
                best = c;
            }
        }
        // If corners agree (within 0.08), average them for a stabler key.
        float agree = 0f;
        for (int i = 0; i < corners.Length; i++)
            agree += ColorRgbDistance(corners[i], best);
        if (agree / 4f < 0.08f)
        {
            best = (c0 + c1 + c2 + c3) * 0.25f;
            best.a = 1f;
        }
        return best;
    }

    /// <summary>
    /// Pixel pass: near-key → Color.clear; optional magenta edge defringe; mutates tex in place.
    /// </summary>
    static void ApplyChromaKeyClearAndDefringe(
        Texture2D tex, Color key, float tolerance, bool defringeMagenta)
    {
        if (tex == null) return;
        int w = tex.width, h = tex.height;
        Color[] pixels = tex.GetPixels();
        float tol = Mathf.Clamp(tolerance, 0.01f, 0.50f);

        // Pass 1: chroma key → Color.clear (also always clear near #FF00FF).
        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];
            if (p.a < 0.004f) { pixels[i] = Color.clear; continue; }
            if (ColorRgbDistance(p, key) <= tol
                || ColorRgbDistance(p, MagentaChromaKey) <= tol)
                pixels[i] = Color.clear;
        }

        // Pass 2: defringe magenta edges (semi-magenta pixels next to clear).
        if (defringeMagenta)
        {
            var outPx = (Color[])pixels.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color p = pixels[i];
                    if (p.a < 0.004f) continue;
                    if (!IsMagentaFringe(p)) continue;
                    if (!HasClearNeighbor(pixels, w, h, x, y)) continue;
                    outPx[i] = DefringeMagentaPixel(p);
                }
            }
            pixels = outPx;
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
    }

    static bool HasClearNeighbor(Color[] pixels, int w, int h, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            if (pixels[ny * w + nx].a < 0.02f) return true;
        }
        return false;
    }

    static bool IsMagentaFringe(Color p)
    {
        // High R+B vs G (magenta cast), still somewhat opaque.
        float mag = (p.r + p.b) * 0.5f - p.g;
        return mag > 0.12f && p.r > 0.25f && p.b > 0.25f;
    }

    static Color DefringeMagentaPixel(Color p)
    {
        float excess = Mathf.Max(0f, Mathf.Min(p.r, p.b) - p.g);
        p.r = Mathf.Clamp01(p.r - excess);
        p.b = Mathf.Clamp01(p.b - excess);
        // If almost nothing left but fringe, clear it.
        float luma = p.r * 0.3f + p.g * 0.59f + p.b * 0.11f;
        if (luma < 0.04f || (p.r + p.b) * 0.5f - p.g > 0.35f)
            return Color.clear;
        // Soften alpha on residual fringe.
        p.a *= Mathf.Clamp01(1f - excess * 1.5f);
        if (p.a < 0.05f) return Color.clear;
        return p;
    }

    static float ColorRgbDistance(Color a, Color b)
    {
        float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    static Texture2D ExtractSpriteTexture(Sprite src)
    {
        if (src == null || src.texture == null) return null;
        Rect tr = src.textureRect;
        int x = Mathf.FloorToInt(tr.x);
        int y = Mathf.FloorToInt(tr.y);
        int w = Mathf.Max(1, Mathf.FloorToInt(tr.width));
        int h = Mathf.Max(1, Mathf.FloorToInt(tr.height));
        string path = AssetDatabase.GetAssetPath(src.texture);
        Texture2D full = null;
        bool madeCopy = false;
        if (src.texture.isReadable)
            full = src.texture;
        else
        {
            full = LoadReadableCopy(src.texture, path);
            madeCopy = full != null;
            if (full == null) return null;
        }
        try
        {
            Color[] pixels = full.GetPixels(x, y, w, h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
        finally
        {
            if (madeCopy && full != null)
                UnityEngine.Object.DestroyImmediate(full);
        }
    }

    static void ConfigureSingleSpritePointBottomCenter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.alphaSource = TextureImporterAlphaSource.FromInput;
        settings.alphaIsTransparency = true;
        settings.filterMode = FilterMode.Point;
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePivot = new Vector2(0.5f, 0f); // Bottom-Center
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    void RegisterSpritesIntoLibrary(List<Sprite> sprites)
    {
        if (sprites == null || sprites.Count == 0) return;
        EnsureLibraryAsset();
        if (targetLibrary == null) return;
        Undo.RecordObject(targetLibrary, "Register Spliced Sprites");
        foreach (var sp in sprites)
            AssignStaticAppend(sp);

        string name = string.IsNullOrWhiteSpace(clipName) ? SpriteAnimClip.Idle : clipName.Trim();
        var clip = new SpriteAnimClip
        {
            clipName = name,
            framesPerSecond = Mathf.Max(0.1f, clipFps),
            loop = clipLoop,
            frames = new List<Sprite>(sprites)
        };
        targetLibrary.RegisterAnimClip(SpriteCategory, clip);
        EditorUtility.SetDirty(targetLibrary);
        AssetDatabase.SaveAssets();
        SyncLibraryToResources();
    }

    /// <summary>Single-sprite workflow: store in basePoses so pews / Verify still resolve a filled library.</summary>
    void AssignStaticAppend(Sprite sp)
    {
        if (sp == null || targetLibrary == null) return;
        targetLibrary.EnsureSizedArrays();
        targetLibrary.basePoses = AppendSprite(targetLibrary.basePoses, sp);
    }

    static Sprite[] AppendSprite(Sprite[] arr, Sprite sp)
    {
        if (sp == null) return arr;
        var list = new List<Sprite>();
        if (arr != null)
        {
            foreach (var s in arr)
            {
                if (s == null) continue;
                if (s == sp) return arr; // already present
                list.Add(s);
            }
        }
        list.Add(sp);
        return list.ToArray();
    }

    void CreateAndRegisterClip()
    {
        EnsureLibraryAsset();
        if (targetLibrary == null) return;
        var frames = new List<Sprite>();
        for (int i = 0; i < croppedFrames.Count; i++)
        {
            if (i < frameSelected.Count && frameSelected[i] && croppedFrames[i] != null)
                frames.Add(croppedFrames[i]);
        }
        if (frames.Count == 0) return;

        string name = string.IsNullOrWhiteSpace(clipName) ? SpriteAnimClip.Idle : clipName.Trim();
        var clip = new SpriteAnimClip
        {
            clipName = name,
            framesPerSecond = Mathf.Max(0.1f, clipFps),
            loop = clipLoop,
            frames = frames
        };
        Undo.RecordObject(targetLibrary, "Register Anim Clip");
        targetLibrary.RegisterAnimClip(SpriteCategory, clip);

        // Also push first frame into basePoses when empty (single-sprite pew library).
        AssignStaticIfEmpty(frames[0]);

        EditorUtility.SetDirty(targetLibrary);
        AssetDatabase.SaveAssets();
        SyncLibraryToResources();
        Debug.Log($"[DemonKnight] Registered [{SpriteCategory}] clip '{name}' ({frames.Count} frames).");
        EditorUtility.DisplayDialog(
            "Animation Clip",
            $"Registered [{SpriteCategory}] {name}\n{frames.Count} frames @ {clipFps:0.#} fps\n→ {AssetDatabase.GetAssetPath(targetLibrary)}",
            "OK");
    }

    void AssignStaticIfEmpty(Sprite first)
    {
        if (first == null || targetLibrary == null) return;
        targetLibrary.EnsureSizedArrays();
        if (targetLibrary.basePoses[0] == null) targetLibrary.basePoses[0] = first;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 2 — Numbered Stage Placer & Moving Background Editor
    // ═══════════════════════════════════════════════════════════════════════

    void DrawStagePlacerTab()
    {
        EditorGUILayout.LabelField("Numbered Stage Placer & Moving Background Editor", EditorStyles.boldLabel);
        stageProfile = (StageLayoutProfile)EditorGUILayout.ObjectField(
            "StageLayoutProfile", stageProfile, typeof(StageLayoutProfile), false);
        if (stageProfile == null)
        {
            if (GUILayout.Button("Ensure Stage Layout Profile Asset"))
            {
                ArthurianStageLayoutProfileEnsure.EnsureAsset();
                stageProfile = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(StageProfilePath);
            }
            return;
        }
        stageProfile.EnsureSlotCounts();

        stageView = (StageViewMode)EditorGUILayout.EnumPopup("Stage View", stageView);
        if (stageView == StageViewMode.ChurchyardBattle)
            battleSide = (BattleSlotSide)EditorGUILayout.EnumPopup("Battle Side", battleSide);

        EditorGUILayout.BeginHorizontal();
        stageZoom = EditorGUILayout.Slider("Zoom", stageZoom, 8f, 64f);
        if (GUILayout.Button("Auto-Arrange 52 Overflow Formation", GUILayout.Height(28)))
            AutoArrangeOverflowFormation();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawStageCanvas();
        DrawSlotInspector();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        DrawSplicedAndCharacterPalette();

        EditorGUILayout.Space(8);
        DrawParallaxAndFxDrawer();
    }

    List<SlotTransformConfig> ActiveSlots()
    {
        if (stageProfile == null) return null;
        if (stageView == StageViewMode.SomersetChurch)
            return stageProfile.churchPewSlots;
        return battleSide == BattleSlotSide.Player
            ? stageProfile.playerBattleSlots
            : stageProfile.enemyBattleSlots;
    }

    void DrawStageCanvas()
    {
        var slots = ActiveSlots();
        Rect canvas = GUILayoutUtility.GetRect(420, 360, GUILayout.ExpandWidth(true), GUILayout.MinHeight(320));
        EditorGUI.DrawRect(canvas, new Color(0.10f, 0.12f, 0.16f, 1f));
        Handles.BeginGUI();
        // Origin crosshair
        Vector2 origin = CanvasToScreen(canvas, Vector2.zero);
        Handles.color = new Color(1f, 1f, 1f, 0.15f);
        Handles.DrawLine(new Vector3(canvas.x, origin.y), new Vector3(canvas.xMax, origin.y));
        Handles.DrawLine(new Vector3(origin.x, canvas.y), new Vector3(origin.x, canvas.yMax));
        Handles.EndGUI();

        if (slots == null) return;
        Event e = Event.current;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null) continue;
            Vector2 screen = CanvasToScreen(canvas, s.position);
            float size = 18f * Mathf.Clamp(s.scale.x, 0.5f, 1.5f);
            Rect handle = new Rect(screen.x - size * 0.5f, screen.y - size * 0.5f, size, size);
            bool sel = i == selectedSlot;

            // Live preview sprite behind the numbered handle
            Sprite preview = ResolveSlotPreviewSprite(s);
            if (preview != null)
            {
                float pw = size * 2.2f * Mathf.Max(0.5f, s.aspectMultiplier.x);
                float ph = size * 2.8f * Mathf.Max(0.5f, s.aspectMultiplier.y);
                Rect pr = new Rect(screen.x - pw * 0.5f, screen.y - ph * 0.85f, pw, ph);
                DrawSprite(pr, preview, Color.white);
            }

            EditorGUI.DrawRect(handle, sel ? new Color(1f, 0.85f, 0.25f, 0.95f) : new Color(0.35f, 0.65f, 0.95f, 0.85f));
            GUI.Label(handle, i.ToString("00"), EditorStyles.centeredGreyMiniLabel);
            if (!string.IsNullOrEmpty(s.assignedCharacterId))
            {
                var idRect = new Rect(handle.x - 8f, handle.yMax, handle.width + 16f, 12f);
                GUI.Label(idRect, s.assignedCharacterId, EditorStyles.miniLabel);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
            {
                selectedSlot = i;
                draggingSlot = true;
                e.Use();
                Repaint();
            }
        }

        if (draggingSlot && selectedSlot >= 0 && selectedSlot < slots.Count)
        {
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                Undo.RecordObject(stageProfile, "Move Stage Slot");
                slots[selectedSlot].position = ScreenToCanvas(canvas, e.mousePosition);
                slots[selectedSlot].sortingOrder = Mathf.RoundToInt(-slots[selectedSlot].position.y * 10f);
                EditorUtility.SetDirty(stageProfile);
                e.Use();
                Repaint();
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                draggingSlot = false;
                e.Use();
            }
        }

        // Pan with middle mouse / alt-drag
        if (e.type == EventType.MouseDrag && (e.button == 2 || (e.button == 0 && e.alt)))
        {
            stagePan += e.delta;
            e.Use();
            Repaint();
        }
    }

    Vector2 CanvasToScreen(Rect canvas, Vector2 world)
    {
        Vector2 c = canvas.center + stagePan;
        return new Vector2(c.x + world.x * stageZoom, c.y - world.y * stageZoom);
    }

    Vector2 ScreenToCanvas(Rect canvas, Vector2 screen)
    {
        Vector2 c = canvas.center + stagePan;
        return new Vector2((screen.x - c.x) / stageZoom, -(screen.y - c.y) / stageZoom);
    }

    void DrawSlotInspector()
    {
        var slots = ActiveSlots();
        EditorGUILayout.BeginVertical(GUILayout.Width(280));
        EditorGUILayout.LabelField("Slot Inspector", EditorStyles.boldLabel);
        if (slots == null || selectedSlot < 0 || selectedSlot >= slots.Count)
        {
            EditorGUILayout.HelpBox("Select a numbered handle.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }
        var s = slots[selectedSlot];
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField($"Slot #{selectedSlot:00}", EditorStyles.boldLabel);
        s.slotNumber = selectedSlot;
        s.position = EditorGUILayout.Vector2Field("Position", s.position);
        s.scale = EditorGUILayout.Vector2Field("Scale", s.scale);
        s.aspectMultiplier = EditorGUILayout.Vector2Field("Aspect Multiplier", s.aspectMultiplier);
        s.sortingOrder = EditorGUILayout.IntField("Sorting Order", s.sortingOrder);
        s.previewOrOverrideSprite = (Sprite)EditorGUILayout.ObjectField(
            "Preview / Override Sprite", s.previewOrOverrideSprite, typeof(Sprite), false);
        s.assignedCharacterId = EditorGUILayout.TextField("Assigned Character Id", s.assignedCharacterId ?? "");
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Slot Stat Upgrades", EditorStyles.miniBoldLabel);
        s.bonusHP = EditorGUILayout.IntField("Bonus HP", s.bonusHP);
        s.bonusATK = EditorGUILayout.IntField("Bonus ATK", s.bonusATK);
        s.bonusSPD = EditorGUILayout.IntField("Bonus SPD", s.bonusSPD);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(stageProfile, "Edit Stage Slot");
            EditorUtility.SetDirty(stageProfile);
        }

        EditorGUILayout.Space(4);
        EditorGUI.BeginDisabledGroup(paletteSelectedSprite == null);
        if (GUILayout.Button("Assign Palette Sprite → Slot", GUILayout.Height(26)))
            AssignPaletteToSelectedSlot();
        EditorGUI.EndDisabledGroup();
        EditorGUI.BeginDisabledGroup(ResolveSlotPreviewSprite(s) == null && paletteSelectedSprite == null);
        if (GUILayout.Button("Quick Add to Parallax Layer", GUILayout.Height(26)))
            QuickAddPreviewToParallax(ResolveSlotPreviewSprite(s) ?? paletteSelectedSprite);
        if (GUILayout.Button("Add Sprite → Pew Library (play)", GUILayout.Height(26)))
            AddSpriteToPewLibrary(ResolveSlotPreviewSprite(s) ?? paletteSelectedSprite);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Clear Slot Preview"))
        {
            Undo.RecordObject(stageProfile, "Clear Slot Preview");
            s.previewOrOverrideSprite = null;
            s.assignedCharacterId = "";
            EditorUtility.SetDirty(stageProfile);
        }
        if (GUILayout.Button("Save StageLayoutProfile"))
        {
            EditorUtility.SetDirty(stageProfile);
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.EndVertical();
    }

    Sprite ResolveSlotPreviewSprite(SlotTransformConfig s)
    {
        if (s == null) return null;
        return s.previewOrOverrideSprite;
    }

    void DrawSplicedAndCharacterPalette()
    {
        RefreshSplicedPalette();

        EditorGUILayout.LabelField("Spliced Sprite Palette", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"Single-type sprites from {SplicedRootFolder}/ (default {DefaultExportFolder}). " +
            "Click a tile, then Assign to the selected slot. Quick Add → parallax layer.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Palette", GUILayout.Width(120)))
            RefreshSplicedPalette(force: true);
        if (paletteSelectedSprite != null)
            EditorGUILayout.LabelField($"Sprite: {paletteSelectedSprite.name}", EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField("Nothing selected", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.Height(120));
        const float thumb = 48f;
        int cols = Mathf.Max(1, Mathf.FloorToInt((position.width - 40f) / (thumb + 6f)));
        EditorGUILayout.LabelField($"Sprites ({splicedPalette.Count})", EditorStyles.miniBoldLabel);
        DrawSpritePaletteRow(splicedPalette, cols, thumb);
        EditorGUILayout.EndScrollView();
    }

    void DrawSpritePaletteRow(List<Sprite> sprites, int cols, float thumb)
    {
        if (sprites == null || sprites.Count == 0)
        {
            EditorGUILayout.LabelField("  (empty — Save Splices from Tab 1)", EditorStyles.miniLabel);
            return;
        }
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < sprites.Count; i++)
        {
            if (i > 0 && i % cols == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
            var sp = sprites[i];
            Rect r = GUILayoutUtility.GetRect(thumb, thumb, GUILayout.Width(thumb), GUILayout.Height(thumb));
            if (sp != null) DrawSprite(r, sp, Color.white);
            else EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.2f));
            bool sel = sp != null && sp == paletteSelectedSprite;
            if (sel) DrawRectOutline(r, new Color(1f, 0.85f, 0.2f), 2f);
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                paletteSelectedSprite = sp;
                Event.current.Use();
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    void AssignPaletteToSelectedSlot()
    {
        var slots = ActiveSlots();
        if (slots == null || selectedSlot < 0 || selectedSlot >= slots.Count) return;
        if (paletteSelectedSprite == null) return;
        var s = slots[selectedSlot];
        Undo.RecordObject(stageProfile, "Assign Palette To Slot");
        s.previewOrOverrideSprite = paletteSelectedSprite;
        EditorUtility.SetDirty(stageProfile);
        Repaint();
    }

    void QuickAddPreviewToParallax(Sprite sp)
    {
        if (stageProfile == null || sp == null) return;
        stageProfile.parallaxLayers ??= new List<StageParallaxLayer>();
        Undo.RecordObject(stageProfile, "Quick Add Parallax Layer");
        stageProfile.parallaxLayers.Add(new StageParallaxLayer
        {
            layerName = sp.name,
            layerSprite = sp,
            sortingOrder = stageProfile.parallaxLayers.Count,
            layerTint = Color.white,
            swayFrequency = 1f,
            scrollSpeed = new Vector2(0.05f, 0f),
            swayAmplitude = new Vector2(0f, 0.02f)
        });
        EditorUtility.SetDirty(stageProfile);
        // Also register into pew ModularSpriteLibrary so play can use the same splice.
        AddSpriteToPewLibrary(sp);
        Debug.Log($"[DemonKnight] Quick-added parallax layer '{sp.name}' (+ pew library)");
        Repaint();
    }

    /// <summary>Register a Spliced/Sprite into ModularSpriteLibrary.basePoses + Resources copy.</summary>
    void AddSpriteToPewLibrary(Sprite sp)
    {
        if (sp == null) return;
        if (ArthurianStageLayoutProfileEnsure.IsPaperdollPlaceholderName(sp.name))
        {
            Debug.LogWarning("[DemonKnight] Refusing placeholder " + sp.name + " for pew library.");
            return;
        }
        EnsureLibraryAsset();
        if (targetLibrary == null) return;
        targetLibrary.ClearPaperdollLayersKeepSprites();
        targetLibrary.basePoses = AppendSprite(targetLibrary.basePoses, sp);
        // Dedup by name.
        var list = new List<Sprite>();
        var seen = new HashSet<string>();
        if (targetLibrary.basePoses != null)
        {
            foreach (var s in targetLibrary.basePoses)
            {
                if (s == null || !seen.Add(s.name)) continue;
                if (ArthurianStageLayoutProfileEnsure.IsPaperdollPlaceholderName(s.name)) continue;
                list.Add(s);
            }
        }
        targetLibrary.basePoses = list.ToArray();
        EditorUtility.SetDirty(targetLibrary);
        AssetDatabase.SaveAssets();
        SyncLibraryToResources();
        TownsfolkVisualApplier.SetLibrary(null);
        Debug.Log($"[DemonKnight] Pew library += '{sp.name}' (basePoses={list.Count}). New Game to see in pews.");
    }

    void RefreshSplicedPalette(bool force = false)
    {
        if (!force && EditorApplication.timeSinceStartup - lastPaletteRefresh < 2.0) return;
        lastPaletteRefresh = EditorApplication.timeSinceStartup;
        splicedPalette.Clear();
        if (!AssetDatabase.IsValidFolder(SplicedRootFolder))
        {
            EnsureFolder(SplicedRootFolder);
            return;
        }
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { SplicedRootFolder });
        var seen = new HashSet<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!seen.Add(path)) continue;
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) splicedPalette.Add(sp);
        }
        splicedPalette.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    void AutoArrangeOverflowFormation()
    {
        if (stageProfile == null) return;
        stageProfile.EnsureSlotCounts();
        Undo.RecordObject(stageProfile, "Auto-Arrange 52 Overflow Formation");
        const int frontlineSize = 6;
        for (int i = 0; i < StageLayoutProfile.PlayerBattleCount; i++)
        {
            int col = i / frontlineSize;
            int row = i % frontlineSize;
            float scale = col <= 1
                ? 1f
                : Mathf.Lerp(0.95f, 0.70f, Mathf.InverseLerp(2f, 8f, col));
            float x = -3.5f + row * 1.1f;
            float y = 2.0f - col * 0.85f;
            var s = stageProfile.playerBattleSlots[i] ?? new SlotTransformConfig();
            s.slotNumber = i;
            s.position = new Vector2(x, y);
            s.scale = new Vector2(scale, scale);
            s.aspectMultiplier = Vector2.one;
            s.sortingOrder = Mathf.RoundToInt(-y * 10f);
            stageProfile.playerBattleSlots[i] = s;
        }
        EditorUtility.SetDirty(stageProfile);
        AssetDatabase.SaveAssets();
        stageView = StageViewMode.ChurchyardBattle;
        battleSide = BattleSlotSide.Player;
        Debug.Log("[DemonKnight] Auto-arranged playerBattleSlots 0..51 (columns of 6).");
        Repaint();
    }

    void DrawParallaxAndFxDrawer()
    {
        EditorGUILayout.LabelField("Stage Layers & FX", EditorStyles.boldLabel);
        stageProfile.parallaxLayers ??= new List<StageParallaxLayer>();
        EditorGUI.BeginChangeCheck();
        stageProfile.enableRainFX = EditorGUILayout.Toggle("Rain FX", stageProfile.enableRainFX);
        stageProfile.enableLightningFlashes = EditorGUILayout.Toggle("Lightning Flashes", stageProfile.enableLightningFlashes);
        stageProfile.enableCandleFlicker = EditorGUILayout.Toggle("Candle Flicker", stageProfile.enableCandleFlicker);
        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(stageProfile);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Parallax Layer"))
        {
            Undo.RecordObject(stageProfile, "Add Parallax Layer");
            stageProfile.parallaxLayers.Add(new StageParallaxLayer
            {
                layerName = $"Layer {stageProfile.parallaxLayers.Count}",
                layerTint = Color.white,
                swayFrequency = 1f
            });
            EditorUtility.SetDirty(stageProfile);
        }
        EditorGUILayout.EndHorizontal();

        parallaxScroll = EditorGUILayout.BeginScrollView(parallaxScroll, GUILayout.MaxHeight(180));
        for (int i = 0; i < stageProfile.parallaxLayers.Count; i++)
        {
            var layer = stageProfile.parallaxLayers[i];
            if (layer == null) continue;
            EditorGUILayout.BeginVertical("box");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            layer.layerName = EditorGUILayout.TextField(layer.layerName);
            if (GUILayout.Button("↑", GUILayout.Width(24)) && i > 0)
            {
                (stageProfile.parallaxLayers[i - 1], stageProfile.parallaxLayers[i]) =
                    (stageProfile.parallaxLayers[i], stageProfile.parallaxLayers[i - 1]);
            }
            if (GUILayout.Button("↓", GUILayout.Width(24)) && i < stageProfile.parallaxLayers.Count - 1)
            {
                (stageProfile.parallaxLayers[i + 1], stageProfile.parallaxLayers[i]) =
                    (stageProfile.parallaxLayers[i], stageProfile.parallaxLayers[i + 1]);
            }
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                Undo.RecordObject(stageProfile, "Remove Parallax Layer");
                stageProfile.parallaxLayers.RemoveAt(i);
                EditorUtility.SetDirty(stageProfile);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();
            layer.layerSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", layer.layerSprite, typeof(Sprite), false);
            layer.sortingOrder = EditorGUILayout.IntField("Sorting Order", layer.sortingOrder);
            layer.scrollSpeed = EditorGUILayout.Vector2Field("Scroll Speed", layer.scrollSpeed);
            layer.swayAmplitude = EditorGUILayout.Vector2Field("Sway Amplitude", layer.swayAmplitude);
            layer.swayFrequency = EditorGUILayout.FloatField("Sway Frequency", layer.swayFrequency);
            layer.layerTint = EditorGUILayout.ColorField("Tint", layer.layerTint);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(stageProfile);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Shared helpers
    // ═══════════════════════════════════════════════════════════════════════

    void EnsureLibraryAsset()
    {
        if (targetLibrary != null) return;
        targetLibrary = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(DefaultLibraryPath);
        if (targetLibrary != null) return;
        string dir = Path.GetDirectoryName(DefaultLibraryPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir)) EnsureFolder(dir);
        targetLibrary = CreateInstance<ModularSpriteLibrary>();
        targetLibrary.EnsureSizedArrays();
        targetLibrary.animClips = new List<ModularAnimRegistration>();
        AssetDatabase.CreateAsset(targetLibrary, DefaultLibraryPath);
        AssetDatabase.SaveAssets();
    }

    void SyncLibraryToResources()
    {
        if (targetLibrary == null) return;
        string resourcesDir = "Assets/Resources/Art/Modular";
        EnsureFolder(resourcesDir);
        string resourcesAsset = resourcesDir + "/ModularSpriteLibrary.asset";
        string srcPath = AssetDatabase.GetAssetPath(targetLibrary);
        if (string.IsNullOrEmpty(srcPath)) return;
        if (AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resourcesAsset) != null)
            AssetDatabase.DeleteAsset(resourcesAsset);
        AssetDatabase.CopyAsset(srcPath, resourcesAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        TownsfolkVisualApplier.SetLibrary(null);
    }

    static void EnsureFolder(string folder)
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

    static Texture2D LoadReadableCopy(Texture2D source, string path)
    {
        if (source == null) return null;
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
            source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        Texture2D copy = null;
        try
        {
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
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

    static List<RectInt> FindOpaqueIslands(Texture2D tex)
    {
        int w = tex.width, h = tex.height;
        var pixels = tex.GetPixels32();
        var visited = new bool[w * h];
        var islands = new List<RectInt>();
        int[] qx = new int[w * h];
        int[] qy = new int[w * h];

        bool Opaque(int x, int y) => pixels[y * w + x].a > 8;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (visited[i] || !Opaque(x, y)) continue;
                int qh = 0, qt = 0;
                qx[qt] = x; qy[qt] = y; qt++;
                visited[i] = true;
                int minX = x, maxX = x, minY = y, maxY = y;
                while (qh < qt)
                {
                    int cx = qx[qh], cy = qy[qh]; qh++;
                    minX = Mathf.Min(minX, cx); maxX = Mathf.Max(maxX, cx);
                    minY = Mathf.Min(minY, cy); maxY = Mathf.Max(maxY, cy);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int ni = ny * w + nx;
                        if (visited[ni] || !Opaque(nx, ny)) continue;
                        visited[ni] = true;
                        qx[qt] = nx; qy[qt] = ny; qt++;
                    }
                }
                int bw = maxX - minX + 1;
                int bh = maxY - minY + 1;
                if (bw >= 4 && bh >= 4)
                    islands.Add(new RectInt(minX, minY, bw, bh));
            }
        }
        islands.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return islands;
    }

    static void ConfigurePreparedImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    static void ConfigureGridImporter(string assetPath, int cols, int rows, int cellW, int cellH)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;

        var metas = new List<SpriteMetaData>();
        int idx = 0;
        // Unity sprite rects: y=0 at bottom
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        int texH = tex != null ? tex.height : rows * cellH;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int x = col * cellW;
                int yFromTop = row * cellH;
                int y = texH - yFromTop - cellH;
                metas.Add(new SpriteMetaData
                {
                    name = Path.GetFileNameWithoutExtension(assetPath) + "_" + idx,
                    rect = new Rect(x, Mathf.Max(0, y), cellW, cellH),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
                idx++;
            }
        }
#pragma warning disable CS0618 // TextureImporter.spritesheet — keep until ISpriteEditorDataProvider migrate
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        importer.SaveAndReimport();
    }

    static void ConfigureIslandImporter(string assetPath, List<RectInt> islands)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        var metas = new SpriteMetaData[islands.Count];
        for (int i = 0; i < islands.Count; i++)
        {
            var r = islands[i];
            metas[i] = new SpriteMetaData
            {
                name = Path.GetFileNameWithoutExtension(assetPath) + "_" + i,
                rect = new Rect(r.x, r.y, r.width, r.height),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
        }
#pragma warning disable CS0618 // TextureImporter.spritesheet — keep until ISpriteEditorDataProvider migrate
        importer.spritesheet = metas;
#pragma warning restore CS0618
        importer.SaveAndReimport();
    }

    static int ParseTrailingIndex(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        int us = name.LastIndexOf('_');
        if (us < 0) return -1;
        return int.TryParse(name.Substring(us + 1), out int n) ? n : -1;
    }

    static void DrawSprite(Rect rect, Sprite sprite, Color tint)
    {
        if (sprite == null || sprite.texture == null) return;
        Rect tr = sprite.textureRect;
        Rect uv = new Rect(
            tr.x / sprite.texture.width,
            tr.y / sprite.texture.height,
            tr.width / sprite.texture.width,
            tr.height / sprite.texture.height);
        float aspect = tr.width / Mathf.Max(1f, tr.height);
        Rect draw = rect;
        if (aspect > draw.width / Mathf.Max(1f, draw.height))
        {
            float h = draw.width / aspect;
            draw.y += (draw.height - h) * 0.5f;
            draw.height = h;
        }
        else
        {
            float w = draw.height * aspect;
            draw.x += (draw.width - w) * 0.5f;
            draw.width = w;
        }
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(draw, sprite.texture, uv, true);
        GUI.color = Color.white;
    }

    static void DrawRectOutline(Rect r, Color c, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
}
#endif
