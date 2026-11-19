using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArthurianAutobattler
{
    [CreateAssetMenu(fileName = "SomersetVisualDatabase", menuName = "Somerset/Visual Database")]
    public class SomersetVisualDatabase : ScriptableObject
    {
        [Header("Paper-Doll Bodies")]
        public List<Sprite> bodyEager;
        public List<Sprite> bodyUpright;
        public List<Sprite> bodySlouched;

        [Header("Heads by Class")]
        public List<Sprite> knightHeads;
        public List<Sprite> warriorHeads;
        public List<Sprite> peasantHeads;

        [Header("Mood Overlays")]
        public Sprite moodGrudge;
        public Sprite moodLoyalist;
        public Sprite moodCoward;

        [Header("Hero / Boss Art")]
        public List<CodexArtEntry> legendArt = new List<CodexArtEntry>();
        public List<CodexArtEntry> bossArt = new List<CodexArtEntry>();

        [Header("Dialogue & Gossip")]
        public List<string> pewDialogues = new List<string>
        {
            "Demons claw the churchyard. Show gold — or a plan.",
            "I remember dying in this pew. Pay more.",
            "For King Arthur — half price.",
            "I'm no hero... unless you steel my nerves."
        };

        public List<string> gossipWhispers = new List<string>
        {
            "He watches the exit.",
            "She mutters prayers under her breath.",
            "That one drinks the offering wine.",
            "Loyal to the crown, or so they claim."
        };

        [Header("Animators (optional)")]
        public RuntimeAnimatorController pewIdleController;
        public RuntimeAnimatorController merlinResurrectionController;

        public void SyncToTownGenerator(TownRNGGenerator gen)
        {
            if (gen == null) return;
            gen.bodyEager = new List<Sprite>(bodyEager);
            gen.bodyUpright = new List<Sprite>(bodyUpright);
            gen.bodySlouched = new List<Sprite>(bodySlouched);
            gen.headPool = new List<Sprite>();
            gen.headPool.AddRange(knightHeads);
            gen.headPool.AddRange(warriorHeads);
            gen.headPool.AddRange(peasantHeads);
            gen.knightAvatars = new List<Sprite>(knightHeads);
            gen.warriorAvatars = new List<Sprite>(warriorHeads);
            gen.peasantAvatars = new List<Sprite>(peasantHeads);
            gen.moodGrudge = moodGrudge;
            gen.moodLoyalist = moodLoyalist;
            gen.moodCoward = moodCoward;
        }
    }

    [Serializable]
    public class CodexArtEntry
    {
        public string displayName;
        public UnitType unitType;
        public Sprite portrait;
        public string blurb;
    }
}
