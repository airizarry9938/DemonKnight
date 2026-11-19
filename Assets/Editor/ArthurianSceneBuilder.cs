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
        GameObject arena = new GameObject("BattlefieldArena");
        arena.SetActive(false);
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ground.name = "ArenaGround";
        ground.transform.SetParent(arena.transform);
        ground.transform.localPosition = Vector3.zero;
        ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ground.transform.localScale = new Vector3(14f, 8f, 1f);
        var groundMat = new Material(Shader.Find("Sprites/Default"));
        groundMat.color = new Color(0.18f, 0.12f, 0.10f, 1f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        visualizer.transform.SetParent(arena.transform, true);
        visualizer.unitActorPrefab = AssetDatabase.LoadAssetAtPath<UnitVisualActor>("Assets/Prefabs/UnitVisualActor.prefab");
        visualizer.combatTextPrefab = AssetDatabase.LoadAssetAtPath<FloatingCombatText>("Assets/Prefabs/FloatingCombatText.prefab");

        // --- Event system ---
        EnsureEventSystem();

        // --- Town canvas (Somerset church full-screen stage) ---
        GameObject townCanvasObj = CreateCanvas("TownCanvas", 0);
        var townUI = townCanvasObj.AddComponent<TownSplitUIController>();
        var churchUI = townCanvasObj.AddComponent<ChurchStageUIController>();
        var pewLayout = townCanvasObj.AddComponent<ChurchPewLayout>();
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

        // Top HUD bar
        GameObject hud = CreatePanel(townCanvasObj.transform, "HUD_Bar",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 72f),
            new Color(0.08f, 0.06f, 0.05f, 0.92f));

        townUI.timerText = CreateTMP(hud.transform, "TimerText", "Horde Arrives: 07:00", 22, TextAlignmentOptions.Left,
            new Vector2(-400f, 0f), new Vector2(320f, 48f));
        townUI.goldText = CreateTMP(hud.transform, "GoldText", "Royal Gold: 75g", 22, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(280f, 48f));
        townUI.partyCountText = CreateTMP(hud.transform, "PartyText", "Warband: 0 Fighters", 20, TextAlignmentOptions.Right,
            new Vector2(360f, 0f), new Vector2(280f, 48f));
        townUI.goToWarButton = CreateButton(hud.transform, "GoToWarButton", "MARCH TO WAR",
            new Vector2(520f, 0f), new Vector2(180f, 48f), new Color(0.55f, 0.12f, 0.12f));

        // TOP-LEFT: Tavern Dice
        GameObject dicePanel = CreatePanel(townCanvasObj.transform, "Panel_TavernDice",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(220f, -260f), new Vector2(400f, 320f),
            new Color(0.16f, 0.10f, 0.08f, 0.95f));
        CreateTMP(dicePanel.transform, "DiceTitle", "TAVERN DICE", 24, TextAlignmentOptions.Center,
            new Vector2(0f, 120f), new Vector2(360f, 40f));
        townUI.diceResultText = CreateTMP(dicePanel.transform, "DiceResult", "Ante up 5g to play.", 18, TextAlignmentOptions.Center,
            new Vector2(0f, 60f), new Vector2(360f, 50f));
        townUI.currentPotText = CreateTMP(dicePanel.transform, "CurrentPot", "Pot: 0g", 20, TextAlignmentOptions.Center,
            new Vector2(0f, 10f), new Vector2(360f, 36f));
        townUI.startDiceButton = CreateButton(dicePanel.transform, "StartDice", "Start (5g)",
            new Vector2(-110f, -60f), new Vector2(140f, 44f), new Color(0.35f, 0.28f, 0.12f));
        townUI.rollDiceButton = CreateButton(dicePanel.transform, "RollDice", "Roll",
            new Vector2(0f, -60f), new Vector2(100f, 44f), new Color(0.30f, 0.22f, 0.12f));
        townUI.cashOutButton = CreateButton(dicePanel.transform, "CashOut", "Cash Out",
            new Vector2(110f, -60f), new Vector2(140f, 44f), new Color(0.15f, 0.35f, 0.18f));
        townUI.tavernRoundButton = CreateButton(dicePanel.transform, "TavernRound", "Buy Round (50g)",
            new Vector2(0f, -120f), new Vector2(220f, 40f), new Color(0.40f, 0.25f, 0.10f));
        townUI.diceGame = dice;

        // Legacy crowd grid kept off — pews are placed on the church background instead.
        GameObject crowdPanel = CreatePanel(townCanvasObj.transform, "Panel_TownCrowd_Legacy",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-360f, -280f), new Vector2(680f, 420f),
            new Color(0.12f, 0.14f, 0.12f, 0.92f));
        crowdPanel.SetActive(false);
        GameObject grid = new GameObject("TownGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.transform.SetParent(crowdPanel.transform, false);
        townUI.townGridContainer = grid.transform;
        townUI.recruitAvatarButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RecruitAvatarButton.prefab");

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

        // Legend Codex modal
        GameObject codexPanel = CreatePanel(townCanvasObj.transform, "CodexPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 520f),
            new Color(0.07f, 0.08f, 0.10f, 0.98f));
        codexPanel.SetActive(false);
        CreateTMP(codexPanel.transform, "CodexTitle", "LEGEND CODEX — 10 Arthurian Fusions", 22, TextAlignmentOptions.Center,
            new Vector2(0f, 220f), new Vector2(680f, 40f));

        GameObject recipeList = new GameObject("RecipeList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        recipeList.transform.SetParent(codexPanel.transform, false);
        var recipeRt = recipeList.GetComponent<RectTransform>();
        recipeRt.anchorMin = new Vector2(0.05f, 0.08f);
        recipeRt.anchorMax = new Vector2(0.95f, 0.85f);
        recipeRt.offsetMin = Vector2.zero;
        recipeRt.offsetMax = Vector2.zero;
        var vlg = recipeList.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        recipeList.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        codex.codexPanel = codexPanel;
        codex.recipeListContainer = recipeList.transform;
        codex.recipeRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CodexRecipeRow.prefab");
        codex.toggleCodexButton = CreateButton(hud.transform, "CodexToggle", "Codex",
            new Vector2(-520f, 0f), new Vector2(110f, 44f), new Color(0.20f, 0.25f, 0.40f));
        SeedDefaultRecipes(codex);

        // Perk Draft Modal
        GameObject perkPanel = CreatePanel(townCanvasObj.transform, "PerkDraftPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 360f),
            new Color(0.06f, 0.08f, 0.12f, 0.98f));
        perkPanel.SetActive(false);
        CreateTMP(perkPanel.transform, "PerkTitle", "CHOOSE A RUN PERK", 26, TextAlignmentOptions.Center,
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

        churchUI.timerText = townUI.timerText;
        churchUI.goldText = townUI.goldText;
        churchUI.warbandCountText = townUI.partyCountText;
        churchUI.goToWarButton = townUI.goToWarButton;
        churchUI.buyRoundButton = townUI.tavernRoundButton;
        churchUI.diceModal = dicePanel;
        churchUI.codexModal = codexPanel;
        churchUI.dossierModal = dossier;

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
            "Tune pew anchors on TownCanvas → ChurchPewLayout if seats need nudging.",
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
