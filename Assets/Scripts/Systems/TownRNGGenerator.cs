using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class TownRNGGenerator : MonoBehaviour
    {
        public List<Sprite> knightAvatars;
        public List<Sprite> warriorAvatars;
        public List<Sprite> peasantAvatars;

        // tip: fix-townrng-name-pools — 48 first names (exact order).
        private readonly string[] firstNames =
        {
            "Alden", "Bram", "Rowan", "Cedric", "Lucan", "Gideon", "Caleb", "Julian",
            "Cole", "Finn", "Jude", "Owen", "Maren", "Tessa", "Clara", "Ivy",
            "Maeve", "Brenna", "Freya", "Nora", "Corbin", "Darius", "Kieran", "Soren",
            "Torin", "Maddox", "Rory", "Ellis", "Damian", "Alistair", "Toby", "Wren",
            "Marcus", "Roman", "Victor", "Hector", "Garrett", "Grant", "Nolan", "Dean",
            "Brock", "Titus", "Cassian", "Damon", "Vera", "Elena", "Diana", "Claire"
        };

        // tip: fix-townrng-name-pools — 75 surnames & nicknames (exact order).
        private readonly string[] epithets =
        {
            "Stone", "Ward", "Holt", "Cole", "Shaw", "Cross", "Grant", "Hayes",
            "Brooks", "Reid", "Vaughn", "Marsh", "Cairn", "Barrow", "Kerr", "Doyle",
            "Boyd", "Tate", "Penn", "Wells", "Ford", "Ross", "Gale", "Nash",
            "Cain", "Vance", "Thorne", "Sterling", "Mercer", "Flint", "Graves", "Locke",
            "Drake", "Frost", "Slate", "Hale", "Crow", "Rook", "Knox", "Steele",
            "Ashford", "Fox", "Wolf", "Briggs", "Pike", "Noodle", "Biscuit", "Waffles",
            "Crumb", "Puddle", "Pickles", "Buttons", "Mittens", "Pebbles", "Turnip", "Cabbage",
            "Sprout", "Pockets", "Fizz", "Spud", "Butterknife", "Half-Pint", "Two-Coins", "No-Luck",
            "Sidekick", "Meatshield", "Coinflip", "Shortstraw", "Tin-Can", "Sore-Back", "Late-Fee",
            "Overdraft", "Glass-Jaw", "Tripwire", "Wildcard"
        };

        /// <summary>
        /// Full display names of fused Legends / wave bosses / special combat units.
        /// TownRNG must never emit an identical First+" "+Surname copycat.
        /// </summary>
        private static readonly HashSet<string> ReservedFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Town Priest", "Vanguard Captain", "King Arthur", "Sir Lancelot", "Morgan le Fay",
            "Lady of the Lake", "Sir Gawain", "Mordred", "Sir Galahad", "Sir Tristan",
            "Sir Percival", "Sir Kay", "Sir Bedivere", "Lady Guinevere", "Arch Priest of Glastonbury",
            "Nimue", "The Green Knight", "Merlin", "Merlin the Archmage",
            "The Black Knight", "Vortigern, Usurper King", "Accolon of Gaul", "The Questing Beast",
            "Nimue, Queen of Thorns", "Feral Dread Lord", "Rabid Hellhound", "Frenzied Imp",
            "Hourglass Dread Lord", "Siren Enchantress"
        };

        // Per-run uniqueness (reset in GenerateNewRunTown).
        private HashSet<string> _usedFirstNames;
        private HashSet<string> _usedSurnames;
        private HashSet<string> _usedFullNames;

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

        [Header("Character registry (optional troop portrait overrides)")]
        public CharacterDatabase characterDatabase;

        public List<RecruitInstance> GenerateNewRunTown()
        {
            // tip: fix-townrng-name-pools — reset per-run name uniqueness before pew generation.
            _usedFirstNames = new HashSet<string>(StringComparer.Ordinal);
            _usedSurnames = new HashSet<string>(StringComparer.Ordinal);
            _usedFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Persistent unitIDs for grudge memory; pew seats are shuffled each run.
            List<RecruitInstance> townList = new List<RecruitInstance>();
            // tip: fix-townrng-name-pools — 12 knights + 14 warriors + 22 peasants = 48.
            for (int i = 0; i < 12; i++) townList.Add(CreateRandomRecruit($"knight_{i}", UnitType.Knight, knightAvatars, i));
            for (int i = 0; i < 14; i++) townList.Add(CreateRandomRecruit($"warrior_{i}", UnitType.Warrior, warriorAvatars, 12 + i));
            for (int i = 0; i < 22; i++) townList.Add(CreateRandomRecruit($"peasant_{i}", UnitType.Peasant, peasantAvatars, 26 + i));

            ShufflePewSeats(townList);
            townList.Sort((a, b) => a.pewSeatIndex.CompareTo(b.pewSeatIndex));
            CardFusionSystem.SanitizeBaseUnitFusionFlags(townList);
            return townList;
        }

        /// <summary>
        /// tip: fix-townrng-name-pools — FirstName + " " + SurnameOrEpithet with per-run uniqueness:
        /// unique surnames; unique first names until the 48-pool is exhausted; never First==Surname;
        /// never a full-name collision with Legends / wave bosses.
        /// </summary>
        private string AllocateUniqueCharacterName()
        {
            if (_usedFirstNames == null) _usedFirstNames = new HashSet<string>(StringComparer.Ordinal);
            if (_usedSurnames == null) _usedSurnames = new HashSet<string>(StringComparer.Ordinal);
            if (_usedFullNames == null) _usedFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string surname = PickUniqueSurname();
            bool allowFirstReuse = _usedFirstNames.Count >= firstNames.Length;

            // Bound retries: 48 first × remaining surnames is large; still belt against reserved hits.
            for (int attempt = 0; attempt < 256; attempt++)
            {
                string first = PickFirstNameCandidate(allowFirstReuse);
                if (string.Equals(first, surname, StringComparison.Ordinal))
                    continue; // block "Cole Cole", "Grant Grant"
                string full = first + " " + surname;
                if (ReservedFullNames.Contains(full) || _usedFullNames.Contains(full))
                    continue;
                if (!allowFirstReuse && _usedFirstNames.Contains(first))
                    continue;

                _usedFirstNames.Add(first);
                _usedSurnames.Add(surname);
                _usedFullNames.Add(full);
                return full;
            }

            // Extreme fallback: keep unique surname, append a disambiguator if needed.
            for (int i = 0; i < firstNames.Length; i++)
            {
                string first = firstNames[i];
                if (string.Equals(first, surname, StringComparison.Ordinal)) continue;
                if (!allowFirstReuse && _usedFirstNames.Contains(first)) continue;
                string full = first + " " + surname;
                if (ReservedFullNames.Contains(full) || _usedFullNames.Contains(full)) continue;
                _usedFirstNames.Add(first);
                _usedSurnames.Add(surname);
                _usedFullNames.Add(full);
                return full;
            }

            string emergency = firstNames[Random.Range(0, firstNames.Length)] + " " + surname;
            _usedSurnames.Add(surname);
            _usedFullNames.Add(emergency);
            return emergency;
        }

        private string PickUniqueSurname()
        {
            // Prefer unused epithets; 75 pool covers 48 seats with room to spare.
            for (int attempt = 0; attempt < 128; attempt++)
            {
                string s = epithets[Random.Range(0, epithets.Length)];
                if (!_usedSurnames.Contains(s)) return s;
            }
            for (int i = 0; i < epithets.Length; i++)
            {
                if (!_usedSurnames.Contains(epithets[i])) return epithets[i];
            }
            // Should never hit with 48 seats / 75 surnames.
            return epithets[Random.Range(0, epithets.Length)];
        }

        private string PickFirstNameCandidate(bool allowReuse)
        {
            if (!allowReuse)
            {
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    string f = firstNames[Random.Range(0, firstNames.Length)];
                    if (!_usedFirstNames.Contains(f)) return f;
                }
                for (int i = 0; i < firstNames.Length; i++)
                {
                    if (!_usedFirstNames.Contains(firstNames[i])) return firstNames[i];
                }
            }
            return firstNames[Random.Range(0, firstNames.Length)];
        }

        /// <summary>Assign seats 0–47 in a fresh random order every run.</summary>
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
            // Tier 1 Scout is free (0g) but NOT auto-unlocked — HP/ATK stay ??? until purchased.
            // Royal Scouting Decree still unlocks Tier 1 on current + future townsfolk.
            bool unlockTier1 = PerkDraftManager.Instance != null
                && PerkDraftManager.Instance.unlockedPerks.Contains(RunPerkID.RoyalScoutingDecree);

            bool lustSiren = SomersetRunes.IsSinActive(SinRuneID.Lust) && Random.value < 0.25f;

            var lib = modularLibrary != null ? modularLibrary : ModularSpriteLibrary.Load();
            if (lib != null) TownsfolkVisualApplier.SetLibrary(lib);

            RecruitInstance r = new RecruitInstance
            {
                unitID = persistentID,
                characterName = AllocateUniqueCharacterName(),
                unitType = type,
                pewSeatIndex = pewSeat,
                battleSlotIndex = pewSeat,
                isDeployed = true,
                isScouted = unlockTier1,
                randomAvatar = (pool != null && pool.Count > 0) ? pool[Random.Range(0, pool.Count)] : null,
                intelTierUnlocked = unlockTier1 ? 1 : 0,
                isSirenEnchantress = lustSiren,
                appearance = TownsfolkAppearance.Generate(type, persistentID, pewSeat, lib)
            };
            // Master Spec Phase 1: base units never carry fusion memory.
            CardFusionSystem.SanitizeBaseUnitFusionFlags(r);
            r.SyncModularIdsFromAppearance();
            if (unlockTier1)
                IntelGossip.MarkScouted(r);

            // Optional CharacterDefinition portrait when avatar pool is empty.
            if (r.randomAvatar == null)
            {
                var db = characterDatabase != null ? characterDatabase : CharacterDatabase.Load();
                var troopDef = db?.GetByUnitType(type);
                if (troopDef != null)
                    r.randomAvatar = troopDef.portraitSprite != null ? troopDef.portraitSprite : troopDef.battleSprite;
            }

            // Keep scout/dossier reveals across nights/waves until WipeAllSavedMemory.
            if (GrudgeSaveManager.Instance != null)
            {
                int remembered = GrudgeSaveManager.Instance.GetRememberedIntel(persistentID);
                if (remembered > r.intelTierUnlocked)
                    r.intelTierUnlocked = remembered;
            }
            if (r.intelTierUnlocked >= 1)
                IntelGossip.MarkScouted(r);

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

            // 5-tier Hidden Potential after base traits (Liability may retarget primaryTrait).
            HiddenPotentialSystem.RollOnto(r);

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

            // BODY = Tab 4 ClassSpritePool only. FACE overlay = Tab 5 (optional headSprite).
            // Never assign face-pool sprites as bodySprite (soft-fail inside ResolvePewVisual).
            Sprite architectBody = LegendPewPortraitApplier.ResolvePewVisual(r);
            if (architectBody != null && TownsfolkVisualApplier.IsUsablePewSprite(architectBody)
                && !LegendPewPortraitApplier.IsFacePoolSprite(architectBody))
            {
                r.bodySprite = architectBody;
                r.randomAvatar = architectBody;
            }
            else if (lib != null)
            {
                var pose = lib.GetPose(r.appearance.poseIndex);
                if (pose != null && !LegendPewPortraitApplier.IsFacePoolSprite(pose))
                {
                    r.bodySprite = pose;
                    if (r.randomAvatar == null) r.randomAvatar = pose;
                }
            }

            if (r.bodySprite == null)
            {
                List<Sprite> bodyPool = bodyUpright;
                if (r.bodyPosture == BodyPosture.EagerForward) bodyPool = bodyEager;
                else if (r.bodyPosture == BodyPosture.SlouchedExit) bodyPool = bodySlouched;
                if (bodyPool != null && bodyPool.Count > 0)
                    r.bodySprite = bodyPool[Random.Range(0, bodyPool.Count)];
            }

            Sprite face = LegendPewPortraitApplier.ResolvePewFaceOverlay(r);
            r.headSprite = (face != null && !LegendPewPortraitApplier.IsBodyPoolSprite(face)) ? face : null;

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
