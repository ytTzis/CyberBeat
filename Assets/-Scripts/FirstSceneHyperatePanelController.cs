using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct PanelElementLayout
{
    public Vector2 anchorMin;
    public Vector2 anchorMax;
    public Vector2 pivot;
    public Vector2 anchoredPosition;
    public Vector2 sizeDelta;
    public int fontSize;
}

[ExecuteAlways]
public class FirstSceneHyperatePanelController : MonoBehaviour
{
    private const string InputPanelName = "InputPanel";
    private const string CardName = "Card";

    private static FirstSceneHyperatePanelController instance;

    [Header("Card")]
    [SerializeField] private Color overlayColor = new Color(0.12f, 0.14f, 0.18f, 0.96f);

    [Header("Text Layout")]
    [SerializeField] private PanelElementLayout titleLayout = new PanelElementLayout
    {
        anchorMin = new Vector2(0.5f, 1f),
        anchorMax = new Vector2(0.5f, 1f),
        pivot = new Vector2(0.5f, 1f),
        anchoredPosition = new Vector2(0f, -110f),
        sizeDelta = new Vector2(960f, 80f),
        fontSize = 52
    };
    [SerializeField] private PanelElementLayout tokenLabelLayout = new PanelElementLayout
    {
        anchorMin = new Vector2(0f, 1f),
        anchorMax = new Vector2(1f, 1f),
        pivot = new Vector2(0f, 1f),
        anchoredPosition = new Vector2(180f, -250f),
        sizeDelta = new Vector2(-360f, 50f),
        fontSize = 34
    };
    [SerializeField] private PanelElementLayout tokenInputLayout = new PanelElementLayout
    {
        anchorMin = new Vector2(0.5f, 1f),
        anchorMax = new Vector2(0.5f, 1f),
        pivot = new Vector2(0.5f, 1f),
        anchoredPosition = new Vector2(0f, -340f),
        sizeDelta = new Vector2(1280f, 88f),
        fontSize = 30
    };
    [SerializeField] private PanelElementLayout idLabelLayout = new PanelElementLayout
    {
        anchorMin = new Vector2(0f, 1f),
        anchorMax = new Vector2(1f, 1f),
        pivot = new Vector2(0f, 1f),
        anchoredPosition = new Vector2(180f, -470f),
        sizeDelta = new Vector2(-360f, 50f),
        fontSize = 34
    };
    [SerializeField] private PanelElementLayout idInputLayout = new PanelElementLayout
    {
        anchorMin = new Vector2(0.5f, 1f),
        anchorMax = new Vector2(0.5f, 1f),
        pivot = new Vector2(0.5f, 1f),
        anchoredPosition = new Vector2(0f, -560f),
        sizeDelta = new Vector2(1280f, 88f),
        fontSize = 30
    };
    [SerializeField] private PanelElementLayout statusLayout = new PanelElementLayout
    {
        anchorMin = new Vector2(0.5f, 0f),
        anchorMax = new Vector2(0.5f, 0f),
        pivot = new Vector2(0.5f, 0f),
        anchoredPosition = new Vector2(0f, 180f),
        sizeDelta = new Vector2(1280f, 60f),
        fontSize = 28
    };
    [SerializeField] private PanelElementLayout saveButtonLayout = new PanelElementLayout
    {
        anchorMin = new Vector2(0.5f, 0f),
        anchorMax = new Vector2(0.5f, 0f),
        pivot = new Vector2(1f, 0f),
        anchoredPosition = new Vector2(-180f, 80f),
        sizeDelta = new Vector2(260f, 78f),
        fontSize = 30
    };
    [SerializeField] private PanelElementLayout closeButtonLayout = new PanelElementLayout
    {
        anchorMin = new Vector2(0.5f, 0f),
        anchorMax = new Vector2(0.5f, 0f),
        pivot = new Vector2(0f, 0f),
        anchoredPosition = new Vector2(180f, 80f),
        sizeDelta = new Vector2(260f, 78f),
        fontSize = 30
    };

    [Header("Colors")]
    [SerializeField] private Color statusColor = new Color(0.78f, 0.87f, 1f, 1f);
    [SerializeField] private Color inputBackgroundColor = new Color(0.92f, 0.94f, 0.98f, 1f);
    [SerializeField] private Color inputTextColor = new Color(0.12f, 0.12f, 0.15f, 1f);
    [SerializeField] private Color placeholderColor = new Color(0.46f, 0.49f, 0.56f, 0.9f);
    [SerializeField] private Color saveButtonColor = new Color(0.24f, 0.60f, 0.42f, 1f);
    [SerializeField] private Color closeButtonColor = new Color(0.34f, 0.36f, 0.42f, 1f);

    private GameObject inputPanel;
    private CanvasGroup panelCanvasGroup;
    private RectTransform cardRect;
    private InputField tokenInputField;
    private InputField hyperateIdInputField;
    private Text statusText;
    private bool hasHiddenOnPlayStart;

    public static void EnsureExists()
    {
        if (instance != null)
        {
            instance.InitializeIfNeeded();
            return;
        }

        instance = FindFirstObjectByType<FirstSceneHyperatePanelController>();
        if (instance == null)
        {
            GameObject inputPanelObject = GameObject.Find(InputPanelName);
            if (inputPanelObject != null)
            {
                instance = inputPanelObject.GetComponent<FirstSceneHyperatePanelController>();
                if (instance == null)
                {
                    instance = inputPanelObject.AddComponent<FirstSceneHyperatePanelController>();
                }
            }
        }

        if (instance != null)
        {
            instance.InitializeIfNeeded();
        }
    }

    public static void ShowPanel()
    {
        EnsureExists();
        if (instance != null)
        {
            instance.ShowInternal();
        }
    }

    public static void HidePanel()
    {
        if (instance != null)
        {
            instance.HideInternal();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (inputPanel == null && gameObject.name == InputPanelName)
        {
            inputPanel = gameObject;
        }

        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();

        if (Application.isPlaying && !hasHiddenOnPlayStart)
        {
            hasHiddenOnPlayStart = true;
            HideInternal();
        }
    }

    private void OnValidate()
    {
        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (inputPanel == null)
        {
            inputPanel = gameObject.name == InputPanelName ? gameObject : GameObject.Find(InputPanelName);
        }

        if (inputPanel == null)
        {
            return;
        }

        BuildOrRefreshPanelUi();
    }

    private void ShowInternal()
    {
        InitializeIfNeeded();
        if (inputPanel == null)
        {
            Debug.LogWarning("[FirstScene] InputPanel was not found in the scene.");
            return;
        }

        LoadStoredValuesIntoInputs();
        SetStatus(string.Empty);
        SetPanelVisible(true);
    }

    private void HideInternal()
    {
        SetPanelVisible(false);
    }

    private void BuildOrRefreshPanelUi()
    {
        RectTransform panelTransform = inputPanel.GetComponent<RectTransform>();
        if (panelTransform == null)
        {
            panelTransform = inputPanel.AddComponent<RectTransform>();
        }

        panelCanvasGroup = GetOrAddComponent<CanvasGroup>(inputPanel);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        RectTransform cardTransform = GetOrCreateChildRectTransform(panelTransform, CardName);
        Image cardBackground = GetOrAddComponent<Image>(cardTransform.gameObject);
        cardBackground.color = overlayColor;
        cardRect = cardBackground.rectTransform;
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;

        CreateOrUpdateLabel("Title", cardRect, font, "Hyperate Connection", FontStyle.Bold, TextAnchor.MiddleCenter, titleLayout, Color.white);

        CreateOrUpdateLabel("TokenLabel", cardRect, font, "Websocket Token", FontStyle.Bold, TextAnchor.MiddleLeft, tokenLabelLayout, Color.white);
        tokenInputField = CreateOrUpdateInputField("TokenInput", cardRect, font, "Enter websocket token",
            tokenInputLayout);

        CreateOrUpdateLabel("IdLabel", cardRect, font, "Hyperate ID", FontStyle.Bold, TextAnchor.MiddleLeft, idLabelLayout, Color.white);
        hyperateIdInputField = CreateOrUpdateInputField("HyperateIdInput", cardRect, font, "Enter Hyperate ID",
            idInputLayout);

        statusText = CreateOrUpdateLabel("Status", cardRect, font, string.Empty, FontStyle.Italic, TextAnchor.MiddleLeft, statusLayout, statusColor);

        Button saveButton = CreateOrUpdateButton("SaveButton", cardRect, font, "Save",
            saveButtonLayout, saveButtonColor);
        saveButton.onClick.RemoveAllListeners();
        saveButton.onClick.AddListener(ApplySettings);

        Button closeButton = CreateOrUpdateButton("CloseButton", cardRect, font, "Close",
            closeButtonLayout, closeButtonColor);
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(HideInternal);
    }

    private void SetPanelVisible(bool visible)
    {
        if (inputPanel == null)
        {
            return;
        }

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = GetOrAddComponent<CanvasGroup>(inputPanel);
        }

        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }

    private void ApplySettings()
    {
        if (tokenInputField == null || hyperateIdInputField == null)
        {
            return;
        }

        string token = tokenInputField.text.Trim();
        string hyperateId = hyperateIdInputField.text.Trim();
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(hyperateId))
        {
            SetStatus("Token and Hyperate ID are required.");
            return;
        }

        hyperateSocket.SaveCredentials(token, hyperateId);

        if (hyperateSocket.Instance != null)
        {
            hyperateSocket.Instance.ApplyCredentialsAndReconnect(token, hyperateId);
            SetStatus("Saved. Active connection is reconnecting with the new values.");
            return;
        }

        SetStatus("Saved. The new values will be used when heart rate monitoring starts.");
    }

    private void LoadStoredValuesIntoInputs()
    {
        if (tokenInputField == null || hyperateIdInputField == null)
        {
            return;
        }

        tokenInputField.text = hyperateSocket.GetSavedOrDefaultWebsocketToken();
        hyperateIdInputField.text = hyperateSocket.GetSavedOrDefaultHyperateId();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private static RectTransform GetOrCreateChildRectTransform(Transform parent, string name)
    {
        Transform existingChild = parent.Find(name);
        if (existingChild != null)
        {
            RectTransform existingRect = existingChild as RectTransform;
            if (existingRect != null)
            {
                return existingRect;
            }
        }

        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.localScale = Vector3.one;
        return rectTransform;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

    private static Text CreateOrUpdateLabel(string name, Transform parent, Font font, string message, FontStyle fontStyle,
        TextAnchor alignment, PanelElementLayout layout, Color color)
    {
        RectTransform targetRect = GetOrCreateChildRectTransform(parent, name);
        Text label = GetOrAddComponent<Text>(targetRect.gameObject);
        label.raycastTarget = false;
        ApplyLayout(label.rectTransform, layout);
        label.font = font;
        label.fontSize = layout.fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.text = message;
        return label;
    }

    private InputField CreateOrUpdateInputField(string name, Transform parent, Font font, string placeholder, PanelElementLayout layout)
    {
        RectTransform targetRect = GetOrCreateChildRectTransform(parent, name);
        Image background = GetOrAddComponent<Image>(targetRect.gameObject);
        ApplyLayout(background.rectTransform, layout);
        background.color = inputBackgroundColor;

        InputField inputField = GetOrAddComponent<InputField>(background.gameObject);
        inputField.targetGraphic = background;

        PanelElementLayout contentLayout = CreateInputContentLayout(layout.fontSize);
        Text placeholderText = CreateOrUpdateLabel("Placeholder", background.rectTransform, font, placeholder, FontStyle.Italic, TextAnchor.MiddleLeft, contentLayout, placeholderColor);

        Text inputText = CreateOrUpdateLabel("Text", background.rectTransform, font, string.Empty, FontStyle.Normal, TextAnchor.MiddleLeft, contentLayout, inputTextColor);

        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;

        return inputField;
    }

    private static Button CreateOrUpdateButton(string name, Transform parent, Font font, string label, PanelElementLayout layout, Color backgroundColor)
    {
        RectTransform targetRect = GetOrCreateChildRectTransform(parent, name);
        Image background = GetOrAddComponent<Image>(targetRect.gameObject);
        ApplyLayout(background.rectTransform, layout);
        background.color = backgroundColor;

        Button button = GetOrAddComponent<Button>(background.gameObject);
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor * 1.08f;
        colors.pressedColor = backgroundColor * 0.92f;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        PanelElementLayout buttonLabelLayout = new PanelElementLayout
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(-16f, -12f),
            fontSize = layout.fontSize
        };
        CreateOrUpdateLabel("Label", background.rectTransform, font, label, FontStyle.Bold, TextAnchor.MiddleCenter, buttonLabelLayout, Color.white);

        return button;
    }

    private static void ApplyLayout(RectTransform rectTransform, PanelElementLayout layout)
    {
        rectTransform.anchorMin = layout.anchorMin;
        rectTransform.anchorMax = layout.anchorMax;
        rectTransform.pivot = layout.pivot;
        rectTransform.anchoredPosition = layout.anchoredPosition;
        rectTransform.sizeDelta = layout.sizeDelta;
    }

    private static PanelElementLayout CreateInputContentLayout(int fontSize)
    {
        return new PanelElementLayout
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(-48f, -20f),
            fontSize = fontSize
        };
    }
}
