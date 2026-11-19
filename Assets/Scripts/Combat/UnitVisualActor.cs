using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArthurianAutobattler
{
    public class UnitVisualActor : MonoBehaviour
    {
        public SpriteRenderer spriteRenderer;
        public Slider healthBar;
        public CombatUnit boundUnit;

        private Vector3 homePosition;

        public void Initialize(CombatUnit unit, Sprite artwork, Vector3 spawnPos, bool flipX)
        {
            boundUnit = unit;
            homePosition = spawnPos;
            transform.position = spawnPos;

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = artwork;
                spriteRenderer.flipX = flipX;
            }

            if (healthBar != null)
            {
                healthBar.maxValue = unit.maxHealth;
                healthBar.value = unit.currentHealth;
            }
        }

        private void Update()
        {
            if (boundUnit == null) return;
            if (healthBar != null) healthBar.value = Mathf.Max(0, boundUnit.currentHealth);
            if (!boundUnit.IsAlive) Destroy(gameObject, 0.25f);
        }

        public void PlayAttackLunge(Vector3 targetPos)
        {
            if (!gameObject.activeInHierarchy) return;
            StopAllCoroutines();
            StartCoroutine(LungeRoutine(targetPos));
        }

        public void PlayHitFlash()
        {
            if (!gameObject.activeInHierarchy || spriteRenderer == null) return;
            StartCoroutine(FlashRedRoutine());
        }

        private IEnumerator LungeRoutine(Vector3 targetPos)
        {
            Vector3 strike = Vector3.Lerp(homePosition, targetPos, 0.45f);
            float t = 0f;
            while (t < 0.12f)
            {
                transform.position = Vector3.Lerp(homePosition, strike, t / 0.12f);
                t += Time.deltaTime;
                yield return null;
            }

            t = 0f;
            while (t < 0.12f)
            {
                transform.position = Vector3.Lerp(strike, homePosition, t / 0.12f);
                t += Time.deltaTime;
                yield return null;
            }

            transform.position = homePosition;
        }

        private IEnumerator FlashRedRoutine()
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }
    }
}
