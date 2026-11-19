using UnityEngine;
using UnityEngine.Events;

namespace ArthurianAutobattler
{
    public class TavernDiceMinigame : MonoBehaviour
    {
        public int entryFee = 5;
        public int currentPot = 0;
        public int rollStreak = 0;
        public bool isPlaying = false;

        public UnityEvent<int, int> OnRollSuccess = new UnityEvent<int, int>();
        public UnityEvent OnBust = new UnityEvent();

        public void StartGame(ref int playerGold)
        {
            if (playerGold < entryFee || isPlaying) return;
            playerGold -= entryFee;
            currentPot = entryFee;
            rollStreak = 0;
            isPlaying = true;
        }

        public void RollDie()
        {
            if (!isPlaying) return;

            int roll = Random.Range(1, 7);
            int gamblerRank = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetRuneRank(BankRuneID.Gambler)
                : 0;

            bool firstRollSafe = (PerkDraftManager.Instance != null
                                  && PerkDraftManager.Instance.unlockedPerks.Contains(RunPerkID.TavernRiggedDice)
                                  && rollStreak == 0)
                                 || gamblerRank >= 1;

            bool cursedDice = RelicManager.Instance != null
                              && RelicManager.Instance.Has(RelicID.CursedDiceOfCaerleon);

            if (roll == 1 && !firstRollSafe)
            {
                currentPot = 0;
                rollStreak = 0;
                isPlaying = false;
                if (cursedDice) ApplyCursedDiceGrudge();
                OnBust?.Invoke();
            }
            else
            {
                if (roll == 1) roll = 2; // Loaded / Gambler protection
                rollStreak++;
                int gain = roll * rollStreak;
                if (gamblerRank >= 2 && roll == 6) gain *= 2;
                if (cursedDice) gain *= 2;
                currentPot += gain;
                OnRollSuccess?.Invoke(roll, currentPot);
            }
        }

        public void CashOut(ref int playerGold)
        {
            if (!isPlaying) return;
            playerGold += currentPot;
            currentPot = 0;
            rollStreak = 0;
            isPlaying = false;

            int gamblerRank = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetRuneRank(BankRuneID.Gambler)
                : 0;
            if (gamblerRank >= 3 && GameLoopManager.Instance != null)
                GameLoopManager.Instance.AddTownTimeBonus(15f);
        }

        private void ApplyCursedDiceGrudge()
        {
            if (GameLoopManager.Instance == null || GameLoopManager.Instance.currentTownPool.Count == 0) return;
            var pool = GameLoopManager.Instance.currentTownPool;
            var victim = pool[Random.Range(0, pool.Count)];
            GrudgeSaveManager.Instance?.ModifyGrudgeStaging(victim.unitID, 10);
        }
    }
}
