#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ArthurianAutobattler;

/// <summary>
/// Seeds CharacterDefinition assets for every current Legend / Boss / Demon so
/// CardFusion / Horde / HUD can run data-driven without code edits for new entries.
/// Menu: DemonKnight → Characters → Seed Default Character Definitions
/// </summary>
public static class ArthurianCharacterCatalogSeeder
{
    const string Root = "Assets/Data/Characters";

    [MenuItem("Arthurian/Art Tools/Seed Default Character Definitions", false, 43)]
    [MenuItem("DemonKnight/Characters/Seed Default Character Definitions", false, 43)]
    public static void SeedAll()
    {
        EnsureFolder(Root + "/Legends");
        EnsureFolder(Root + "/Bosses");
        EnsureFolder(Root + "/Demons");

        // —— Legends / fusions ——
        SeedLegend("Priest", "unit_priest", "Town Priest", UnitType.Priest,
            CharacterCombatRole.Backline, TraitTag.Zealot, 40, 9, 1.4f, true, false,
            CharacterPassiveType.None, 0f, "Backline support priest.",
            Rule(UnitType.Peasant, 2));

        SeedLegend("Captain", "unit_captain", "Vanguard Captain", UnitType.Captain,
            CharacterCombatRole.Tank, TraitTag.Noble, 110, 16, 1.6f, false, false,
            CharacterPassiveType.None, 0f, "Frontline captain forged from warriors.",
            Rule(UnitType.Warrior, 2));

        SeedLegend("KingArthur", "hero_arthur", "King Arthur", UnitType.KingArthur,
            CharacterCombatRole.Tank, TraitTag.Loyalist, 290, 34, 1.9f, false, false,
            CharacterPassiveType.AuraAtkBuff, 0f, "High King of Camelot — anchors the front.",
            Rule(UnitType.Knight, 2), Rule(UnitType.Captain, 1));

        SeedLegend("Merlin", "hero_merlin", "Merlin the Archmage", UnitType.Merlin,
            CharacterCombatRole.Backline, TraitTag.Zealot, 195, 30, 1.5f, true, false,
            CharacterPassiveType.None, 0f, "Archmage — devastating backline caster.",
            Rule(UnitType.Priest, 2), Rule(UnitType.Knight, 1));

        SeedLegend("Lancelot", "hero_lancelot", "Sir Lancelot", UnitType.SirLancelot,
            CharacterCombatRole.Tank, TraitTag.Noble, 240, 29, 2.0f, false, false,
            CharacterPassiveType.ExtraDamageVsFront, 0f, "Peerless knight — strikes the front hard.",
            Rule(UnitType.Knight, 1, TraitTag.Noble),
            Rule(UnitType.Knight, 1),
            Rule(UnitType.Warrior, 1));

        SeedLegend("Morgan", "hero_morgan", "Morgan le Fay", UnitType.MorganLeFay,
            CharacterCombatRole.Backline, TraitTag.Outlaw, 175, 24, 1.5f, true, false,
            CharacterPassiveType.Custom, 0f, "Dark sorceress — hexes and withers foes.",
            Rule(UnitType.Priest, 2),
            TraitOnly(TraitTag.Outlaw, 1));

        SeedLegend("LadyOfTheLake", "hero_lady_lake", "Lady of the Lake", UnitType.LadyOfTheLake,
            CharacterCombatRole.Backline, TraitTag.Zealot, 210, 18, 1.4f, true, false,
            CharacterPassiveType.HealAlliesOnHit, 0f, "Blesses allies; backline mystic.",
            Rule(UnitType.Priest, 2), Rule(UnitType.Captain, 1));

        SeedLegend("Gawain", "hero_gawain", "Sir Gawain", UnitType.SirGawain,
            CharacterCombatRole.Tank, TraitTag.Loyalist, 245, 26, 1.6f, false, false,
            CharacterPassiveType.None, 0f, "Sun-knight of the Round Table.",
            Rule(UnitType.Warrior, 2), Rule(UnitType.Knight, 1));

        SeedLegend("Mordred", "hero_mordred", "Mordred", UnitType.Mordred,
            CharacterCombatRole.Tank, TraitTag.Outlaw, 260, 36, 1.8f, false, true,
            CharacterPassiveType.PersistAcrossWaves, 0f,
            "Usurper — persists across waves until slain (never returns to a pew).",
            Rule(UnitType.Knight, 2), TraitOnly(TraitTag.Outlaw, 1));

        SeedLegend("Galahad", "hero_galahad", "Sir Galahad", UnitType.SirGalahad,
            CharacterCombatRole.Tank, TraitTag.Loyalist, 270, 27, 1.7f, false, false,
            CharacterPassiveType.None, 0f, "Grail knight — pure of purpose.",
            Rule(UnitType.Knight, 1, TraitTag.Zealot, TraitTag.Loyalist),
            Rule(UnitType.Priest, 1),
            Rule(UnitType.Warrior, 1));

        SeedLegend("Tristan", "hero_tristan", "Sir Tristan", UnitType.SirTristan,
            CharacterCombatRole.Backline, TraitTag.Noble, 185, 23, 1.8f, true, false,
            CharacterPassiveType.None, 0f, "Marksman knight — can attack from back.",
            Rule(UnitType.Knight, 1),
            Rule(UnitType.Peasant, 1, TraitTag.Outlaw, TraitTag.Noble));

        SeedLegend("Percival", "hero_percival", "Sir Percival", UnitType.SirPercival,
            CharacterCombatRole.Tank, TraitTag.Zealot, 200, 22, 1.6f, false, false,
            CharacterPassiveType.None, 0f, "Questing knight of the Grail road.",
            Rule(UnitType.Knight, 1, TraitTag.Zealot),
            Rule(UnitType.Peasant, 1));

        SeedLegend("Kay", "hero_kay", "Sir Kay", UnitType.SirKay,
            CharacterCombatRole.Tank, TraitTag.Loyalist, 135, 17, 1.5f, false, false,
            CharacterPassiveType.None, 0f, "Seneschal — sturdy early legend.",
            Rule(UnitType.Warrior, 1), Rule(UnitType.Peasant, 2));

        SeedLegend("Bedivere", "hero_bedivere", "Sir Bedivere", UnitType.SirBedivere,
            CharacterCombatRole.Tank, TraitTag.Loyalist, 190, 21, 1.7f, false, false,
            CharacterPassiveType.None, 0f, "One-handed knight — loyal to the end.",
            Rule(UnitType.Knight, 1), Rule(UnitType.Warrior, 1));

        SeedLegend("Guinevere", "hero_guinevere", "Guinevere", UnitType.Guinevere,
            CharacterCombatRole.Backline, TraitTag.Noble, 140, 12, 1.4f, true, false,
            CharacterPassiveType.AuraHpBuff, 0f, "Queen — steadies the backline.",
            TraitOnly(TraitTag.Noble, 1), Rule(UnitType.Priest, 1));

        // —— Bosses (wave 3–10) ——
        SeedBoss("BlackKnight", "boss_black_knight", "The Black Knight", UnitType.BossBlackKnight,
            3, 210, 22, 1.4f, 22, 2.0f,
            CharacterPassiveType.SurviveFirstLethalAt1Hp, 1f,
            "Survives first lethal blow at 1 HP.");

        SeedBoss("Vortigern", "boss_vortigern", "Vortigern, Usurper King", UnitType.BossVortigern,
            4, 260, 26, 1.3f, 28, 2.0f,
            CharacterPassiveType.Custom, 0f, "Usurper King — brutal frontline pressure.");

        SeedBoss("GreenKnight", "boss_green_knight", "The Green Knight", UnitType.BossGreenKnight,
            5, 320, 32, 1.2f, 34, 2.0f,
            CharacterPassiveType.ReviveOnce, 1f, "Returns once after falling (Green Knight trial).");

        SeedBoss("Accolon", "boss_accolon", "Accolon of Gaul", UnitType.BossAccolon,
            6, 380, 38, 1.7f, 40, 2.0f,
            CharacterPassiveType.ExtraDamageVsFront, 0f, "Enchanted blade — shreds the front.");

        SeedBoss("Meleagant", "boss_meleagant", "Sir Meleagant", UnitType.BossMeleagant,
            7, 440, 42, 1.6f, 44, 2.0f,
            CharacterPassiveType.Custom, 0f, "Kidnapper knight — locks the front.");

        SeedBoss("KingLot", "boss_king_lot", "King Lot of Lothian", UnitType.BossKingLot,
            8, 510, 46, 1.5f, 48, 2.0f,
            CharacterPassiveType.AuraAtkBuff, 0f, "Warlord king — inspires the horde.");

        SeedBoss("QuestingBeast", "boss_questing_beast", "The Questing Beast", UnitType.BossQuestingBeast,
            9, 600, 50, 1.9f, 52, 2.1f,
            CharacterPassiveType.Custom, 0f, "Serpent-tailed beast — feet/tail sit on the cobbles.");

        SeedBoss("Nimue", "boss_nimue", "Nimue, Queen of Thorns", UnitType.BossNimue,
            10, 720, 55, 1.6f, 55, 2.2f,
            CharacterPassiveType.CharmChance, 0.2f, "Queen of Thorns — charms and entangles.");

        // —— Demons ——
        SeedDemon("Imp", "demon_imp", "Frenzied Imp", UnitType.DemonImp,
            22, 6, 1.7f, 3, 1.0f, CharacterPassiveType.None, 0f, "Cheap chaotic fodder.");
        SeedDemon("Hellhound", "demon_hound", "Rabid Hellhound", UnitType.DemonHellhound,
            65, 12, 1.6f, 7, 1.0f, CharacterPassiveType.None, 0f, "Fast mid-cost demon.");
        SeedDemon("DreadLord", "demon_lord", "Feral Dread Lord", UnitType.DemonDreadLord,
            185, 26, 1.2f, 18, 1.4f, CharacterPassiveType.None, 0f, "Elite demon bruiser.");

        var db = ArthurianCharacterDatabaseEditor.EnsureDatabaseAsset();
        ArthurianCharacterDatabaseEditor.AutoCollect(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Character Catalog",
            "Seeded default Legend / Boss / Demon definitions and refreshed CharacterDatabase.",
            "OK");
    }

    static void SeedLegend(
        string file, string id, string name, UnitType type,
        CharacterCombatRole role, TraitTag trait,
        int hp, int atk, float aps, bool fromBack, bool persist,
        CharacterPassiveType passive, float pVal, string pDesc,
        params FusionIngredientRule[] recipe)
    {
        var def = LoadOrCreate(Root + "/Legends/" + file + ".asset");
        def.characterId = id;
        def.displayName = name;
        def.category = CharacterCategory.Legend;
        def.unitType = type;
        def.role = role;
        def.trait = trait;
        def.baseHP = hp;
        def.baseATK = atk;
        def.baseAPS = aps;
        def.canAttackFromBack = fromBack;
        def.powerCost = 0;
        def.visualScale = 1f;
        def.persistsAcrossWaves = persist;
        def.bossWaveNumber = 0;
        def.passiveType = passive;
        def.passiveValue = pVal;
        def.passiveDescription = pDesc;
        def.fusionRecipe = new List<FusionIngredientRule>(recipe);
        EditorUtility.SetDirty(def);
    }

    static void SeedBoss(
        string file, string id, string name, UnitType type,
        int wave, int hp, int atk, float aps, int power, float scale,
        CharacterPassiveType passive, float pVal, string pDesc)
    {
        var def = LoadOrCreate(Root + "/Bosses/" + file + ".asset");
        def.characterId = id;
        def.displayName = name;
        def.category = CharacterCategory.Boss;
        def.unitType = type;
        def.role = CharacterCombatRole.Tank;
        def.trait = TraitTag.None;
        def.baseHP = hp;
        def.baseATK = atk;
        def.baseAPS = aps;
        def.canAttackFromBack = false;
        def.powerCost = power;
        def.visualScale = scale;
        def.persistsAcrossWaves = false;
        def.bossWaveNumber = wave;
        def.passiveType = passive;
        def.passiveValue = pVal;
        def.passiveDescription = pDesc;
        def.fusionRecipe = new List<FusionIngredientRule>();
        EditorUtility.SetDirty(def);
    }

    static void SeedDemon(
        string file, string id, string name, UnitType type,
        int hp, int atk, float aps, int power, float scale,
        CharacterPassiveType passive, float pVal, string pDesc)
    {
        var def = LoadOrCreate(Root + "/Demons/" + file + ".asset");
        def.characterId = id;
        def.displayName = name;
        def.category = CharacterCategory.Demon;
        def.unitType = type;
        def.role = CharacterCombatRole.Hybrid;
        def.trait = TraitTag.None;
        def.baseHP = hp;
        def.baseATK = atk;
        def.baseAPS = aps;
        def.canAttackFromBack = false;
        def.powerCost = power;
        def.visualScale = scale;
        def.persistsAcrossWaves = false;
        def.bossWaveNumber = 0;
        def.passiveType = passive;
        def.passiveValue = pVal;
        def.passiveDescription = pDesc;
        def.fusionRecipe = new List<FusionIngredientRule>();
        EditorUtility.SetDirty(def);
    }

    static FusionIngredientRule Rule(UnitType type, int count, TraitTag trait = TraitTag.None, TraitTag alt = TraitTag.None)
    {
        return new FusionIngredientRule
        {
            matchUnitType = true,
            unitType = type,
            requiredTrait = trait,
            alternateTrait = alt,
            count = count
        };
    }

    static FusionIngredientRule TraitOnly(TraitTag trait, int count)
    {
        return new FusionIngredientRule
        {
            matchUnitType = false,
            unitType = UnitType.Peasant,
            requiredTrait = trait,
            alternateTrait = TraitTag.None,
            count = count
        };
    }

    static CharacterDefinition LoadOrCreate(string path)
    {
        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        var existing = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
        if (existing != null) return existing;
        var def = ScriptableObject.CreateInstance<CharacterDefinition>();
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        path = path.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
