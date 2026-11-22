using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

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
                ? SomersetRunes.GetRank(BankRuneID.Gambler)
                : 0;

            bool firstRollSafe = (PerkDraftManager.Instance != null
                                  && PerkDraftManager.Instance.unlockedPerks.Contains(RunPerkID.TavernRiggedDice)
                                  && rollStreak == 0)
                                 || gamblerRank >= 1;

            bool cursedDice = SomersetRunes.HasRelic(RelicID.CursedDiceOfCaerleon);

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
                ? SomersetRunes.GetRank(BankRuneID.Gambler)
                : 0;
            if (gamblerRank >= 3 && GameLoopManager.Instance != null)
                GameLoopManager.Instance.AddTownTimeBonus(15f);

            // Successful tavern cash-out counts as a dice win for The Green Knight.
            LegendUnlockManager.Ensure()?.NotifyDiceWin();
        }

        private void ApplyCursedDiceGrudge()
        {
            if (GameLoopManager.Instance == null || GameLoopManager.Instance.currentTownPool.Count == 0) return;
            var pool = GameLoopManager.Instance.currentTownPool;
            var victim = pool[Random.Range(0, pool.Count)];
            GrudgeSaveManager.Instance?.ModifyGrudgeStaging(victim.unitID, 10);
        }
    }

    // ===== DA-59 EMBEDDED DiceManager (Force Sync nuclear — do not remove) =====
    /// <summary>
    /// Master Spec Phase 5 — free stakes roll once per StartTownRecruitPhase.
    /// Small Win 1.5× gold · Big Win +2 Training Voucher · Jackpot free hire.
    /// Cursed Dice relic: wager +1 staged grudge for guaranteed Jackpot.
    /// </summary>
    public class DiceManager : MonoBehaviour
    {
        public static DiceManager Instance;

        public const string LegendSparkId = RecruitInstance.LegendSparkId;

        [Tooltip("Free stakes rolls remaining this town phase.")]
        public int freeRollsRemaining;

        [Tooltip("When true and Cursed Dice relic owned, next free roll wagers +1 staged grudge for Jackpot.")]
        public bool wagerCursedDiceForJackpot;

        public string lastResultMessage = "";

        public static DiceManager Ensure()
        {
            if (Instance != null) return Instance;
            Instance = FindFirstObjectByType<DiceManager>();
            if (Instance != null) return Instance;
            var go = new GameObject("DiceManager");
            Instance = go.AddComponent<DiceManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        /// <summary>Call from StartTownRecruitPhase — grants exactly 1 free roll.</summary>
        public void GrantTownPhaseFreeRoll()
        {
            freeRollsRemaining = 1;
            wagerCursedDiceForJackpot = false;
            lastResultMessage = "Free stakes roll ready.";
            Debug.Log("[DemonKnight] DiceManager: +1 free stakes roll this town phase.");
        }

        public bool HasCursedDiceRelic => SomersetRunes.HasRelic(RelicID.CursedDiceOfCaerleon);

        /// <summary>
        /// Spend one free roll. When <paramref name="cursedWager"/> and relic held,
        /// stage +1 grudge on a random pew soul and force Jackpot.
        /// </summary>
        public bool TryFreeRoll(bool cursedWager, out string message)
        {
            message = "";
            if (freeRollsRemaining <= 0)
            {
                message = "No free stakes roll left this night.";
                lastResultMessage = message;
                return false;
            }

            freeRollsRemaining--;
            bool forceJackpot = cursedWager && HasCursedDiceRelic;
            if (forceJackpot)
            {
                StageCursedWagerGrudge();
                ApplyJackpot(out message);
                message = "<color=#FF66AA>Cursed wager (+1 grudge)!</color> " + message;
                lastResultMessage = message;
                return true;
            }

            float roll = Random.value;
            if (roll < 0.50f)
                ApplySmallWin(out message);
            else if (roll < 0.82f)
                ApplyBigWin(out message);
            else
                ApplyJackpot(out message);

            lastResultMessage = message;
            // Free stakes rolls always resolve as a win tier — count for Green Knight unlock.
            LegendUnlockManager.Ensure()?.NotifyDiceWin();
            Debug.Log($"[DemonKnight] DiceManager free roll → {message}");
            return true;
        }

        void StageCursedWagerGrudge()
        {
            var loop = GameLoopManager.Instance;
            var pool = loop?.currentTownPool;
            if (pool == null || pool.Count == 0) return;
            var victim = pool[Random.Range(0, pool.Count)];
            if (victim == null || string.IsNullOrEmpty(victim.unitID)) return;
            GrudgeSaveManager.Instance?.ModifyGrudgeStaging(victim.unitID, 1);
            Debug.Log($"[DemonKnight] Cursed Dice wager: +1 staged grudge on {victim.characterName}.");
        }

        void ApplySmallWin(out string message)
        {
            var town = ResolveTown();
            int before = town != null ? town.playerGold : 0;
            // 1.5× gold → add 50% of current purse (min +10g).
            int bonus = Mathf.Max(10, Mathf.RoundToInt(before * 0.5f));
            if (town != null) town.playerGold = before + bonus;
            message = $"<color=#7CFF9A>Small Win!</color> Gold ×1.5 (+{bonus}g → {(town != null ? town.playerGold : before)}g).";
            SyncHud();
        }

        void ApplyBigWin(out string message)
        {
            var loop = GameLoopManager.Instance ?? GameLoopManager.Resolve();
            if (loop != null) loop.trainingVouchers += 2;
            int vouchers = loop != null ? loop.trainingVouchers : 2;
            message = $"<color=#FFE066>Big Win!</color> +2 Training Vouchers (now {vouchers}).";
            SyncHud();
        }

        void ApplyJackpot(out string message)
        {
            var loop = GameLoopManager.Instance ?? GameLoopManager.Resolve();
            var pool = loop?.currentTownPool;
            if (pool == null || pool.Count == 0)
            {
                message = "<color=#FFD700>Jackpot!</color> No unrecruited souls left to zero.";
                return;
            }

            var candidates = new List<RecruitInstance>();
            foreach (var r in pool)
            {
                if (r == null) continue;
                candidates.Add(r);
            }
            if (candidates.Count == 0)
            {
                message = "<color=#FFD700>Jackpot!</color> Pew empty.";
                return;
            }

            var pick = candidates[Random.Range(0, candidates.Count)];
            pick.hireCostForcedZero = true;
            pick.baseHireCost = 0;
            message =
                $"<color=#FFD700>Jackpot!</color> {pick.characterName}'s hire is <b>FREE</b> this night.";
            SyncHud();
        }

        static TownSplitUIController ResolveTown()
        {
            var loop = GameLoopManager.Instance;
            if (loop?.townUI != null) return loop.townUI;
            return Object.FindFirstObjectByType<TownSplitUIController>();
        }

        static void SyncHud()
        {
            var loop = GameLoopManager.Instance;
            loop?.townUI?.UpdateHUD();
            loop?.ResolveChurchUI()?.RefreshHUD();
        }
    }

}
