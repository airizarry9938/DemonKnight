using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArthurianAutobattler
{
    public class CardFusionSystem : MonoBehaviour
    {
        public static CardFusionSystem Instance;

        [Header("Hero Sprites")]
        public Sprite priestSprite;
        public Sprite captainSprite;
        public Sprite kingArthurSprite;
        public Sprite merlinSprite;
        public Sprite lancelotSprite;
        public Sprite morganSprite;
        public Sprite ladyOfLakeSprite;
        public Sprite gawainSprite;
        public Sprite mordredSprite;
        public Sprite galahadSprite;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public bool CanFusePriest(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Peasant) >= 2;

        public bool CanFuseCaptain(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Warrior) >= 2;

        public bool CanFuseKingArthur(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Knight) >= 2
            && p.Count(u => u.unitType == UnitType.Captain) >= 1
            && !p.Any(u => u.unitType == UnitType.KingArthur);

        public bool CanFuseMerlin(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Priest) >= 3
            && p.Count(u => u.unitType == UnitType.Warrior) >= 2
            && p.Count(u => u.unitType == UnitType.Knight) >= 2
            && !p.Any(u => u.unitType == UnitType.Merlin);

        public bool CanFuseLancelot(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Knight) >= 3
            && p.Any(u => u.unitType == UnitType.Knight && u.primaryTrait == TraitTag.Noble)
            && !p.Any(u => u.unitType == UnitType.SirLancelot);

        public bool CanFuseMorganLeFay(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Priest) >= 2
            && p.Count(u => u.primaryTrait == TraitTag.Outlaw) >= 2
            && !p.Any(u => u.unitType == UnitType.MorganLeFay);

        public bool CanFuseLadyOfTheLake(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Priest) >= 3
            && p.Count(u => u.unitType == UnitType.Captain) >= 1
            && !p.Any(u => u.unitType == UnitType.LadyOfTheLake);

        public bool CanFuseGawain(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Warrior) >= 2
            && p.Count(u => u.unitType == UnitType.Knight) >= 2
            && !p.Any(u => u.unitType == UnitType.SirGawain);

        public bool CanFuseMordred(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Knight) >= 2
            && p.Count(u => u.primaryTrait == TraitTag.Outlaw) >= 2
            && !p.Any(u => u.unitType == UnitType.Mordred);

        public bool CanFuseGalahad(List<RecruitInstance> p) =>
            p.Count(u => u.unitType == UnitType.Knight
                         && (u.primaryTrait == TraitTag.Zealot || u.primaryTrait == TraitTag.Loyalist)) >= 2
            && p.Count(u => u.unitType == UnitType.Priest) >= 1
            && !p.Any(u => u.unitType == UnitType.SirGalahad);

        public bool FusePriest(List<RecruitInstance> p)
        {
            if (!CanFusePriest(p)) return false;
            ConsumeUnits(p, UnitType.Peasant, 2);
            p.Add(CreateHero("unit_priest", "Town Priest", UnitType.Priest, priestSprite, 35, 8, 1.4f, TraitTag.Zealot, CombatRole.BacklineRanged));
            return true;
        }

        public bool FuseCaptain(List<RecruitInstance> p)
        {
            if (!CanFuseCaptain(p)) return false;
            ConsumeUnits(p, UnitType.Warrior, 2);
            p.Add(CreateHero("unit_captain", "Vanguard Captain", UnitType.Captain, captainSprite, 95, 14, 1.6f, TraitTag.Noble, CombatRole.FrontlineTank));
            return true;
        }

        public bool FuseKingArthur(List<RecruitInstance> p)
        {
            if (!CanFuseKingArthur(p)) return false;
            ConsumeUnits(p, UnitType.Knight, 2);
            ConsumeUnits(p, UnitType.Captain, 1);
            p.Add(CreateHero("hero_arthur", "King Arthur", UnitType.KingArthur, kingArthurSprite, 260, 32, 1.9f, TraitTag.Loyalist, CombatRole.FrontlineTank));
            return true;
        }

        public bool FuseMerlin(List<RecruitInstance> p)
        {
            if (!CanFuseMerlin(p)) return false;
            ConsumeUnits(p, UnitType.Priest, 3);
            ConsumeUnits(p, UnitType.Warrior, 2);
            ConsumeUnits(p, UnitType.Knight, 2);
            p.Add(CreateHero("hero_merlin", "Merlin the Archmage", UnitType.Merlin, merlinSprite, 180, 28, 1.5f, TraitTag.Zealot, CombatRole.BacklineRanged));
            return true;
        }

        public bool FuseLancelot(List<RecruitInstance> p)
        {
            if (!CanFuseLancelot(p)) return false;
            ConsumeUnits(p, UnitType.Knight, 3);
            p.Add(CreateHero("hero_lancelot", "Sir Lancelot", UnitType.SirLancelot, lancelotSprite, 210, 26, 2.0f, TraitTag.Noble, CombatRole.FrontlineTank));
            return true;
        }

        public bool FuseMorganLeFay(List<RecruitInstance> p)
        {
            if (!CanFuseMorganLeFay(p)) return false;
            ConsumeUnits(p, UnitType.Priest, 2);
            ConsumeByTrait(p, TraitTag.Outlaw, 2);
            p.Add(CreateHero("hero_morgan", "Morgan le Fay", UnitType.MorganLeFay, morganSprite, 165, 22, 1.5f, TraitTag.Outlaw, CombatRole.BacklineRanged));
            return true;
        }

        public bool FuseLadyOfTheLake(List<RecruitInstance> p)
        {
            if (!CanFuseLadyOfTheLake(p)) return false;
            ConsumeUnits(p, UnitType.Priest, 3);
            ConsumeUnits(p, UnitType.Captain, 1);
            p.Add(CreateHero("hero_lady_lake", "Lady of the Lake", UnitType.LadyOfTheLake, ladyOfLakeSprite, 190, 14, 1.4f, TraitTag.Zealot, CombatRole.BacklineRanged));
            return true;
        }

        public bool FuseGawain(List<RecruitInstance> p)
        {
            if (!CanFuseGawain(p)) return false;
            ConsumeUnits(p, UnitType.Warrior, 2);
            ConsumeUnits(p, UnitType.Knight, 2);
            p.Add(CreateHero("hero_gawain", "Sir Gawain", UnitType.SirGawain, gawainSprite, 230, 24, 1.6f, TraitTag.Loyalist, CombatRole.FrontlineTank));
            return true;
        }

        public bool FuseMordred(List<RecruitInstance> p)
        {
            if (!CanFuseMordred(p)) return false;
            ConsumeUnits(p, UnitType.Knight, 2);
            ConsumeByTrait(p, TraitTag.Outlaw, 2);
            p.Add(CreateHero("hero_mordred", "Mordred", UnitType.Mordred, mordredSprite, 240, 34, 1.8f, TraitTag.Outlaw, CombatRole.FrontlineTank));
            return true;
        }

        public bool FuseGalahad(List<RecruitInstance> p)
        {
            if (!CanFuseGalahad(p)) return false;
            ConsumeUnits(p, UnitType.Knight, 2);
            ConsumeUnits(p, UnitType.Priest, 1);
            p.Add(CreateHero("hero_galahad", "Sir Galahad", UnitType.SirGalahad, galahadSprite, 250, 25, 1.7f, TraitTag.Loyalist, CombatRole.FrontlineTank));
            return true;
        }

        private void ConsumeUnits(List<RecruitInstance> party, UnitType type, int count)
        {
            int removed = 0;
            for (int i = party.Count - 1; i >= 0 && removed < count; i--)
            {
                if (party[i].unitType == type)
                {
                    party.RemoveAt(i);
                    removed++;
                }
            }
        }

        private void ConsumeByTrait(List<RecruitInstance> party, TraitTag trait, int count)
        {
            int removed = 0;
            for (int i = party.Count - 1; i >= 0 && removed < count; i--)
            {
                if (party[i].primaryTrait == trait)
                {
                    party.RemoveAt(i);
                    removed++;
                }
            }
        }

        private RecruitInstance CreateHero(string id, string name, UnitType type, Sprite sp, int hp, int atk, float aps, TraitTag trait, CombatRole role)
        {
            return new RecruitInstance
            {
                unitID = id,
                characterName = name,
                unitType = type,
                randomAvatar = sp,
                maxHealth = hp,
                attackDamage = atk,
                attackSpeed = aps,
                primaryTrait = trait,
                assignedRole = role,
                intelTierUnlocked = 3
            };
        }
    }
}
