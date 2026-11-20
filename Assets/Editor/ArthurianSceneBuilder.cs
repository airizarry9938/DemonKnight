#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

        // Top HUD bar — full-bleed strip; EnsureTopHubLayout redistributes children at runtime.
        GameObject hud = CreatePanel(townCanvasObj.transform, "HUD_Bar",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 72f),
            new Color(0.08f, 0.06f, 0.05f, 0.92f));
        var hudRt = hud.GetComponent<RectTransform>();
        hudRt.offsetMin = new Vector2(12f, hudRt.offsetMin.y);
        hudRt.offsetMax = new Vector2(-12f, -6f);
        hudRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 72f);

        var nightHud = CreateTMP(hud.transform, "NightText", "Night 1", 20, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(100f, 40f));
        townUI.timerText = CreateTMP(hud.transform, "TimerText", "Horde Arrives: 07:00", 16, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(200f, 40f));
        townUI.goldText = CreateTMP(hud.transform, "GoldText", "Royal Gold: 150g", 16, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(150f, 40f));

        var diceHudBtn = CreateButton(hud.transform, "DiceButton", "Dice",
            Vector2.zero, new Vector2(120f, 44f), new Color(0.35f, 0.22f, 0.12f));
        var warbandBtn = CreateButton(hud.transform, "WarbandButton", "Warband (0)",
            Vector2.zero, new Vector2(160f, 44f), new Color(0.22f, 0.28f, 0.22f));
        townUI.partyCountText = warbandBtn.GetComponentInChildren<TextMeshProUGUI>();
        townUI.goToWarButton = CreateButton(hud.transform, "GoToWarButton", "MARCH TO WAR",
            Vector2.zero, new Vector2(170f, 48f), new Color(0.55f, 0.12f, 0.12f));

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

        // BOTTOM: Dossier / Training / Mediation
        GameObject dossier = CreatePanel(townCanvasObj.transform, "Panel_Dossier",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(1180f, 280f),
            new Color(0.10f, 0.09f, 0.08f, 0.96f));

        GameObject avatarObj = new GameObject("SelectedAvatar", typeof(RectTransform), typeof(Image));
        avatarObj.transform.SetParent(dossier.transform, false);
        var avatarRt = avatarObj.GetComponent<RectTransform>();
        avatarRt.anchoredPosition = new Vector2(-480f, 20f);
        avatarRt.sizeDelta = new Vector2(120f, 120f);
        townUI.selectedAvatarImage = avatarObj.GetComponent<Image>();
        townUI.selectedAvatarImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        townUI.selectedAvatarImage.enabled = false;

        townUI.characterDialogueText = CreateTMP(dossier.transform, "Dialogue",
            "The tavern murmurs. Select a citizen.", 18, TextAlignmentOptions.Left,
            new Vector2(-180f, 80f), new Vector2(520f, 70f));
        townUI.dossierStatsText = CreateTMP(dossier.transform, "DossierStats",
            "Select a citizen on the right to inspect, train, mediate, or recruit.", 16, TextAlignmentOptions.TopLeft,
            new Vector2(-180f, -10f), new Vector2(520f, 120f));

        townUI.tier1RevealButton = CreateButton(dossier.transform, "ScoutBtn", "Scout 10g",
            new Vector2(280f, 90f), new Vector2(140f, 36f), new Color(0.25f, 0.28f, 0.35f));
        townUI.tier2RevealButton = CreateButton(dossier.transform, "InvestigateBtn", "Investigate 50g",
            new Vector2(430f, 90f), new Vector2(160f, 36f), new Color(0.25f, 0.28f, 0.35f));
        townUI.tier3RevealButton = CreateButton(dossier.transform, "DossierBtn", "Dossier 100g",
            new Vector2(280f, 45f), new Vector2(140f, 36f), new Color(0.25f, 0.28f, 0.35f));
        townUI.hireRecruitButton = CreateButton(dossier.transform, "HireBtn", "Recruit",
            new Vector2(430f, 45f), new Vector2(160f, 36f), new Color(0.18f, 0.40f, 0.22f));
        townUI.hireButtonText = townUI.hireRecruitButton.GetComponentInChildren<TextMeshProUGUI>();

        townUI.trainTankButton = CreateButton(dossier.transform, "TrainTank", "Train Tank 15g",
            new Vector2(280f, -10f), new Vector2(160f, 36f), new Color(0.35f, 0.22f, 0.15f));
        townUI.trainRangedButton = CreateButton(dossier.transform, "TrainRanged", "Train Ranged 15g",
            new Vector2(450f, -10f), new Vector2(170f, 36f), new Color(0.20f, 0.30f, 0.35f));
        townUI.mediateRivalryButton = CreateButton(dossier.transform, "Mediate", "Mediate 40g",
            new Vector2(360f, -60f), new Vector2(180f, 36f), new Color(0.30f, 0.20f, 0.35f));

        // Legend Codex modal — near full-screen so all 10 recipes + Fuse buttons fit.
        GameObject codexPanel = CreatePanel(townCanvasObj.transform, "CodexPanel",
            new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero,
            new Color(0.07f, 0.08f, 0.10f, 0.98f));
        codexPanel.SetActive(false);
        var codexTitle = CreateTMP(codexPanel.transform, "CodexTitle", "LEGEND CODEX — 10 Arthurian Fusions", 28, TextAlignmentOptions.Center,
            new Vector2(0f, -18f), new Vector2(0f, 48f));
        var codexTitleRt = codexTitle.rectTransform;
        codexTitleRt.anchorMin = new Vector2(0.05f, 1f);
        codexTitleRt.anchorMax = new Vector2(0.95f, 1f);
        codexTitleRt.pivot = new Vector2(0.5f, 1f);
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
        scrollHostRt.anchorMax = new Vector2(0.96f, 0.88f);
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
        var grid = recipeList.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2(420f, 96f);
        grid.spacing = new Vector2(16f, 14f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
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
            Vector2.zero, new Vector2(120f, 44f), new Color(0.20f, 0.25f, 0.40f));
        // Sibling order for EnsureTopHubLayout: Night, Timer, Gold, Dice, Legends, Warband, March
        nightHud.transform.SetSiblingIndex(0);
        townUI.timerText.transform.SetSiblingIndex(1);
        townUI.goldText.transform.SetSiblingIndex(2);
        diceHudBtn.transform.SetSiblingIndex(3);
        codex.toggleCodexButton.transform.SetSiblingIndex(4);
        warbandBtn.transform.SetSiblingIndex(5);
        townUI.goToWarButton.transform.SetSiblingIndex(6);
        SeedDefaultRecipes(codex);

        // Warband formation modal (opened from the evenly-spaced Warband HUD button)
        GameObject warbandPanel = CreatePanel(townCanvasObj.transform, "WarbandPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 480f),
            new Color(0.07f, 0.09f, 0.08f, 0.98f));
        warbandPanel.SetActive(false);
        CreateTMP(warbandPanel.transform, "WarbandTitle", "CAMELOT CROWD — CURRENT WARBAND", 24, TextAlignmentOptions.Center,
            new Vector2(0f, 200f), new Vector2(600f, 36f));
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

        // Perk Draft Modal
        GameObject perkPanel = CreatePanel(townCanvasObj.transform, "PerkDraftPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 360f),
            new Color(0.06f, 0.08f, 0.12f, 0.98f));
        perkPanel.SetActive(false);
        CreateTMP(perkPanel.transform, "PerkTitle", "WAVE CLEAR — CHOOSE A RUN PERK", 26, TextAlignmentOptions.Center,
            new Vector2(0f, 140f), new Vector2(860f, 40f));

        Button[] perkButtons = new Button[3];
        TextMeshProUGUI[] perkTitles = new TextMeshProUGUI[3];
        TextMeshProUGUI[] perkDescs = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            float x = -280f + i * 280f;
            GameObject card = CreatePanel(perkPanel.transform, $"PerkChoice_{i}",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -20f), new Vector2(250f, 220f),
                new Color(0.14f, 0.16f, 0.22f, 1f));
            perkTitles[i] = CreateTMP(card.transform, "Title", "Perk", 18, TextAlignmentOptions.Center,
                new Vector2(0f, 70f), new Vector2(230f, 40f));
            perkDescs[i] = CreateTMP(card.transform, "Desc", "Description", 14, TextAlignmentOptions.Center,
                new Vector2(0f, 10f), new Vector2(220f, 90f));
            perkButtons[i] = CreateButton(card.transform, "Select", "Choose",
                new Vector2(0f, -70f), new Vector2(140f, 40f), new Color(0.25f, 0.35f, 0.55f));
        }

        perks.perkDraftPanel = perkPanel;
        perks.choiceButtons = perkButtons;
        perks.choiceTitleTexts = perkTitles;
        perks.choiceDescTexts = perkDescs;

        // Merlin Resurrection Canvas
        GameObject resCanvasObj = CreateCanvas("ResurrectionCanvas", 20);
        resCanvasObj.SetActive(false);
        GameObject resPanel = CreatePanel(resCanvasObj.transform, "ResurrectionPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 520f),
            new Color(0.05f, 0.06f, 0.12f, 0.98f));
        var lore = CreateTMP(resPanel.transform, "LoreText",
            "Merlin turns the hourglass...", 20, TextAlignmentOptions.Center,
            new Vector2(0f, 160f), new Vector2(820f, 120f));

        GameObject bankPanel = CreatePanel(resPanel.transform, "LegacyBankPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 280f),
            new Color(0.10f, 0.12f, 0.18f, 1f));
        bankPanel.SetActive(false);

        var pointsTxt = CreateTMP(bankPanel.transform, "Points", "Legacy Points: 0", 22, TextAlignmentOptions.Center,
            new Vector2(0f, 100f), new Vector2(700f, 36f));
        var goldBank = CreateTMP(bankPanel.transform, "GoldBank", "Royal Treasury", 16, TextAlignmentOptions.Left,
            new Vector2(-120f, 40f), new Vector2(420f, 32f));
        var discountBank = CreateTMP(bankPanel.transform, "DiscountBank", "Royal Writ", 16, TextAlignmentOptions.Left,
            new Vector2(-120f, 0f), new Vector2(420f, 32f));
        var rationsBank = CreateTMP(bankPanel.transform, "RationsBank", "War Rations", 16, TextAlignmentOptions.Left,
            new Vector2(-120f, -40f), new Vector2(420f, 32f));

        var upgradeGold = CreateButton(bankPanel.transform, "UpgradeGold", "+Gold",
            new Vector2(280f, 40f), new Vector2(120f, 32f), new Color(0.45f, 0.35f, 0.12f));
        var upgradeDiscount = CreateButton(bankPanel.transform, "UpgradeDiscount", "+Writ",
            new Vector2(280f, 0f), new Vector2(120f, 32f), new Color(0.25f, 0.30f, 0.45f));
        var upgradeRations = CreateButton(bankPanel.transform, "UpgradeRations", "+Rations",
            new Vector2(280f, -40f), new Vector2(120f, 32f), new Color(0.20f, 0.40f, 0.25f));
        var nextRun = CreateButton(bankPanel.transform, "NextRun", "Enter Next Timeline",
            new Vector2(0f, -100f), new Vector2(260f, 44f), new Color(0.35f, 0.15f, 0.40f));

        // Persist listeners so they survive scene save / Play Mode.
        UnityEventTools.AddPersistentListener(upgradeGold.onClick, merlin.UpgradeStartingGold);
        UnityEventTools.AddPersistentListener(upgradeDiscount.onClick, merlin.UpgradeRecruitmentDiscount);
        UnityEventTools.AddPersistentListener(upgradeRations.onClick, merlin.UpgradeSupplyRations);
        UnityEventTools.AddPersistentListener(nextRun.onClick, merlin.StartNextTimelineRun);

        merlin.resurrectionCanvas = resCanvasObj;
        merlin.loreNarrativeText = lore;
        merlin.legacyBankPanel = bankPanel;
        merlin.pointsAvailableText = pointsTxt;
        merlin.goldBankText = goldBank;
        merlin.discountBankText = discountBank;
        merlin.rationsBankText = rationsBank;

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
        // Dossier content lives on the shared bottom panel (wired on townUI).
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

        // Codex recipe row
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CodexRecipeRow.prefab") == null)
        {
            GameObject row = new GameObject("CodexRecipeRow", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            row.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.20f, 1f);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.spacing = 8f;
            hlg.childForceExpandWidth = true;

            for (int i = 0; i < 3; i++)
            {
                GameObject t = new GameObject($"Text_{i}", typeof(RectTransform), typeof(TextMeshProUGUI));
                t.transform.SetParent(row.transform, false);
                var tmp = t.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 14;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.text = i == 0 ? "Hero" : (i == 1 ? "Recipe" : "Perk");
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
            le.minHeight = 56f;
            le.preferredHeight = 56f;

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
            new CodexRecipeEntry { heroName = "King Arthur", resultingType = UnitType.KingArthur, ingredientFormula = "2 Knights + 1 Captain", combatPerkSummary = "Cleaves 2 foes; blocks Nimue seduction" },
            new CodexRecipeEntry { heroName = "Merlin", resultingType = UnitType.Merlin, ingredientFormula = "3 Priests + 2 Warriors + 2 Knights", combatPerkSummary = "Hits all enemies" },
            new CodexRecipeEntry { heroName = "Sir Lancelot", resultingType = UnitType.SirLancelot, ingredientFormula = "3 Knights (incl. 1 Noble)", combatPerkSummary = "+5 ATK per fallen ally" },
            new CodexRecipeEntry { heroName = "Morgan le Fay", resultingType = UnitType.MorganLeFay, ingredientFormula = "2 Priests + 2 Outlaws", combatPerkSummary = "Hexes targets (halve APS)" },
            new CodexRecipeEntry { heroName = "Lady of the Lake", resultingType = UnitType.LadyOfTheLake, ingredientFormula = "3 Priests + 1 Captain", combatPerkSummary = "Shields 3 weakest (+45)" },
            new CodexRecipeEntry { heroName = "Sir Gawain", resultingType = UnitType.SirGawain, ingredientFormula = "2 Warriors + 2 Knights", combatPerkSummary = "Double damage for first 8s" },
            new CodexRecipeEntry { heroName = "Mordred", resultingType = UnitType.Mordred, ingredientFormula = "2 Knights + 2 Outlaws", combatPerkSummary = "+15 vs bosses; demoralizes Nobles" },
            new CodexRecipeEntry { heroName = "Sir Galahad", resultingType = UnitType.SirGalahad, ingredientFormula = "2 Zealot/Loyalist Knights + 1 Priest", combatPerkSummary = "Lifesteal to allies; blocks seduction" },
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
