#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using System.Text;
using System.Reflection;
using ArthurianAutobattler;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
#pragma warning disable CS0618 // TextureImporter.spritesheet — splice export still uses SpriteMetaData API

/// <summary>
/// Master Spec Phase 3 — Art &amp; Stage Architect.
/// Menu: Arthurian → Art &amp; Stage Architect
/// Tabs: Sheet Splicer | Stage Placer | Scene Backgrounds | Class Sprites / Unit Pools | Legend / Pew Portraits | Battle Good/Evil
/// Single sprite category only (no paperdoll / multi-layer body·outfit·head).
/// </summary>
public class ArthurianArtArchitectWindow : EditorWindow
{
    /// <summary>Visible in window title/header — must match DemonKnightPackageVersion.Id tip.</summary>
    public const string UiBuildId = "fix-pew-body-pool-empty";

    const string DefaultLibraryPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
    const string DefaultSliceFolder = "Assets/Art/Modular/ArchitectSliced";
    const string SplicedRootFolder = "Assets/Art/Spliced";
    const string SpriteCategory = "Sprite";
    const string DefaultExportFolder = SplicedRootFolder + "/" + SpriteCategory;
    const string StageProfilePath = "Assets/Resources/StageLayoutProfile.asset";
    const string SceneBgProfilePath = "Assets/Resources/SceneBackgroundProfile.asset";
    const string ClassPoolProfilePath = "Assets/Resources/ClassSpritePoolProfile.asset";
    const string LegendPewProfilePath = "Assets/Resources/LegendPewPortraitProfile.asset";
    const string BattleGoodEvilProfilePath = "Assets/Resources/BattleGoodEvilSpritePoolProfile.asset";
    /// <summary>Same art as SomersetChurchUI / SomersetPewLayout (play stage).</summary>
    const string ChurchBackgroundPath = "Assets/Art/Backgrounds/SomersetChurchInterior.jpg";
    /// <summary>Reference pixel size of SomersetChurchInterior (play layout tuned to this).</summary>
    const float ChurchRefWidth = 1408f;
    const float ChurchRefHeight = 768f;
    /// <summary>Must match SomersetChurchUI.baseSeatSize tip (play pew sprite size).</summary>
    static readonly Vector2 PlayPewBaseSize = new Vector2(88f, 112f);

    enum ArchitectTab { Splicer, StagePlacer, SceneBackgrounds, ClassSprites, LegendPewPortraits, BattleGoodEvil }
    enum StageViewMode { SomersetChurch, ChurchyardBattle }
    enum BattleSlotSide { Player, Enemy }
    enum CropToolMode { ManualBox, GridCrop, AutoIsland }
    enum BoxPivotMode { BottomCenter, Center, Custom }
    enum ClassPoolKind { Peasant, Warrior, Knight }
    enum PewFacePoolKind { Peasant, Warrior, Knight }
    enum BattleGoodPoolKind { Peasant, Warrior, Knight, Priest }
    enum BattleEvilPoolKind { DemonImp, Hellhound, DreadLord }
    enum BattleSideSection { GoodTroops, GoodLegends, EvilWave, EvilBosses }

    ArchitectTab tab;
    Vector2 scroll;
    SceneBackgroundProfile sceneBgProfile;
    Vector2 sceneBgScroll;
    int selectedSceneBgSlot;
    ClassSpritePoolProfile classPoolProfile;
    Vector2 classPoolScroll;
    ClassPoolKind selectedClassPool = ClassPoolKind.Peasant;
    Sprite classPoolAddSprite;
    LegendPewPortraitProfile legendPewProfile;
    Vector2 legendPewScroll;
    Vector2 legendSlotScroll;
    PewFacePoolKind selectedPewFacePool = PewFacePoolKind.Peasant;
    Sprite pewFaceAddSprite;
    int legendPewSection; // 0 = Legends, 1 = Townsfolk pew faces
    BattleGoodEvilSpritePoolProfile battleGoodEvilProfile;
    Vector2 battleGoodEvilScroll;
    Vector2 battleLegendSlotScroll;
    Vector2 battleBossSlotScroll;
    BattleSideSection battleSideSection = BattleSideSection.GoodTroops;
    BattleGoodPoolKind selectedBattleGoodPool = BattleGoodPoolKind.Peasant;
    BattleEvilPoolKind selectedBattleEvilPool = BattleEvilPoolKind.DemonImp;
    Sprite battlePoolAddSprite;

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
    float colorTolerance = 0.34f; // stronger default — cut magenta sheet bleed (0.01–0.65)
    bool defringeMagentaEdges = true;
    int defringePasses = 2;

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
        Debug.Log($"[DemonKnight] Opened Art & Stage Architect ({UiBuildId}) — Category: Sprite, 6 tabs.");
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
        ReloadProfilesFromDisk("OnEnable");
        SyncDefaultExportFolder();
        EditorApplication.playModeStateChanged -= OnArchitectPlayModeChanged;
        EditorApplication.playModeStateChanged += OnArchitectPlayModeChanged;
    }

    /// <summary>
    /// Always re-Ensure from disk so Tab 2/4/5 show current saved layout + pools
    /// (not a stale CreateInstance stand-in). Rematerialize Missing sprites from path backups.
    /// </summary>
    void ReloadProfilesFromDisk(string reason)
    {
        stageProfile = ArthurianStageLayoutProfileEnsure.EnsureAsset();
        stageProfile?.EnsureSlotCounts();
        if (stageProfile != null && stageProfile.churchPewSlots != null)
            EnsureChurchSlotsSeededFromPlayLayout(stageProfile.churchPewSlots);

        sceneBgProfile = ArthurianStageLayoutProfileEnsure.EnsureSceneBackgroundAsset();
        sceneBgProfile?.EnsureDefaultEntries();

        classPoolProfile = ArthurianStageLayoutProfileEnsure.EnsureClassSpritePoolAsset();
        classPoolProfile?.EnsureLists();
        int poolRestored = classPoolProfile != null ? classPoolProfile.RematerializeSpritesFromPaths() : 0;

        legendPewProfile = ArthurianStageLayoutProfileEnsure.EnsureLegendPewPortraitAsset();
        legendPewProfile?.EnsureSlotsAndLists();
        int faceRestored = legendPewProfile != null ? legendPewProfile.RematerializeSpritesFromPaths() : 0;

        battleGoodEvilProfile = ArthurianStageLayoutProfileEnsure.EnsureBattleGoodEvilSpritePoolAsset();
        battleGoodEvilProfile?.EnsureSlotsAndLists();
        int battleRestored = battleGoodEvilProfile != null ? battleGoodEvilProfile.RematerializeSpritesFromPaths() : 0;

        try { ClassSpritePoolApplier.InvalidateCache(); } catch { /* ignore */ }
        try { LegendPewPortraitApplier.InvalidateCache(); } catch { /* ignore */ }
        try { BattleGoodEvilSpritePoolApplier.InvalidateCache(); } catch { /* ignore */ }
        try { SomersetChurchUI.InvalidateChurchAssignCache(); } catch { /* ignore */ }

        if (poolRestored > 0 || faceRestored > 0 || battleRestored > 0)
            Debug.Log(
                $"[DemonKnight] Architect reload ({reason}): rematerialized pools={poolRestored} faces={faceRestored} battle={battleRestored}.");
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnArchitectPlayModeChanged;
        PersistAllProfiles("OnDisable");
    }

    void OnDestroy()
    {
        // Belt: some EditorWindow close paths hit OnDestroy without a reliable OnDisable Persist.
        PersistAllProfiles("OnDestroy");
    }

    /// <summary>
    /// Flush every Architect profile to disk + SoftPromote into Resources/ArchitectArt.
    /// tip: fix-pew-body-pool-empty — assigns must survive window close / domain reload.
    /// </summary>
    void PersistAllProfiles(string reason)
    {
        PersistStageProfile(reason);
        PersistSceneBgProfile(reason);
        PersistClassPoolProfile(reason);
        PersistLegendPewProfile(reason);
        PersistBattleGoodEvilProfile(reason);
    }

    void OnArchitectPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode
            || state == PlayModeStateChange.EnteredEditMode)
        {
            PersistAllProfiles("PlayMode:" + state);
            if (state == PlayModeStateChange.EnteredEditMode)
                ReloadProfilesFromDisk("PlayModeEnteredEdit");
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
            case ArchitectTab.LegendPewPortraits: DrawLegendPewPortraitsTab(); break;
            case ArchitectTab.BattleGoodEvil: DrawBattleGoodEvilTab(); break;
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
        if (GUILayout.Toggle(tab == ArchitectTab.LegendPewPortraits, "5 · Legend / Pew Portraits", EditorStyles.toolbarButton))
            tab = ArchitectTab.LegendPewPortraits;
        if (GUILayout.Toggle(tab == ArchitectTab.BattleGoodEvil, "6 · Battle Good / Evil", EditorStyles.toolbarButton))
            tab = ArchitectTab.BattleGoodEvil;
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
        SoftPromoteStageLayoutIntoResources(stageProfile);
        EditorUtility.SetDirty(stageProfile);
        AssetDatabase.SaveAssets();
        try { SomersetChurchUI.InvalidateChurchAssignCache(); } catch { /* ignore */ }
    }

    void PersistSceneBgProfile(string reason)
    {
        if (sceneBgProfile == null) return;
        string path = AssetDatabase.GetAssetPath(sceneBgProfile);
        if (string.IsNullOrEmpty(path))
        {
            // Non-asset CreateInstance stand-in — copy entries into disk asset (Stage Persist lesson).
            var disk = ArthurianStageLayoutProfileEnsure.EnsureSceneBackgroundAsset();
            if (disk != null && !ReferenceEquals(disk, sceneBgProfile))
            {
                CopySceneBgEntries(sceneBgProfile, disk);
                sceneBgProfile = disk;
                path = AssetDatabase.GetAssetPath(sceneBgProfile);
                Debug.LogWarning(
                    "[DemonKnight] Architect was editing a non-asset SceneBackgroundProfile — copied into "
                    + path + " (" + reason + ").");
            }
        }
        if (sceneBgProfile == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sceneBgProfile)))
            return;
        sceneBgProfile.RematerializeSpritesFromPaths();
        SoftPromoteSceneBackgroundIntoResources(sceneBgProfile);
        EditorUtility.SetDirty(sceneBgProfile);
        AssetDatabase.SaveAssets();
        try { SceneBackgroundApplier.InvalidateCache(); } catch { /* ignore */ }
    }

    static void CopySceneBgEntries(SceneBackgroundProfile from, SceneBackgroundProfile to)
    {
        if (from == null || to == null) return;
        from.EnsureDefaultEntries();
        to.EnsureDefaultEntries();
        if (from.entries == null) return;
        to.entries ??= new List<SceneBackgroundEntry>();
        for (int i = 0; i < from.entries.Count; i++)
        {
            var src = from.entries[i];
            if (src == null) continue;
            SceneBackgroundEntry dest = null;
            for (int j = 0; j < to.entries.Count; j++)
            {
                if (to.entries[j] != null && to.entries[j].slot == src.slot)
                {
                    dest = to.entries[j];
                    break;
                }
            }
            if (dest == null)
            {
                dest = new SceneBackgroundEntry { slot = src.slot, displayName = src.displayName };
                to.entries.Add(dest);
            }
            dest.displayName = src.displayName;
            dest.imageSprite = src.imageSprite;
            dest.videoClip = src.videoClip;
            dest.imageAssetPath = src.imageAssetPath;
            dest.videoAssetPath = src.videoAssetPath;
        }
    }

    void PersistClassPoolProfile(string reason)
    {
        if (classPoolProfile == null) return;
        string path = AssetDatabase.GetAssetPath(classPoolProfile);
        if (string.IsNullOrEmpty(path))
        {
            // Non-asset CreateInstance stand-in — copy lists into disk asset (Stage Persist lesson).
            var disk = ArthurianStageLayoutProfileEnsure.EnsureClassSpritePoolAsset();
            if (disk != null && !ReferenceEquals(disk, classPoolProfile))
            {
                CopyClassPoolLists(classPoolProfile, disk);
                classPoolProfile = disk;
                path = AssetDatabase.GetAssetPath(classPoolProfile);
                Debug.LogWarning(
                    "[DemonKnight] Architect was editing a non-asset ClassSpritePoolProfile — copied into "
                    + path + " (" + reason + ").");
            }
        }
        if (classPoolProfile == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(classPoolProfile)))
            return;
        classPoolProfile.RematerializeSpritesFromPaths();
        SyncClassPoolAssetPaths(classPoolProfile);
        SoftPromoteClassPoolIntoResources(classPoolProfile);
        SyncClassPoolAssetPaths(classPoolProfile);
        EditorUtility.SetDirty(classPoolProfile);
        AssetDatabase.SaveAssets();
        try { ClassSpritePoolApplier.InvalidateCache(); } catch { /* ignore */ }
    }

    void PersistLegendPewProfile(string reason)
    {
        if (legendPewProfile == null) return;
        string path = AssetDatabase.GetAssetPath(legendPewProfile);
        if (string.IsNullOrEmpty(path))
        {
            var disk = ArthurianStageLayoutProfileEnsure.EnsureLegendPewPortraitAsset();
            if (disk != null && !ReferenceEquals(disk, legendPewProfile))
            {
                CopyLegendPewLists(legendPewProfile, disk);
                legendPewProfile = disk;
                path = AssetDatabase.GetAssetPath(legendPewProfile);
                Debug.LogWarning(
                    "[DemonKnight] Architect was editing a non-asset LegendPewPortraitProfile — copied into "
                    + path + " (" + reason + ").");
            }
        }
        if (legendPewProfile == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(legendPewProfile)))
            return;
        legendPewProfile.RematerializeSpritesFromPaths();
        SyncLegendPewAssetPaths(legendPewProfile);
        SoftPromoteLegendPewIntoResources(legendPewProfile);
        SyncLegendPewAssetPaths(legendPewProfile);
        EditorUtility.SetDirty(legendPewProfile);
        AssetDatabase.SaveAssets();
        try { LegendPewPortraitApplier.InvalidateCache(); } catch { /* ignore */ }
        try { CharacterDatabase.InvalidateCache(); } catch { /* ignore */ }
    }

    void PersistBattleGoodEvilProfile(string reason)
    {
        if (battleGoodEvilProfile == null) return;
        string path = AssetDatabase.GetAssetPath(battleGoodEvilProfile);
        if (string.IsNullOrEmpty(path))
        {
            var disk = ArthurianStageLayoutProfileEnsure.EnsureBattleGoodEvilSpritePoolAsset();
            if (disk != null && !ReferenceEquals(disk, battleGoodEvilProfile))
            {
                CopyBattleGoodEvilLists(battleGoodEvilProfile, disk);
                battleGoodEvilProfile = disk;
                path = AssetDatabase.GetAssetPath(battleGoodEvilProfile);
                Debug.LogWarning(
                    "[DemonKnight] Architect was editing a non-asset BattleGoodEvilSpritePoolProfile — copied into "
                    + path + " (" + reason + ").");
            }
        }
        if (battleGoodEvilProfile == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(battleGoodEvilProfile)))
            return;
        battleGoodEvilProfile.RematerializeSpritesFromPaths();
        SyncBattleGoodEvilAssetPaths(battleGoodEvilProfile);
        SoftPromoteBattleGoodEvilIntoResources(battleGoodEvilProfile);
        SyncBattleGoodEvilAssetPaths(battleGoodEvilProfile);
        EditorUtility.SetDirty(battleGoodEvilProfile);
        AssetDatabase.SaveAssets();
        try { BattleGoodEvilSpritePoolApplier.InvalidateCache(); } catch { /* ignore */ }
    }

    static void CopyBattleGoodEvilLists(BattleGoodEvilSpritePoolProfile from, BattleGoodEvilSpritePoolProfile to)
    {
        if (from == null || to == null) return;
        from.EnsureSlotsAndLists();
        to.EnsureSlotsAndLists();
        to.peasantBattleSprites = new List<Sprite>(from.peasantBattleSprites);
        to.warriorBattleSprites = new List<Sprite>(from.warriorBattleSprites);
        to.knightBattleSprites = new List<Sprite>(from.knightBattleSprites);
        to.priestBattleSprites = new List<Sprite>(from.priestBattleSprites);
        to.peasantBattleAssetPaths = new List<string>(from.peasantBattleAssetPaths);
        to.warriorBattleAssetPaths = new List<string>(from.warriorBattleAssetPaths);
        to.knightBattleAssetPaths = new List<string>(from.knightBattleAssetPaths);
        to.priestBattleAssetPaths = new List<string>(from.priestBattleAssetPaths);
        to.demonImpBattleSprites = new List<Sprite>(from.demonImpBattleSprites);
        to.hellhoundBattleSprites = new List<Sprite>(from.hellhoundBattleSprites);
        to.dreadLordBattleSprites = new List<Sprite>(from.dreadLordBattleSprites);
        to.demonImpBattleAssetPaths = new List<string>(from.demonImpBattleAssetPaths);
        to.hellhoundBattleAssetPaths = new List<string>(from.hellhoundBattleAssetPaths);
        to.dreadLordBattleAssetPaths = new List<string>(from.dreadLordBattleAssetPaths);
        CopyBattleSlots(from.goodLegendSlots, to.goodLegendSlots);
        CopyBattleSlots(from.evilBossSlots, to.evilBossSlots);
    }

    static void CopyBattleSlots(List<BattleSpriteSlotEntry> from, List<BattleSpriteSlotEntry> to)
    {
        if (from == null || to == null) return;
        for (int i = 0; i < from.Count; i++)
        {
            var src = from[i];
            if (src == null) continue;
            BattleSpriteSlotEntry dest = null;
            for (int j = 0; j < to.Count; j++)
            {
                if (to[j] != null && to[j].unitType == src.unitType) { dest = to[j]; break; }
            }
            if (dest == null)
            {
                dest = new BattleSpriteSlotEntry
                {
                    unitType = src.unitType,
                    displayName = src.displayName,
                    softOptional = src.softOptional
                };
                to.Add(dest);
            }
            dest.battleSprite = src.battleSprite;
            dest.assetPath = src.assetPath;
            dest.displayName = src.displayName;
            dest.softOptional = src.softOptional;
        }
    }

    static void SyncBattleGoodEvilAssetPaths(BattleGoodEvilSpritePoolProfile p)
    {
        if (p == null) return;
        p.EnsureSlotsAndLists();
        SyncPathList(p.peasantBattleSprites, p.peasantBattleAssetPaths);
        SyncPathList(p.warriorBattleSprites, p.warriorBattleAssetPaths);
        SyncPathList(p.knightBattleSprites, p.knightBattleAssetPaths);
        SyncPathList(p.priestBattleSprites, p.priestBattleAssetPaths);
        SyncPathList(p.demonImpBattleSprites, p.demonImpBattleAssetPaths);
        SyncPathList(p.hellhoundBattleSprites, p.hellhoundBattleAssetPaths);
        SyncPathList(p.dreadLordBattleSprites, p.dreadLordBattleAssetPaths);
        SyncBattleSlotPaths(p.goodLegendSlots);
        SyncBattleSlotPaths(p.evilBossSlots);
    }

    static void SyncBattleSlotPaths(List<BattleSpriteSlotEntry> slots)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            var e = slots[i];
            if (e == null || e.battleSprite == null) continue;
            string ap = AssetDatabase.GetAssetPath(e.battleSprite);
            if (!string.IsNullOrEmpty(ap)) e.assetPath = ap;
        }
    }

    static void CopyClassPoolLists(ClassSpritePoolProfile from, ClassSpritePoolProfile to)
    {
        if (from == null || to == null) return;
        from.EnsureLists();
        to.EnsureLists();
        to.peasantSprites = new List<Sprite>(from.peasantSprites);
        to.warriorSprites = new List<Sprite>(from.warriorSprites);
        to.knightSprites = new List<Sprite>(from.knightSprites);
        to.peasantAssetPaths = new List<string>(from.peasantAssetPaths);
        to.warriorAssetPaths = new List<string>(from.warriorAssetPaths);
        to.knightAssetPaths = new List<string>(from.knightAssetPaths);
    }

    static void CopyLegendPewLists(LegendPewPortraitProfile from, LegendPewPortraitProfile to)
    {
        if (from == null || to == null) return;
        from.EnsureSlotsAndLists();
        to.EnsureSlotsAndLists();
        to.peasantFaceProfiles = new List<Sprite>(from.peasantFaceProfiles);
        to.warriorFaceProfiles = new List<Sprite>(from.warriorFaceProfiles);
        to.knightFaceProfiles = new List<Sprite>(from.knightFaceProfiles);
        to.peasantFaceAssetPaths = new List<string>(from.peasantFaceAssetPaths);
        to.warriorFaceAssetPaths = new List<string>(from.warriorFaceAssetPaths);
        to.knightFaceAssetPaths = new List<string>(from.knightFaceAssetPaths);
        if (from.legendPortraits == null) return;
        to.legendPortraits ??= new List<LegendPortraitEntry>();
        for (int i = 0; i < from.legendPortraits.Count; i++)
        {
            var src = from.legendPortraits[i];
            if (src == null) continue;
            var dest = to.FindLegendEntry(src.unitType);
            if (dest == null)
            {
                dest = new LegendPortraitEntry
                {
                    unitType = src.unitType,
                    displayName = src.displayName,
                    softOptional = src.softOptional
                };
                to.legendPortraits.Add(dest);
            }
            dest.facePortrait = src.facePortrait;
            dest.assetPath = src.assetPath;
            dest.displayName = src.displayName;
            dest.softOptional = src.softOptional;
        }
    }

    static void SyncClassPoolAssetPaths(ClassSpritePoolProfile p)
    {
        if (p == null) return;
        p.EnsureLists();
        SyncPathList(p.peasantSprites, p.peasantAssetPaths);
        SyncPathList(p.warriorSprites, p.warriorAssetPaths);
        SyncPathList(p.knightSprites, p.knightAssetPaths);
    }

    static void SyncLegendPewAssetPaths(LegendPewPortraitProfile p)
    {
        if (p == null) return;
        p.EnsureSlotsAndLists();
        SyncPathList(p.peasantFaceProfiles, p.peasantFaceAssetPaths);
        SyncPathList(p.warriorFaceProfiles, p.warriorFaceAssetPaths);
        SyncPathList(p.knightFaceProfiles, p.knightFaceAssetPaths);
        if (p.legendPortraits == null) return;
        for (int i = 0; i < p.legendPortraits.Count; i++)
        {
            var e = p.legendPortraits[i];
            if (e == null) continue;
            // Keep assetPath backup when face is temporarily Missing — never wipe.
            if (e.facePortrait == null) continue;
            string ap = AssetDatabase.GetAssetPath(e.facePortrait);
            if (!string.IsNullOrEmpty(ap)) e.assetPath = ap;
        }
    }

    static void SyncPathList(List<Sprite> sprites, List<string> paths)
    {
        if (sprites == null || paths == null) return;
        while (paths.Count < sprites.Count) paths.Add("");
        // Keep path backups for trailing Missing slots — do not Truncate paths below sprites.
        // Only trim paths when sprites were intentionally removed (shorter list).
        if (paths.Count > sprites.Count)
            paths.RemoveRange(sprites.Count, paths.Count - sprites.Count);
        for (int i = 0; i < sprites.Count; i++)
        {
            // CRITICAL: never wipe path backups when sprite is temporarily Missing
            // (reimport / Force Sync). RematerializeSpritesFromPaths restores from paths.
            if (sprites[i] == null) continue;
            string ap = AssetDatabase.GetAssetPath(sprites[i]);
            if (!string.IsNullOrEmpty(ap)) paths[i] = ap;
        }
    }

    static int CountNonEmptyPaths(List<string> paths)
    {
        if (paths == null) return 0;
        int n = 0;
        for (int i = 0; i < paths.Count; i++)
            if (!string.IsNullOrEmpty(paths[i])) n++;
        return n;
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
        EditorGUILayout.LabelField("Save Splices — Chroma Key", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        saveChromaKey = EditorGUILayout.ColorField("Chroma Key", saveChromaKey);
        if (GUILayout.Button("#FF00FF", GUILayout.Width(70)))
            saveChromaKey = MagentaChromaKey;
        EditorGUILayout.EndHorizontal();
        autoDetectCornerColor = EditorGUILayout.ToggleLeft(
            "Auto-Detect Corner Color (overrides key from sheet/frame corners)", autoDetectCornerColor);
        colorTolerance = EditorGUILayout.Slider("Color Tolerance", colorTolerance, 0.01f, 0.65f);
        defringeMagentaEdges = EditorGUILayout.ToggleLeft(
            "Defringe Magenta Edges (after key → Color.clear)", defringeMagentaEdges);
        if (defringeMagentaEdges)
            defringePasses = EditorGUILayout.IntSlider("Defringe Passes", defringePasses, 1, 4);

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
            + (defringeMagentaEdges ? $" + magenta defringe×{defringePasses}" : "") + "\n"
            + "Importer: Sprite / alphaIsTransparency / FromInput / Point / Uncompressed / Bottom-Center\n"
            + $"→ {folder}\nRegistered into ModularSpriteLibrary.",
            "OK");
    }

    /// <summary>
    /// Re-key every PNG under Target Export Folder (default Assets/Art/Spliced/Sprite)
    /// with current chroma/defringe settings and reimport alphaIsTransparency.
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
        ClearPaperdollLibraryLocal();
        EditorUtility.DisplayDialog(
            "Re-bake Existing Splices",
            $"Re-keyed {ok} PNG(s), miss={miss}\n"
            + $"Chroma tol={colorTolerance:0.00}"
            + (defringeMagentaEdges ? $" + defringe×{defringePasses}" : "") + "\n"
            + "alphaIsTransparency + FromInput baked.\n→ " + folder,
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
        ApplyChromaKeyClearAndDefringe(
            readable, key, colorTolerance, defringeMagentaEdges, defringePasses);

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
    /// Pixel pass: near-key → Color.clear; optional magenta edge defringe; mutates tex in place.
    /// Stronger defaults: higher tol + multi-pass defringe so sheet BG does not show on pews.
    /// </summary>
    static void ApplyChromaKeyClearAndDefringe(
        Texture2D tex, Color key, float tolerance, bool defringeMagenta, int passes = 2)
    {
        if (tex == null) return;
        int w = tex.width, h = tex.height;
        Color[] pixels = tex.GetPixels();
        float tol = Mathf.Clamp(tolerance, 0.01f, 0.65f);
        float softTol = tol * 1.35f;

        // Pass 1: chroma key → Color.clear (also always clear near #FF00FF + magenta cast).
        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];
            if (p.a < 0.008f) { pixels[i] = Color.clear; continue; }
            float dKey = ColorRgbDistance(p, key);
            float dMag = ColorRgbDistance(p, MagentaChromaKey);
            float magCast = (p.r + p.b) * 0.5f - p.g;
            bool nearKey = dKey <= tol || dMag <= tol;
            // Soft punch: magenta-cast pixels near key range → clear (kills sheet bleed).
            bool softMagenta = magCast > 0.18f && p.r > 0.35f && p.b > 0.35f && p.g < 0.55f
                               && (dKey <= softTol || dMag <= softTol);
            if (nearKey || softMagenta)
                pixels[i] = Color.clear;
        }

        // Pass 2+: defringe magenta edges (semi-magenta pixels next to clear) — multi-pass.
        if (defringeMagenta)
        {
            int n = Mathf.Clamp(passes, 1, 4);
            for (int pass = 0; pass < n; pass++)
            {
                var outPx = (Color[])pixels.Clone();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        Color p = pixels[i];
                        if (p.a < 0.008f) continue;
                        if (!IsMagentaFringe(p)) continue;
                        if (!HasClearNeighbor(pixels, w, h, x, y)) continue;
                        outPx[i] = DefringeMagentaPixel(p);
                    }
                }
                pixels = outPx;
            }
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
            if (pixels[ny * w + nx].a < 0.04f) return true;
        }
        return false;
    }

    static bool IsMagentaFringe(Color p)
    {
        // More aggressive: catch faint magenta halo left by soft anti-alias against #FF00FF.
        float mag = (p.r + p.b) * 0.5f - p.g;
        return mag > 0.06f && p.r > 0.18f && p.b > 0.18f;
    }

    static Color DefringeMagentaPixel(Color p)
    {
        float excess = Mathf.Max(0f, Mathf.Min(p.r, p.b) - p.g);
        // Pull toward green channel more aggressively.
        p.r = Mathf.Clamp01(p.r - excess * 1.35f);
        p.b = Mathf.Clamp01(p.b - excess * 1.35f);
        float luma = p.r * 0.3f + p.g * 0.59f + p.b * 0.11f;
        float magLeft = (p.r + p.b) * 0.5f - p.g;
        if (luma < 0.06f || magLeft > 0.22f)
            return Color.clear;
        p.a *= Mathf.Clamp01(1f - excess * 2.2f);
        if (p.a < 0.08f) return Color.clear;
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
        EditorGUILayout.HelpBox(
            "StageLayoutProfile pew slots are POSITION/SCALE placement guides (and optional "
            + "empty-seat preview sprites). Gameplay church visuals pick from Tab 4 Class "
            + "Sprite pools by Peasant/Warrior/Knight. Ensure remaps script GUID / transplants "
            + "YAML — never wipes seat guides. Empty seats stay empty (no auto-fill).",
            MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        stageProfile = (StageLayoutProfile)EditorGUILayout.ObjectField(
            "StageLayoutProfile", stageProfile, typeof(StageLayoutProfile), false);
        if (GUILayout.Button("Reload from disk", GUILayout.Width(120)))
            ReloadProfilesFromDisk("ReloadButton");
        EditorGUILayout.EndHorizontal();
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
        if (stageProfile.churchPewSlots != null)
            EnsureChurchSlotsSeededFromPlayLayout(stageProfile.churchPewSlots);
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
            "BODY sprites only (full pew avatar). Peasant / Warrior / Knight pools. "
            + "Church pews pick from the matching class pool (deterministic by unitID). "
            + "Do NOT put face portraits here (Tab 5) or Autobattle-only art (Tab 6). "
            + "Cross-assign soft-fails (sprite skipped, no crash). "
            + "Empty pool → StageLayout assign-guide / solid fallback. "
            + "Persists on " + ClassPoolProfilePath + " (must ship in player builds under Resources).",
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
            if (TryAddBodyPoolSprite(pool, classPoolAddSprite))
            {
                classPoolAddSprite = null;
                PersistClassPoolProfile("AddClassPoolSprite");
            }
        }
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("Add selected palette", GUILayout.Width(150))
            && paletteSelectedSprite != null)
        {
            if (TryAddBodyPoolSprite(pool, paletteSelectedSprite))
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
            int pathBackups = CountNonEmptyPaths(
                selectedClassPool == UnitType.Peasant ? classPoolProfile?.peasantAssetPaths
                : selectedClassPool == UnitType.Warrior ? classPoolProfile?.warriorAssetPaths
                : classPoolProfile?.knightAssetPaths);

            if (pathBackups > 0)
            {
                EditorGUILayout.HelpBox(
                    "Sprite refs empty but " + pathBackups + " path backup(s) remain — click "
                    + "'Reload from disk' or Save to rematerialize. If list stays blank after reload, "
                    + "run Arthurian → Ensure Stage Layout Profile (remaps MonoScript GUID/fileID).",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Tab 4 BODY pool empty for " + selectedClassPool + ".\n"
                    + "• Add full-body sprites here (Peasant / Warrior / Knight) then Save.\n"
                    + "• Empty seats fall back to StageLayout assign-guides / solid color.\n"
                    + "• Optional: if Tab 6 Good battle pools have troops, Play may use those as "
                    + "pew BODY fallback ONLY while Tab 4 stays empty (prefer filling Tab 4).\n"
                    + "• Assigns stick after close — tip fix-pew-body-pool-empty Persists on OnDisable/OnDestroy.",
                    MessageType.Warning);
            }
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
                if (TryAddBodyPoolSprite(pool, sp))
                    PersistClassPoolProfile("PaletteClickClassPool");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    bool TryAddBodyPoolSprite(List<Sprite> pool, Sprite sp)
    {
        if (pool == null || sp == null) return false;
        if (LegendPewPortraitApplier.IsFacePoolSprite(sp))
        {
            Debug.LogWarning(
                "[DemonKnight] Soft-fail: refusing face-pool sprite '" + sp.name
                + "' as Tab 4 BODY. Use Tab 5 · Legend / Pew Portraits for faces.");
            EditorUtility.DisplayDialog(
                "Body vs Face",
                "That sprite is already in a Tab 5 FACE pool.\n"
                + "Tab 4 is BODY (full pew/combat avatar) only.\n"
                + "Assign faces on Tab 5.",
                "OK");
            return false;
        }
        Undo.RecordObject(classPoolProfile, "Add Class Pool Sprite");
        pool.Add(sp);
        return true;
    }

    bool TryAddFacePoolSprite(List<Sprite> pool, Sprite sp)
    {
        if (pool == null || sp == null) return false;
        if (LegendPewPortraitApplier.IsBodyPoolSprite(sp))
        {
            Debug.LogWarning(
                "[DemonKnight] Soft-fail: refusing body-pool sprite '" + sp.name
                + "' as Tab 5 FACE. Use Tab 4 · Class Sprites for full-body avatars.");
            EditorUtility.DisplayDialog(
                "Body vs Face",
                "That sprite is already in a Tab 4 BODY pool.\n"
                + "Tab 5 is FACE only (Legends FacePortrait + optional pew face overlay).\n"
                + "Assign full-body avatars on Tab 4.",
                "OK");
            return false;
        }
        Undo.RecordObject(legendPewProfile, "Add Pew Face Profile");
        pool.Add(sp);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 5 — Legend face portraits + townsfolk pew FACE profiles
    // ═══════════════════════════════════════════════════════════════════════

    void DrawLegendPewPortraitsTab()
    {
        EditorGUILayout.LabelField("Legend / Pew Portraits", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "FACE only. Assign Legends FacePortrait (14; Merlin soft) + townsfolk pew FACE overlays "
            + "for Peasant / Warrior / Knight. Distinct from Tab 4 BODY (full pew/combat avatar). "
            + "Never put full-body class sprites here — cross-assign soft-fails. "
            + "Persists on " + LegendPewProfilePath + " (must ship in player builds under Resources).",
            MessageType.Info);

        legendPewProfile = (LegendPewPortraitProfile)EditorGUILayout.ObjectField(
            "LegendPewPortraitProfile", legendPewProfile, typeof(LegendPewPortraitProfile), false);
        if (legendPewProfile == null
            || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(legendPewProfile)))
        {
            if (GUILayout.Button("Ensure Legend Pew Portrait Profile Asset"))
                legendPewProfile = ArthurianStageLayoutProfileEnsure.EnsureLegendPewPortraitAsset();
            if (legendPewProfile == null) return;
        }
        legendPewProfile.EnsureSlotsAndLists();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save LegendPewPortraitProfile", GUILayout.Height(26)))
            PersistLegendPewProfile("SaveButton");
        if (GUILayout.Button("Ping Asset", GUILayout.Width(90)))
        {
            Selection.activeObject = legendPewProfile;
            EditorGUIUtility.PingObject(legendPewProfile);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            "Asset: " + AssetDatabase.GetAssetPath(legendPewProfile), EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"Assigned legends {legendPewProfile.CountAssignedLegendFaces()}/14 · "
            + $"Pew faces P={legendPewProfile.CountUsablePewFaces(UnitType.Peasant)} "
            + $"W={legendPewProfile.CountUsablePewFaces(UnitType.Warrior)} "
            + $"K={legendPewProfile.CountUsablePewFaces(UnitType.Knight)}",
            EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(legendPewSection == 0, "Legends (14 + Merlin soft)", EditorStyles.toolbarButton))
            legendPewSection = 0;
        if (GUILayout.Toggle(legendPewSection == 1, "Townsfolk pew faces", EditorStyles.toolbarButton))
            legendPewSection = 1;
        EditorGUILayout.EndHorizontal();

        if (legendPewSection == 0)
            DrawLegendPortraitSlots();
        else
            DrawTownsfolkPewFacePools();
    }

    void DrawLegendPortraitSlots()
    {
        EditorGUILayout.HelpBox(
            "One face sprite per playable Legend. Empty → soft grey FacePortrait placeholder "
            + "(CharacterDatabase / battleSprite fallback still tried). Merlin is soft-optional.",
            MessageType.None);

        legendSlotScroll = EditorGUILayout.BeginScrollView(legendSlotScroll, GUILayout.MinHeight(360f));
        var slots = legendPewProfile.legendPortraits;
        for (int i = 0; i < slots.Count; i++)
        {
            var entry = slots[i];
            if (entry == null) continue;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            string label = string.IsNullOrEmpty(entry.displayName)
                ? entry.unitType.ToString()
                : entry.displayName;
            if (entry.softOptional) label += "  [soft]";
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(220f));
            EditorGUI.BeginChangeCheck();
            entry.facePortrait = (Sprite)EditorGUILayout.ObjectField(
                entry.facePortrait, typeof(Sprite), false, GUILayout.Height(72f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(legendPewProfile, "Assign Legend Face Portrait");
                PersistLegendPewProfile("EditLegendFace");
            }
            if (GUILayout.Button("Clear", GUILayout.Width(52), GUILayout.Height(72f)))
            {
                Undo.RecordObject(legendPewProfile, "Clear Legend Face Portrait");
                entry.facePortrait = null;
                entry.assetPath = "";
                PersistLegendPewProfile("ClearLegendFace");
            }
            EditorGUILayout.EndHorizontal();
            if (entry.facePortrait != null)
            {
                Rect prev = GUILayoutUtility.GetRect(56f, 56f, GUILayout.ExpandWidth(false));
                DrawSprite(prev, entry.facePortrait, Color.white);
                EditorGUILayout.LabelField(entry.facePortrait.name, EditorStyles.miniLabel);
            }
            else if (paletteSelectedSprite != null && GUILayout.Button("Use selected palette sprite", GUILayout.Width(200)))
            {
                Undo.RecordObject(legendPewProfile, "Palette → Legend Face");
                entry.facePortrait = paletteSelectedSprite;
                PersistLegendPewProfile("PaletteLegendFace");
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();

        DrawLegendPewPaletteQuickAdd(forLegendSlots: true);
    }

    void DrawTownsfolkPewFacePools()
    {
        EditorGUILayout.HelpBox(
            "Townsfolk pew FACE overlays only (Head layer). Body avatars stay on Tab 4 ClassSpritePool. "
            + "Deterministic by unitID. Empty face pool → body-only pew (no crash).",
            MessageType.None);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(selectedPewFacePool == PewFacePoolKind.Peasant, "Peasant", EditorStyles.toolbarButton))
            selectedPewFacePool = PewFacePoolKind.Peasant;
        if (GUILayout.Toggle(selectedPewFacePool == PewFacePoolKind.Warrior, "Warrior", EditorStyles.toolbarButton))
            selectedPewFacePool = PewFacePoolKind.Warrior;
        if (GUILayout.Toggle(selectedPewFacePool == PewFacePoolKind.Knight, "Knight", EditorStyles.toolbarButton))
            selectedPewFacePool = PewFacePoolKind.Knight;
        EditorGUILayout.EndHorizontal();

        List<Sprite> pool = selectedPewFacePool switch
        {
            PewFacePoolKind.Warrior => legendPewProfile.warriorFaceProfiles,
            PewFacePoolKind.Knight => legendPewProfile.knightFaceProfiles,
            _ => legendPewProfile.peasantFaceProfiles
        };

        EditorGUILayout.BeginHorizontal();
        pewFaceAddSprite = (Sprite)EditorGUILayout.ObjectField(
            "Add Sprite", pewFaceAddSprite, typeof(Sprite), false);
        EditorGUI.BeginDisabledGroup(pewFaceAddSprite == null);
        if (GUILayout.Button("Add to pool", GUILayout.Width(110)))
        {
            if (TryAddFacePoolSprite(pool, pewFaceAddSprite))
            {
                pewFaceAddSprite = null;
                PersistLegendPewProfile("AddPewFace");
            }
        }
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("Add selected palette", GUILayout.Width(150))
            && paletteSelectedSprite != null)
        {
            if (TryAddFacePoolSprite(pool, paletteSelectedSprite))
                PersistLegendPewProfile("AddPalettePewFace");
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear this pool", GUILayout.Width(140)))
        {
            if (EditorUtility.DisplayDialog(
                    "Clear pew face pool",
                    "Remove all sprites from the " + selectedPewFacePool + " pew face pool?",
                    "Clear", "Cancel"))
            {
                Undo.RecordObject(legendPewProfile, "Clear Pew Face Pool");
                pool.Clear();
                PersistLegendPewProfile("ClearPewFacePool");
            }
        }

        legendPewScroll = EditorGUILayout.BeginScrollView(legendPewScroll, GUILayout.MinHeight(280f));
        if (pool.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Face pool empty — pews show Tab 4 BODY only (no face overlay).",
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
                Undo.RecordObject(legendPewProfile, "Edit Pew Face Profile");
                PersistLegendPewProfile("EditPewFace");
            }
            if (GUILayout.Button("×", GUILayout.Width(28), GUILayout.Height(64f)))
            {
                Undo.RecordObject(legendPewProfile, "Remove Pew Face Profile");
                pool.RemoveAt(i);
                PersistLegendPewProfile("RemovePewFace");
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

        DrawLegendPewPaletteQuickAdd(forLegendSlots: false);
    }

    void DrawLegendPewPaletteQuickAdd(bool forLegendSlots)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Spliced palette (quick pick)", EditorStyles.boldLabel);
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
                if (!forLegendSlots && legendPewProfile != null)
                {
                    List<Sprite> pool = selectedPewFacePool switch
                    {
                        PewFacePoolKind.Warrior => legendPewProfile.warriorFaceProfiles,
                        PewFacePoolKind.Knight => legendPewProfile.knightFaceProfiles,
                        _ => legendPewProfile.peasantFaceProfiles
                    };
                    if (TryAddFacePoolSprite(pool, sp))
                        PersistLegendPewProfile("PaletteClickPewFace");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 6 — Battle Good / Evil sprite pools (UnitVisualActor Autobattle)
    // Keep separate from Tab 4 BODY pew pools and Tab 5 FACE portraits.
    // ═══════════════════════════════════════════════════════════════════════

    void DrawBattleGoodEvilTab()
    {
        EditorGUILayout.LabelField("Battle Good / Evil Sprite Pools", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Autobattle UnitVisualActor sprites only. "
            + "GOOD: Peasant / Warrior / Knight / Priest pools + each playable Legend slot. "
            + "EVIL: DemonImp / Hellhound / DreadLord wave pools + Wave 3–10+ boss slots. "
            + "Distinct from Tab 4 BODY (pew) and Tab 5 FACE — cross-assign soft-fails. "
            + "Persists on " + BattleGoodEvilProfilePath + " (must ship in player builds under Resources).",
            MessageType.Info);

        battleGoodEvilProfile = (BattleGoodEvilSpritePoolProfile)EditorGUILayout.ObjectField(
            "BattleGoodEvilSpritePoolProfile", battleGoodEvilProfile, typeof(BattleGoodEvilSpritePoolProfile), false);
        if (battleGoodEvilProfile == null
            || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(battleGoodEvilProfile)))
        {
            if (GUILayout.Button("Ensure Battle Good/Evil Pool Profile Asset"))
                battleGoodEvilProfile = ArthurianStageLayoutProfileEnsure.EnsureBattleGoodEvilSpritePoolAsset();
            if (battleGoodEvilProfile == null) return;
        }
        battleGoodEvilProfile.EnsureSlotsAndLists();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save BattleGoodEvilSpritePoolProfile", GUILayout.Height(26)))
            PersistBattleGoodEvilProfile("SaveButton");
        if (GUILayout.Button("Ping Asset", GUILayout.Width(90)))
        {
            Selection.activeObject = battleGoodEvilProfile;
            EditorGUIUtility.PingObject(battleGoodEvilProfile);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            "Asset: " + AssetDatabase.GetAssetPath(battleGoodEvilProfile), EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"Good legends {battleGoodEvilProfile.CountAssignedLegendBattles()}/14 · "
            + $"Bosses {battleGoodEvilProfile.CountAssignedBossBattles()}/8 · "
            + $"P {battleGoodEvilProfile.CountUsablePool(UnitType.Peasant)} · "
            + $"Priest {battleGoodEvilProfile.CountUsablePool(UnitType.Priest)} · "
            + $"Imp {battleGoodEvilProfile.CountUsablePool(UnitType.DemonImp)}",
            EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(battleSideSection == BattleSideSection.GoodTroops, "Good · Troops", EditorStyles.toolbarButton))
            battleSideSection = BattleSideSection.GoodTroops;
        if (GUILayout.Toggle(battleSideSection == BattleSideSection.GoodLegends, "Good · Legends", EditorStyles.toolbarButton))
            battleSideSection = BattleSideSection.GoodLegends;
        if (GUILayout.Toggle(battleSideSection == BattleSideSection.EvilWave, "Evil · Wave", EditorStyles.toolbarButton))
            battleSideSection = BattleSideSection.EvilWave;
        if (GUILayout.Toggle(battleSideSection == BattleSideSection.EvilBosses, "Evil · Bosses", EditorStyles.toolbarButton))
            battleSideSection = BattleSideSection.EvilBosses;
        EditorGUILayout.EndHorizontal();

        switch (battleSideSection)
        {
            case BattleSideSection.GoodTroops: DrawBattleTroopPools(good: true); break;
            case BattleSideSection.GoodLegends: DrawBattleLegendSlots(); break;
            case BattleSideSection.EvilWave: DrawBattleTroopPools(good: false); break;
            case BattleSideSection.EvilBosses: DrawBattleBossSlots(); break;
        }
    }

    void DrawBattleTroopPools(bool good)
    {
        if (good)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(selectedBattleGoodPool == BattleGoodPoolKind.Peasant, "Peasant", EditorStyles.toolbarButton))
                selectedBattleGoodPool = BattleGoodPoolKind.Peasant;
            if (GUILayout.Toggle(selectedBattleGoodPool == BattleGoodPoolKind.Warrior, "Warrior", EditorStyles.toolbarButton))
                selectedBattleGoodPool = BattleGoodPoolKind.Warrior;
            if (GUILayout.Toggle(selectedBattleGoodPool == BattleGoodPoolKind.Knight, "Knight", EditorStyles.toolbarButton))
                selectedBattleGoodPool = BattleGoodPoolKind.Knight;
            if (GUILayout.Toggle(selectedBattleGoodPool == BattleGoodPoolKind.Priest, "Priest", EditorStyles.toolbarButton))
                selectedBattleGoodPool = BattleGoodPoolKind.Priest;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(selectedBattleEvilPool == BattleEvilPoolKind.DemonImp, "DemonImp", EditorStyles.toolbarButton))
                selectedBattleEvilPool = BattleEvilPoolKind.DemonImp;
            if (GUILayout.Toggle(selectedBattleEvilPool == BattleEvilPoolKind.Hellhound, "Hellhound", EditorStyles.toolbarButton))
                selectedBattleEvilPool = BattleEvilPoolKind.Hellhound;
            if (GUILayout.Toggle(selectedBattleEvilPool == BattleEvilPoolKind.DreadLord, "DreadLord", EditorStyles.toolbarButton))
                selectedBattleEvilPool = BattleEvilPoolKind.DreadLord;
            EditorGUILayout.EndHorizontal();
        }

        UnitType poolType = good
            ? selectedBattleGoodPool switch
            {
                BattleGoodPoolKind.Warrior => UnitType.Warrior,
                BattleGoodPoolKind.Knight => UnitType.Knight,
                BattleGoodPoolKind.Priest => UnitType.Priest,
                _ => UnitType.Peasant
            }
            : selectedBattleEvilPool switch
            {
                BattleEvilPoolKind.Hellhound => UnitType.DemonHellhound,
                BattleEvilPoolKind.DreadLord => UnitType.DemonDreadLord,
                _ => UnitType.DemonImp
            };
        List<Sprite> pool = battleGoodEvilProfile.GetTroopOrWavePool(poolType);

        EditorGUILayout.BeginHorizontal();
        battlePoolAddSprite = (Sprite)EditorGUILayout.ObjectField(
            "Add Sprite", battlePoolAddSprite, typeof(Sprite), false);
        EditorGUI.BeginDisabledGroup(battlePoolAddSprite == null);
        if (GUILayout.Button("Add to pool", GUILayout.Width(110)))
        {
            if (TryAddBattlePoolSprite(pool, battlePoolAddSprite))
            {
                battlePoolAddSprite = null;
                PersistBattleGoodEvilProfile("AddBattlePoolSprite");
            }
        }
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("Add selected palette", GUILayout.Width(150))
            && paletteSelectedSprite != null)
        {
            if (TryAddBattlePoolSprite(pool, paletteSelectedSprite))
                PersistBattleGoodEvilProfile("AddPaletteToBattlePool");
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear this pool", GUILayout.Width(140)))
        {
            if (EditorUtility.DisplayDialog(
                    "Clear battle pool",
                    "Remove all sprites from the " + poolType + " battle pool?",
                    "Clear", "Cancel"))
            {
                Undo.RecordObject(battleGoodEvilProfile, "Clear Battle Pool");
                pool.Clear();
                PersistBattleGoodEvilProfile("ClearBattlePool");
            }
        }

        battleGoodEvilScroll = EditorGUILayout.BeginScrollView(battleGoodEvilScroll, GUILayout.MinHeight(280f));
        if (pool == null || pool.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Pool empty — Autobattle falls back to CharacterDatabase / demon color blocks.",
                MessageType.Warning);
        }
        else
        {
            for (int i = 0; i < pool.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                pool[i] = (Sprite)EditorGUILayout.ObjectField(
                    "#" + i, pool[i], typeof(Sprite), false, GUILayout.Height(64f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(battleGoodEvilProfile, "Edit Battle Pool Sprite");
                    PersistBattleGoodEvilProfile("EditBattlePoolSprite");
                }
                if (GUILayout.Button("×", GUILayout.Width(28), GUILayout.Height(64f)))
                {
                    Undo.RecordObject(battleGoodEvilProfile, "Remove Battle Pool Sprite");
                    pool.RemoveAt(i);
                    PersistBattleGoodEvilProfile("RemoveBattlePoolSprite");
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
        }
        EditorGUILayout.EndScrollView();
        DrawBattlePaletteQuickAdd(pool);
    }

    void DrawBattleLegendSlots()
    {
        EditorGUILayout.HelpBox(
            "One battle sprite per playable Legend (Merlin soft). Used by UnitVisualActor on March.",
            MessageType.None);
        battleLegendSlotScroll = EditorGUILayout.BeginScrollView(battleLegendSlotScroll, GUILayout.MinHeight(360f));
        var slots = battleGoodEvilProfile.goodLegendSlots;
        for (int i = 0; i < slots.Count; i++)
        {
            var e = slots[i];
            if (e == null) continue;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            string label = string.IsNullOrEmpty(e.displayName) ? e.unitType.ToString() : e.displayName;
            if (e.softOptional) label += " (soft)";
            EditorGUI.BeginChangeCheck();
            e.battleSprite = (Sprite)EditorGUILayout.ObjectField(
                label, e.battleSprite, typeof(Sprite), false, GUILayout.Height(56f));
            if (EditorGUI.EndChangeCheck())
            {
                if (e.battleSprite != null && LegendPewPortraitApplier.IsFacePoolSprite(e.battleSprite))
                {
                    EditorUtility.DisplayDialog(
                        "Battle vs Face",
                        "That sprite is in a Tab 5 FACE pool.\nTab 6 is Autobattle sprites only.",
                        "OK");
                    e.battleSprite = null;
                }
                else
                {
                    Undo.RecordObject(battleGoodEvilProfile, "Assign Legend Battle Sprite");
                    PersistBattleGoodEvilProfile("EditLegendBattle");
                }
            }
            if (GUILayout.Button("Clear", GUILayout.Width(56), GUILayout.Height(56f)))
            {
                Undo.RecordObject(battleGoodEvilProfile, "Clear Legend Battle Sprite");
                e.battleSprite = null;
                PersistBattleGoodEvilProfile("ClearLegendBattle");
            }
            EditorGUILayout.EndHorizontal();
            if (e.battleSprite != null)
            {
                Rect prev = GUILayoutUtility.GetRect(48f, 48f, GUILayout.ExpandWidth(false));
                DrawSprite(prev, e.battleSprite, Color.white);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawBattleBossSlots()
    {
        EditorGUILayout.HelpBox(
            "Wave 3–10+ boss battle sprites (lore-boss-wave-order). Soft-fail empty → CharacterDatabase / dreadLord fallback.",
            MessageType.None);
        battleBossSlotScroll = EditorGUILayout.BeginScrollView(battleBossSlotScroll, GUILayout.MinHeight(360f));
        var slots = battleGoodEvilProfile.evilBossSlots;
        for (int i = 0; i < slots.Count; i++)
        {
            var e = slots[i];
            if (e == null) continue;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            string label = string.IsNullOrEmpty(e.displayName) ? e.unitType.ToString() : e.displayName;
            EditorGUI.BeginChangeCheck();
            e.battleSprite = (Sprite)EditorGUILayout.ObjectField(
                label, e.battleSprite, typeof(Sprite), false, GUILayout.Height(56f));
            if (EditorGUI.EndChangeCheck())
            {
                if (e.battleSprite != null && LegendPewPortraitApplier.IsFacePoolSprite(e.battleSprite))
                {
                    EditorUtility.DisplayDialog(
                        "Battle vs Face",
                        "That sprite is in a Tab 5 FACE pool.\nTab 6 is Autobattle sprites only.",
                        "OK");
                    e.battleSprite = null;
                }
                else
                {
                    Undo.RecordObject(battleGoodEvilProfile, "Assign Boss Battle Sprite");
                    PersistBattleGoodEvilProfile("EditBossBattle");
                }
            }
            if (GUILayout.Button("Clear", GUILayout.Width(56), GUILayout.Height(56f)))
            {
                Undo.RecordObject(battleGoodEvilProfile, "Clear Boss Battle Sprite");
                e.battleSprite = null;
                PersistBattleGoodEvilProfile("ClearBossBattle");
            }
            EditorGUILayout.EndHorizontal();
            if (e.battleSprite != null)
            {
                Rect prev = GUILayoutUtility.GetRect(48f, 48f, GUILayout.ExpandWidth(false));
                DrawSprite(prev, e.battleSprite, Color.white);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawBattlePaletteQuickAdd(List<Sprite> pool)
    {
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
                if (TryAddBattlePoolSprite(pool, sp))
                    PersistBattleGoodEvilProfile("PaletteClickBattlePool");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    bool TryAddBattlePoolSprite(List<Sprite> pool, Sprite sp)
    {
        if (pool == null || sp == null) return false;
        if (LegendPewPortraitApplier.IsFacePoolSprite(sp))
        {
            Debug.LogWarning(
                "[DemonKnight] Soft-fail: refusing face-pool sprite '" + sp.name
                + "' as Tab 6 BATTLE. Use Tab 5 for faces.");
            EditorUtility.DisplayDialog(
                "Battle vs Face",
                "That sprite is already in a Tab 5 FACE pool.\n"
                + "Tab 6 is Autobattle UnitVisualActor sprites only.\n"
                + "Assign faces on Tab 5; pew bodies on Tab 4.",
                "OK");
            return false;
        }
        Undo.RecordObject(battleGoodEvilProfile, "Add Battle Pool Sprite");
        pool.Add(sp);
        return true;
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

    // ===== SoftPromote: Type.GetType / reflection no-op (CS0103 belt — no compile-time type name) =====
    static Type ResolveArchitectBuildPersistType()
    {
        return Type.GetType("ArthurianArchitectBuildPersist")
               ?? Type.GetType("ArthurianArchitectBuildPersist, Assembly-CSharp-Editor")
               ?? typeof(ArthurianArtArchitectWindow).Assembly.GetType("ArthurianArchitectBuildPersist");
    }

    static void SoftInvokePromote(string methodName, object arg)
    {
        try
        {
            Type t = ResolveArchitectBuildPersistType();
            if (t == null)
            {
                Debug.LogWarning(
                    "[DemonKnight] SoftPromote no-op: ArthurianArchitectBuildPersist missing — "
                    + "run Force Sync / Escape tip (" + UiBuildId + ").");
                return;
            }
            MethodInfo m = t.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { arg != null ? arg.GetType() : typeof(object) },
                null);
            if (m == null)
            {
                // Fallback: match by name only (profile types may be from same assembly).
                foreach (MethodInfo cand in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (cand.Name == methodName && cand.GetParameters().Length == 1)
                    {
                        m = cand;
                        break;
                    }
                }
            }
            if (m == null)
            {
                Debug.LogWarning("[DemonKnight] SoftPromote missing method " + methodName);
                return;
            }
            m.Invoke(null, new[] { arg });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] SoftPromote " + methodName + " soft-fail: " + ex.Message);
        }
    }

    static void SoftPromoteStageLayoutIntoResources(StageLayoutProfile p) =>
        SoftInvokePromote("PromoteStageLayoutIntoResources", p);

    static void SoftPromoteSceneBackgroundIntoResources(SceneBackgroundProfile p) =>
        SoftInvokePromote("PromoteSceneBackgroundIntoResources", p);

    static void SoftPromoteClassPoolIntoResources(ClassSpritePoolProfile p) =>
        SoftInvokePromote("PromoteClassPoolIntoResources", p);

    static void SoftPromoteLegendPewIntoResources(LegendPewPortraitProfile p) =>
        SoftInvokePromote("PromoteLegendPewIntoResources", p);

    static void SoftPromoteBattleGoodEvilIntoResources(BattleGoodEvilSpritePoolProfile p) =>
        SoftInvokePromote("PromoteBattleGoodEvilIntoResources", p);

}


// ===== fix-pew-body-pool-empty EMBEDDED ArthurianArchitectBuildPersist (Force Sync nuclear — do not remove) =====
// Standalone ArthurianArchitectBuildPersist.cs is a stub only. Type lives HERE so MustRestore
// Architect host always compiles Promote* + IPreprocessBuild (CS0103 hard fix after #47).
/// <summary>
/// Keeps Architect Resources profiles + artwork in player builds.
/// tip: fix-pew-body-pool-empty (embedded in ArthurianArtArchitectWindow)
///
/// Profiles (StageLayout, ClassSpritePool, SceneBackground, LegendPewPortrait, BattleGoodEvil) live under
/// Assets/Resources/ and must ship with the build. Ensure/Load rematerialize from path
/// backups via Resources.Load / StreamingAssets — never Editor-only wipe at runtime.
/// SoftPromote runs from Architect PersistAll on OnDisable/OnDestroy/Save.
/// </summary>
public class ArthurianArchitectBuildPersist : IPreprocessBuildWithReport
{
    public const string TipId = "fix-pew-body-pool-empty";
    public const string ArchitectArtRoot = "Assets/Resources/ArchitectArt";
    public const string StreamingRel = "Assets/StreamingAssets/DemonKnightArchitect";
    public const string ManifestRel = "Assets/StreamingAssets/DemonKnightArchitect/BUILD_INCLUDE.txt";

    public int callbackOrder => 50;

    [MenuItem("Arthurian/Prepare Architect Art for Player Build", false, 52)]
    public static void PrepareMenu()
    {
        string report = PrepareForPlayerBuild(forceCopyStreaming: true);
        EditorUtility.DisplayDialog(
            "Architect Build Persist",
            report + "\n\nInclude these in File → Build Settings / Build:\n"
            + "• Assets/Resources/*.asset profiles\n"
            + "• Assets/Resources/ArchitectArt/**\n"
            + "• Assets/StreamingAssets/DemonKnightArchitect/** (optional fallback)\n"
            + "• Referenced sprite textures (or their ArchitectArt copies)",
            "OK");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        try
        {
            string msg = PrepareForPlayerBuild(forceCopyStreaming: true);
            Debug.Log("[DemonKnight] Architect build persist (" + TipId + "):\n" + msg);
        }
        catch (Exception ex)
        {
            // Soft-fail — never wipe profiles / never abort build for missing optional art.
            Debug.LogWarning("[DemonKnight] Architect build persist soft-fail: " + ex.Message);
        }
    }

    /// <summary>
    /// Ensure profile assets exist, rematerialize path backups, promote sprites into
    /// Resources/ArchitectArt, mirror files into StreamingAssets. Never deletes profile YAML.
    /// </summary>
    public static string PrepareForPlayerBuild(bool forceCopyStreaming)
    {
        var log = new StringBuilder();
        log.AppendLine("tip=" + TipId);

        EnsureFolder("Assets/Resources");
        EnsureFolder(ArchitectArtRoot);
        EnsureFolder(ArchitectArtRoot + "/Body");
        EnsureFolder(ArchitectArtRoot + "/Face");
        EnsureFolder(ArchitectArtRoot + "/Stage");
        EnsureFolder(ArchitectArtRoot + "/SceneBg");
        EnsureFolder(ArchitectArtRoot + "/Battle");
        EnsureFolder("Assets/StreamingAssets");
        EnsureFolder(StreamingRel);

        var stage = ArthurianStageLayoutProfileEnsure.EnsureAsset();
        var scene = ArthurianStageLayoutProfileEnsure.EnsureSceneBackgroundAsset();
        var body = ArthurianStageLayoutProfileEnsure.EnsureClassSpritePoolAsset();
        var face = ArthurianStageLayoutProfileEnsure.EnsureLegendPewPortraitAsset();
        var battle = ArthurianStageLayoutProfileEnsure.EnsureBattleGoodEvilSpritePoolAsset();

        int promoted = 0;
        promoted += PromoteStageLayoutIntoResources(stage);
        promoted += PromoteSceneBackgroundIntoResources(scene);
        promoted += PromoteClassPoolIntoResources(body);
        promoted += PromoteLegendPewIntoResources(face);
        promoted += PromoteBattleGoodEvilIntoResources(battle);

        // Rematerialize after promote (Editor + runtime-safe paths).
        stage?.RematerializeSpritesFromPaths();
        body?.RematerializeSpritesFromPaths();
        face?.RematerializeSpritesFromPaths();
        battle?.RematerializeSpritesFromPaths();

        if (forceCopyStreaming)
            CopyReferencedTexturesToStreaming(stage, scene, body, face, battle, log);

        WriteManifest(stage, scene, body, face, battle, promoted, log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine(
            $"Profiles: Stage={(stage != null)} SceneBg={(scene != null)} "
            + $"ClassPool={(body != null)} LegendPew={(face != null)} "
            + $"Battle={(battle != null)}; promotedSprites≈{promoted}");
        log.AppendLine("NO Editor wipe of Resources profiles during build preprocess.");
        return log.ToString().TrimEnd();
    }

    public static int PromoteClassPoolIntoResources(ClassSpritePoolProfile p)
    {
        if (p == null) return 0;
        p.EnsureLists();
        int n = 0;
        n += PromoteList(p.peasantSprites, p.peasantAssetPaths, "Body/Peasant");
        n += PromoteList(p.warriorSprites, p.warriorAssetPaths, "Body/Warrior");
        n += PromoteList(p.knightSprites, p.knightAssetPaths, "Body/Knight");
        EditorUtility.SetDirty(p);
        return n;
    }

    public static int PromoteLegendPewIntoResources(LegendPewPortraitProfile p)
    {
        if (p == null) return 0;
        p.EnsureSlotsAndLists();
        int n = 0;
        n += PromoteList(p.peasantFaceProfiles, p.peasantFaceAssetPaths, "Face/Peasant");
        n += PromoteList(p.warriorFaceProfiles, p.warriorFaceAssetPaths, "Face/Warrior");
        n += PromoteList(p.knightFaceProfiles, p.knightFaceAssetPaths, "Face/Knight");
        if (p.legendPortraits != null)
        {
            for (int i = 0; i < p.legendPortraits.Count; i++)
            {
                var e = p.legendPortraits[i];
                if (e == null || e.facePortrait == null) continue;
                Sprite promoted = PromoteSprite(e.facePortrait, "Face/Legend/" + e.unitType);
                if (promoted != null && promoted != e.facePortrait)
                {
                    e.facePortrait = promoted;
                    n++;
                }
                string ap = AssetDatabase.GetAssetPath(e.facePortrait);
                if (!string.IsNullOrEmpty(ap)) e.assetPath = ap;
            }
        }
        EditorUtility.SetDirty(p);
        return n;
    }

    public static int PromoteBattleGoodEvilIntoResources(BattleGoodEvilSpritePoolProfile p)
    {
        if (p == null) return 0;
        p.EnsureSlotsAndLists();
        int n = 0;
        n += PromoteList(p.peasantBattleSprites, p.peasantBattleAssetPaths, "Battle/Good/Peasant");
        n += PromoteList(p.warriorBattleSprites, p.warriorBattleAssetPaths, "Battle/Good/Warrior");
        n += PromoteList(p.knightBattleSprites, p.knightBattleAssetPaths, "Battle/Good/Knight");
        n += PromoteList(p.priestBattleSprites, p.priestBattleAssetPaths, "Battle/Good/Priest");
        n += PromoteList(p.demonImpBattleSprites, p.demonImpBattleAssetPaths, "Battle/Evil/Imp");
        n += PromoteList(p.hellhoundBattleSprites, p.hellhoundBattleAssetPaths, "Battle/Evil/Hellhound");
        n += PromoteList(p.dreadLordBattleSprites, p.dreadLordBattleAssetPaths, "Battle/Evil/DreadLord");
        if (p.goodLegendSlots != null)
        {
            for (int i = 0; i < p.goodLegendSlots.Count; i++)
            {
                var e = p.goodLegendSlots[i];
                if (e == null || e.battleSprite == null) continue;
                Sprite promoted = PromoteSprite(e.battleSprite, "Battle/Good/Legend/" + e.unitType);
                if (promoted != null && promoted != e.battleSprite)
                {
                    e.battleSprite = promoted;
                    n++;
                }
                string ap = AssetDatabase.GetAssetPath(e.battleSprite);
                if (!string.IsNullOrEmpty(ap)) e.assetPath = ap;
            }
        }
        if (p.evilBossSlots != null)
        {
            for (int i = 0; i < p.evilBossSlots.Count; i++)
            {
                var e = p.evilBossSlots[i];
                if (e == null || e.battleSprite == null) continue;
                Sprite promoted = PromoteSprite(e.battleSprite, "Battle/Evil/Boss/" + e.unitType);
                if (promoted != null && promoted != e.battleSprite)
                {
                    e.battleSprite = promoted;
                    n++;
                }
                string ap = AssetDatabase.GetAssetPath(e.battleSprite);
                if (!string.IsNullOrEmpty(ap)) e.assetPath = ap;
            }
        }
        EditorUtility.SetDirty(p);
        return n;
    }

    public static int PromoteStageLayoutIntoResources(StageLayoutProfile p)
    {
        if (p == null) return 0;
        p.EnsureSlotCounts();
        int n = 0;
        n += PromoteSlots(p.churchPewSlots, "Stage/Church");
        n += PromoteSlots(p.playerBattleSlots, "Stage/Player");
        n += PromoteSlots(p.enemyBattleSlots, "Stage/Enemy");
        EditorUtility.SetDirty(p);
        return n;
    }

    public static int PromoteSceneBackgroundIntoResources(SceneBackgroundProfile p)
    {
        if (p == null) return 0;
        p.EnsureDefaultEntries();
        int n = 0;
        if (p.entries == null) return 0;
        for (int i = 0; i < p.entries.Count; i++)
        {
            var e = p.entries[i];
            if (e == null) continue;
            if (e.imageSprite != null)
            {
                Sprite promoted = PromoteSprite(e.imageSprite, "SceneBg/" + e.slot);
                if (promoted != null && promoted != e.imageSprite)
                {
                    e.imageSprite = promoted;
                    n++;
                }
                string ap = AssetDatabase.GetAssetPath(e.imageSprite);
                if (!string.IsNullOrEmpty(ap)) e.imageAssetPath = ap;
            }
        }
        EditorUtility.SetDirty(p);
        return n;
    }

    static int PromoteSlots(List<SlotTransformConfig> slots, string sub)
    {
        if (slots == null) return 0;
        int n = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null || s.previewOrOverrideSprite == null) continue;
            Sprite promoted = PromoteSprite(s.previewOrOverrideSprite, sub + "/Seat" + i);
            if (promoted != null && promoted != s.previewOrOverrideSprite)
            {
                s.previewOrOverrideSprite = promoted;
                n++;
            }
            string ap = AssetDatabase.GetAssetPath(s.previewOrOverrideSprite);
            if (!string.IsNullOrEmpty(ap)) s.previewAssetPath = ap;
        }
        return n;
    }

    static int PromoteList(List<Sprite> sprites, List<string> paths, string sub)
    {
        if (sprites == null) return 0;
        paths ??= new List<string>();
        while (paths.Count < sprites.Count) paths.Add("");
        int n = 0;
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null) continue;
            Sprite promoted = PromoteSprite(sprites[i], sub + "/" + i);
            if (promoted != null && promoted != sprites[i])
            {
                sprites[i] = promoted;
                n++;
            }
            string ap = AssetDatabase.GetAssetPath(sprites[i]);
            if (!string.IsNullOrEmpty(ap)) paths[i] = ap;
        }
        return n;
    }

    /// <summary>
    /// Copy sprite texture into Resources/ArchitectArt if it is not already under Resources.
    /// Returns the Resources sprite (or original when already in Resources / copy fails).
    /// Soft-fail — never clears the source assignment.
    /// </summary>
    public static Sprite PromoteSprite(Sprite sp, string logicalSubPath)
    {
        if (sp == null) return null;
        string src = AssetDatabase.GetAssetPath(sp);
        if (string.IsNullOrEmpty(src)) return sp;
        string norm = src.Replace('\\', '/');
        if (norm.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
            return sp; // already build-safe

        try
        {
            string ext = Path.GetExtension(norm);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            string safe = Sanitize(logicalSubPath) + ext;
            string destFolder = ArchitectArtRoot + "/" + Path.GetDirectoryName(logicalSubPath)!.Replace('\\', '/');
            EnsureFolder(destFolder);
            string dest = destFolder + "/" + Path.GetFileName(safe);
            // Avoid clobbering distinct sprites that share a name — include guid suffix.
            string guid = AssetDatabase.AssetPathToGUID(norm);
            if (!string.IsNullOrEmpty(guid) && guid.Length >= 8)
            {
                string baseName = Path.GetFileNameWithoutExtension(safe);
                dest = destFolder + "/" + baseName + "_" + guid.Substring(0, 8) + ext;
            }

            if (!File.Exists(DestFull(dest)))
            {
                if (!AssetDatabase.CopyAsset(norm, dest))
                {
                    Debug.LogWarning("[DemonKnight] PromoteSprite CopyAsset soft-fail: " + norm + " → " + dest);
                    return sp;
                }
            }

            AssetDatabase.ImportAsset(dest);
            var copy = AssetDatabase.LoadAssetAtPath<Sprite>(dest);
            if (copy == null)
            {
                var objs = AssetDatabase.LoadAllAssetsAtPath(dest);
                if (objs != null)
                {
                    for (int i = 0; i < objs.Length; i++)
                        if (objs[i] is Sprite s) { copy = s; break; }
                }
            }
            return copy != null ? copy : sp;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DemonKnight] PromoteSprite soft-fail: " + ex.Message);
            return sp;
        }
    }

    static void CopyReferencedTexturesToStreaming(
        StageLayoutProfile stage,
        SceneBackgroundProfile scene,
        ClassSpritePoolProfile body,
        LegendPewPortraitProfile face,
        BattleGoodEvilSpritePoolProfile battle,
        StringBuilder log)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectPaths(body?.peasantAssetPaths, paths);
        CollectPaths(body?.warriorAssetPaths, paths);
        CollectPaths(body?.knightAssetPaths, paths);
        CollectPaths(face?.peasantFaceAssetPaths, paths);
        CollectPaths(face?.warriorFaceAssetPaths, paths);
        CollectPaths(face?.knightFaceAssetPaths, paths);
        if (face?.legendPortraits != null)
        {
            foreach (var e in face.legendPortraits)
                if (e != null && !string.IsNullOrEmpty(e.assetPath)) paths.Add(e.assetPath);
        }
        CollectPaths(battle?.peasantBattleAssetPaths, paths);
        CollectPaths(battle?.warriorBattleAssetPaths, paths);
        CollectPaths(battle?.knightBattleAssetPaths, paths);
        CollectPaths(battle?.priestBattleAssetPaths, paths);
        CollectPaths(battle?.demonImpBattleAssetPaths, paths);
        CollectPaths(battle?.hellhoundBattleAssetPaths, paths);
        CollectPaths(battle?.dreadLordBattleAssetPaths, paths);
        if (battle?.goodLegendSlots != null)
        {
            foreach (var e in battle.goodLegendSlots)
                if (e != null && !string.IsNullOrEmpty(e.assetPath)) paths.Add(e.assetPath);
        }
        if (battle?.evilBossSlots != null)
        {
            foreach (var e in battle.evilBossSlots)
                if (e != null && !string.IsNullOrEmpty(e.assetPath)) paths.Add(e.assetPath);
        }
        if (stage != null)
        {
            CollectSlotPaths(stage.churchPewSlots, paths);
            CollectSlotPaths(stage.playerBattleSlots, paths);
            CollectSlotPaths(stage.enemyBattleSlots, paths);
        }
        if (scene?.entries != null)
        {
            foreach (var e in scene.entries)
                if (e != null && !string.IsNullOrEmpty(e.imageAssetPath)) paths.Add(e.imageAssetPath);
        }

        int copied = 0;
        foreach (string ap in paths)
        {
            if (string.IsNullOrEmpty(ap)) continue;
            string full = DestFull(ap);
            if (!File.Exists(full)) continue;
            string ext = Path.GetExtension(full).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;
            string dest = Path.Combine(DestFull(StreamingRel), Path.GetFileName(full));
            try
            {
                File.Copy(full, dest, overwrite: true);
                copied++;
            }
            catch (Exception ex)
            {
                log.AppendLine("Streaming copy soft-fail " + ap + ": " + ex.Message);
            }
        }
        log.AppendLine("StreamingAssets texture copies: " + copied);
    }

    static void CollectPaths(List<string> list, HashSet<string> into)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
            if (!string.IsNullOrEmpty(list[i])) into.Add(list[i]);
    }

    static void CollectSlotPaths(List<SlotTransformConfig> slots, HashSet<string> into)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s == null) continue;
            if (!string.IsNullOrEmpty(s.previewAssetPath)) into.Add(s.previewAssetPath);
            else if (s.previewOrOverrideSprite != null)
            {
                string ap = AssetDatabase.GetAssetPath(s.previewOrOverrideSprite);
                if (!string.IsNullOrEmpty(ap))
                {
                    s.previewAssetPath = ap;
                    into.Add(ap);
                }
            }
        }
    }

    static void WriteManifest(
        StageLayoutProfile stage,
        SceneBackgroundProfile scene,
        ClassSpritePoolProfile body,
        LegendPewPortraitProfile face,
        BattleGoodEvilSpritePoolProfile battle,
        int promoted,
        StringBuilder log)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DemonKnight Architect — player build include list");
        sb.AppendLine("tip=" + TipId);
        sb.AppendLine("generated=" + DateTime.UtcNow.ToString("o"));
        sb.AppendLine();
        sb.AppendLine("REQUIRED Resources profiles (Assets/Resources/):");
        sb.AppendLine("  StageLayoutProfile.asset");
        sb.AppendLine("  ClassSpritePoolProfile.asset");
        sb.AppendLine("  SceneBackgroundProfile.asset");
        sb.AppendLine("  LegendPewPortraitProfile.asset");
        sb.AppendLine("  BattleGoodEvilSpritePoolProfile.asset");
        sb.AppendLine();
        sb.AppendLine("REQUIRED promoted art:");
        sb.AppendLine("  Assets/Resources/ArchitectArt/** (Body + Face + Battle + Stage + SceneBg)");
        sb.AppendLine();
        sb.AppendLine("OPTIONAL fallback:");
        sb.AppendLine("  Assets/StreamingAssets/DemonKnightArchitect/*.(png|jpg)");
        sb.AppendLine();
        sb.AppendLine("Counts:");
        sb.AppendLine("  Body P/W/K = "
            + (body?.CountUsable(UnitType.Peasant) ?? 0) + "/"
            + (body?.CountUsable(UnitType.Warrior) ?? 0) + "/"
            + (body?.CountUsable(UnitType.Knight) ?? 0));
        sb.AppendLine("  Face P/W/K = "
            + (face?.CountUsablePewFaces(UnitType.Peasant) ?? 0) + "/"
            + (face?.CountUsablePewFaces(UnitType.Warrior) ?? 0) + "/"
            + (face?.CountUsablePewFaces(UnitType.Knight) ?? 0));
        sb.AppendLine("  Legend faces = " + (face?.CountAssignedLegendFaces() ?? 0));
        sb.AppendLine("  Battle good legends = " + (battle?.CountAssignedLegendBattles() ?? 0));
        sb.AppendLine("  Battle bosses = " + (battle?.CountAssignedBossBattles() ?? 0));
        sb.AppendLine("  Battle P/Priest/Imp = "
            + (battle?.CountUsablePool(UnitType.Peasant) ?? 0) + "/"
            + (battle?.CountUsablePool(UnitType.Priest) ?? 0) + "/"
            + (battle?.CountUsablePool(UnitType.DemonImp) ?? 0));
        sb.AppendLine("  Promoted this pass ≈ " + promoted);
        sb.AppendLine();
        sb.AppendLine("Body = Tab 4 Class Sprites (full pew avatar → bodySprite).");
        sb.AppendLine("Face = Tab 5 Legend/Pew Portraits (FacePortrait + optional Head overlay).");
        sb.AppendLine("Battle = Tab 6 Good/Evil pools (UnitVisualActor Autobattle sprites).");
        sb.AppendLine("Keep Tab 4 BODY / Tab 5 FACE / Tab 6 BATTLE separate (soft-fail cross-assign).");
        sb.AppendLine("Runtime Load rematerializes from path backups — no Editor wipe in player.");

        string full = DestFull(ManifestRel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, sb.ToString());
        log.AppendLine("Wrote " + ManifestRel);
    }

    static void EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        string[] parts = assetPath.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    static string Sanitize(string logical)
    {
        if (string.IsNullOrEmpty(logical)) return "sprite";
        var sb = new StringBuilder(logical.Length);
        foreach (char c in logical)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '/') sb.Append(c);
            else sb.Append('_');
        }
        return sb.ToString().Trim('/');
    }

    static string DestFull(string assetPath)
    {
        string root = Directory.GetParent(Application.dataPath)!.FullName;
        return Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
#endif
