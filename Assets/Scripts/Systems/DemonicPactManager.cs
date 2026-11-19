using UnityEngine;

namespace ArthurianAutobattler
{
    public class DemonicPactManager : MonoBehaviour
    {
        public static DemonicPactManager Instance;

        public DemonicPactID ActivePact { get; private set; } = DemonicPactID.None;
        public bool chapelCrimsonTintActive;

        [Header("Optional Visual")]
        public UnityEngine.UI.Image chapelTintOverlay;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void ClearPact()
        {
            ActivePact = DemonicPactID.None;
            chapelCrimsonTintActive = false;
            if (chapelTintOverlay != null)
            {
                var c = chapelTintOverlay.color;
                c.a = 0f;
                chapelTintOverlay.color = c;
            }
        }

        public bool TryAcceptPact(DemonicPactID pact, TownSplitUIController townUI)
        {
            if (ActivePact != DemonicPactID.None) return false;
            ActivePact = pact;
            chapelCrimsonTintActive = true;
            if (chapelTintOverlay != null)
            {
                chapelTintOverlay.color = new Color(0.55f, 0.05f, 0.08f, 0.28f);
            }

            switch (pact)
            {
                case DemonicPactID.SilverTongue:
                    if (townUI != null) townUI.playerGold += 120;
                    if (GameLoopManager.Instance != null)
                    {
                        foreach (var c in GameLoopManager.Instance.currentTownPool)
                            c.intelTierUnlocked = 3;
                    }
                    break;
                case DemonicPactID.Usurpation:
                    // Mordred & Morgan spawn into warband at wave 1 start (handled in AutoBattlerEngine).
                    break;
                case DemonicPactID.EndlessHourglass:
                    if (GameLoopManager.Instance != null)
                        GameLoopManager.Instance.disableTownTimer = true;
                    break;
            }

            // Pacts double cross-run grudge staging via GrudgeSaveManager multiplier.
            if (GrudgeSaveManager.Instance != null)
                GrudgeSaveManager.Instance.grudgeDeltaMultiplier = 2f;

            Debug.Log($"[Pact] Accepted {pact}");
            return true;
        }

        public float GetEndlessHourglassExtraBudget(float townSecondsSpent)
        {
            if (ActivePact != DemonicPactID.EndlessHourglass) return 0f;
            return 15f * (townSecondsSpent / 60f);
        }

        public int GetEndlessHourglassExtraDreadLords(float townSecondsSpent)
        {
            if (ActivePact != DemonicPactID.EndlessHourglass) return 0;
            return Mathf.FloorToInt(townSecondsSpent / 60f);
        }
    }
}
