#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using ArthurianAutobattler;

/// <summary>
/// One-click scene scaffold for DemonKnight (Arthurian Autobattler systems).
/// Menu: Arthurian / Build Complete Scene Hierarchy
/// </summary>
public static class ArthurianSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/CamelotMain.unity";

    // Align Crowd lives in ArthurianAlignCrowdPews.cs (no TMP dependency) so the menu
    // still appears when this builder fails to compile.

    [MenuItem("Arthurian/Build Complete Scene Hierarchy")]
    public static void BuildCompleteScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        EnsureFolders();
        CreateRuntimePrefabs();

        // --- Managers root ---
        GameObject managers = new GameObject("=== SYSTEMS ===");
        managers.AddComponent<GrudgeSaveManager>();
        var merlin = CreateChild<MerlinResurrectionManager>(managers, "MerlinResurrectionManager");
        var perks = CreateChild<PerkDraftManager>(managers, "PerkDraftManager");
        var fusion = CreateChild<CardFusionSystem>(managers, "CardFusionSystem");
        var rng = CreateChild<TownRNGGenerator>(managers, "TownRNGGenerator");
        var horde = CreateChild<DemonHordeGenerator>(managers, "DemonHordeGenerator");
        var battle = CreateChild<AutoBattlerEngine>(managers, "AutoBattlerEngine");
        var loop = CreateChild<GameLoopManager>(managers, "GameLoopManager");
        var visualizer = CreateChild<BattlefieldVisualizer>(managers, "BattlefieldVisualizer");
        var dice = CreateChild<TavernDiceMinigame>(managers, "TavernDiceMinigame");
        CreateChild<DiceManager>(managers, "DiceManager");
        CreateChild<RelicManager>(managers, "RelicManager");
        CreateChild<DemonicPactManager>(managers, "DemonicPactManager");
        CreateChild<SinRuneManager>(managers, "SinRuneManager");

        battle.hordeGenerator = horde;

        // --- Battlefield arena (world space) ---
        // Andrew: drop background art on BattleBackground; place unit markers under PlayerSlots / EnemySlots.
        GameObject arena = new GameObject("BattlefieldArena");
        arena.SetActive(false);

        var bgGo = new GameObject("BattleBackground", typeof(SpriteRenderer));
        bgGo.transform.SetParent(arena.transform, false);
        bgGo.transform.localPosition = new Vector3(0f, 0f, 1f);
        bgGo.transform.localScale = new Vector3(12f, 7f, 1f);
        var bgSr = bgGo.GetComponent<SpriteRenderer>();
        bgSr.color = new Color(0.22f, 0.16f, 0.12f, 1f);
        bgSr.sortingOrder = -20;
        Sprite battleBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/Battlefield.jpg");
        if (battleBgSprite == null)
            battleBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/Battlefield.png");
        if (battleBgSprite != null)
        {
            bgSr.sprite = battleBgSprite;
            bgSr.color = Color.white;
        }

        // Video loop (optional) — drop Assets/Art/Backgrounds/BattlefieldLoop.mp4
        // and/or copy to Assets/Resources/Art/Backgrounds/BattlefieldLoop.mp4.
        VideoClip battleVideo = AssetDatabase.LoadAssetAtPath<VideoClip>(
            "Assets/Art/Backgrounds/BattlefieldLoop.mp4");
        if (battleVideo == null)
            battleVideo = AssetDatabase.LoadAssetAtPath<VideoClip>(
                "Assets/Resources/Art/Backgrounds/BattlefieldLoop.mp4");

        Transform playerSlotsRoot = new GameObject("PlayerSlots").transform;
        playerSlotsRoot.SetParent(arena.transform, false);
        Transform enemySlotsRoot = new GameObject("EnemySlots").transform;
        enemySlotsRoot.SetParent(arena.transform, false);
        var playerSlotArr = new Transform[12];
        var enemySlotArr = new Transform[12];
        for (int i = 0; i < 12; i++)
        {
            int row = i % 3;
            int col = i / 3;
            var ps = new GameObject($"Slot_{i}").transform;
            ps.SetParent(playerSlotsRoot, false);
            ps.localPosition = new Vector3(-3.5f - col * 1.2f, 2.0f - row * 1.1f, 0f);
            playerSlotArr[i] = ps;

            var es = new GameObject($"Slot_{i}").transform;
            es.SetParent(enemySlotsRoot, false);
            es.localPosition = new Vector3(3.5f + col * 1.2f, 2.0f - row * 1.1f, 0f);
            enemySlotArr[i] = es;
        }

        // Visualizer stays under Managers (never parented under Arena) so arena toggle/destroy
        // cannot leave AutoBattlerEngine holding a destroyed singleton.
        visualizer.unitActorPrefab = AssetDatabase.LoadAssetAtPath<UnitVisualActor>("Assets/Prefabs/UnitVisualActor.prefab");
        visualizer.combatTextPrefab = AssetDatabase.LoadAssetAtPath<FloatingCombatText>("Assets/Prefabs/FloatingCombatText.prefab");
        visualizer.battleBackground = bgSr;
        visualizer.battleBackgroundSprite = battleBgSprite;
        visualizer.battleBackgroundVideo = battleVideo;
        visualizer.playerSlots = playerSlotArr;
        visualizer.enemySlots = enemySlotArr;

        // --- Event system ---
        EnsureEventSystem();

        // --- Main menu (New Game / Load / Save slots) ---
        GameObject mainMenuCanvas = CreateCanvas("MainMenuCanvas", 200);
        mainMenuCanvas.AddComponent<MainMenuUI>();

        // --- Town canvas (Somerset church full-screen stage) ---
        GameObject townCanvasObj = CreateCanvas("TownCanvas", 0);
        var townUI = townCanvasObj.AddComponent<TownSplitUIController>();
        var churchUI = townCanvasObj.AddComponent<ChurchStageUIController>();
        var pewLayout = townCanvasObj.AddComponent<SomersetPewLayout>();
        pewLayout.ResetToChurchArtDefaults();
        var codex = townCanvasObj.AddComponent<LegendCodexUI>();

        // Full-screen church background (first child = behind everything)
        GameObject churchBg = new GameObject("ChurchBackground", typeof(RectTransform), typeof(Image));
        churchBg.transform.SetParent(townCanvasObj.transform, false);
        churchBg.transform.SetAsFirstSibling();
        var bgRt = churchBg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImage = churchBg.GetComponent<Image>();
        bgImage.color = Color.white;
        bgImage.preserveAspect = true;
        Sprite churchSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/SomersetChurchInterior.jpg");
        if (churchSprite != null) bgImage.sprite = churchSprite;
        else
        {
            // Fallback tint until Unity imports the jpg as a Sprite.
            bgImage.color = new Color(0.12f, 0.11f, 0.14f, 1f);
            Debug.LogWarning("[DemonKnight] Set SomersetChurchInterior.jpg Texture Type to Sprite (2D and UI), then re-assign on ChurchBackground.");
        }

        GameObject seatLayer = new GameObject("PewSeatLayer", typeof(RectTransform));
        seatLayer.transform.SetParent(churchBg.transform, false);
        var seatRt = seatLayer.GetComponent<RectTransform>();
        seatRt.anchorMin = Vector2.zero;
        seatRt.anchorMax = Vector2.one;
        seatRt.offsetMin = Vector2.zero;
        seatRt.offsetMax = Vector2.zero;

        pewLayout.backgroundRect = bgRt;
        churchUI.churchBackgroundImage = bgImage;
        churchUI.pewLayout = pewLayout;
        churchUI.pewSeatLayer = seatRt;
        churchUI.paperDollSeatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PaperDollSeat.prefab");

        // Camelot crowd buttons land on the same pew seat layer (not a separate grid panel).
        townUI.pewLayout = pewLayout;
        townUI.townGridContainer = seatRt;
        townUI.recruitAvatarButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RecruitAvatarButton.prefab");

        // Top HUD bar — HorizontalLayoutGroup + preferred widths (tip fix-hud-bar-button-layout).
        // EnsureTopHubLayout reinforces the same spacing after Play / hierarchy rebuild.
        GameObject hud = CreatePanel(townCanvasObj.transform, "HUD_Bar",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 84f),
            new Color(0.08f, 0.06f, 0.05f, 0.92f));
        var hudRt = hud.GetComponent<RectTransform>();
        hudRt.offsetMin = new Vector2(8f, hudRt.offsetMin.y);
        hudRt.offsetMax = new Vector2(-8f, -6f);
        hudRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 84f);
        var hudHlg = hud.AddComponent<HorizontalLayoutGroup>();
        hudHlg.padding = new RectOffset(12, 12, 10, 10);
        hudHlg.spacing = 10f;
        hudHlg.childAlignment = TextAnchor.MiddleLeft;
        hudHlg.childControlWidth = true;
        hudHlg.childControlHeight = true;
        hudHlg.childForceExpandWidth = false;
        hudHlg.childForceExpandHeight = false;

        var menuHudBtn = CreateButton(hud.transform, "MenuButton", "☰ Esc",
            Vector2.zero, new Vector2(110f, 56f), new Color(0.18f, 0.22f, 0.28f));
        AddHudLayoutElement(menuHudBtn.gameObject, 110f, 56f);

        var nightHud = CreateTMP(hud.transform, "NightText", "Night 1", 20, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(200f, 40f));
        AddHudLayoutElement(nightHud.gameObject, 200f, 56f);
        townUI.timerText = CreateTMP(hud.transform, "TimerText", "Horde Arrives: 07:00", 16, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(200f, 40f));
        AddHudLayoutElement(townUI.timerText.gameObject, 200f, 56f);
        townUI.goldText = CreateTMP(hud.transform, "GoldText", "Royal Gold: 200g", 16, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(150f, 40f));
        AddHudLayoutElement(townUI.goldText.gameObject, 150f, 56f);

        var diceHudBtn = CreateButton(hud.transform, "DiceButton", "Dice",
            Vector2.zero, new Vector2(120f, 56f), new Color(0.35f, 0.22f, 0.12f));
        AddHudLayoutElement(diceHudBtn.gameObject, 120f, 56f);
        var warbandBtn = CreateButton(hud.transform, "WarbandButton", "Warband (0)",
            Vector2.zero, new Vector2(160f, 56f), new Color(0.22f, 0.28f, 0.22f));
        AddHudLayoutElement(warbandBtn.gameObject, 160f, 56f);
        townUI.partyCountText = warbandBtn.GetComponentInChildren<TextMeshProUGUI>();
        townUI.goToWarButton = CreateButton(hud.transform, "GoToWarButton", "MARCH TO WAR",
            Vector2.zero, new Vector2(170f, 56f), new Color(0.55f, 0.12f, 0.12f));
        AddHudLayoutElement(townUI.goToWarButton.gameObject, 170f, 56f);

        // Tavern Dice — centered popup modal (opened from HUD Dice button / hotkey T).
        GameObject dicePanel = CreatePanel(townCanvasObj.transform, "Panel_TavernDice",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 360f),
            new Color(0.16f, 0.10f, 0.08f, 0.97f));
        CreateTMP(dicePanel.transform, "DiceTitle", "TAVERN DICE", 24, TextAlignmentOptions.Center,
            new Vector2(0f, 140f), new Vector2(360f, 40f));
        townUI.diceResultText = CreateTMP(dicePanel.transform, "DiceResult", "Ante up 5g to play.", 18, TextAlignmentOptions.Center,
            new Vector2(0f, 70f), new Vector2(360f, 50f));
        townUI.currentPotText = CreateTMP(dicePanel.transform, "CurrentPot", "Pot: 0g", 20, TextAlignmentOptions.Center,
            new Vector2(0f, 20f), new Vector2(360f, 36f));
        townUI.startDiceButton = CreateButton(dicePanel.transform, "StartDice", "Start (5g)",
            new Vector2(-110f, -50f), new Vector2(140f, 44f), new Color(0.35f, 0.28f, 0.12f));
        townUI.rollDiceButton = CreateButton(dicePanel.transform, "RollDice", "Roll",
            new Vector2(0f, -50f), new Vector2(100f, 44f), new Color(0.30f, 0.22f, 0.12f));
        townUI.cashOutButton = CreateButton(dicePanel.transform, "CashOut", "Cash Out",
            new Vector2(110f, -50f), new Vector2(140f, 44f), new Color(0.15f, 0.35f, 0.18f));
        townUI.tavernRoundButton = CreateButton(dicePanel.transform, "TavernRound", "Buy Round (50g)",
            new Vector2(0f, -110f), new Vector2(220f, 40f), new Color(0.40f, 0.25f, 0.10f));
        var diceClose = CreateButton(dicePanel.transform, "DiceCloseX", "X",
            new Vector2(190f, 150f), new Vector2(36f, 36f), new Color(0.45f, 0.12f, 0.12f));
        var diceMin = CreateButton(dicePanel.transform, "DiceMinimize", "—",
            new Vector2(148f, 150f), new Vector2(36f, 36f), new Color(0.25f, 0.22f, 0.18f));
        townUI.diceGame = dice;
        dicePanel.SetActive(false);

        // Legacy side-panel crowd (kept for reference, disabled — seats live on PewSeatLayer).
        GameObject crowdPanel = CreatePanel(townCanvasObj.transform, "Panel_TownCrowd_Legacy",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-360f, -280f), new Vector2(680f, 420f),
            new Color(0.12f, 0.14f, 0.12f, 0.92f));
        crowdPanel.SetActive(false);

        // Larger unit stats panel (floats upper-right of selected pew at runtime).
        // No ChurchBottomDialogue — Andrew removed the bottom NPC quote bar.
        GameObject dossier = CreatePanel(townCanvasObj.transform, "Panel_Dossier",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(200f, 40f), new Vector2(520f, 560f),
            new Color(0.10f, 0.09f, 0.08f, 0.96f));
        dossier.SetActive(false);

        GameObject avatarObj = new GameObject("SelectedAvatar", typeof(RectTransform), typeof(Image));
        avatarObj.transform.SetParent(dossier.transform, false);
        var avatarRt = avatarObj.GetComponent<RectTransform>();
        avatarRt.anchorMin = avatarRt.anchorMax = new Vector2(0f, 0f);
        avatarRt.pivot = new Vector2(0f, 0f);
        avatarRt.anchoredPosition = new Vector2(16f, 420f);
        avatarRt.sizeDelta = new Vector2(100f, 100f);
        townUI.selectedAvatarImage = avatarObj.GetComponent<Image>();
        townUI.selectedAvatarImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        townUI.selectedAvatarImage.enabled = false;

        // Status feedback on the panel (not a bottom-of-screen quote bar).
        townUI.characterDialogueText = CreateTMP(dossier.transform, "StatusLine",
            "", 15, TextAlignmentOptions.Left,
            new Vector2(16f, 8f), new Vector2(488f, 28f));
        var statusRt = townUI.characterDialogueText.rectTransform;
        statusRt.anchorMin = statusRt.anchorMax = new Vector2(0f, 0f);
        statusRt.pivot = new Vector2(0f, 0f);

        townUI.dossierStatsText = CreateTMP(dossier.transform, "DossierStats",
            "Click a pew citizen for hire / train / intel.", 15, TextAlignmentOptions.TopLeft,
            new Vector2(128f, 280f), new Vector2(376f, 240f));
        var statsRt = townUI.dossierStatsText.rectTransform;
        statsRt.anchorMin = statsRt.anchorMax = new Vector2(0f, 0f);
        statsRt.pivot = new Vector2(0f, 0f);

        const float pad = 16f;
        const float gap = 12f;
        float inner = 520f - pad * 2f;
        float third = (inner - gap * 2f) / 3f;

        townUI.tier1RevealButton = CreateButton(dossier.transform, "ScoutBtn", "Scout 10g",
            new Vector2(pad, 220f), new Vector2(third, 40f), new Color(0.25f, 0.28f, 0.35f));
        townUI.tier2RevealButton = CreateButton(dossier.transform, "InvestigateBtn", "Investigate 20g",
            new Vector2(pad + third + gap, 220f), new Vector2(third, 40f), new Color(0.25f, 0.28f, 0.35f));
        townUI.tier3RevealButton = CreateButton(dossier.transform, "FullReportBtn", "Full Report 30g",
            new Vector2(pad + (third + gap) * 2f, 220f), new Vector2(third, 40f), new Color(0.25f, 0.28f, 0.35f));
        townUI.hireRecruitButton = CreateButton(dossier.transform, "HireBtn", "Recruit",
            new Vector2(pad, 160f), new Vector2(inner, 44f), new Color(0.18f, 0.40f, 0.22f));
        townUI.hireButtonText = townUI.hireRecruitButton.GetComponentInChildren<TextMeshProUGUI>();

        townUI.trainRangedButton = CreateButton(dossier.transform, "TrainATK", "ATK 50g",
            new Vector2(pad, 100f), new Vector2(third, 44f), new Color(0.20f, 0.30f, 0.35f));
        townUI.trainTankButton = CreateButton(dossier.transform, "TrainHP", "HP 50g",
            new Vector2(pad + third + gap, 100f), new Vector2(third, 44f), new Color(0.35f, 0.22f, 0.15f));
        townUI.trainSpeedButton = CreateButton(dossier.transform, "TrainSPD", "SPD 50g",
            new Vector2(pad + (third + gap) * 2f, 100f), new Vector2(third, 44f), new Color(0.22f, 0.28f, 0.18f));
        townUI.mediateRivalryButton = CreateButton(dossier.transform, "Mediate", "Mediate",
            new Vector2(pad, 48f), new Vector2(inner, 40f), new Color(0.30f, 0.20f, 0.35f));

        // Legend Codex modal — near full-screen so all 10 recipes + Fuse buttons fit.
        GameObject codexPanel = CreatePanel(townCanvasObj.transform, "CodexPanel",
            new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero,
            new Color(0.07f, 0.08f, 0.10f, 0.98f));
        codexPanel.SetActive(false);
        // Title object kept for Find() compat but inactive (Andrew: drop Legends title panel).
        var codexTitle = CreateTMP(codexPanel.transform, "CodexTitle", "LEGENDS", 28, TextAlignmentOptions.Center,
            new Vector2(0f, -18f), new Vector2(0f, 48f));
        var codexTitleRt = codexTitle.rectTransform;
        codexTitleRt.anchorMin = new Vector2(0.05f, 1f);
        codexTitleRt.anchorMax = new Vector2(0.95f, 1f);
        codexTitleRt.pivot = new Vector2(0.5f, 1f);
        codexTitle.gameObject.SetActive(false);
        CreateButton(codexPanel.transform, "CodexCloseX", "X",
            new Vector2(-12f, -12f), new Vector2(44f, 44f), new Color(0.45f, 0.12f, 0.12f));
        var codexCloseRt = codexPanel.transform.Find("CodexCloseX") as RectTransform;
        if (codexCloseRt != null)
        {
            codexCloseRt.anchorMin = codexCloseRt.anchorMax = new Vector2(1f, 1f);
            codexCloseRt.pivot = new Vector2(1f, 1f);
        }

        // Scrollable recipe list inside the large modal.
        var scrollHost = new GameObject("CodexScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        scrollHost.transform.SetParent(codexPanel.transform, false);
        var scrollHostRt = scrollHost.GetComponent<RectTransform>();
        scrollHostRt.anchorMin = new Vector2(0.04f, 0.06f);
        scrollHostRt.anchorMax = new Vector2(0.96f, 0.895f);
        scrollHostRt.offsetMin = Vector2.zero;
        scrollHostRt.offsetMax = Vector2.zero;
        scrollHost.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
        scrollHost.GetComponent<Mask>().showMaskGraphic = false;

        GameObject recipeList = new GameObject("RecipeList", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        recipeList.transform.SetParent(scrollHost.transform, false);
        var recipeRt = recipeList.GetComponent<RectTransform>();
        recipeRt.anchorMin = new Vector2(0f, 1f);
        recipeRt.anchorMax = new Vector2(1f, 1f);
        recipeRt.pivot = new Vector2(0.5f, 1f);
        recipeRt.anchoredPosition = Vector2.zero;
        recipeRt.sizeDelta = new Vector2(0f, 0f);
        // One full-width column — taller rows for face portrait + readable lore.
        var grid = recipeList.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.cellSize = new Vector2(980f, 268f);
        grid.spacing = new Vector2(0f, 14f);
        grid.padding = new RectOffset(14, 14, 10, 12);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.startAxis = GridLayoutGroup.Axis.Vertical;
        recipeList.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var hostScroll = scrollHost.GetComponent<ScrollRect>();
        hostScroll.content = recipeRt;
        hostScroll.viewport = scrollHostRt;
        hostScroll.horizontal = false;
        hostScroll.vertical = true;
        hostScroll.movementType = ScrollRect.MovementType.Clamped;

        codex.codexPanel = codexPanel;
        codex.recipeListContainer = recipeList.transform;
        codex.recipeRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CodexRecipeRow.prefab");
        codex.toggleCodexButton = CreateButton(hud.transform, "CodexToggle", "Legends",
            Vector2.zero, new Vector2(120f, 56f), new Color(0.20f, 0.25f, 0.40f));
        AddHudLayoutElement(codex.toggleCodexButton.gameObject, 120f, 56f);
        // Sibling order: Menu | Night | Timer | Gold | Dice | Legends | Warband | March
        menuHudBtn.transform.SetSiblingIndex(0);
        nightHud.transform.SetSiblingIndex(1);
        townUI.timerText.transform.SetSiblingIndex(2);
        townUI.goldText.transform.SetSiblingIndex(3);
        diceHudBtn.transform.SetSiblingIndex(4);
        codex.toggleCodexButton.transform.SetSiblingIndex(5);
        warbandBtn.transform.SetSiblingIndex(6);
        townUI.goToWarButton.transform.SetSiblingIndex(7);
        SeedDefaultRecipes(codex);

        // Warband formation modal (opened from the evenly-spaced Warband HUD button)
        GameObject warbandPanel = CreatePanel(townCanvasObj.transform, "WarbandPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 480f),
            new Color(0.07f, 0.09f, 0.08f, 0.98f));
        warbandPanel.SetActive(false);
        // Title kept for Find() compat but inactive (Andrew: drop Warband title panel).
        var warbandTitle = CreateTMP(warbandPanel.transform, "WarbandTitle", "WARBAND", 24, TextAlignmentOptions.Center,
            new Vector2(0f, 200f), new Vector2(600f, 36f));
        warbandTitle.gameObject.SetActive(false);
        // Scroll host + list (SomersetChurchUI.EnsureWarbandModal will reinforce at runtime).
        GameObject warbandScroll = new GameObject("WarbandScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        warbandScroll.transform.SetParent(warbandPanel.transform, false);
        var warbandScrollRt = warbandScroll.GetComponent<RectTransform>();
        warbandScrollRt.anchorMin = new Vector2(0.04f, 0.06f);
        warbandScrollRt.anchorMax = new Vector2(0.96f, 0.86f);
        warbandScrollRt.offsetMin = Vector2.zero;
        warbandScrollRt.offsetMax = Vector2.zero;
        warbandScroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
        warbandScroll.GetComponent<Mask>().showMaskGraphic = false;
        GameObject warbandList = new GameObject("WarbandList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        warbandList.transform.SetParent(warbandScroll.transform, false);
        var warbandListRt = warbandList.GetComponent<RectTransform>();
        warbandListRt.anchorMin = new Vector2(0f, 1f);
        warbandListRt.anchorMax = new Vector2(1f, 1f);
        warbandListRt.pivot = new Vector2(0.5f, 1f);
        warbandListRt.anchoredPosition = Vector2.zero;
        warbandListRt.sizeDelta = new Vector2(0f, 0f);
        var warbandVlg = warbandList.GetComponent<VerticalLayoutGroup>();
        warbandVlg.spacing = 8f;
        warbandVlg.padding = new RectOffset(8, 8, 8, 8);
        warbandVlg.childForceExpandHeight = false;
        warbandVlg.childControlHeight = true;
        warbandVlg.childForceExpandWidth = true;
        warbandList.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var warbandScrollRect = warbandScroll.GetComponent<ScrollRect>();
        warbandScrollRect.content = warbandListRt;
        warbandScrollRect.viewport = warbandScrollRt;
        warbandScrollRect.horizontal = false;
        warbandScrollRect.vertical = true;

        // Perk draft UI removed till further notice — leave manager stub with no panel.
        perks.perkDraftPanel = null;
        perks.choiceButtons = null;
        perks.choiceTitleTexts = null;
        perks.choiceDescTexts = null;

        // Merlin Resurrection Canvas
        GameObject resCanvasObj = CreateCanvas("ResurrectionCanvas", 5000);
        resCanvasObj.SetActive(false);
        GameObject resPanel = CreatePanel(resCanvasObj.transform, "ResurrectionPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 520f),
            new Color(0.05f, 0.06f, 0.12f, 0.98f));
        var lore = CreateTMP(resPanel.transform, "LoreText",
            "The Grail Nightmare… Broken Vigil…", 20, TextAlignmentOptions.Center,
            new Vector2(0f, 160f), new Vector2(820f, 120f));

        // Legacy bank panel removed — Next Night advances the night; End Run → Main Menu.
        // Runtime MerlinResurrectionManager also hides any leftover LegacyBankPanel in Downloads scenes.
        var nextNight = CreateButton(resPanel.transform, "NextNight", "Next Night",
            new Vector2(170f, -200f), new Vector2(320f, 72f), new Color(0.72f, 0.52f, 0.12f));
        var endRun = CreateButton(resPanel.transform, "EndRun", "End Run",
            new Vector2(-170f, -200f), new Vector2(320f, 72f), new Color(0.55f, 0.18f, 0.16f));
        // Wired at runtime (single listener) — do not add persistent Next Night here.
        _ = nextNight;
        _ = endRun;

        merlin.resurrectionCanvas = resCanvasObj;
        merlin.loreNarrativeText = lore;
        merlin.legacyBankPanel = null;
        merlin.pointsAvailableText = null;
        merlin.goldBankText = null;
        merlin.discountBankText = null;
        merlin.rationsBankText = null;

        // Wire GameLoop + Church Stage
        loop.rngGenerator = rng;
        loop.townUI = townUI;
        loop.churchUI = churchUI;
        loop.battleEngine = battle;
        loop.townCanvasObj = townCanvasObj;
        loop.battlefieldArenaObj = arena;

        churchUI.nightText = nightHud;
        churchUI.timerText = townUI.timerText;
        churchUI.goldText = townUI.goldText;
        churchUI.warbandCountText = townUI.partyCountText;
        churchUI.goToWarButton = townUI.goToWarButton;
        churchUI.codexButton = codex.toggleCodexButton;
        churchUI.warbandButton = warbandBtn;
        churchUI.tavernDiceButton = diceHudBtn;
        churchUI.buyRoundButton = townUI.tavernRoundButton;
        churchUI.diceModal = dicePanel;
        churchUI.codexModal = codexPanel;
        churchUI.warbandModal = warbandPanel;
        churchUI.modalCloseButtons = new[] { diceClose, diceMin };
        churchUI.warbandListContainer = warbandList.transform;
        churchUI.dossierModal = dossier;
        // Dossier content lives on the shared floating stats panel (wired on townUI).
        churchUI.dossierDialogue = townUI.characterDialogueText;
        churchUI.dossierStats = townUI.dossierStatsText;
        churchUI.dossierPortrait = townUI.selectedAvatarImage;
        churchUI.hireButton = townUI.hireRecruitButton;
        churchUI.hireButtonLabel = townUI.hireButtonText;
        churchUI.tier1Button = townUI.tier1RevealButton;
        churchUI.tier2Button = townUI.tier2RevealButton;
        churchUI.tier3Button = townUI.tier3RevealButton;
        churchUI.trainTankButton = townUI.trainTankButton;
        churchUI.trainRangedButton = townUI.trainRangedButton;
        churchUI.trainSpeedButton = townUI.trainSpeedButton;
        churchUI.mediateButton = townUI.mediateRivalryButton;

        // Camera framing for 2D battlefield
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.backgroundColor = new Color(0.08f, 0.07f, 0.09f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Last Knights of Somerset",
            "Scene built at Assets/Scenes/CamelotMain.unity\n\n" +
            "Church background + 12-pew seat layout (3×4) wired.\n" +
            "If the background is blank: select Assets/Art/Backgrounds/SomersetChurchInterior.jpg → Texture Type = Sprite (2D and UI) → Apply, then re-run this menu or assign the sprite on TownCanvas/ChurchBackground.\n" +
            "Tune pew anchors on TownCanvas → SomersetPewLayout if seats need nudging.",
            "OK");

        Debug.Log("[DemonKnight] Somerset church scene built with 12-pew layout → " + ScenePath);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
    }

    private static void CreateRuntimePrefabs()
    {
        // Recruit avatar button
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RecruitAvatarButton.prefab") == null)
        {
            GameObject btn = new GameObject("RecruitAvatarButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btn.GetComponent<Image>().color = new Color(0.45f, 0.40f, 0.32f, 1f);
            PrefabUtility.SaveAsPrefabAsset(btn, "Assets/Prefabs/RecruitAvatarButton.prefab");
            Object.DestroyImmediate(btn);
        }

        // Paper-doll pew seat (3 layers)
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PaperDollSeat.prefab") == null)
        {
            GameObject seat = new GameObject("PaperDollSeat", typeof(RectTransform), typeof(Button));
            var srt = seat.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(56f, 72f);

            GameObject body = new GameObject("Body", typeof(RectTransform), typeof(Image));
            body.transform.SetParent(seat.transform, false);
            StretchFull(body.GetComponent<RectTransform>());
            body.GetComponent<Image>().color = new Color(0.55f, 0.48f, 0.38f, 1f);

            GameObject head = new GameObject("Head", typeof(RectTransform), typeof(Image));
            head.transform.SetParent(seat.transform, false);
            var hrt = head.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.2f, 0.55f);
            hrt.anchorMax = new Vector2(0.8f, 1f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;

            GameObject mood = new GameObject("Mood", typeof(RectTransform), typeof(Image));
            mood.transform.SetParent(seat.transform, false);
            var mrt = mood.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.65f, 0.7f);
            mrt.anchorMax = new Vector2(1f, 1f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            mood.GetComponent<Image>().enabled = false;

            var view = seat.AddComponent<PaperDollSeatView>();
            view.bodyLayer = body.GetComponent<Image>();
            view.headLayer = head.GetComponent<Image>();
            view.moodLayer = mood.GetComponent<Image>();
            view.seatButton = seat.GetComponent<Button>();

            PrefabUtility.SaveAsPrefabAsset(seat, "Assets/Prefabs/PaperDollSeat.prefab");
            Object.DestroyImmediate(seat);
        }

        // Unit visual actor
        if (AssetDatabase.LoadAssetAtPath<UnitVisualActor>("Assets/Prefabs/UnitVisualActor.prefab") == null)
        {
            GameObject actor = new GameObject("UnitVisualActor");
            var sr = actor.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;
            var uva = actor.AddComponent<UnitVisualActor>();
            uva.spriteRenderer = sr;

            GameObject canvas = new GameObject("HPCanvas", typeof(Canvas));
            canvas.transform.SetParent(actor.transform);
            canvas.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            canvas.transform.localScale = Vector3.one * 0.01f;
            var c = canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 10;

            GameObject sliderObj = new GameObject("HealthBar", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(canvas.transform, false);
            var srt = sliderObj.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(100f, 12f);
            var slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 100;
            uva.healthBar = slider;

            PrefabUtility.SaveAsPrefabAsset(actor, "Assets/Prefabs/UnitVisualActor.prefab");
            Object.DestroyImmediate(actor);
        }

        // Floating combat text
        if (AssetDatabase.LoadAssetAtPath<FloatingCombatText>("Assets/Prefabs/FloatingCombatText.prefab") == null)
        {
            GameObject fct = new GameObject("FloatingCombatText");
            var canvas = fct.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            fct.transform.localScale = Vector3.one * 0.01f;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(fct.transform, false);
            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36;
            tmp.rectTransform.sizeDelta = new Vector2(200f, 60f);

            var comp = fct.AddComponent<FloatingCombatText>();
            comp.popupText = tmp;

            PrefabUtility.SaveAsPrefabAsset(fct, "Assets/Prefabs/FloatingCombatText.prefab");
            Object.DestroyImmediate(fct);
        }

        // Codex recipe row — face portrait frame + named lore labels (runtime EnsureRowChrome fills scroll).
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CodexRecipeRow.prefab") == null)
        {
            GameObject row = new GameObject("CodexRecipeRow", typeof(RectTransform), typeof(Image));
            row.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.20f, 1f);

            var frame = new GameObject("FacePortraitFrame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(row.transform, false);
            var frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0.5f);
            frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(12f, 0f);
            frt.sizeDelta = new Vector2(128f, 128f);
            frame.GetComponent<Image>().color = new Color(0.42f, 0.36f, 0.28f, 0.95f);
            var face = new GameObject("FacePortrait", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(frame.transform, false);
            var faceRt = face.GetComponent<RectTransform>();
            faceRt.anchorMin = Vector2.zero;
            faceRt.anchorMax = Vector2.one;
            faceRt.offsetMin = new Vector2(4f, 4f);
            faceRt.offsetMax = new Vector2(-4f, -4f);
            face.GetComponent<Image>().color = new Color(0.28f, 0.30f, 0.36f, 0.92f);
            face.GetComponent<Image>().preserveAspect = true;

            string[] labels = { "Name", "Recipe", "Lore" };
            for (int i = 0; i < labels.Length; i++)
            {
                GameObject t = new GameObject(labels[i], typeof(RectTransform), typeof(TextMeshProUGUI));
                t.transform.SetParent(row.transform, false);
                var tmp = t.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = i == 0 ? 18 : 14;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.text = labels[i];
                tmp.textWrappingMode = TextWrappingModes.Normal;
            }

            GameObject fuse = new GameObject("FuseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            fuse.transform.SetParent(row.transform, false);
            fuse.GetComponent<Image>().color = new Color(0.35f, 0.25f, 0.15f);
            GameObject fuseLabel = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            fuseLabel.transform.SetParent(fuse.transform, false);
            var fl = fuseLabel.GetComponent<TextMeshProUGUI>();
            fl.text = "FUSE";
            fl.alignment = TextAlignmentOptions.Center;
            fl.fontSize = 14;
            fl.rectTransform.anchorMin = Vector2.zero;
            fl.rectTransform.anchorMax = Vector2.one;
            fl.rectTransform.offsetMin = Vector2.zero;
            fl.rectTransform.offsetMax = Vector2.zero;

            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 260f;
            le.preferredHeight = 268f;
            le.flexibleWidth = 1f;

            PrefabUtility.SaveAsPrefabAsset(row, "Assets/Prefabs/CodexRecipeRow.prefab");
            Object.DestroyImmediate(row);
        }
    }

    private static void SeedDefaultRecipes(LegendCodexUI codex)
    {
        codex.allRecipes = new List<CodexRecipeEntry>
        {
            new CodexRecipeEntry { heroName = "Town Priest", resultingType = UnitType.Priest, ingredientFormula = "2 Peasants", combatPerkSummary = "Heals lowest ally, else weak attack" },
            new CodexRecipeEntry { heroName = "Vanguard Captain", resultingType = UnitType.Captain, ingredientFormula = "2 Warriors", combatPerkSummary = "+0.25 APS to whole army" },
            new CodexRecipeEntry { heroName = "Sir Kay", resultingType = UnitType.SirKay, ingredientFormula = "1 Warrior + 2 Peasants", combatPerkSummary = "Early bridge tank" },
            new CodexRecipeEntry { heroName = "Sir Bedivere", resultingType = UnitType.SirBedivere, ingredientFormula = "1 Knight + 1 Warrior", combatPerkSummary = "Loyalist frontline" },
            new CodexRecipeEntry { heroName = "Sir Percival", resultingType = UnitType.SirPercival, ingredientFormula = "1 Zealot Knight + 1 Peasant", combatPerkSummary = "Zealot tank" },
            new CodexRecipeEntry { heroName = "Sir Tristan", resultingType = UnitType.SirTristan, ingredientFormula = "1 Knight + 1 Peasant (Outlaw/Noble)", combatPerkSummary = "Bow — attacks from BACK" },
            new CodexRecipeEntry { heroName = "Guinevere", resultingType = UnitType.Guinevere, ingredientFormula = "1 Noble + 1 Priest", combatPerkSummary = "+15% ATK aura to Loyalist/Noble allies" },
            new CodexRecipeEntry { heroName = "King Arthur", resultingType = UnitType.KingArthur, ingredientFormula = "2 Knights + 1 Captain", combatPerkSummary = "Cleaves 2 foes; blocks Nimue seduction" },
            new CodexRecipeEntry { heroName = "Sir Lancelot", resultingType = UnitType.SirLancelot, ingredientFormula = "2 Knights (incl. 1 Noble) + 1 Warrior", combatPerkSummary = "+5 ATK per fallen ally" },
            new CodexRecipeEntry { heroName = "Morgan le Fay", resultingType = UnitType.MorganLeFay, ingredientFormula = "2 Priests + 1 Outlaw", combatPerkSummary = "Hexes targets (halve APS)" },
            new CodexRecipeEntry { heroName = "Lady of the Lake", resultingType = UnitType.LadyOfTheLake, ingredientFormula = "2 Priests + 1 Captain", combatPerkSummary = "Shields / Chalice synergy" },
            new CodexRecipeEntry { heroName = "Sir Gawain", resultingType = UnitType.SirGawain, ingredientFormula = "2 Warriors + 1 Knight", combatPerkSummary = "Double damage for first 8s" },
            new CodexRecipeEntry { heroName = "Mordred", resultingType = UnitType.Mordred, ingredientFormula = "2 Knights + 1 Outlaw", combatPerkSummary = "+15 vs bosses; demoralizes Nobles & Loyalists; persists until killed" },
            new CodexRecipeEntry { heroName = "Sir Galahad", resultingType = UnitType.SirGalahad, ingredientFormula = "1 Zealot/Loyalist Knight + 1 Priest + 1 Warrior", combatPerkSummary = "Lifesteal to allies; blocks seduction" },
        };
        EditorUtility.SetDirty(codex);
    }

    private static T CreateChild<T>(GameObject parent, string name) where T : Component
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        return go.AddComponent<T>();
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }


    private static void AddHudLayoutElement(GameObject go, float preferredWidth, float preferredHeight)
    {
        if (go == null) return;
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = false;
        le.minWidth = 48f;
        le.preferredWidth = preferredWidth;
        le.flexibleWidth = 0f;
        le.minHeight = 40f;
        le.preferredHeight = preferredHeight;
        le.flexibleHeight = 0f;
    }

    private static GameObject CreateCanvas(string name, int sortOrder)
    {
        GameObject canvasObj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasObj;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float size, TextAlignmentOptions align, Vector2 pos, Vector2 dim)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = new Color(0.92f, 0.88f, 0.78f);
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 dim, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;
        go.GetComponent<Image>().color = color;

        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(go.transform, false);
        var trt = textObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go.GetComponent<Button>();
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        es.name = "EventSystem";
    }

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes)
        {
            if (s.path == path) return;
        }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
