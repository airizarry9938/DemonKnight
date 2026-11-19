using UnityEngine;

namespace ArthurianAutobattler
{
    public class SinRuneManager : MonoBehaviour
    {
        public static SinRuneManager Instance;

        public bool jealousyActive;
        public bool lustActive;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public bool IsActive(SinRuneID id)
        {
            switch (id)
            {
                case SinRuneID.Jealousy: return jealousyActive;
                case SinRuneID.Lust: return lustActive;
                default: return false;
            }
        }

        public void SetSinRune(SinRuneID id, bool active)
        {
            switch (id)
            {
                case SinRuneID.Jealousy: jealousyActive = active; break;
                case SinRuneID.Lust: lustActive = active; break;
            }
        }

        public float GetLegacyPointMultiplier()
        {
            float m = 1f;
            if (jealousyActive) m += 0.75f;
            if (lustActive) m += 1.0f;
            return m;
        }

        /// <summary>Jealousy flips Loyalists into Outlaws when a legend is fused into the party.</summary>
        public void OnLegendFused()
        {
            if (!jealousyActive || GameLoopManager.Instance == null) return;
            foreach (var m in GameLoopManager.Instance.recruitedParty)
            {
                if (m.primaryTrait == TraitTag.Loyalist && !RecruitInstance.IsLegendUnit(m.unitType))
                    m.primaryTrait = TraitTag.Outlaw;
            }
        }
    }
}
