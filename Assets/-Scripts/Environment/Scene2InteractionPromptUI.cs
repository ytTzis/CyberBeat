using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Scene2InteractionPromptUI : MonoBehaviour
{
    private static Scene2InteractionPromptUI instance;
    private static TMP_FontAsset promptFontAsset;
    private static Material promptFontMaterial;
    private static float promptFontSize = 34f;
    private static Color promptTextColor = new Color(0.93f, 0.96f, 1f, 1f);

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI promptText;
    private int lastRequestFrame = -1;
    private float bestSqrDistance = float.MaxValue;
    private string pendingMessage;

    public static Scene2InteractionPromptUI Instance
    {
        get
        {
            if (instance == null)
            {
                CreateInstance();
            }

            return instance;
        }
    }

    public static void ConfigureStyle(TMP_FontAsset fontAsset, Material fontMaterial, float fontSize, Color textColor)
    {
        promptFontAsset = fontAsset;
        promptFontMaterial = fontMaterial;
        promptFontSize = fontSize;
        promptTextColor = textColor;

        if (instance != null)
        {
            instance.ApplyStyle();
        }
    }

    public void RequestPrompt(string message, float sqrDistance)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (lastRequestFrame != Time.frameCount)
        {
            bestSqrDistance = float.MaxValue;
            pendingMessage = null;
            lastRequestFrame = Time.frameCount;
        }

        if (sqrDistance > bestSqrDistance)
        {
            return;
        }

        bestSqrDistance = sqrDistance;
        pendingMessage = message;
        SetVisible(true);
        promptText.text = message;
    }

    private void LateUpdate()
    {
        if (lastRequestFrame == Time.frameCount && !string.IsNullOrEmpty(pendingMessage))
        {
            return;
        }

        SetVisible(false);
        pendingMessage = null;
        bestSqrDistance = float.MaxValue;
    }

    private static void CreateInstance()
    {
        GameObject root = new GameObject("Scene2 Interaction Prompt UI");
        instance = root.AddComponent<Scene2InteractionPromptUI>();
        instance.BuildUi(root);
    }

    private void BuildUi(GameObject root)
    {
        DontDestroyOnLoad(root);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10020;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        GameObject textObject = new GameObject("Prompt Text", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.sizeDelta = new Vector2(1100f, 90f);
        rectTransform.anchoredPosition = new Vector2(0f, 70f);

        promptText = textObject.AddComponent<TextMeshProUGUI>();
        promptText.text = string.Empty;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.enableWordWrapping = true;
        ApplyStyle();
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
    }

    private void ApplyStyle()
    {
        if (promptText == null)
        {
            return;
        }

        if (promptFontAsset != null)
        {
            promptText.font = promptFontAsset;
        }

        if (promptFontMaterial != null)
        {
            promptText.fontMaterial = promptFontMaterial;
        }

        promptText.fontSize = promptFontSize;
        promptText.color = promptTextColor;
    }
}
