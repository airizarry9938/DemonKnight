using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Persistent 15-Legend meta-progression. JsonUtility save at
    /// Application.persistentDataPath/legend_unlocks.json.
    /// Unlocks never come from Perks / Demonic Pacts / Sin Runes.
    /// </summary>
    public class LegendUnlockManager : MonoBehaviour
    {
        public static LegendUnlockManager Instance;

        public const string SaveFileName = "legend_unlocks.json";

        public static readonly string[] StarterLegendIds =
        {
            "SirKay",
            "SirBedivere",
            "LadyGuinevere",
            "ArchPriestOfGlastonbury",
        };

        public static readonly string[] AllLegendIds =
        {
            "SirKay",
            "SirBedivere",
            "LadyGuinevere",
            "ArchPriestOfGlastonbury",
            "SirGawain",
            "SirTristan",
            "SirLancelot",
            "KingArthur",
            "SirGalahad",
            "SirPercival",
            "Merlin",
            "Nimue",
            "Mordred",
            "MorganLeFay",
            "TheGreenKnight",
        };

        [Serializable]
        public class LegendUnlockSaveData
        {
            public List<string> unlockedLegendIds = new List<string>();
        }

        readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);

        // —— Per-run counters (reset on New Game / night wipe start) ——
        public int diceWinsThisRun;
        public int priestOrLegendFusionsThisRun;
        public bool survivedRunWithCursedDice;

        string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadOrCreate();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public static LegendUnlockManager Ensure()
        {
            if (Instance != null) return Instance;
            Instance = FindFirstObjectByType<LegendUnlockManager>();
            if (Instance != null) return Instance;
            var go = new GameObject("LegendUnlockManager");
            Instance = go.AddComponent<LegendUnlockManager>();
            return Instance;
        }

        public void ResetRunCounters()
        {
            diceWinsThisRun = 0;
            priestOrLegendFusionsThisRun = 0;
            survivedRunWithCursedDice = false;
        }

        public void LoadOrCreate()
        {
            unlocked.Clear();
            if (File.Exists(SaveFilePath))
            {
                try
                {
                    var data = JsonUtility.FromJson<LegendUnlockSaveData>(File.ReadAllText(SaveFilePath));
                    if (data?.unlockedLegendIds != null)
                    {
                        foreach (var id in data.unlockedLegendIds)
                        {
                            if (!string.IsNullOrEmpty(id))
                                unlocked.Add(id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[DemonKnight] legend_unlocks.json load failed: " + ex.Message);
                }
            }

            bool addedStarter = false;
            foreach (var id in StarterLegendIds)
            {
                if (unlocked.Add(id)) addedStarter = true;
            }
            if (addedStarter || !File.Exists(SaveFilePath))
                Save();
        }

        public void Save()
        {
            var data = new LegendUnlockSaveData();
            foreach (var id in AllLegendIds)
            {
                if (unlocked.Contains(id))
                    data.unlockedLegendIds.Add(id);
            }
            // Also persist any unexpected ids already unlocked.
            foreach (var id in unlocked)
            {
                if (!data.unlockedLegendIds.Contains(id))
                    data.unlockedLegendIds.Add(id);
            }
            try
            {
                File.WriteAllText(SaveFilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DemonKnight] legend_unlocks.json save failed: " + ex.Message);
            }
        }

        public bool IsLegendUnlocked(string legendId)
        {
            if (string.IsNullOrEmpty(legendId)) return false;
            Ensure();
            return unlocked.Contains(legendId);
        }

        public bool IsLegendUnlocked(UnitType type) =>
            IsLegendUnlocked(LegendIdFor(type));

        public bool Unlock(string legendId, string reason = null)
        {
            if (string.IsNullOrEmpty(legendId)) return false;
            Ensure();
            if (!unlocked.Add(legendId)) return false;
            Save();
            Debug.Log(
                $"[DemonKnight] Legend UNLOCKED: {legendId}"
                + (string.IsNullOrEmpty(reason) ? "" : $" ({reason})"));
            return true;
        }

        public static string LegendIdFor(UnitType type) => type switch
        {
            UnitType.SirKay => "SirKay",
            UnitType.SirBedivere => "SirBedivere",
            UnitType.Guinevere => "LadyGuinevere",
            UnitType.LadyGuinevere => "LadyGuinevere",
            UnitType.Priest => "ArchPriestOfGlastonbury",
            UnitType.ArchPriestOfGlastonbury => "ArchPriestOfGlastonbury",
            UnitType.SirGawain => "SirGawain",
            UnitType.SirTristan => "SirTristan",
            UnitType.SirLancelot => "SirLancelot",
            UnitType.KingArthur => "KingArthur",
            UnitType.SirGalahad => "SirGalahad",
            UnitType.SirPercival => "SirPercival",
            UnitType.Merlin => "Merlin",
            UnitType.Nimue => "Nimue",
            UnitType.Mordred => "Mordred",
            UnitType.MorganLeFay => "MorganLeFay",
            UnitType.TheGreenKnight => "TheGreenKnight",
            _ => type.ToString()
        };

        /// <summary>True when this unit type is one of the 15 gated legends.</summary>
        public static bool IsGatedLegend(UnitType type)
        {
            switch (type)
            {
                case UnitType.SirKay:
                case UnitType.SirBedivere:
                case UnitType.Guinevere:
                case UnitType.LadyGuinevere:
                case UnitType.Priest:
                case UnitType.ArchPriestOfGlastonbury:
                case UnitType.SirGawain:
                case UnitType.SirTristan:
                case UnitType.SirLancelot:
                case UnitType.KingArthur:
                case UnitType.SirGalahad:
                case UnitType.SirPercival:
                case UnitType.Merlin:
                case UnitType.Nimue:
                case UnitType.Mordred:
                case UnitType.MorganLeFay:
                case UnitType.TheGreenKnight:
                    return true;
                default:
                    return false;
            }
        }

        public static string LoreFor(string legendId) => legendId switch
        {
            "SirKay" =>
                "Arthur's sharp-tongued foster brother and castle steward; he once claimed he pulled the Sword from the Stone himself before admitting the truth.",
            "SirBedivere" =>
                "One of Arthur's first and most loyal knights, famous for fighting with one hand and throwing Excalibur back into the lake when Arthur fell.",
            "LadyGuinevere" =>
                "Queen of Camelot whose grace held the Round Table together, though her secret romance with Lancelot tore the kingdom apart.",
            "ArchPriestOfGlastonbury" =>
                "Head priest of Somerset's ancient church, where local monks claimed the Holy Grail was buried beneath the hill.",
            "SirGawain" =>
                "Arthur's fierce nephew whose strength grows with the morning sun until noon, making him nearly unbeatable in daylight.",
            "SirTristan" =>
                "A master archer, harp player, and champion knight doomed by a love potion he drank with the Irish princess Iseult.",
            "SirLancelot" =>
                "Raised by the Lady of the Lake and undefeated in one-on-one duels, yet haunted by the betrayal that ruined Camelot.",
            "KingArthur" =>
                "The Once and Future King who united Britain with Excalibur and sleeps on the island of Avalon until his people need him again.",
            "SirGalahad" =>
                "Lancelot's pure-hearted son and the only knight worthy to sit in the cursed empty seat at the Round Table and claim the Holy Grail.",
            "SirPercival" =>
                "Raised in the Welsh woods knowing nothing of knights, he walked into Camelot in rough clothes and became a Grail hero.",
            "Merlin" =>
                "A prophet and master wizard born of a demon father, bound to guide Arthur's bloodline through death and rebirth.",
            "Nimue" =>
                "An enchantress from Avalon who gave Arthur Excalibur and its magic scabbard, which stops its wearer from ever bleeding to death.",
            "Mordred" =>
                "Arthur's traitorous relative who stole Camelot's throne and gave the King his fatal wound in their final battle.",
            "MorganLeFay" =>
                "Arthur's sorceress half-sister and Merlin's bitter rival, who stole Excalibur's protective scabbard out of spite.",
            "TheGreenKnight" =>
                "A towering, axe-wielding stranger who survived having his head chopped off at Camelot's winter feast to test Gawain's courage.",
            _ => ""
        };

        public static string RiddleFor(string legendId) => legendId switch
        {
            "SirGawain" => "Locked: Slay the lord of the fourth night.",
            "SirTristan" => "Locked: Survive the sixth night's slaughter.",
            "SirLancelot" => "Locked: Conquer the eighth night's champion.",
            "KingArthur" => "Locked: Endure all nights and claim victory.",
            "SirGalahad" => "Locked: Reach the fifth night without losing a single soul.",
            "SirPercival" => "Locked: Awaken the hidden spark inside a Darkhorse recruit.",
            "Merlin" => "Locked: Uncover or fuse a rare Golden Egg.",
            "Nimue" => "Locked: Fuse a Grail-Touched soul or complete five fusions in one run.",
            "Mordred" => "Locked: Carry three or more bitter grudges to the end of a run.",
            "MorganLeFay" => "Locked: Win a battle burdened by three grudges or a flawed recruit.",
            "TheGreenKnight" => "Locked: Win three wagers at the church dice table in a single run.",
            _ => ""
        };

        // —— Trigger hooks ——

        public void NotifyWaveCleared(int waveNumber, int soulsLostThisRun)
        {
            Ensure();
            // Boss clears (wave N boss defeated by clearing wave N).
            if (waveNumber >= 4) Unlock("SirGawain", $"cleared Wave {waveNumber} boss");
            if (waveNumber >= 6) Unlock("SirTristan", $"cleared Wave {waveNumber} boss");
            if (waveNumber >= 8) Unlock("SirLancelot", $"cleared Wave {waveNumber} boss");

            // Shepherd's Vow: reach Wave 5 with zero combat deaths this run.
            if (waveNumber >= 5 && soulsLostThisRun == 0)
                Unlock("SirGalahad", "Shepherd's Vow — Wave 5 with 0 souls lost");
        }

        public void NotifyFullRunVictory()
        {
            Unlock("KingArthur", "full run victory");
            TryGreenKnightCursedDiceSurvive();
        }

        public void NotifyRunEnded(int stagedGrudgeCount)
        {
            if (stagedGrudgeCount >= 3)
                Unlock("Mordred", $"{stagedGrudgeCount} staged grudges at run end");
            TryGreenKnightCursedDiceSurvive();
        }

        void TryGreenKnightCursedDiceSurvive()
        {
            if (survivedRunWithCursedDice || SomersetRunes.HasRelic(RelicID.CursedDiceOfCaerleon))
                Unlock("TheGreenKnight", "survived a run with Cursed Dice relic");
        }

        public void NotifyWaveWonWithParty(IList<RecruitInstance> marchedOrParty, int stagedGrudgeCount)
        {
            if (stagedGrudgeCount >= 3)
                Unlock("MorganLeFay", $"{stagedGrudgeCount} staged grudges on wave win");

            if (marchedOrParty != null)
            {
                foreach (var r in marchedOrParty)
                {
                    if (r != null && IsLiability(r.primaryTrait))
                    {
                        Unlock("MorganLeFay", $"deployed Liability ({r.primaryTrait}) and won");
                        break;
                    }
                }
            }
        }

        /// <summary>Liability = Coward / Turncoat(Outlaw) / Glutton(Drunkard).</summary>
        public static bool IsLiability(TraitTag trait) =>
            trait == TraitTag.Coward
            || trait == TraitTag.Outlaw
            || trait == TraitTag.Drunkard;

        public void NotifyHiddenPotentialAwakened(string hiddenTraitId)
        {
            if (string.IsNullOrEmpty(hiddenTraitId)) return;
            if (string.Equals(hiddenTraitId, RecruitInstance.DarkhorseId, StringComparison.Ordinal)
                || string.Equals(hiddenTraitId, RecruitInstance.LegendSparkId, StringComparison.Ordinal))
            {
                // Legend Spark counts as Darkhorse-class spark for Percival.
                Unlock("SirPercival", $"awakened {hiddenTraitId}");
            }
            if (string.Equals(hiddenTraitId, RecruitInstance.GoldenEggId, StringComparison.Ordinal))
                Unlock("Merlin", "awakened Golden Egg");
        }

        public void NotifyFusionWithIngredients(UnitType productType, IList<RecruitInstance> ingredients)
        {
            Ensure();
            if (IsGatedLegend(productType) || productType == UnitType.Captain || productType == UnitType.Priest
                || productType == UnitType.ArchPriestOfGlastonbury || productType == UnitType.LadyOfTheLake)
            {
                priestOrLegendFusionsThisRun++;
                if (priestOrLegendFusionsThisRun >= 5)
                    Unlock("Nimue", $"{priestOrLegendFusionsThisRun} Priest/Legend fusions this run");
            }

            if (ingredients == null) return;
            bool hadDarkhorse = false;
            bool hadGolden = false;
            bool hadGrail = false;
            foreach (var ing in ingredients)
            {
                if (ing == null || string.IsNullOrEmpty(ing.hiddenTraitId)) continue;
                if (string.Equals(ing.hiddenTraitId, RecruitInstance.DarkhorseId, StringComparison.Ordinal)
                    || string.Equals(ing.hiddenTraitId, RecruitInstance.LegendSparkId, StringComparison.Ordinal))
                    hadDarkhorse = true;
                if (string.Equals(ing.hiddenTraitId, RecruitInstance.GoldenEggId, StringComparison.Ordinal))
                    hadGolden = true;
                if (string.Equals(ing.hiddenTraitId, RecruitInstance.GrailTouchedId, StringComparison.Ordinal))
                    hadGrail = true;
            }

            if (hadDarkhorse)
                Unlock("SirPercival", "ascended/fused Darkhorse recruit");
            if (hadGolden)
                Unlock("Merlin", "fused Golden Egg");
            if (hadGrail)
                Unlock("Nimue", "fused Grail-Touched soul");
        }

        public void NotifyDiceWin()
        {
            Ensure();
            diceWinsThisRun++;
            if (diceWinsThisRun >= 3)
                Unlock("TheGreenKnight", $"{diceWinsThisRun} dice wins this run");
        }

        public void MarkCursedDiceOwnedThisRun()
        {
            if (SomersetRunes.HasRelic(RelicID.CursedDiceOfCaerleon))
                survivedRunWithCursedDice = true;
        }
    }
}
