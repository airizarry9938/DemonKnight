using UnityEngine;
using TMPro;

namespace ArthurianAutobattler
{
    public class FloatingCombatText : MonoBehaviour
    {
        public TextMeshProUGUI popupText;
        public float floatSpeed = 1.4f;
        public float lifetime = 0.85f;

        private float timer;
        private Color startColor;

        public void Setup(string message, Color textColor)
        {
            if (popupText == null) popupText = GetComponentInChildren<TextMeshProUGUI>();
            popupText.text = message;
            popupText.color = textColor;
            startColor = textColor;
            timer = lifetime;
            transform.position += new Vector3(Random.Range(-0.25f, 0.25f), 0.5f, 0f);
        }

        private void Update()
        {
            transform.position += Vector3.up * (floatSpeed * Time.deltaTime);
            timer -= Time.deltaTime;

            if (popupText != null)
            {
                popupText.color = new Color(
                    startColor.r, startColor.g, startColor.b,
                    Mathf.Clamp01(timer / lifetime));
            }

            if (timer <= 0f) Destroy(gameObject);
        }
    }
}
