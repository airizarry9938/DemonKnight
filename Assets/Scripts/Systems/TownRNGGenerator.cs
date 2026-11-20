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

        [Header("Paper-Doll Pools (optional / legacy)")]
        public List<Sprite> bodyEager;
        public List<Sprite> bodyUpright;
        public List<Sprite> bodySlouched;
        public List<Sprite> headPool;
        public Sprite moodGrudge;
        public Sprite moodLoyalist;
        public Sprite moodCoward;

        [Header("Modular townsfolk (preferred)")]
        public ModularSpriteLibrary modularLibrary;

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
            // Tier 1 Scout is free but not auto-unlocked unless Royal Scouting Decree.
            bool unlockTier1 = PerkDraftManager.Instance != null
                && PerkDraftManager.Instance.unlockedPerks.Contains(RunPerkID.RoyalScoutingDecree);

            bool lustSiren = SomersetRunes.IsSinActive(SinRuneID.Lust) && Random.value < 0.25f;

            var lib = modularLibrary != null ? modularLibrary : ModularSpriteLibrary.Load();
            if (lib != null) TownsfolkVisualApplier.SetLibrary(lib);

            RecruitInstance r = new RecruitInstance
            {
                unitID = persistentID,
                characterName = $"{firstNames[Random.Range(0, firstNames.Length)]} {epithets[Random.Range(0, epithets.Length)]}",
                unitType = type,
                pewSeatIndex = pewSeat,
                randomAvatar = (pool != null && pool.Count > 0) ? pool[Random.Range(0, pool.Count)] : null,
                intelTierUnlocked = unlockTier1 ? 1 : 0,
                isSirenEnchantress = lustSiren,
                appearance = TownsfolkAppearance.Generate(type, persistentID, pewSeat, lib)
            };

            // Keep scout/dossier reveals across nights/waves until WipeAllSavedMemory.
            if (GrudgeSaveManager.Instance != null)
            {
                int remembered = GrudgeSaveManager.Instance.GetRememberedIntel(persistentID);
                if (remembered > r.intelTierUnlocked)
                    r.intelTierUnlocked = remembered;
            }

            switch (type)
            {
                case UnitType.Knight:
                {
                    // Balance V2: HP 115–145, ATK 14–19 → hire 110–160.
                    int hpLo = 115, hpHi = 146;
                    int atkLo = 14, atkHi = 20;
                    r.maxHealth = Mathf.RoundToInt(Random.Range(hpLo, hpHi) * hpMult);
                    r.attackDamage = Random.Range(atkLo, atkHi);
                    r.attackSpeed = Random.Range(1.6f, 2.0f);
                    r.baseHireCost = HireCostFromStats(r.maxHealth / Mathf.Max(0.01f, hpMult), r.attackDamage,
                        hpLo, hpHi - 1, atkLo, atkHi - 1,
                        RecruitInstance.KnightHireFloor, RecruitInstance.KnightHireCeil);
                    break;
                }
                case UnitType.Warrior:
                {
                    // Balance V2: HP 45–65, ATK 7–10 → hire 35–55.
                    int hpLo = 45, hpHi = 66;
                    int atkLo = 7, atkHi = 11;
                    r.maxHealth = Mathf.RoundToInt(Random.Range(hpLo, hpHi) * hpMult);
                    r.attackDamage = Random.Range(atkLo, atkHi);
                    r.attackSpeed = Random.Range(1.3f, 1.6f);
                    r.baseHireCost = HireCostFromStats(r.maxHealth / Mathf.Max(0.01f, hpMult), r.attackDamage,
                        hpLo, hpHi - 1, atkLo, atkHi - 1,
                        RecruitInstance.WarriorHireFloor, RecruitInstance.WarriorHireCeil);
                    break;
                }
                case UnitType.Peasant:
                {
                    // Balance V2: HP 14–22, ATK 2–3 → hire 10–15.
                    int hpLo = 14, hpHi = 23;
                    int atkLo = 2, atkHi = 4;
                    r.maxHealth = Mathf.RoundToInt(Random.Range(hpLo, hpHi) * hpMult);
                    r.attackDamage = Random.Range(atkLo, atkHi);
                    r.attackSpeed = Random.Range(1.1f, 1.3f);
                    r.baseHireCost = HireCostFromStats(r.maxHealth / Mathf.Max(0.01f, hpMult), r.attackDamage,
                        hpLo, hpHi - 1, atkLo, atkHi - 1,
                        RecruitInstance.PeasantHireFloor, RecruitInstance.PeasantHireCeil);
                    break;
                }
            }

            // Hard clamp rolled base into band (defensive).
            r.baseHireCost = Mathf.Max(r.baseHireCost, RecruitInstance.HireFloorFor(type));
            if (type == UnitType.Peasant)
                r.baseHireCost = Mathf.Clamp(r.baseHireCost, RecruitInstance.PeasantHireFloor, RecruitInstance.PeasantHireCeil);
            else if (type == UnitType.Warrior)
                r.baseHireCost = Mathf.Clamp(r.baseHireCost, RecruitInstance.WarriorHireFloor, RecruitInstance.WarriorHireCeil);
            else if (type == UnitType.Knight)
                r.baseHireCost = Mathf.Clamp(r.baseHireCost, RecruitInstance.KnightHireFloor, RecruitInstance.KnightHireCeil);

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

        /// <summary>
        /// Map HP+ATK quality into a hire band (55% HP + 45% ATK).
        /// </summary>
        private static int HireCostFromStats(
            float hp, int atk,
            int hpLo, int hpHi, int atkLo, int atkHi,
            int costLo, int costHi)
        {
            float hpT = Mathf.InverseLerp(hpLo, Mathf.Max(hpLo + 1, hpHi), hp);
            float atkT = Mathf.InverseLerp(atkLo, Mathf.Max(atkLo + 1, atkHi), atk);
            float t = Mathf.Clamp01(0.55f * hpT + 0.45f * atkT);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(costLo, costHi, t)), costLo, costHi);
        }

        private void AssignPaperDollLayers(RecruitInstance r, int grudge)
        {
            // Modular appearance is authoritative when library art exists; keep legacy
            // body/head sprites as fallback for empty ModularSpriteLibrary slots.
            var lib = modularLibrary != null ? modularLibrary : ModularSpriteLibrary.Load();
            if (r.appearance == null)
                r.appearance = TownsfolkAppearance.Generate(r.unitType, r.unitID, r.pewSeatIndex, lib);
            else
                r.appearance.BindSeatSalt(r.pewSeatIndex);

            if (lib != null)
            {
                var pose = lib.GetPose(r.appearance.poseIndex);
                var head = lib.GetHead(r.appearance.headIndex);
                if (pose != null) r.bodySprite = pose;
                if (head != null) r.headSprite = head;
                if (r.randomAvatar == null) r.randomAvatar = head != null ? head : pose;
            }

            if (r.bodySprite == null)
            {
                List<Sprite> bodyPool = bodyUpright;
                if (r.bodyPosture == BodyPosture.EagerForward) bodyPool = bodyEager;
                else if (r.bodyPosture == BodyPosture.SlouchedExit) bodyPool = bodySlouched;
                if (bodyPool != null && bodyPool.Count > 0)
                    r.bodySprite = bodyPool[Random.Range(0, bodyPool.Count)];
            }
            if (r.headSprite == null)
            {
                if (headPool != null && headPool.Count > 0)
                    r.headSprite = headPool[Random.Range(0, headPool.Count)];
                else
                    r.headSprite = r.randomAvatar;
            }

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
