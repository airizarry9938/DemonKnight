using UnityEngine;
using TMPro;

namespace ArthurianAutobattler
{
    /// <summary>
    /// Thin alias kept so existing scene references keep working.
    /// Most logic lives in SomersetChurchUI.cs.
    /// SetNight is defined here so GameLoopManager always compiles even when a
    /// partial Sync left SomersetChurchUI without the night API.
    /// If you see CS0111 on OpenModal/RefreshHUD, DELETE this file's old contents
    /// and replace with only this stub (or run Arthurian → Repair Church UI).
    /// </summary>
    public class ChurchStageUIController : SomersetChurchUI
    {
        /// <summary>
        /// Update the top-HUD Night label. Resolves NightText by name so this
        /// compiles even if the base SomersetChurchUI copy is missing nightText/SetNight.
        /// </summary>
        public void SetNight(int night)
        {
            int n = Mathf.Max(1, night);
            TextMeshProUGUI tmp = ResolveNightLabel();
            if (tmp != null) tmp.text = $"Night {n}";
        }

        private static TextMeshProUGUI ResolveNightLabel()
        {
            var hud = GameObject.Find("HUD_Bar");
            if (hud != null)
            {
                var child = hud.transform.Find("NightText");
                if (child != null)
                {
                    var tmp = child.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) return tmp;
                }
            }

            var found = GameObject.Find("NightText");
            return found != null ? found.GetComponent<TextMeshProUGUI>() : null;
        }
    }
}
