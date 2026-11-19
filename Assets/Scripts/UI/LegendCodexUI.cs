using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArthurianAutobattler
{
    [Serializable]
    public class CodexRecipeEntry
    {
        public string heroName;
        public Sprite heroPortrait;
        public string ingredientFormula;
        public string combatPerkSummary;
        public UnitType resultingType;
    }

    public class LegendCodexUI : MonoBehaviour
    {
        public GameObject codexPanel;
        public Button toggleCodexButton;
        public Transform recipeListContainer;
        public GameObject recipeRowPrefab;
        public List<CodexRecipeEntry> allRecipes = new List<CodexRecipeEntry>();

        private void Start()
        {
            if (codexPanel != null) codexPanel.SetActive(false);

            toggleCodexButton?.onClick.AddListener(() =>
            {
                codexPanel.SetActive(!codexPanel.activeSelf);
                if (codexPanel.activeSelf) RefreshCodexList();
            });
        }

        public void RefreshCodexList()
        {
            if (codexPanel == null || !codexPanel.activeSelf || GameLoopManager.Instance == null) return;

            foreach (Transform child in recipeListContainer) Destroy(child.gameObject);

            var party = GameLoopManager.Instance.recruitedParty;
            foreach (var entry in allRecipes)
            {
                GameObject row = Instantiate(recipeRowPrefab, recipeListContainer);
                TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();
                Button fuseBtn = row.GetComponentInChildren<Button>();
                bool ready = CheckRecipeReady(entry.resultingType, party);

                if (texts.Length >= 3)
                {
                    texts[0].text = ready
                        ? $"<color=#00FF00>{entry.heroName} [READY]</color>"
                        : entry.heroName;
                    texts[1].text = $"Recipe: {entry.ingredientFormula}";
                    texts[2].text = $"Perk: {entry.combatPerkSummary}";
                }

                if (fuseBtn != null)
                {
                    UnitType type = entry.resultingType;
                    fuseBtn.interactable = ready;
                    fuseBtn.onClick.RemoveAllListeners();
                    fuseBtn.onClick.AddListener(() => ExecuteFusion(type, party));
                }
            }
        }

        private bool CheckRecipeReady(UnitType t, List<RecruitInstance> p)
        {
            var f = CardFusionSystem.Instance;
            if (f == null) return false;

            switch (t)
            {
                case UnitType.Priest: return f.CanFusePriest(p);
                case UnitType.Captain: return f.CanFuseCaptain(p);
                case UnitType.KingArthur: return f.CanFuseKingArthur(p);
                case UnitType.Merlin: return f.CanFuseMerlin(p);
                case UnitType.SirLancelot: return f.CanFuseLancelot(p);
                case UnitType.MorganLeFay: return f.CanFuseMorganLeFay(p);
                case UnitType.LadyOfTheLake: return f.CanFuseLadyOfTheLake(p);
                case UnitType.SirGawain: return f.CanFuseGawain(p);
                case UnitType.Mordred: return f.CanFuseMordred(p);
                case UnitType.SirGalahad: return f.CanFuseGalahad(p);
                default: return false;
            }
        }

        private void ExecuteFusion(UnitType t, List<RecruitInstance> p)
        {
            var f = CardFusionSystem.Instance;
            if (f == null) return;

            bool ok = false;
            switch (t)
            {
                case UnitType.Priest: ok = f.FusePriest(p); break;
                case UnitType.Captain: ok = f.FuseCaptain(p); break;
                case UnitType.KingArthur: ok = f.FuseKingArthur(p); break;
                case UnitType.Merlin: ok = f.FuseMerlin(p); break;
                case UnitType.SirLancelot: ok = f.FuseLancelot(p); break;
                case UnitType.MorganLeFay: ok = f.FuseMorganLeFay(p); break;
                case UnitType.LadyOfTheLake: ok = f.FuseLadyOfTheLake(p); break;
                case UnitType.SirGawain: ok = f.FuseGawain(p); break;
                case UnitType.Mordred: ok = f.FuseMordred(p); break;
                case UnitType.SirGalahad: ok = f.FuseGalahad(p); break;
            }

            if (ok)
            {
                RefreshCodexList();
                FindFirstObjectByType<TownSplitUIController>()?.UpdateHUD();
            }
        }
    }
}
