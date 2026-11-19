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
            bool firstRollSafe = PerkDraftManager.Instance != null
                && PerkDraftManager.Instance.unlockedPerks.Contains(RunPerkID.TavernRiggedDice)
                && rollStreak == 0;

            if (roll == 1 && !firstRollSafe)
            {
                currentPot = 0;
                rollStreak = 0;
                isPlaying = false;
                OnBust?.Invoke();
            }
            else
            {
                if (roll == 1) roll = 2; // Loaded dice protection
                rollStreak++;
                currentPot += roll * rollStreak;
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
        }
    }
}
