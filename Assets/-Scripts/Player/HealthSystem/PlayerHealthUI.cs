using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UGG.Health
{
    public class PlayerHealthUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealthSystem playerHealthSystem;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("Display")]
        [SerializeField] private bool updateSlider = true;
        [SerializeField] private bool updateFillImage = true;
        [SerializeField] private bool updateHealthText = true;
        [SerializeField] private bool useNormalizedValue = true;

        [Header("血条美化")]
        [SerializeField] private bool useSmoothHealth = true;
        [SerializeField] private float healthSmoothSpeed = 14f;
        [SerializeField] private bool createDelayDamageBar = true;
        [SerializeField] private Color delayDamageColor = new Color(1f, 0.78f, 0.25f, 0.86f);
        [SerializeField] private float delayStartTime = 0.22f;
        [SerializeField] private float delaySmoothSpeed = 4f;
        [SerializeField] private bool createDarkBackground = true;
        [SerializeField] private Color backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.72f);

        [Header("血量颜色")]
        [SerializeField] private Color highHealthColor = new Color(0.95f, 0.08f, 0.12f, 1f);
        [SerializeField] private Color midHealthColor = new Color(1f, 0.48f, 0.08f, 1f);
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.02f, 0.02f, 1f);
        [SerializeField, Range(0f, 1f)] private float midHealthThreshold = 0.55f;
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.25f;

        [Header("受伤反馈")]
        [SerializeField] private bool flashOnDamage = true;
        [SerializeField] private Color damageFlashColor = Color.white;
        [SerializeField] private float damageFlashTime = 0.12f;
        [SerializeField] private bool pulseWhenLowHealth = true;
        [SerializeField] private float lowHealthPulseSpeed = 7f;
        [SerializeField] private float lowHealthPulseAmount = 0.28f;
        [Header("Recovering Glow")]
        [SerializeField] private bool showRecoveringGlow = true;
        [SerializeField] private Color recoveringGlowColor = new Color(0.2f, 0.95f, 1f, 0.85f);
        [SerializeField] private float recoveringGlowPulseSpeed = 2.4f;
        [SerializeField, Range(0f, 1f)] private float recoveringGlowMinAlpha = 0.18f;
        [SerializeField, Range(0f, 1f)] private float recoveringGlowMaxAlpha = 0.72f;
        [SerializeField] private Vector2 recoveringGlowSizeOffset = new Vector2(14f, 10f);

        private Image delayDamageImage;
        private Image backgroundImage;
        private Image recoveringGlowImage;
        private float displayedNormalizedHealth = 1f;
        private float delayedNormalizedHealth = 1f;
        private float previousNormalizedHealth = 1f;
        private float delayTimer;
        private float flashTimer;
        private bool initialized;
        private HeartRateStateController heartRateStateController;

        private void Awake()
        {
            TryAutoBindPlayerHealth();
            TryAutoBindHeartRateStateController();
            TryBuildExtraBars();
            ForceRefreshUI();
        }

        private void Update()
        {
            TryAutoBindHeartRateStateController();
            RefreshUI(Time.deltaTime);
        }

        private void ForceRefreshUI()
        {
            if (playerHealthSystem == null)
            {
                return;
            }

            float normalizedHealth = Mathf.Clamp01(playerHealthSystem.HealthNormalized);
            displayedNormalizedHealth = normalizedHealth;
            delayedNormalizedHealth = normalizedHealth;
            previousNormalizedHealth = normalizedHealth;
            initialized = true;
            RefreshUI(0f);
        }

        private void RefreshUI(float deltaTime)
        {
            if (playerHealthSystem == null)
            {
                return;
            }

            float normalizedHealth = Mathf.Clamp01(playerHealthSystem.HealthNormalized);
            float currentHealth = playerHealthSystem.CurrentHealth;
            float maxHealth = playerHealthSystem.MaxHealth;

            if (!initialized)
            {
                displayedNormalizedHealth = normalizedHealth;
                delayedNormalizedHealth = normalizedHealth;
                previousNormalizedHealth = normalizedHealth;
                initialized = true;
            }

            if (normalizedHealth < previousNormalizedHealth)
            {
                delayTimer = delayStartTime;
                flashTimer = damageFlashTime;
            }
            else if (normalizedHealth > previousNormalizedHealth)
            {
                delayedNormalizedHealth = normalizedHealth;
            }

            previousNormalizedHealth = normalizedHealth;

            if (useSmoothHealth && deltaTime > 0f)
            {
                displayedNormalizedHealth = Mathf.Lerp(displayedNormalizedHealth, normalizedHealth, 1f - Mathf.Exp(-healthSmoothSpeed * deltaTime));
            }
            else
            {
                displayedNormalizedHealth = normalizedHealth;
            }

            if (delayTimer > 0f)
            {
                delayTimer -= deltaTime;
            }
            else if (deltaTime > 0f)
            {
                delayedNormalizedHealth = Mathf.Lerp(delayedNormalizedHealth, normalizedHealth, 1f - Mathf.Exp(-delaySmoothSpeed * deltaTime));
            }

            if (flashTimer > 0f)
            {
                flashTimer -= deltaTime;
            }

            if (updateSlider && healthSlider != null)
            {
                if (useNormalizedValue)
                {
                    healthSlider.minValue = 0f;
                    healthSlider.maxValue = 1f;
                    healthSlider.value = displayedNormalizedHealth;
                }
                else
                {
                    healthSlider.minValue = 0f;
                    healthSlider.maxValue = maxHealth;
                    healthSlider.value = displayedNormalizedHealth * maxHealth;
                }
            }

            if (updateFillImage && healthFillImage != null)
            {
                healthFillImage.fillAmount = displayedNormalizedHealth;
                healthFillImage.color = GetDisplayHealthColor(normalizedHealth, deltaTime);
            }

            if (delayDamageImage != null)
            {
                delayDamageImage.fillAmount = delayedNormalizedHealth;
            }

            UpdateRecoveringGlow();

            if (updateHealthText && healthText != null)
            {
                healthText.text = $"{currentHealth:0} / {maxHealth:0}";
            }
        }

        private Color GetDisplayHealthColor(float normalizedHealth, float deltaTime)
        {
            Color targetColor;

            if (normalizedHealth <= lowHealthThreshold)
            {
                targetColor = lowHealthColor;
            }
            else if (normalizedHealth <= midHealthThreshold)
            {
                float t = Mathf.InverseLerp(lowHealthThreshold, midHealthThreshold, normalizedHealth);
                targetColor = Color.Lerp(lowHealthColor, midHealthColor, t);
            }
            else
            {
                float t = Mathf.InverseLerp(midHealthThreshold, 1f, normalizedHealth);
                targetColor = Color.Lerp(midHealthColor, highHealthColor, t);
            }

            if (pulseWhenLowHealth && normalizedHealth <= lowHealthThreshold && deltaTime > 0f)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * lowHealthPulseSpeed) + 1f) * 0.5f;
                targetColor = Color.Lerp(targetColor, Color.white, pulse * lowHealthPulseAmount);
            }

            if (flashOnDamage && damageFlashTime > 0f && flashTimer > 0f)
            {
                float flash = flashTimer / damageFlashTime;
                targetColor = Color.Lerp(targetColor, damageFlashColor, flash);
            }

            return targetColor;
        }

        private void TryBuildExtraBars()
        {
            if (healthFillImage == null)
            {
                return;
            }

            if (createDarkBackground && backgroundImage == null)
            {
                backgroundImage = CreateBarImage("Health_Background", backgroundColor, 0);
                backgroundImage.fillAmount = 1f;
            }

            if (createDelayDamageBar && delayDamageImage == null)
            {
                delayDamageImage = CreateBarImage("Health_DelayDamage", delayDamageColor, 1);
                delayDamageImage.fillAmount = healthFillImage.fillAmount;
            }

            if (showRecoveringGlow && recoveringGlowImage == null)
            {
                recoveringGlowImage = CreateBarImage("Health_RecoveringGlow", recoveringGlowColor, healthFillImage.transform.GetSiblingIndex());
                RectTransform glowRect = recoveringGlowImage.rectTransform;
                glowRect.sizeDelta += recoveringGlowSizeOffset;
                recoveringGlowImage.fillAmount = healthFillImage.fillAmount;
                recoveringGlowImage.enabled = false;
            }
        }

        private Image CreateBarImage(string objectName, Color color, int siblingIndex)
        {
            GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barObject.layer = healthFillImage.gameObject.layer;
            barObject.transform.SetParent(healthFillImage.transform.parent, false);
            barObject.transform.SetSiblingIndex(Mathf.Min(siblingIndex, healthFillImage.transform.GetSiblingIndex()));

            RectTransform sourceRect = healthFillImage.rectTransform;
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = sourceRect.anchorMin;
            barRect.anchorMax = sourceRect.anchorMax;
            barRect.anchoredPosition = sourceRect.anchoredPosition;
            barRect.sizeDelta = sourceRect.sizeDelta;
            barRect.pivot = sourceRect.pivot;
            barRect.localRotation = sourceRect.localRotation;
            barRect.localScale = sourceRect.localScale;

            Image image = barObject.GetComponent<Image>();
            image.sprite = healthFillImage.sprite;
            image.type = healthFillImage.type;
            image.fillMethod = healthFillImage.fillMethod;
            image.fillOrigin = healthFillImage.fillOrigin;
            image.fillClockwise = healthFillImage.fillClockwise;
            image.fillCenter = healthFillImage.fillCenter;
            image.pixelsPerUnitMultiplier = healthFillImage.pixelsPerUnitMultiplier;
            image.raycastTarget = false;
            image.color = color;

            return image;
        }

        private void TryAutoBindPlayerHealth()
        {
            if (playerHealthSystem != null)
            {
                return;
            }

            playerHealthSystem = FindFirstObjectByType<PlayerHealthSystem>();
        }

        private void TryAutoBindHeartRateStateController()
        {
            if (heartRateStateController == null)
            {
                heartRateStateController = HeartRateStateController.Instance;
            }
        }

        private void UpdateRecoveringGlow()
        {
            if (recoveringGlowImage == null)
            {
                return;
            }

            bool isRecovering = heartRateStateController != null &&
                                heartRateStateController.CurrentState == HeartRateStateController.HeartRateState.Recovering;

            recoveringGlowImage.fillAmount = displayedNormalizedHealth;
            recoveringGlowImage.enabled = isRecovering;

            if (!isRecovering)
            {
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * recoveringGlowPulseSpeed) + 1f) * 0.5f;
            Color glowColor = recoveringGlowColor;
            glowColor.a = Mathf.Lerp(recoveringGlowMinAlpha, recoveringGlowMaxAlpha, pulse);
            recoveringGlowImage.color = glowColor;
        }
    }
}
