#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ArthurianAutobattler;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
#pragma warning disable CS0618 // TextureImporter.spritesheet — splice export still uses SpriteMetaData API

/// <summary>
/// Master Spec Phase 3 — Art &amp; Stage Architect.
/// Menu: Arthurian → Art &amp; Stage Architect
/// Tabs: Sheet Splicer | Stage Placer | Scene Backgrounds | Class Sprites / Unit Pools
/// Single sprite category only (no paperdoll / multi-layer body·outfit·head).
/// </summary>
public class ArthurianArtArchitectWindow : EditorWindow
{
    /// <summary>Visible in window title/header — must match DemonKnightPackageVersion.Id tip.</summary>
    public const string UiBuildId = "sheet-splicer-chroma-despill";

    const string DefaultLibraryPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
    const string DefaultSliceFolder = "Assets/Art/Modular/ArchitectSliced";
    const string SplicedRootFolder = "Assets/Art/Spliced";
    const string SpriteCategory = "Sprite";
    const string DefaultExportFolder = SplicedRootFolder + "/" + SpriteCategory;
    const string StageProfilePath = "Assets/Resources/StageLayoutProfile.asset";
    const string SceneBgProfilePath = "Assets/Resources/SceneBackgroundProfile.asset";
    const string ClassPoolProfilePath = "Assets/Resources/ClassSpritePoolProfile.asset";
    /// <summary>Same art as SomersetChurchUI / SomersetPewLayout (play stage).</summary>
    const string ChurchBackgroundPath = "Assets/Art/Backgrounds/SomersetChurchInterior.jpg";
    /// <summary>Reference pixel size of SomersetChurchInterior (play layout tuned to this).</summary>
    const float ChurchRefWidth = 1408f;
    const float ChurchRefHeight = 768f;
    /// <summary>Must match SomersetChurchUI.baseSeatSize tip (play pew sprite size).</summary>
    static readonly Vector2 PlayPewBaseSize = new Vector2(88f, 112f);

    enum ArchitectTab { Splicer, StagePlacer, SceneBackgrounds, ClassSprites }
    enum StageViewMode { SomersetChurch, ChurchyardBattle }
    enum BattleSlotSide { Player, Enemy }
    enum CropToolMode { ManualBox, GridCrop, AutoIsland }
    enum BoxPivotMode { BottomCenter, Center, Custom }
    enum ClassPoolKind { Peasant, Warrior, Knight }

    ArchitectTab tab;
    Vector2 scroll;
    SceneBackgroundProfile sceneBgProfile;
    Vector2 sceneBgScroll;
    int selectedSceneBgSlot;
    ClassSpritePoolProfile classPoolProfile;
    Vector2 classPoolScroll;
    ClassPoolKind selectedClassPool = ClassPoolKind.Peasant;
    Sprite classPoolAddSprite;

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

    // Save Splices chroma key (#FF00FF) + multi-pass magenta despill / fringe trim / alpha bleed
    static readonly Color MagentaChromaKey = new Color(1f, 0f, 1f, 1f); // #FF00FF
    Color saveChromaKey = MagentaChromaKey;
    bool autoDetectCornerColor;
    float colorTolerance = 0.34f; // stronger default — cut magenta sheet bleed (0.01–0.65)
    /// <summary>0–1. Edge Despill / Fringe Trim strength (Pass 2). Default cleans AI magenta halos.</summary>
    float edgeDespillStrength = 0.72f;
    // Live Splicer preview cache (despilled thumbs before Save)
    readonly List<Texture2D> despillPreviewTex = new List<Texture2D>();
    int despillPreviewHash;
    int despillPreviewSelected = -1;
    Texture2D despillPreviewLarge;

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
    bool showChurchBackground = true;
    bool usePlayPewLayoutPreview = true;
    Texture2D cachedChurchBg;
    Sprite cachedChurchSprite;
    Vector2 tab1ChurchPreviewPan;
    float tab1ChurchPreviewZoom = 1f;
    bool scalingSlot;
    int churchDragSeat = -1;
    Rect lastChurchContentRect;

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
        Debug.Log($"[DemonKnight] Opened Art & Stage Architect ({UiBuildId}) — Category: Sprite, 4 tabs.");
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
        // NEVER StageLayoutProfile.Load() here — that CreateInstance fallback is not an asset
        // and SetDirty/SaveAssets will not persist seat sprites across sessions.
        stageProfile = ArthurianStageLayoutProfileEnsure.EnsureAsset();
        stageProfile?.EnsureSlotCounts();
        sceneBgProfile = ArthurianStageLayoutProfileEnsure.EnsureSceneBackgroundAsset();
        sceneBgProfile?.EnsureDefaultEntries();
        classPoolProfile = ArthurianStageLayoutProfileEnsure.EnsureClassSpritePoolAsset();
        classPoolProfile?.EnsureLists();
        SyncDefaultExportFolder();
        EditorApplication.playModeStateChanged -= OnArchitectPlayModeChanged;
        EditorApplication.playModeStateChanged += OnArchitectPlayModeChanged;
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnArchitectPlayModeChanged;
        PersistStageProfile("OnDisable");
        PersistSceneBgProfile("OnDisable");
        PersistClassPoolProfile("OnDisable");
        ClearDespillPreviewCache();
    }

    void OnArchitectPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode
            || state == PlayModeStateChange.EnteredEditMode)
        {
            PersistStageProfile("PlayMode:" + state);
            PersistSceneBgProfile("PlayMode:" + state);
            PersistClassPoolProfile("PlayMode:" + state);
        }
    }

    void OnGUI()
    {
        DrawToolbar();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case ArchitectTab.Splicer: DrawSplicerTab(); break;
            case ArchitectTab.StagePlacer: DrawStagePlacerTab(); break;
            case ArchitectTab.SceneBackgrounds: DrawSceneBackgroundsTab(); break;
            case ArchitectTab.ClassSprites: DrawClassSpritesTab(); break;
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
        if (GUILayout.Toggle(tab == ArchitectTab.SceneBackgrounds, "3 · Scene Backgrounds", EditorStyles.toolbarButton))
            tab = ArchitectTab.SceneBackgrounds;
        if (GUILayout.Toggle(tab == ArchitectTab.ClassSprites, "4 · Class Sprites / Unit Pools", EditorStyles.toolbarButton))
            tab = ArchitectTab.ClassSprites;
        GUILayout.FlexibleSpace();
        GUILayout.Label(UiBuildId, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            $"Art & Stage Architect  ·  {UiBuildId}  ·  Category: {SpriteCategory} (no paperdoll)",
            EditorStyles.boldLabel);
    }

    /// <summary>
    /// Flush StageLayoutProfile to disk. Refuses runtime CreateInstance stand-ins —
    /// those were the assign-only “saves that vanish after Unity restart” root cause.
    /// </summary>
    void PersistStageProfile(string reason)
    {
        if (stageProfile == null) return;
        string path = AssetDatabase.GetAssetPath(stageProfile);
        if (string.IsNullOrEmpty(path))
        {
            var disk = ArthurianStageLayoutProfileEnsure.EnsureAsset();
            if (disk != null && !ReferenceEquals(disk, stageProfile))
            {
                CopyStageSlots(stageProfile, disk);
                stageProfile = disk;
                path = AssetDatabase.GetAssetPath(stageProfile);
                Debug.LogWarning(
                    "[DemonKnight] Architect was editing a non-asset StageLayoutProfile — copied into "
                    + path + " (" + reason + ").");
            }
        }
        if (stageProfile == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(stageProfile)))
            return;
        EditorUtility.SetDirty(stageProfile);
        AssetDatabase.SaveAssets();
        try { SomersetChurchUI.InvalidateChurchAssignCache(); } catch { /* ignore */ }
    }

    void PersistSceneBgProfile(string reason)
    {
        if (sceneBgProfile == null) return;
        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sceneBgProfile)))
        {
            sceneBgProfile = ArthurianStageLayoutProfileEnsure.EnsureSceneBackgroundAsset();
            if (sceneBgProfile == null) return;
        }
        EditorUtility.SetDirty(sceneBgProfile);
        AssetDatabase.SaveAssets();
        try { SceneBackgroundApplier.InvalidateCache(); } catch { /* ignore */ }
    }

    void PersistClassPoolProfile(string reason)
    {
        if (classPoolProfile == null) return;
        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(classPoolProfile)))
        {
            classPoolProfile = ArthurianStageLayoutProfileEnsure.EnsureClassSpritePoolAsset();
            if (classPoolProfile == null) return;
        }
        SyncClassPoolAssetPaths(classPoolProfile);
        EditorUtility.SetDirty(classPoolProfile);
        AssetDatabase.SaveAssets();
        try { ClassSpritePoolApplier.InvalidateCache(); } catch { /* ignore */ }
    }

    static void SyncClassPoolAssetPaths(ClassSpritePoolProfile p)
    {
        if (p == null) return;
        p.EnsureLists();
        SyncPathList(p.peasantSprites, p.peasantAssetPaths);
        SyncPathList(p.warriorSprites, p.warriorAssetPaths);
        SyncPathList(p.knightSprites, p.knightAssetPaths);
    }

    static void SyncPathList(List<Sprite> sprites, List<string> paths)
    {
        if (sprites == null || paths == null) return;
        while (paths.Count < sprites.Count) paths.Add("");
        if (paths.Count > sprites.Count)
            paths.RemoveRange(sprites.Count, paths.Count - sprites.Count);
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null) { paths[i] = ""; continue; }
            string ap = AssetDatabase.GetAssetPath(sprites[i]);
            if (!string.IsNullOrEmpty(ap)) paths[i] = ap;
        }
    }

    static void CopyStageSlots(StageLayoutProfile from, StageLayoutProfile to)
    {
        if (from == null || to == null) return;
        to.churchPewSlots = CloneSlots(from.churchPewSlots);
        to.playerBattleSlots = CloneSlots(from.playerBattleSlots);
        to.enemyBattleSlots = CloneSlots(from.enemyBattleSlots);
        to.parallaxLayers = from.parallaxLayers != null
            ? new List<StageParallaxLayer>(from.parallaxLayers)
            : new List<StageParallaxLayer>();
        to.enableRainFX = from.enableRainFX;
        to.enableLightningFlashes = from.enableLightningFlashes;
        to.enableCandleFlicker = from.enableCandleFlicker;
        to.EnsureSlotCounts();
    }

    static List<SlotTransformConfig> CloneSlots(List<SlotTransformConfig> src)
    {
        var list = new List<SlotTransformConfig>();
        if (src == null) return list;
        for (int i = 0; i < src.Count; i++)
        {
            var s = src[i];
            if (s == null) { list.Add(null); continue; }
            list.Add(new SlotTransformConfig
            {
                slotNumber = s.slotNumber,
                position = s.position,
                scale = s.scale,
                aspectMultiplier = s.aspectMultiplier,
                sortingOrder = s.sortingOrder,
                bonusHP = s.bonusHP,
                bonusATK = s.bonusATK,
                bonusSPD = s.bonusSPD,
                previewOrOverrideSprite = s.previewOrOverrideSprite,
                assignedCharacterId = s.assignedCharacterId
            });
        }
        return list;
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
        EditorGUILayout.LabelField("Save Splices — Multi-Pass Magenta & Chroma", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        saveChromaKey = EditorGUILayout.ColorField("Chroma Key", saveChromaKey);
        if (GUILayout.Button("#FF00FF", GUILayout.Width(70)))
            saveChromaKey = MagentaChromaKey;
        EditorGUILayout.EndHorizontal();
        autoDetectCornerColor = EditorGUILayout.ToggleLeft(
            "Auto-Detect Corner Color (overrides key from sheet/frame corners)", autoDetectCornerColor);
        EditorGUILayout.BeginHorizontal();
        colorTolerance = EditorGUILayout.Slider("Color Tolerance", colorTolerance, 0.01f, 0.65f);
        edgeDespillStrength = EditorGUILayout.Slider(
            "Edge Despill / Fringe Trim", edgeDespillStrength, 0f, 1f);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "Pass 1: key + magenta-bias BG remove · Pass 2: edge despill/fringe trim · "
            + "Pass 3: alpha-bleed RGB pad (bilinear-safe). Live preview below uses these settings.",
            MessageType.None);

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
        if (GUILayout.Button("Re-bake Existing Splices (chroma + alpha)", GUILayout.Height(28)))
            RebakeExistingSplicesInExportFolder();

        EditorGUILayout.Space(8);
        DrawTab1ChurchPewPreview();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Play library (single Sprite)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Sprite Library → Resources", GUILayout.Height(28)))
        {
            string report = ClearPaperdollLibraryLocal();
            EditorUtility.DisplayDialog(
                "Sprite Library Saved",
                report + "\n\nStop Play → New Game. Bind: outfit=NULL head=NULL.\n"
                + "StageLayoutProfile is pew/battle slots only — not required for sprites.",
                "OK");
        }
        if (GUILayout.Button("Clear Paperdoll Layers", GUILayout.Height(28)))
        {
            string report = ClearPaperdollLibraryLocal();
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
        EnsureDespillPreviewCache();
        EditorGUILayout.LabelField(
            $"Cropped frames ({croppedFrames.Count}) — despilled preview (before Save)",
            EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All", GUILayout.Width(90)))
            for (int i = 0; i < frameSelected.Count; i++) frameSelected[i] = true;
        if (GUILayout.Button("Select None", GUILayout.Width(90)))
            for (int i = 0; i < frameSelected.Count; i++) frameSelected[i] = false;
        EditorGUILayout.EndHorizontal();

        const float thumb = 56f;
        int cols = Mathf.Max(1, Mathf.FloorToInt((position.width - 40f) / (thumb + 6f)));
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < croppedFrames.Count; i++)
        {
            if (i > 0 && i % cols == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
            var sp = croppedFrames[i];
            Rect r = GUILayoutUtility.GetRect(thumb, thumb, GUILayout.Width(thumb), GUILayout.Height(thumb));
            DrawCheckerboard(r);
            Texture2D prev = (i < despillPreviewTex.Count) ? despillPreviewTex[i] : null;
            if (prev != null)
                GUI.DrawTexture(r, prev, ScaleMode.ScaleToFit, true);
            else if (sp != null)
                DrawSprite(r, sp, Color.white);
            else
                EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.2f));
            if (i < frameSelected.Count && frameSelected[i])
                DrawRectOutline(r, new Color(1f, 0.85f, 0.2f, 1f), 2f);
            if (despillPreviewSelected == i)
                DrawRectOutline(r, new Color(0.35f, 0.85f, 1f, 1f), 2f);
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                {
                    if (i < frameSelected.Count)
                        frameSelected[i] = !frameSelected[i];
                    despillPreviewSelected = i;
                    RebuildLargeDespillPreview(i);
                }
                Event.current.Use();
                Repaint();
            }
            GUI.Label(new Rect(r.x, r.yMax - 14f, r.width, 14f), i.ToString(), EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();

        // Large live despilled preview of the focused frame
        if (despillPreviewLarge != null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Live Edge Despill preview (Pass 1–3) — frame "
                + Mathf.Max(0, despillPreviewSelected),
                EditorStyles.miniBoldLabel);
            float side = Mathf.Clamp(Mathf.Max(despillPreviewLarge.width, despillPreviewLarge.height) * 2f, 96f, 220f);
            Rect big = GUILayoutUtility.GetRect(side, side, GUILayout.Width(side), GUILayout.Height(side));
            DrawCheckerboard(big);
            GUI.DrawTexture(big, despillPreviewLarge, ScaleMode.ScaleToFit, true);
        }
    }

    int ComputeDespillPreviewHash()
    {
        unchecked
        {
            int h = croppedFrames.Count * 397;
            h = (h * 31) + colorTolerance.GetHashCode();
            h = (h * 31) + edgeDespillStrength.GetHashCode();
            h = (h * 31) + saveChromaKey.GetHashCode();
            h = (h * 31) + (autoDetectCornerColor ? 1 : 0);
            for (int i = 0; i < croppedFrames.Count; i++)
            {
                var sp = croppedFrames[i];
                h = (h * 31) + (sp != null ? sp.GetInstanceID() : 0);
            }
            return h;
        }
    }

    void ClearDespillPreviewCache()
    {
        for (int i = 0; i < despillPreviewTex.Count; i++)
        {
            if (despillPreviewTex[i] != null)
                DestroyImmediate(despillPreviewTex[i]);
        }
        despillPreviewTex.Clear();
        if (despillPreviewLarge != null)
        {
            DestroyImmediate(despillPreviewLarge);
            despillPreviewLarge = null;
        }
        despillPreviewHash = 0;
    }

    void EnsureDespillPreviewCache()
    {
        int hash = ComputeDespillPreviewHash();
        if (hash == despillPreviewHash && despillPreviewTex.Count == croppedFrames.Count)
            return;
        ClearDespillPreviewCache();
        despillPreviewHash = hash;
        for (int i = 0; i < croppedFrames.Count; i++)
            despillPreviewTex.Add(BuildDespillPreviewTexture(croppedFrames[i]));
        if (despillPreviewSelected < 0 || despillPreviewSelected >= croppedFrames.Count)
        {
            despillPreviewSelected = -1;
            for (int i = 0; i < frameSelected.Count; i++)
            {
                if (frameSelected[i]) { despillPreviewSelected = i; break; }
            }
            if (despillPreviewSelected < 0 && croppedFrames.Count > 0)
                despillPreviewSelected = 0;
        }
        if (despillPreviewSelected >= 0)
            RebuildLargeDespillPreview(despillPreviewSelected);
    }

    void RebuildLargeDespillPreview(int index)
    {
        if (despillPreviewLarge != null)
        {
            DestroyImmediate(despillPreviewLarge);
            despillPreviewLarge = null;
        }
        if (index < 0 || index >= croppedFrames.Count) return;
        despillPreviewLarge = BuildDespillPreviewTexture(croppedFrames[index]);
    }

    Texture2D BuildDespillPreviewTexture(Sprite src)
    {
        if (src == null || src.texture == null) return null;
        Texture2D readable = ExtractSpriteTexture(src);
        if (readable == null) return null;
        Color key = saveChromaKey;
        if (autoDetectCornerColor)
            key = DetectCornerKeyColor(readable);
        ApplyMultiPassChromaDespill(readable, key, colorTolerance, edgeDespillStrength);
        return readable;
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
        ClearDespillPreviewCache();
        Debug.Log($"[DemonKnight] Saved {saved.Count} splices → {folder} + ModularSpriteLibrary");
        string keyDesc = autoDetectCornerColor ? "Auto-Corner" : ColorUtility.ToHtmlStringRGB(saveChromaKey);
        EditorUtility.DisplayDialog(
            "Save Splices to Folder",
            $"Wrote {saved.Count} PNG(s)\n"
            + $"Chroma {keyDesc} tol={colorTolerance:0.00}"
            + $" + Edge Despill {edgeDespillStrength:0.00} (3-pass)\n"
            + "Importer: Sprite 2D+UI / Point / alphaIsTransparency / no mip / Compression None / Bottom-Center\n"
            + $"→ {folder}\nRegistered into ModularSpriteLibrary.",
            "OK");
    }

    /// <summary>
    /// Re-key every PNG under Target Export Folder (default Assets/Art/Spliced/Sprite)
    /// with current multi-pass chroma/despill settings and reimport Sprite Point importer.
    /// </summary>
    void RebakeExistingSplicesInExportFolder()
    {
        SyncDefaultExportFolder();
        string folder = string.IsNullOrWhiteSpace(targetExportFolder)
            ? DefaultExportFolder
            : targetExportFolder.Replace('\\', '/').TrimEnd('/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            EditorUtility.DisplayDialog("Re-bake Splices", "Folder missing:\n" + folder, "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        int ok = 0, miss = 0;
        var saved = new List<Sprite>();
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null)
                {
                    // Ensure sprite import then retry.
                    ConfigureSingleSpritePointBottomCenter(path);
                    sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                if (sp == null) { miss++; continue; }
                Sprite written = WriteSpritePngPointBottomCenter(sp, path);
                if (written != null) { ok++; saved.Add(written); }
                else miss++;
            }
            catch (Exception ex)
            {
                miss++;
                Debug.LogWarning("[DemonKnight] Re-bake miss " + path + ": " + ex.Message);
            }
        }

        if (saved.Count > 0)
            RegisterSpritesIntoLibrary(saved);
        RefreshSplicedPalette(force: true);
        ClearDespillPreviewCache();
        ClearPaperdollLibraryLocal();
        EditorUtility.DisplayDialog(
            "Re-bake Existing Splices",
            $"Re-keyed {ok} PNG(s), miss={miss}\n"
            + $"Chroma tol={colorTolerance:0.00}"
            + $" + Edge Despill {edgeDespillStrength:0.00} (3-pass)\n"
            + "Sprite Point / alphaIsTransparency / no mip / Compression None.\n→ " + folder,
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
        // Multi-pass: key+magenta-bias → edge despill → alpha-bleed RGB pad.
        ApplyMultiPassChromaDespill(readable, key, colorTolerance, edgeDespillStrength);

        // EncodeToPNG preserves alpha — bake clear pixels into the file.
        byte[] png = readable.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(readable);
        if (png == null || png.Length < 32) return null;
        File.WriteAllBytes(assetPath, png);
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
    /// Multi-pass Magenta &amp; Chroma Keying (crop/save + live preview).
    /// Pass 1: BG removal — RGB Euclidean distance from key PLUS magenta-bias (R&amp;B ≫ G).
    /// Pass 2: Edge Despill &amp; Fringe Trim — 4-adjacent opaque→transparent edges; strength slider.
    /// Pass 3: Alpha-bleed color padding — copy opaque edge RGB into adjacent transparent (a=0).
    /// </summary>
    static void ApplyMultiPassChromaDespill(
        Texture2D tex, Color key, float tolerance, float edgeDespill)
    {
        if (tex == null) return;
        int w = tex.width, h = tex.height;
        Color[] pixels = tex.GetPixels();
        float tol = Mathf.Clamp(tolerance, 0.01f, 0.65f);
        float softTol = tol * 1.45f;
        float strength = Mathf.Clamp01(edgeDespill);

        // ── Pass 1: Background removal (key distance + magenta-bias blotches) ──
        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];
            if (p.a < 0.008f) { pixels[i] = Color.clear; continue; }
            float dKey = ColorRgbDistance(p, key);
            float dMag = ColorRgbDistance(p, MagentaChromaKey);
            bool nearKey = dKey <= tol || dMag <= tol;

            // Magenta-bias: R and B significantly exceed G (AI compression blotches around #FF00FF).
            float magBias = Mathf.Min(p.r, p.b) - p.g;
            bool magentaBiasKill = magBias > 0.22f
                                   && p.r > 0.40f && p.b > 0.40f && p.g < 0.62f
                                   && (dKey <= softTol || dMag <= softTol || magBias > 0.38f);
            // Soft punch for near-key magenta cast even when Euclidean is slightly over tol.
            float magCast = (p.r + p.b) * 0.5f - p.g;
            bool softMagenta = magCast > 0.16f && p.r > 0.32f && p.b > 0.32f && p.g < 0.58f
                               && (dKey <= softTol || dMag <= softTol);

            if (nearKey || magentaBiasKill || softMagenta)
                pixels[i] = Color.clear;
        }

        // ── Pass 2: Edge Despill & Fringe Trim (4-adjacent to transparent) ──
        if (strength > 0.001f)
        {
            int passes = strength < 0.35f ? 1 : (strength < 0.75f ? 2 : 3);
            for (int pass = 0; pass < passes; pass++)
            {
                var outPx = (Color[])pixels.Clone();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        Color p = pixels[i];
                        if (p.a < 0.04f) continue;
                        if (!HasClearNeighbor4(pixels, w, h, x, y)) continue;

                        float mag = (p.r + p.b) * 0.5f - p.g;
                        float excessRb = Mathf.Max(0f, Mathf.Min(p.r, p.b) - p.g);
                        // Strong pink/magenta fringe → alpha 0
                        float strongThresh = Mathf.Lerp(0.42f, 0.14f, strength);
                        if (mag > strongThresh && p.r > 0.28f && p.b > 0.28f)
                        {
                            outPx[i] = Color.clear;
                            continue;
                        }
                        // Faint spill → desaturate excess R/B toward G (neutral dark outlines, not pink)
                        if (excessRb > 0.02f || mag > 0.04f)
                        {
                            float pull = excessRb * Mathf.Lerp(0.55f, 1.55f, strength);
                            p.r = Mathf.Clamp01(p.r - pull);
                            p.b = Mathf.Clamp01(p.b - pull);
                            // Extra nudge when still magenta-cast
                            float magLeft = (p.r + p.b) * 0.5f - p.g;
                            if (magLeft > 0.05f)
                            {
                                float t = magLeft * strength * 0.85f;
                                p.r = Mathf.Lerp(p.r, p.g, t);
                                p.b = Mathf.Lerp(p.b, p.g, t);
                            }
                            float luma = p.r * 0.3f + p.g * 0.59f + p.b * 0.11f;
                            magLeft = (p.r + p.b) * 0.5f - p.g;
                            if (luma < 0.05f || magLeft > Mathf.Lerp(0.35f, 0.16f, strength))
                            {
                                outPx[i] = Color.clear;
                                continue;
                            }
                            p.a *= Mathf.Clamp01(1f - excessRb * Mathf.Lerp(0.8f, 2.4f, strength));
                            if (p.a < 0.06f) { outPx[i] = Color.clear; continue; }
                            outPx[i] = p;
                        }
                    }
                }
                pixels = outPx;
            }
        }

        // ── Pass 3: Alpha-bleed color padding (RGB into transparent; alpha stays 0) ──
        {
            var padded = (Color[])pixels.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (pixels[i].a >= 0.04f) continue;
                    if (!TryAverageOpaqueNeighbor4(pixels, w, h, x, y, out Color avgRgb))
                        continue;
                    padded[i] = new Color(avgRgb.r, avgRgb.g, avgRgb.b, 0f);
                }
            }
            pixels = padded;
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
    }

    static bool HasClearNeighbor4(Color[] pixels, int w, int h, int x, int y)
    {
        // 4-adjacent only (N/E/S/W) — matches Edge Despill / Fringe Trim spec.
        int[] dxs = { 0, 1, 0, -1 };
        int[] dys = { 1, 0, -1, 0 };
        for (int k = 0; k < 4; k++)
        {
            int nx = x + dxs[k], ny = y + dys[k];
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            if (pixels[ny * w + nx].a < 0.04f) return true;
        }
        return false;
    }

    static bool TryAverageOpaqueNeighbor4(
        Color[] pixels, int w, int h, int x, int y, out Color avg)
    {
        float r = 0f, g = 0f, b = 0f;
        int n = 0;
        int[] dxs = { 0, 1, 0, -1 };
        int[] dys = { 1, 0, -1, 0 };
        for (int k = 0; k < 4; k++)
        {
            int nx = x + dxs[k], ny = y + dys[k];
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            Color p = pixels[ny * w + nx];
            if (p.a < 0.04f) continue;
            r += p.r; g += p.g; b += p.b;
            n++;
        }
        if (n == 0) { avg = Color.clear; return false; }
        float inv = 1f / n;
        avg = new Color(r * inv, g * inv, b * inv, 0f);
        return true;
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

    /// <summary>
    /// After each PNG save/import under Assets/Art/Spliced/Sprite:
    /// Sprite (2D and UI), Filter Mode Point, Alpha Is Transparency, no mip maps, Compression None.
    /// </summary>
    static void ConfigureSingleSpritePointBottomCenter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite; // Sprite (2D and UI)
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed; // Compression None
        importer.isReadable = true;
        var platform = new TextureImporterPlatformSettings
        {
            name = "DefaultTexturePlatform",
            overridden = true,
            format = TextureImporterFormat.RGBA32,
            maxTextureSize = 8192,
            textureCompression = TextureImporterCompression.Uncompressed
        };
        importer.SetPlatformTextureSettings(platform);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.alphaSource = TextureImporterAlphaSource.FromInput;
        settings.alphaIsTransparency = true;
        settings.mipmapEnabled = false;
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
        EditorGUILayout.HelpBox(
            "StageLayoutProfile pew slots are POSITION/SCALE placement guides (and optional "
            + "empty-seat preview sprites). Gameplay church visuals pick from Tab 4 Class "
            + "Sprite pools by Peasant/Warrior/Knight. Ensure remaps script GUID / transplants "
            + "YAML — never wipes seat guides. Empty seats stay empty (no auto-fill).",
            MessageType.Info);
        stageProfile = (StageLayoutProfile)EditorGUILayout.ObjectField(
            "StageLayoutProfile", stageProfile, typeof(StageLayoutProfile), false);
        if (stageProfile == null
            || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(stageProfile)))
        {
            if (GUILayout.Button("Ensure Stage Layout Profile Asset"))
            {
                stageProfile = ArthurianStageLayoutProfileEnsure.EnsureAsset();
            }
            if (stageProfile == null) return;
        }
        stageProfile.EnsureSlotCounts();
        EditorGUILayout.LabelField(
            $"Assigned pew sprites: {ArthurianStageLayoutProfileEnsure.CountAssignedSprites(stageProfile)} / 52  ·  asset={AssetDatabase.GetAssetPath(stageProfile)}",
            EditorStyles.miniLabel);

        stageView = (StageViewMode)EditorGUILayout.EnumPopup("Stage View", stageView);
        if (stageView == StageViewMode.ChurchyardBattle)
            battleSide = (BattleSlotSide)EditorGUILayout.EnumPopup("Battle Side", battleSide);

        showChurchBackground = EditorGUILayout.ToggleLeft(
            "Church background (SomersetChurchInterior — match play stage)", showChurchBackground);
        if (stageView == StageViewMode.SomersetChurch)
        {
            usePlayPewLayoutPreview = EditorGUILayout.ToggleLeft(
                "Play pew layout + scale (SomersetPewLayout · 3 sprite rows · 88×112)",
                usePlayPewLayoutPreview);
        }

        EditorGUILayout.BeginHorizontal();
        stageZoom = EditorGUILayout.Slider("Zoom", stageZoom, 8f, 64f);
        if (GUILayout.Button("Fit Church", GUILayout.Width(90)))
        {
            stagePan = Vector2.zero;
            stageZoom = 28f;
        }
        if (GUILayout.Button("Auto-Arrange 52 Overflow Formation", GUILayout.Height(28)))
            AutoArrangeOverflowFormation();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "Assigned sprites only (no auto-fill). Drag seats to move; SE handle or Seat scale slider. "
            + "Positions save as normalized 0–1 on StageLayoutProfile (play reads them).",
            MessageType.Info);

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

        Rect content = canvas;
        bool churchMode = stageView == StageViewMode.SomersetChurch
                          && showChurchBackground
                          && usePlayPewLayoutPreview;
        if (showChurchBackground && stageView == StageViewMode.SomersetChurch)
            content = DrawChurchBackgroundInRect(canvas, stagePan, stageZoom / 28f);
        else
        {
            Handles.BeginGUI();
            Vector2 origin = CanvasToScreen(canvas, Vector2.zero);
            Handles.color = new Color(1f, 1f, 1f, 0.15f);
            Handles.DrawLine(new Vector3(canvas.x, origin.y), new Vector3(canvas.xMax, origin.y));
            Handles.DrawLine(new Vector3(origin.x, canvas.y), new Vector3(origin.x, canvas.yMax));
            Handles.EndGUI();
        }

        if (slots == null) return;
        Event e = Event.current;

        if (churchMode)
            DrawPlayPewLayoutOnChurch(canvas, content, slots, e);
        else
            DrawAbstractSlotHandles(canvas, slots, e);

        // Pan with middle mouse / alt-drag
        if (e.type == EventType.MouseDrag && (e.button == 2 || (e.button == 0 && e.alt)))
        {
            stagePan += e.delta;
            e.Use();
            Repaint();
        }
        if (e.type == EventType.ScrollWheel && canvas.Contains(e.mousePosition))
        {
            stageZoom = Mathf.Clamp(stageZoom * (e.delta.y > 0 ? 0.92f : 1.08f), 8f, 64f);
            e.Use();
            Repaint();
        }
    }

    void DrawAbstractSlotHandles(Rect canvas, List<SlotTransformConfig> slots, Event e)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null) continue;
            Vector2 screen = CanvasToScreen(canvas, s.position);
            float size = 18f * Mathf.Clamp(s.scale.x, 0.5f, 1.5f);
            Rect handle = new Rect(screen.x - size * 0.5f, screen.y - size * 0.5f, size, size);
            bool sel = i == selectedSlot;

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
                PersistStageProfile("BattleSlotDragEnd");
                e.Use();
            }
        }
    }

    /// <summary>
    /// Church pew preview: only assigned StageLayoutProfile sprites (no palette RNG fill).
    /// Drag move + SE scale handle; positions stored as normalized 0–1 (play-matched).
    /// </summary>
    void DrawPlayPewLayoutOnChurch(Rect canvas, Rect content, List<SlotTransformConfig> slots, Event e)
    {
        lastChurchContentRect = content;
        EnsureChurchSlotsSeededFromPlayLayout(slots);

        var anchors = SomersetPewLayout.CreateDefaultPews();
        var order = new List<int>(SomersetPewLayout.TotalSeats);
        for (int i = 0; i < SomersetPewLayout.TotalSeats; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            int rowA = SomersetPewLayout.GetStaticPewRow(a);
            int rowB = SomersetPewLayout.GetStaticPewRow(b);
            int cmp = rowB.CompareTo(rowA);
            return cmp != 0 ? cmp : a.CompareTo(b);
        });

        float pxScale = content.width / ChurchRefWidth;
        float pyScale = content.height / ChurchRefHeight;
        int assignedDrawn = 0;

        foreach (int seatIndex in order)
        {
            if (!SeatShowsSpriteSafe(seatIndex)) continue;
            if (seatIndex >= slots.Count || slots[seatIndex] == null) continue;
            var s = slots[seatIndex];

            Vector2 norm = GetChurchNormalizedPosition(s, anchors, seatIndex);
            Vector2 screen = NormToScreen(content, norm);

            float rowScale = GetPlaySeatScale(anchors, seatIndex);
            float mul = Mathf.Clamp(s.scale.x > 0.01f ? s.scale.x : 1f, 0.2f, 3f);
            float pw = PlayPewBaseSize.x * rowScale * mul * pxScale;
            float ph = PlayPewBaseSize.y * rowScale * mul * pyScale;
            Rect pr = new Rect(screen.x - pw * 0.5f, screen.y - ph * 0.88f, pw, ph);

            // Assigned only — never cycle splicedPalette onto empty seats.
            Sprite preview = ResolveSlotPreviewSprite(s);
            if (preview == null && paletteSelectedSprite != null && seatIndex == selectedSlot)
                preview = paletteSelectedSprite;

            bool sel = seatIndex == selectedSlot;
            if (preview != null)
            {
                DrawSprite(pr, preview, Color.white);
                assignedDrawn++;
            }
            else
            {
                // Empty marker only (no sprite copy).
                EditorGUI.DrawRect(pr, new Color(0.15f, 0.18f, 0.28f, sel ? 0.45f : 0.22f));
                DrawRectOutline(pr, new Color(1f, 1f, 1f, sel ? 0.35f : 0.12f), 1f);
            }

            float handle = Mathf.Clamp(pw * 0.28f, 14f, 22f);
            Rect hr = new Rect(screen.x - handle * 0.5f, screen.y - handle * 0.5f, handle, handle);
            EditorGUI.DrawRect(hr, sel ? new Color(1f, 0.85f, 0.25f, 0.95f) : new Color(0.35f, 0.65f, 0.95f, 0.55f));
            GUI.Label(hr, seatIndex.ToString("00"), EditorStyles.centeredGreyMiniLabel);

            // SE scale handle (selected assigned seats).
            Rect scaleHandle = new Rect(pr.xMax - 10f, pr.yMax - 10f, 12f, 12f);
            if (sel)
            {
                EditorGUI.DrawRect(scaleHandle, new Color(1f, 0.55f, 0.2f, 0.95f));
                GUI.Label(new Rect(pr.x, pr.y - 14f, pr.width, 14f),
                    $"scale {mul:0.00}", EditorStyles.miniLabel);
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (sel && scaleHandle.Contains(e.mousePosition))
                {
                    selectedSlot = seatIndex;
                    churchDragSeat = seatIndex;
                    scalingSlot = true;
                    draggingSlot = false;
                    e.Use();
                    Repaint();
                }
                else if (hr.Contains(e.mousePosition) || pr.Contains(e.mousePosition))
                {
                    selectedSlot = seatIndex;
                    churchDragSeat = seatIndex;
                    draggingSlot = true;
                    scalingSlot = false;
                    e.Use();
                    Repaint();
                }
            }
        }

        if ((draggingSlot || scalingSlot) && churchDragSeat >= 0 && churchDragSeat < slots.Count
            && slots[churchDragSeat] != null)
        {
            var s = slots[churchDragSeat];
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                Undo.RecordObject(stageProfile, scalingSlot ? "Scale Church Pew" : "Move Church Pew");
                if (scalingSlot)
                {
                    Vector2 norm = GetChurchNormalizedPosition(s, anchors, churchDragSeat);
                    Vector2 pivot = NormToScreen(content, norm);
                    float dist = Vector2.Distance(e.mousePosition, pivot);
                    float baseDist = Mathf.Max(20f, PlayPewBaseSize.y * GetPlaySeatScale(anchors, churchDragSeat) * pyScale * 0.5f);
                    float mul = Mathf.Clamp(dist / baseDist, 0.2f, 3f);
                    s.scale = new Vector2(mul, mul);
                }
                else
                {
                    Vector2 norm = ScreenToNorm(content, e.mousePosition);
                    s.position = new Vector2(
                        Mathf.Clamp01(norm.x),
                        Mathf.Clamp01(norm.y));
                    s.sortingOrder = Mathf.RoundToInt(-s.position.y * 100f);
                }
                EditorUtility.SetDirty(stageProfile);
                e.Use();
                Repaint();
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                draggingSlot = false;
                scalingSlot = false;
                churchDragSeat = -1;
                PersistStageProfile("ChurchPewDragEnd");
                e.Use();
            }
        }

        // Selected seat scale slider (precise).
        if (selectedSlot >= 0 && selectedSlot < slots.Count && slots[selectedSlot] != null
            && SeatShowsSpriteSafe(selectedSlot))
        {
            var s = slots[selectedSlot];
            float mul = Mathf.Clamp(s.scale.x > 0.01f ? s.scale.x : 1f, 0.2f, 3f);
            Rect sliderR = new Rect(canvas.x + 8f, canvas.y + 6f, Mathf.Min(220f, canvas.width - 16f), 18f);
            EditorGUI.BeginChangeCheck();
            float nMul = EditorGUI.Slider(sliderR, "Seat scale", mul, 0.2f, 3f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(stageProfile, "Scale Church Pew");
                s.scale = new Vector2(nMul, nMul);
                PersistStageProfile("ChurchPewScaleSlider");
            }
        }

        GUI.Label(
            new Rect(canvas.x + 6f, canvas.yMax - 18f, canvas.width - 12f, 16f),
            $"Assigned sprites only ({assignedDrawn}) · drag move · SE handle / slider scale · 3 rows",
            EditorStyles.miniLabel);
    }

    static Vector2 NormToScreen(Rect content, Vector2 norm)
        => new Vector2(
            content.x + norm.x * content.width,
            content.yMax - norm.y * content.height);

    static Vector2 ScreenToNorm(Rect content, Vector2 screen)
    {
        float x = content.width > 1f ? (screen.x - content.x) / content.width : 0.5f;
        float y = content.height > 1f ? (content.yMax - screen.y) / content.height : 0.5f;
        return new Vector2(x, y);
    }

    static bool IsChurchNormalizedPosition(Vector2 pos)
        => pos.x >= 0f && pos.x <= 1f && pos.y >= 0f && pos.y <= 1f;

    Vector2 GetChurchNormalizedPosition(SlotTransformConfig s, PewAnchor[] anchors, int seatIndex)
    {
        if (s != null && IsChurchNormalizedPosition(s.position))
            return s.position;
        return GetPlayNormalizedSeatPosition(anchors, seatIndex);
    }

    void EnsureChurchSlotsSeededFromPlayLayout(List<SlotTransformConfig> slots)
    {
        if (slots == null || stageProfile == null) return;
        var anchors = SomersetPewLayout.CreateDefaultPews();
        bool any = false;
        for (int i = 0; i < slots.Count && i < SomersetPewLayout.TotalSeats; i++)
        {
            if (slots[i] == null) continue;
            if (IsChurchNormalizedPosition(slots[i].position)) continue;
            // Old ChurchPewDefault world coords → seed play layout normalized.
            slots[i].position = GetPlayNormalizedSeatPosition(anchors, i);
            if (slots[i].scale.x < 0.05f) slots[i].scale = Vector2.one;
            any = true;
        }
        if (any)
        {
            EditorUtility.SetDirty(stageProfile);
            Debug.Log("[DemonKnight] Seeded church pew slots to play-normalized 0–1 positions.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 3 — Scene / Screen Backgrounds (jpg·png or mp4)
    // ═══════════════════════════════════════════════════════════════════════

    void DrawSceneBackgroundsTab()
    {
        EditorGUILayout.LabelField("Scene / Screen Backgrounds", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Assign-only: pick a jpg/png Sprite and/or mp4 VideoClip per game screen. "
            + "Empty slots keep each screen’s built-in default. Persists on "
            + SceneBgProfilePath + " (same discipline as StageLayoutProfile).",
            MessageType.Info);

        sceneBgProfile = (SceneBackgroundProfile)EditorGUILayout.ObjectField(
            "SceneBackgroundProfile", sceneBgProfile, typeof(SceneBackgroundProfile), false);
        if (sceneBgProfile == null
            || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sceneBgProfile)))
        {
            if (GUILayout.Button("Ensure Scene Background Profile Asset"))
                sceneBgProfile = ArthurianStageLayoutProfileEnsure.EnsureSceneBackgroundAsset();
            if (sceneBgProfile == null) return;
        }
        sceneBgProfile.EnsureDefaultEntries();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save SceneBackgroundProfile", GUILayout.Height(26)))
            PersistSceneBgProfile("SaveButton");
        if (GUILayout.Button("Ping Asset", GUILayout.Width(90)))
        {
            Selection.activeObject = sceneBgProfile;
            EditorGUIUtility.PingObject(sceneBgProfile);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            "Asset: " + AssetDatabase.GetAssetPath(sceneBgProfile), EditorStyles.miniLabel);

        sceneBgScroll = EditorGUILayout.BeginScrollView(sceneBgScroll, GUILayout.MinHeight(360f));
        var entries = sceneBgProfile.entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            bool selected = selectedSceneBgSlot == (int)e.slot;
            EditorGUILayout.BeginVertical(selected ? EditorStyles.helpBox : "box");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selected, e.displayName + "  (" + e.slot + ")", EditorStyles.foldoutHeader))
                selectedSceneBgSlot = (int)e.slot;
            GUILayout.FlexibleSpace();
            bool has = e.imageSprite != null || e.videoClip != null
                       || !string.IsNullOrEmpty(e.imageAssetPath)
                       || !string.IsNullOrEmpty(e.videoAssetPath);
            GUILayout.Label(has ? "ASSIGNED" : "empty", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            e.displayName = EditorGUILayout.TextField("Label", e.displayName);
            e.imageSprite = (Sprite)EditorGUILayout.ObjectField(
                "Image (jpg/png Sprite)", e.imageSprite, typeof(Sprite), false);
            e.videoClip = (VideoClip)EditorGUILayout.ObjectField(
                "Video (mp4)", e.videoClip, typeof(VideoClip), false);

            if (e.imageSprite != null)
            {
                string p = AssetDatabase.GetAssetPath(e.imageSprite);
                if (!string.IsNullOrEmpty(p)) e.imageAssetPath = p;
            }
            if (e.videoClip != null)
            {
                string p = AssetDatabase.GetAssetPath(e.videoClip);
                if (!string.IsNullOrEmpty(p)) e.videoAssetPath = p;
            }

            EditorGUILayout.LabelField("Image path", string.IsNullOrEmpty(e.imageAssetPath) ? "(none)" : e.imageAssetPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Video path", string.IsNullOrEmpty(e.videoAssetPath) ? "(none)" : e.videoAssetPath, EditorStyles.miniLabel);

            // Preview strip
            Rect prev = GUILayoutUtility.GetRect(64f, 72f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(prev, new Color(0.12f, 0.12f, 0.14f));
            if (e.imageSprite != null)
            {
                Rect sprR = new Rect(prev.x + 8f, prev.y + 4f, 64f, 64f);
                DrawSprite(sprR, e.imageSprite, Color.white);
            }
            else if (e.videoClip != null)
            {
                GUI.Label(new Rect(prev.x + 8f, prev.y + 24f, prev.width - 16f, 24f),
                    "Video: " + e.videoClip.name, EditorStyles.boldLabel);
            }
            else
            {
                GUI.Label(new Rect(prev.x + 8f, prev.y + 24f, prev.width - 16f, 24f),
                    "(no assignment — screen keeps default)", EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear slot", GUILayout.Width(100)))
            {
                Undo.RecordObject(sceneBgProfile, "Clear Scene Background");
                e.imageSprite = null;
                e.videoClip = null;
                e.imageAssetPath = "";
                e.videoAssetPath = "";
                PersistSceneBgProfile("ClearSceneBg");
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sceneBgProfile, "Edit Scene Background");
                PersistSceneBgProfile("EditSceneBg");
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }
        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 4 — Class Sprites / Unit Pools (Peasant · Warrior · Knight)
    // ═══════════════════════════════════════════════════════════════════════

    void DrawClassSpritesTab()
    {
        EditorGUILayout.LabelField("Class Sprites / Unit Pools", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Submit multiple sprites into Peasant, Warrior, and Knight pools. "
            + "Church spawn picks from the matching class pool (deterministic by unitID — "
            + "same recruit keeps the same sprite across refreshes). "
            + "Empty pool → soft-fail to StageLayout assign-guide / solid fallback (no crash). "
            + "Tab 2 Stage Placer seat sprites stay POSITION/SCALE placement guides only — "
            + "they are NOT gameplay identity. Persists on " + ClassPoolProfilePath + ".",
            MessageType.Info);

        classPoolProfile = (ClassSpritePoolProfile)EditorGUILayout.ObjectField(
            "ClassSpritePoolProfile", classPoolProfile, typeof(ClassSpritePoolProfile), false);
        if (classPoolProfile == null
            || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(classPoolProfile)))
        {
            if (GUILayout.Button("Ensure Class Sprite Pool Profile Asset"))
                classPoolProfile = ArthurianStageLayoutProfileEnsure.EnsureClassSpritePoolAsset();
            if (classPoolProfile == null) return;
        }
        classPoolProfile.EnsureLists();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save ClassSpritePoolProfile", GUILayout.Height(26)))
            PersistClassPoolProfile("SaveButton");
        if (GUILayout.Button("Ping Asset", GUILayout.Width(90)))
        {
            Selection.activeObject = classPoolProfile;
            EditorGUIUtility.PingObject(classPoolProfile);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            "Asset: " + AssetDatabase.GetAssetPath(classPoolProfile), EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"Counts — Peasant {classPoolProfile.CountUsable(UnitType.Peasant)} · "
            + $"Warrior {classPoolProfile.CountUsable(UnitType.Warrior)} · "
            + $"Knight {classPoolProfile.CountUsable(UnitType.Knight)}",
            EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(selectedClassPool == ClassPoolKind.Peasant, "Peasant", EditorStyles.toolbarButton))
            selectedClassPool = ClassPoolKind.Peasant;
        if (GUILayout.Toggle(selectedClassPool == ClassPoolKind.Warrior, "Warrior", EditorStyles.toolbarButton))
            selectedClassPool = ClassPoolKind.Warrior;
        if (GUILayout.Toggle(selectedClassPool == ClassPoolKind.Knight, "Knight", EditorStyles.toolbarButton))
            selectedClassPool = ClassPoolKind.Knight;
        EditorGUILayout.EndHorizontal();

        List<Sprite> pool = selectedClassPool switch
        {
            ClassPoolKind.Warrior => classPoolProfile.warriorSprites,
            ClassPoolKind.Knight => classPoolProfile.knightSprites,
            _ => classPoolProfile.peasantSprites
        };

        EditorGUILayout.BeginHorizontal();
        classPoolAddSprite = (Sprite)EditorGUILayout.ObjectField(
            "Add Sprite", classPoolAddSprite, typeof(Sprite), false);
        EditorGUI.BeginDisabledGroup(classPoolAddSprite == null);
        if (GUILayout.Button("Add to pool", GUILayout.Width(110)))
        {
            Undo.RecordObject(classPoolProfile, "Add Class Pool Sprite");
            pool.Add(classPoolAddSprite);
            classPoolAddSprite = null;
            PersistClassPoolProfile("AddClassPoolSprite");
        }
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("Add selected palette", GUILayout.Width(150))
            && paletteSelectedSprite != null)
        {
            Undo.RecordObject(classPoolProfile, "Add Palette to Class Pool");
            pool.Add(paletteSelectedSprite);
            PersistClassPoolProfile("AddPaletteToClassPool");
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear this pool", GUILayout.Width(140)))
        {
            if (EditorUtility.DisplayDialog(
                    "Clear class pool",
                    "Remove all sprites from the " + selectedClassPool + " pool?",
                    "Clear", "Cancel"))
            {
                Undo.RecordObject(classPoolProfile, "Clear Class Pool");
                pool.Clear();
                PersistClassPoolProfile("ClearClassPool");
            }
        }

        classPoolScroll = EditorGUILayout.BeginScrollView(classPoolScroll, GUILayout.MinHeight(320f));
        if (pool.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Pool empty — church seats for this class keep StageLayout assign-guide / solid fallback.",
                MessageType.Warning);
        }
        for (int i = 0; i < pool.Count; i++)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            pool[i] = (Sprite)EditorGUILayout.ObjectField(
                "#" + i, pool[i], typeof(Sprite), false, GUILayout.Height(64f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(classPoolProfile, "Edit Class Pool Sprite");
                PersistClassPoolProfile("EditClassPoolSprite");
            }
            if (GUILayout.Button("×", GUILayout.Width(28), GUILayout.Height(64f)))
            {
                Undo.RecordObject(classPoolProfile, "Remove Class Pool Sprite");
                pool.RemoveAt(i);
                PersistClassPoolProfile("RemoveClassPoolSprite");
                i--;
            }
            EditorGUILayout.EndHorizontal();
            if (i >= 0 && i < pool.Count && pool[i] != null)
            {
                Rect prev = GUILayoutUtility.GetRect(48f, 48f, GUILayout.ExpandWidth(false));
                DrawSprite(prev, pool[i], Color.white);
                EditorGUILayout.LabelField(pool[i].name, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Spliced palette (quick add)", EditorStyles.boldLabel);
        RefreshSplicedPalette();
        paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, GUILayout.Height(120f));
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < splicedPalette.Count; i++)
        {
            Sprite sp = splicedPalette[i];
            if (sp == null) continue;
            Rect r = GUILayoutUtility.GetRect(56f, 56f, GUILayout.Width(56f), GUILayout.Height(56f));
            if (Event.current.type == EventType.Repaint)
                DrawSprite(r, sp, Color.white);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                paletteSelectedSprite = sp;
                Undo.RecordObject(classPoolProfile, "Add Palette Click to Class Pool");
                pool.Add(sp);
                PersistClassPoolProfile("PaletteClickClassPool");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Local copy of SomersetPewLayout.SeatShowsSprite — avoids CS0117 when tip Architect
    /// compiles before tip PewLayout lands (Break-Glass partial / stale Downloads).
    /// </summary>
    static bool SeatShowsSpriteSafe(int pewSeatIndex)
    {
        const int total = 52;
        const int rowSize = 13;
        const int visualRows = 3;
        return (Mathf.Clamp(pewSeatIndex, 0, total - 1) / rowSize) < visualRows;
    }

    static Vector2 GetPlayNormalizedSeatPosition(PewAnchor[] pews, int globalSeatIndex)
    {
        if (pews == null || pews.Length == 0) return new Vector2(0.5f, 0.5f);
        int remaining = Mathf.Clamp(globalSeatIndex, 0, SomersetPewLayout.TotalSeats - 1);
        int pewIndex = 0;
        int slot = 0;
        for (int p = 0; p < pews.Length; p++)
        {
            int cap = Mathf.Max(1, pews[p].seatCapacity);
            if (remaining < cap) { pewIndex = p; slot = remaining; break; }
            remaining -= cap;
            pewIndex = p;
        }
        PewAnchor pew = pews[Mathf.Clamp(pewIndex, 0, pews.Length - 1)];
        int capN = Mathf.Max(1, pew.seatCapacity);
        float t = capN == 1 ? 0.5f : slot / (float)(capN - 1);
        float x = pew.normalizedCenter.x + Mathf.Lerp(-pew.seatSpread * 0.5f, pew.seatSpread * 0.5f, t);
        return new Vector2(x, pew.normalizedCenter.y);
    }

    static float GetPlaySeatScale(PewAnchor[] pews, int globalSeatIndex)
    {
        if (pews == null || pews.Length == 0) return 1f;
        int remaining = Mathf.Clamp(globalSeatIndex, 0, SomersetPewLayout.TotalSeats - 1);
        for (int p = 0; p < pews.Length; p++)
        {
            int cap = Mathf.Max(1, pews[p].seatCapacity);
            if (remaining < cap) return pews[p].scale;
            remaining -= cap;
        }
        return pews[pews.Length - 1].scale;
    }

    Texture2D EnsureChurchBackgroundTexture()
    {
        if (cachedChurchBg != null) return cachedChurchBg;
        cachedChurchSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChurchBackgroundPath);
        cachedChurchBg = AssetDatabase.LoadAssetAtPath<Texture2D>(ChurchBackgroundPath);
        if (cachedChurchBg == null && cachedChurchSprite != null)
            cachedChurchBg = cachedChurchSprite.texture;
        return cachedChurchBg;
    }

    /// <summary>
    /// Draw Somerset church art preserveAspect inside canvas (letterbox like play Image).
    /// Returns the drawn content rect used for pew normalized mapping.
    /// </summary>
    Rect DrawChurchBackgroundInRect(Rect canvas, Vector2 pan, float zoomMul)
    {
        Texture2D tex = EnsureChurchBackgroundTexture();
        EditorGUI.DrawRect(canvas, new Color(0.06f, 0.05f, 0.07f, 1f));
        if (tex == null)
        {
            GUI.Label(canvas, "Missing " + ChurchBackgroundPath, EditorStyles.centeredGreyMiniLabel);
            return canvas;
        }

        float z = Mathf.Clamp(zoomMul, 0.5f, 3f);
        float spriteAspect = ChurchRefWidth / ChurchRefHeight;
        float rectAspect = canvas.width / Mathf.Max(1f, canvas.height);
        Rect content;
        if (spriteAspect > rectAspect)
        {
            float h = canvas.width / spriteAspect;
            content = new Rect(canvas.x, canvas.y + (canvas.height - h) * 0.5f, canvas.width, h);
        }
        else
        {
            float w = canvas.height * spriteAspect;
            content = new Rect(canvas.x + (canvas.width - w) * 0.5f, canvas.y, w, canvas.height);
        }

        // Zoom about content center + pan (pew math uses this same rect).
        Vector2 center = content.center + pan;
        float w2 = content.width * z;
        float h2 = content.height * z;
        Rect draw = new Rect(center.x - w2 * 0.5f, center.y - h2 * 0.5f, w2, h2);
        GUI.BeginClip(canvas);
        Rect local = new Rect(draw.x - canvas.x, draw.y - canvas.y, draw.width, draw.height);
        GUI.DrawTexture(local, tex, ScaleMode.StretchToFill, true);
        GUI.EndClip();
        return draw;
    }

    void DrawTab1ChurchPewPreview()
    {
        EditorGUILayout.LabelField("Church pew preview (assigned only)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Shows StageLayoutProfile assigned pew sprites only — never auto-fills empty seats. "
            + "Assign on Tab 2 (palette → slot). Drag/scale on Tab 2 church canvas.",
            MessageType.None);
        if (stageProfile == null)
            stageProfile = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(StageProfilePath);
        EditorGUILayout.BeginHorizontal();
        tab1ChurchPreviewZoom = EditorGUILayout.Slider("Preview Zoom", tab1ChurchPreviewZoom, 0.6f, 2f);
        if (GUILayout.Button("Open Tab 2", GUILayout.Width(90)))
            tab = ArchitectTab.StagePlacer;
        if (GUILayout.Button("Reset", GUILayout.Width(60)))
        {
            tab1ChurchPreviewZoom = 1f;
            tab1ChurchPreviewPan = Vector2.zero;
        }
        EditorGUILayout.EndHorizontal();

        Rect canvas = GUILayoutUtility.GetRect(10, 220, GUILayout.ExpandWidth(true), GUILayout.MinHeight(200));
        Rect content = DrawChurchBackgroundInRect(canvas, tab1ChurchPreviewPan, tab1ChurchPreviewZoom);

        float pxScale = content.width / ChurchRefWidth;
        float pyScale = content.height / ChurchRefHeight;
        var anchors = SomersetPewLayout.CreateDefaultPews();
        var slots = stageProfile != null ? stageProfile.churchPewSlots : null;
        if (slots != null) EnsureChurchSlotsSeededFromPlayLayout(slots);

        int drawn = 0;
        if (slots != null)
        {
            for (int seat = 0; seat < SomersetPewLayout.TotalSeats && seat < slots.Count; seat++)
            {
                if (!SeatShowsSpriteSafe(seat)) continue;
                var s = slots[seat];
                if (s == null) continue;
                Sprite sp = ResolveSlotPreviewSprite(s);
                if (sp == null) continue; // assigned only — skip empty

                Vector2 norm = GetChurchNormalizedPosition(s, anchors, seat);
                Vector2 screen = NormToScreen(content, norm);
                float rowScale = GetPlaySeatScale(anchors, seat);
                float mul = Mathf.Clamp(s.scale.x > 0.01f ? s.scale.x : 1f, 0.2f, 3f);
                float pw = PlayPewBaseSize.x * rowScale * mul * pxScale;
                float ph = PlayPewBaseSize.y * rowScale * mul * pyScale;
                Rect pr = new Rect(screen.x - pw * 0.5f, screen.y - ph * 0.88f, pw, ph);
                DrawSprite(pr, sp, Color.white);
                drawn++;
            }
        }

        Event e = Event.current;
        if (e.type == EventType.MouseDrag && canvas.Contains(e.mousePosition)
            && (e.button == 2 || (e.button == 0 && e.alt)))
        {
            tab1ChurchPreviewPan += e.delta;
            e.Use();
            Repaint();
        }
        GUI.Label(
            new Rect(canvas.x + 6f, canvas.yMax - 16f, canvas.width - 12f, 14f),
            drawn == 0
                ? "No assigned pew sprites — Tab 2 → Assign Palette Sprite → Slot"
                : drawn + " assigned · no auto-fill",
            EditorStyles.miniLabel);
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
            PersistStageProfile("EditSlotInspector");
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
            PersistStageProfile("ClearSlotPreview");
        }
        if (GUILayout.Button("Save StageLayoutProfile"))
            PersistStageProfile("SaveButton");
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
        if (stageProfile == null
            || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(stageProfile)))
        {
            stageProfile = ArthurianStageLayoutProfileEnsure.EnsureAsset();
            slots = ActiveSlots();
            if (slots == null || selectedSlot < 0 || selectedSlot >= slots.Count) return;
        }
        var s = slots[selectedSlot];
        Undo.RecordObject(stageProfile, "Assign Palette To Slot");
        s.previewOrOverrideSprite = paletteSelectedSprite;
        // Clear other church seats' accidental copies? No — only this slot.
        PersistStageProfile("AssignPalette");
        Debug.Log(
            $"[DemonKnight] Assigned '{paletteSelectedSprite.name}' → church seat #{selectedSlot:00} only "
            + $"(persisted → {AssetDatabase.GetAssetPath(stageProfile)}).");
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

    /// <summary>
    /// Fully local wipe — NO PackageVersion.Wipe* / Bootstrap.Clear* (CS0117 when tip Architect
    /// lands before tip PackageVersion/Bootstrap).
    /// </summary>
    string ClearPaperdollLibraryLocal()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("Architect local wipe (no PackageVersion/Bootstrap deps)");
        const string artPath = DefaultLibraryPath;
        const string resPath = "Assets/Resources/Art/Modular/ModularSpriteLibrary.asset";
        const string splicedFolder = DefaultExportFolder;

        EnsureFolder("Assets/Art/Modular");
        EnsureFolder("Assets/Resources/Art/Modular");
        EnsureFolder(splicedFolder);
        EnsureLibraryAsset();

        // Import Spliced/Sprite PNGs into basePoses; clear outfits/heads/props + legacy clips.
        var spliced = new List<Sprite>();
        if (AssetDatabase.IsValidFolder(splicedFolder))
        {
            foreach (string g in AssetDatabase.FindAssets("t:Sprite", new[] { splicedFolder }))
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g));
                if (sp == null) continue;
                string n = sp.name ?? "";
                if (n.StartsWith("Pose_", StringComparison.OrdinalIgnoreCase)) continue;
                if (n.StartsWith("Outfit_", StringComparison.OrdinalIgnoreCase)) continue;
                if (n.StartsWith("Head_", StringComparison.OrdinalIgnoreCase)) continue;
                if (n.StartsWith("DK_", StringComparison.OrdinalIgnoreCase)) continue;
                if (n.IndexOf("Prepared_Pose", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Prepared_Outfit", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                spliced.Add(sp);
            }
            spliced.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        void WipeLib(ModularSpriteLibrary lib, string path)
        {
            if (lib == null) return;
            lib.outfits = Array.Empty<Sprite>();
            lib.heads = Array.Empty<ModularHeadEntry>();
            lib.props = Array.Empty<Sprite>();
            if (lib.animClips != null)
            {
                lib.animClips.RemoveAll(e =>
                    e == null || !string.Equals(e.category, "Sprite", StringComparison.OrdinalIgnoreCase));
            }
            if (spliced.Count > 0)
                lib.basePoses = spliced.ToArray();
            else
            {
                // Empty — never seed Pose_/Outfit_ silhouette placeholders.
                lib.basePoses = new Sprite[4];
            }
            EditorUtility.SetDirty(lib);
            log.AppendLine("Wiped " + path + " spliced=" + spliced.Count);
        }

        WipeLib(targetLibrary, artPath);
        AssetDatabase.SaveAssets();

        if (AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resPath) != null)
            AssetDatabase.DeleteAsset(resPath);
        if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(targetLibrary)))
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(targetLibrary), resPath);
        var res = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(resPath);
        WipeLib(res, resPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        targetLibrary = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(artPath);
        TownsfolkVisualApplier.SetLibrary(null);
        log.AppendLine(spliced.Count == 0
            ? "No Spliced/Sprite yet — pews solid color until Save Splices."
            : "Pew library ← Assets/Art/Spliced/Sprite. New Game → outfit=NULL.");
        return log.ToString().TrimEnd();
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
