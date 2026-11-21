#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ArthurianAutobattler;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Master Spec Phase 3 — Art &amp; Stage Architect.
/// Menu: Arthurian → Art &amp; Stage Architect
/// Tabs: Sheet Splicer &amp; Animation Builder | Paper-Doll Generator | Numbered Stage Placer
/// </summary>
public class ArthurianArtArchitectWindow : EditorWindow
{
    const string DefaultLibraryPath = "Assets/Art/Modular/ModularSpriteLibrary.asset";
    const string DefaultSliceFolder = "Assets/Art/Modular/ArchitectSliced";
    const string CharactersFolder = "Assets/Resources/Characters";
    const string StageProfilePath = "Assets/Resources/StageLayoutProfile.asset";

    enum ArchitectTab { Splicer, PaperDoll, StagePlacer }
    enum SheetCategory { Pose, Outfit, Head, Weapon, EnemyBoss }
    enum StageViewMode { SomersetChurch, ChurchyardBattle }
    enum BattleSlotSide { Player, Enemy }

    ArchitectTab tab;
    Vector2 scroll;

    // ── Tab 1: Splicer ─────────────────────────────────────────────────────
    Texture2D sheetTexture;
    SheetCategory sheetCategory = SheetCategory.Pose;
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

    // ── Tab 2: Paper-Doll ──────────────────────────────────────────────────
    int poseIndex;
    int outfitIndex;
    int headIndex;
    int weaponIndex;
    Color skinTint = new Color(0.96f, 0.82f, 0.70f, 1f);
    Color hairTint = new Color(0.12f, 0.10f, 0.10f, 1f);
    Color armorTint = Color.white;
    bool previewPlaying;
    double previewStartTime;
    int previewFrame;
    string saveCharacterId = "townsfolk_custom";
    string saveDisplayName = "Custom Townsfolk";
    UnitType saveUnitType = UnitType.Peasant;

    // ── Tab 3: Stage Placer ────────────────────────────────────────────────
    StageLayoutProfile stageProfile;
    StageViewMode stageView = StageViewMode.SomersetChurch;
    BattleSlotSide battleSide = BattleSlotSide.Player;
    int selectedSlot;
    bool draggingSlot;
    Vector2 stagePan;
    float stageZoom = 28f;
    Vector2 parallaxScroll;

    [MenuItem("Arthurian/Art & Stage Architect", false, 35)]
    public static void Open()
    {
        var win = GetWindow<ArthurianArtArchitectWindow>("Art & Stage Architect");
        win.minSize = new Vector2(780, 560);
        win.Show();
    }

    void OnEnable()
    {
        if (targetLibrary == null)
            targetLibrary = AssetDatabase.LoadAssetAtPath<ModularSpriteLibrary>(DefaultLibraryPath)
                            ?? ModularSpriteLibrary.Load();
        if (stageProfile == null)
            stageProfile = AssetDatabase.LoadAssetAtPath<StageLayoutProfile>(StageProfilePath)
                           ?? StageLayoutProfile.Load();
        stageProfile?.EnsureSlotCounts();
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    void OnEditorUpdate()
    {
        if (!previewPlaying || tab != ArchitectTab.PaperDoll) return;
        Repaint();
    }

    void OnGUI()
    {
        DrawToolbar();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case ArchitectTab.Splicer: DrawSplicerTab(); break;
            case ArchitectTab.PaperDoll: DrawPaperDollTab(); break;
            case ArchitectTab.StagePlacer: DrawStagePlacerTab(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Toggle(tab == ArchitectTab.Splicer, "1 · Sheet Splicer & Animation", EditorStyles.toolbarButton))
            tab = ArchitectTab.Splicer;
        if (GUILayout.Toggle(tab == ArchitectTab.PaperDoll, "2 · Paper-Doll Generator", EditorStyles.toolbarButton))
            tab = ArchitectTab.PaperDoll;
        if (GUILayout.Toggle(tab == ArchitectTab.StagePlacer, "3 · Stage Placer & Parallax", EditorStyles.toolbarButton))
            tab = ArchitectTab.StagePlacer;
        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 1 — Sheet Splicer & Animation Builder
    // ═══════════════════════════════════════════════════════════════════════

    void DrawSplicerTab()
    {
        EditorGUILayout.LabelField("Sheet Splicer & Animation Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drop a sprite sheet, Auto-Prepare (Sprite/Point/RGBA32/readable + key-out), " +
            "Grid Crop or Auto-Island Crop, select frames, Create Animation Clip → ModularSpriteLibrary.",
            MessageType.Info);

        sheetTexture = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet", sheetTexture, typeof(Texture2D), false);
        sheetCategory = (SheetCategory)EditorGUILayout.EnumPopup("Category", sheetCategory);
        targetLibrary = (ModularSpriteLibrary)EditorGUILayout.ObjectField(
            "ModularSpriteLibrary", targetLibrary, typeof(ModularSpriteLibrary), false);
        sliceOutputFolder = EditorGUILayout.TextField("Slice Output Folder", sliceOutputFolder);
        keyColor = EditorGUILayout.ColorField("Key Color", keyColor);
        keyTolerance = EditorGUILayout.Slider("Key Tolerance", keyTolerance, 0f, 64f);

        EditorGUILayout.BeginHorizontal();
        cellWidth = Mathf.Max(4, EditorGUILayout.IntField("Cell Width", cellWidth));
        cellHeight = Mathf.Max(4, EditorGUILayout.IntField("Cell Height", cellHeight));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUI.BeginDisabledGroup(sheetTexture == null);
        if (GUILayout.Button("Auto-Prepare Sheet", GUILayout.Height(28)))
            AutoPrepareSheet();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Grid Crop", GUILayout.Height(32)))
            RunGridCrop();
        if (GUILayout.Button("Auto-Island Crop", GUILayout.Height(32)))
            RunIslandCrop();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        DrawCroppedFramePicker();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Create Animation Clip", EditorStyles.boldLabel);
        clipName = EditorGUILayout.TextField("Clip Name (Idle/Attack/Hit/Death)", clipName);
        clipFps = EditorGUILayout.Slider("FPS", clipFps, 1f, 24f);
        clipLoop = EditorGUILayout.Toggle("Loop", clipLoop);

        int selectedCount = CountSelectedFrames();
        EditorGUI.BeginDisabledGroup(selectedCount == 0 || targetLibrary == null);
        if (GUILayout.Button($"Create Animation Clip ({selectedCount} frames) → Register Library", GUILayout.Height(36)))
            CreateAndRegisterClip();
        EditorGUI.EndDisabledGroup();

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
        targetLibrary.RegisterAnimClip(sheetCategory.ToString(), clip);

        // Also push first frame into static library slots when empty (Pose/Outfit/Head/Weapon).
        AssignStaticIfEmpty(sheetCategory, frames[0]);

        EditorUtility.SetDirty(targetLibrary);
        AssetDatabase.SaveAssets();
        SyncLibraryToResources();
        Debug.Log($"[DemonKnight] Registered [{sheetCategory}] clip '{name}' ({frames.Count} frames).");
        EditorUtility.DisplayDialog(
            "Animation Clip",
            $"Registered [{sheetCategory}] {name}\n{frames.Count} frames @ {clipFps:0.#} fps\n→ {AssetDatabase.GetAssetPath(targetLibrary)}",
            "OK");
    }

    void AssignStaticIfEmpty(SheetCategory cat, Sprite first)
    {
        if (first == null || targetLibrary == null) return;
        targetLibrary.EnsureSizedArrays();
        switch (cat)
        {
            case SheetCategory.Pose:
                if (targetLibrary.basePoses[0] == null) targetLibrary.basePoses[0] = first;
                break;
            case SheetCategory.Outfit:
                if (targetLibrary.outfits[0] == null) targetLibrary.outfits[0] = first;
                break;
            case SheetCategory.Head:
                if (targetLibrary.heads == null || targetLibrary.heads.Length == 0)
                    targetLibrary.AssignHeadsFromSprites(new List<Sprite> { first }, new List<bool> { true });
                break;
            case SheetCategory.Weapon:
                if (targetLibrary.props == null || targetLibrary.props.Length <= 1)
                    targetLibrary.props = new[] { (Sprite)null, first };
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 2 — Paper-Doll Unit Generator & Live Animator
    // ═══════════════════════════════════════════════════════════════════════

    void DrawPaperDollTab()
    {
        EditorGUILayout.LabelField("Paper-Doll Unit Generator & Live Animator", EditorStyles.boldLabel);
        targetLibrary = (ModularSpriteLibrary)EditorGUILayout.ObjectField(
            "ModularSpriteLibrary", targetLibrary, typeof(ModularSpriteLibrary), false);
        if (targetLibrary == null)
        {
            EditorGUILayout.HelpBox("Assign a ModularSpriteLibrary (or create via Art Tools importer).", MessageType.Warning);
            if (GUILayout.Button("Create / Load Default Library"))
                EnsureLibraryAsset();
            return;
        }
        targetLibrary.EnsureSizedArrays();

        int poseMax = Mathf.Max(0, targetLibrary.PoseCount - 1);
        int outfitMax = Mathf.Max(0, targetLibrary.OutfitCount - 1);
        int headMax = Mathf.Max(0, targetLibrary.HeadCount - 1);
        int propMax = Mathf.Max(0, targetLibrary.PropCount - 1);

        poseIndex = poseMax >= 0 ? EditorGUILayout.IntSlider("Pose", poseIndex, 0, poseMax) : 0;
        outfitIndex = outfitMax >= 0 ? EditorGUILayout.IntSlider("Outfit", outfitIndex, 0, outfitMax) : 0;
        headIndex = headMax >= 0 ? EditorGUILayout.IntSlider("Head", headIndex, 0, headMax) : 0;
        weaponIndex = propMax >= 0 ? EditorGUILayout.IntSlider("Weapon / Prop", weaponIndex, 0, propMax) : 0;
        skinTint = EditorGUILayout.ColorField("Skin Tint", skinTint);
        hairTint = EditorGUILayout.ColorField("Hair Tint", hairTint);
        armorTint = EditorGUILayout.ColorField("Armor Tint", armorTint);

        EditorGUILayout.Space(6);
        Rect preview = GUILayoutUtility.GetRect(180, 220, GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(preview, new Color(0.12f, 0.11f, 0.14f, 1f));
        DrawPaperDollPreview(preview);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(previewPlaying ? "Pause" : "Play", GUILayout.Height(28), GUILayout.Width(80)))
        {
            previewPlaying = !previewPlaying;
            if (previewPlaying) previewStartTime = EditorApplication.timeSinceStartup;
        }
        if (GUILayout.Button("Randomize Unit", GUILayout.Height(28)))
            RandomizeUnit();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Save as CharacterDefinition", EditorStyles.boldLabel);
        saveCharacterId = EditorGUILayout.TextField("Character Id", saveCharacterId);
        saveDisplayName = EditorGUILayout.TextField("Display Name", saveDisplayName);
        saveUnitType = (UnitType)EditorGUILayout.EnumPopup("Unit Type", saveUnitType);
        if (GUILayout.Button("Save as CharacterDefinition → Resources/Characters", GUILayout.Height(34)))
            SaveAsCharacterDefinition();
    }

    void DrawPaperDollPreview(Rect area)
    {
        if (targetLibrary == null) return;
        Sprite pose = ResolvePreviewSprite(SheetCategory.Pose, poseIndex, targetLibrary.GetPose(poseIndex));
        Sprite outfit = ResolvePreviewSprite(SheetCategory.Outfit, outfitIndex, targetLibrary.GetOutfit(outfitIndex));
        Sprite head = ResolvePreviewSprite(SheetCategory.Head, headIndex, targetLibrary.GetHead(headIndex));
        Sprite weapon = ResolvePreviewSprite(SheetCategory.Weapon, weaponIndex, targetLibrary.GetProp(weaponIndex));

        if (previewPlaying)
        {
            var idle = targetLibrary.FindAnimClip("Pose", SpriteAnimClip.Idle);
            float fps = idle != null ? Mathf.Max(0.1f, idle.framesPerSecond) : 6f;
            previewFrame = Mathf.FloorToInt((float)(EditorApplication.timeSinceStartup - previewStartTime) * fps);
        }

        Rect inner = new Rect(area.x + 20f, area.y + 10f, area.width - 40f, area.height - 20f);
        if (pose != null) DrawSprite(inner, pose, skinTint);
        if (outfit != null) DrawSprite(inner, outfit, armorTint);
        if (head != null)
        {
            bool hair = targetLibrary.HeadIsHair(headIndex);
            DrawSprite(inner, head, hair ? hairTint : Color.white);
        }
        if (weapon != null) DrawSprite(inner, weapon, Color.white);
    }

    Sprite ResolvePreviewSprite(SheetCategory cat, int index, Sprite fallback)
    {
        if (!previewPlaying || targetLibrary == null) return fallback;
        var clip = targetLibrary.FindAnimClip(cat.ToString(), SpriteAnimClip.Idle);
        if (clip == null || clip.FrameCount == 0) return fallback;
        int f = previewFrame % clip.FrameCount;
        return clip.FrameAt(f) ?? fallback;
    }

    void RandomizeUnit()
    {
        if (targetLibrary == null) return;
        targetLibrary.EnsureSizedArrays();
        poseIndex = targetLibrary.PoseCount > 0 ? UnityEngine.Random.Range(0, targetLibrary.PoseCount) : 0;
        outfitIndex = targetLibrary.OutfitCount > 0 ? UnityEngine.Random.Range(0, targetLibrary.OutfitCount) : 0;
        headIndex = targetLibrary.HeadCount > 0 ? UnityEngine.Random.Range(0, targetLibrary.HeadCount) : 0;
        weaponIndex = targetLibrary.PropCount > 1 ? UnityEngine.Random.Range(0, targetLibrary.PropCount) : 0;
        int skin = UnityEngine.Random.Range(0, TownsfolkAppearance.SkinToneCount);
        int hair = UnityEngine.Random.Range(0, TownsfolkAppearance.HairColorCount);
        skinTint = TownsfolkAppearance.SkinTones[skin];
        hairTint = TownsfolkAppearance.HairColors[hair];
        armorTint = Color.white;
        saveUnitType = outfitIndex <= 1 ? UnitType.Peasant
            : outfitIndex <= 3 ? UnitType.Warrior : UnitType.Knight;
        saveDisplayName = $"{saveUnitType} {UnityEngine.Random.Range(100, 999)}";
        saveCharacterId = $"townsfolk_{saveUnitType.ToString().ToLowerInvariant()}_{UnityEngine.Random.Range(1000, 9999)}";
        Repaint();
    }

    void SaveAsCharacterDefinition()
    {
        EnsureFolder(CharactersFolder);
        string id = string.IsNullOrWhiteSpace(saveCharacterId)
            ? $"townsfolk_{Guid.NewGuid().ToString("N").Substring(0, 8)}"
            : saveCharacterId.Trim();
        string assetPath = $"{CharactersFolder}/{id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assetPath);
        CharacterDefinition def = existing != null ? existing : CreateInstance<CharacterDefinition>();
        def.characterId = id;
        def.displayName = string.IsNullOrWhiteSpace(saveDisplayName) ? id : saveDisplayName.Trim();
        def.unitType = saveUnitType;
        def.category = CharacterCategory.Legend;
        def.role = saveUnitType == UnitType.Peasant || saveUnitType == UnitType.Priest
            ? CharacterCombatRole.Backline
            : CharacterCombatRole.Tank;
        def.baseHP = saveUnitType == UnitType.Knight ? 130 : saveUnitType == UnitType.Warrior ? 55 : 28;
        def.baseATK = saveUnitType == UnitType.Knight ? 16 : saveUnitType == UnitType.Warrior ? 8 : 4;
        def.baseAPS = 1.5f;
        def.portraitSprite = targetLibrary != null ? targetLibrary.GetHead(headIndex) : null;
        def.battleSprite = targetLibrary != null ? targetLibrary.GetPose(poseIndex) : null;
        def.visualScale = 1f;

        if (existing == null)
            AssetDatabase.CreateAsset(def, assetPath);
        else
            EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(def);
        Debug.Log($"[DemonKnight] Saved CharacterDefinition → {assetPath}");
        EditorUtility.DisplayDialog("CharacterDefinition", $"Saved\n{assetPath}", "OK");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tab 3 — Numbered Stage Placer & Moving Background Editor
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
        EditorGUILayout.BeginVertical(GUILayout.Width(260));
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
        if (GUILayout.Button("Save StageLayoutProfile"))
        {
            EditorUtility.SetDirty(stageProfile);
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.EndVertical();
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
        importer.spritesheet = metas.ToArray();
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
        importer.spritesheet = metas;
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
