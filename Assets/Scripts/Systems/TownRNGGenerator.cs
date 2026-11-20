using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class TownRNGGenerator : MonoBehaviour
    {
        public List<Sprite> knightAvatars;
        public List<Sprite> warriorAvatars;
        public List<Sprite> peasantAvatars;

        private readonly string[] firstNames =
        {
            "Gawain", "Cedric", "Alden", "Bram", "Rowan", "Gareth", "Tristan", "Lucan",
            "Bors", "Ector", "Percival", "Lamorak", "Kay", "Bedivere", "Mordred", "Agravain",
            "Pellinore", "Urien", "Accolon", "Dinadan", "Palamedes", "Sagramore", "Yvain",
            "Willem", "Thomas", "Hugh", "Wat", "Piers", "Hob", "Jack", "Oswin", "Eadric"
        };

        private readonly string[] epithets =
        {
            "the Grim", "of Camelot", "the Drunk", "Ironhand", "the Skeptic", "Two-Coins",
            "the Bold", "Mudfoot", "the Scarred", "Half-Helm", "the Pious", "Quickblade",
            "the Red", "the Stout", "of the Hills", "the Unlucky", "the Loyal", "Blacktooth"
        };

        [Header("Paper-Doll Pools (optional)")]
        public List<Sprite> bodyEager;
        public List<Sprite> bodyUpright;
        public List<Sprite> bodySlouched;
        public List<Sprite> headPool;
        public Sprite moodGrudge;
        public Sprite moodLoyalist;
        public Sprite moodCoward;

        public List<RecruitInstance> GenerateNewRunTown()
        {
            // Persistent unitIDs for grudge memory; pew seats are shuffled each run.
            List<RecruitInstance> townList = new List<RecruitInstance>();
            for (int i = 0; i < 12; i++) townList.Add(CreateRandomRecruit($"knight_{i}", UnitType.Knight, knightAvatars, i));
            for (int i = 0; i < 15; i++) townList.Add(CreateRandomRecruit($"warrior_{i}", UnitType.Warrior, warriorAvatars, 12 + i));
            for (int i = 0; i < 25; i++) townList.Add(CreateRandomRecruit($"peasant_{i}", UnitType.Peasant, peasantAvatars, 27 + i));

            ShufflePewSeats(townList);
            townList.Sort((a, b) => a.pewSeatIndex.CompareTo(b.pewSeatIndex));
            return townList;
        }

        /// <summary>Assign seats 0–51 in a fresh random order every run.</summary>
        private static void ShufflePewSeats(List<RecruitInstance> townList)
        {
            int n = townList.Count;
            var seats = new int[n];
            for (int i = 0; i < n; i++) seats[i] = i;

            for (int i = n - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (seats[i], seats[j]) = (seats[j], seats[i]);
            }

            for (int i = 0; i < n; i++)
                townList[i].pewSeatIndex = seats[i];
        }

        private RecruitInstance CreateRandomRecruit(string persistentID, UnitType type, List<Sprite> pool, int pewSeat)
        {
            float hpMult = MerlinResurrectionManager.Instance != null
                ? MerlinResurrectionManager.Instance.GetBonusHealthMultiplier()
                : 1f;
            bool freeTier1 = PerkDraftManager.Instance != null
                && PerkDraftManager.Instance.unlockedPerks.Contains(RunPerkID.RoyalScoutingDecree);

            bool lustSiren = SomersetRunes.IsSinActive(SinRuneID.Lust) && Random.value < 0.25f;

            RecruitInstance r = new RecruitInstance
            {
                unitID = persistentID,
                characterName = $"{firstNames[Random.Range(0, firstNames.Length)]} {epithets[Random.Range(0, epithets.Length)]}",
                unitType = type,
                pewSeatIndex = pewSeat,
                randomAvatar = (pool != null && pool.Count > 0) ? pool[Random.Range(0, pool.Count)] : null,
                intelTierUnlocked = freeTier1 ? 1 : 0,
                isSirenEnchantress = lustSiren
            };

            switch (type)
            {
                case UnitType.Knight:
                    r.maxHealth = Mathf.RoundToInt(Random.Range(75, 105) * hpMult);
                    r.attackDamage = Random.Range(10, 16);
                    r.attackSpeed = Random.Range(1.5f, 2.0f);
                    r.baseHireCost = Random.Range(35, 50);
                    break;
                case UnitType.Warrior:
                    r.maxHealth = Mathf.RoundToInt(Random.Range(32, 52) * hpMult);
                    r.attackDamage = Random.Range(5, 9);
                    r.attackSpeed = Random.Range(1.2f, 1.6f);
                    r.baseHireCost = Random.Range(18, 28);
                    break;
                case UnitType.Peasant:
                    r.maxHealth = Mathf.RoundToInt(Random.Range(10, 22) * hpMult);
                    r.attackDamage = Random.Range(1, 4);
                    r.attackSpeed = Random.Range(1.0f, 1.3f);
                    r.baseHireCost = Random.Range(6, 12);
                    break;
            }

            r.primaryTrait = RollPrimaryTrait();
            r.hatedTrait = RollSecondaryTrait(r.primaryTrait);
            r.lovedTrait = RollSecondaryTrait(r.primaryTrait);

            int grudge = GrudgeSaveManager.Instance != null
                ? GrudgeSaveManager.Instance.GetGrudgeCostPenalty(r.unitID)
                : 0;
            r.RefreshBodyPostureFromTraits(grudge);
            AssignPaperDollLayers(r, grudge);
            return r;
        }

        private void AssignPaperDollLayers(RecruitInstance r, int grudge)
        {
            List<Sprite> bodyPool = bodyUpright;
            if (r.bodyPosture == BodyPosture.EagerForward) bodyPool = bodyEager;
            else if (r.bodyPosture == BodyPosture.SlouchedExit) bodyPool = bodySlouched;

            if (bodyPool != null && bodyPool.Count > 0)
                r.bodySprite = bodyPool[Random.Range(0, bodyPool.Count)];
            if (headPool != null && headPool.Count > 0)
                r.headSprite = headPool[Random.Range(0, headPool.Count)];
            else
                r.headSprite = r.randomAvatar;

            if (grudge > 0) r.moodOverlaySprite = moodGrudge;
            else if (r.primaryTrait == TraitTag.Loyalist) r.moodOverlaySprite = moodLoyalist;
            else if (r.primaryTrait == TraitTag.Coward) r.moodOverlaySprite = moodCoward;
            else r.moodOverlaySprite = null;
        }

        private TraitTag RollPrimaryTrait()
        {
            if (Random.value < 0.15f) return TraitTag.Loyalist;
            TraitTag[] traits = { TraitTag.Noble, TraitTag.Outlaw, TraitTag.Drunkard, TraitTag.Zealot, TraitTag.Coward };
            return traits[Random.Range(0, traits.Length)];
        }

        private TraitTag RollSecondaryTrait(TraitTag exclude)
        {
            if (Random.value < 0.25f) return TraitTag.None;
            TraitTag[] traits = { TraitTag.Noble, TraitTag.Outlaw, TraitTag.Drunkard, TraitTag.Zealot, TraitTag.Coward, TraitTag.Loyalist };
            TraitTag picked = traits[Random.Range(0, traits.Length)];
            return picked == exclude ? TraitTag.None : picked;
        }
    }
}
