using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
public class Scene2InteractiveItem : MonoBehaviour
{
    [SerializeField, Header("Scene Restriction")] private bool restrictToScene = true;
    [SerializeField] private string sceneName = "2_Game Scene";

    [SerializeField, Header("Detection")] private float promptRadius = 3f;
    [SerializeField] private float interactRadius = 1.5f;
    [SerializeField] private string playerTag = "Player";

    [SerializeField, Header("Prompt")] private string nearbyPrompt = "发现可交互物品，靠近后按 F 交互";
    [SerializeField] private string interactPrompt = "按 F 交互";
    [SerializeField] private KeyCode interactionKey = KeyCode.F;
    [SerializeField, Header("Prompt Style")] private TMP_FontAsset promptFont;
    [SerializeField] private Material promptFontMaterial;
    [SerializeField] private float promptFontSize = 34f;
    [SerializeField] private Color promptTextColor = new Color(0.93f, 0.96f, 1f, 1f);

    [SerializeField, Header("Interaction")] private bool allowRepeatedInteraction;
    [SerializeField] private UnityEvent onInteract;

    private CharacterInputSystem characterInputSystem;
    private Transform playerTarget;
    private bool hasInteracted;

    private void Awake()
    {
        ResolvePlayerTarget();
    }

    private void Update()
    {
        if (!IsSceneAllowed() || (!allowRepeatedInteraction && hasInteracted))
        {
            return;
        }

        if (!ResolvePlayerTarget())
        {
            return;
        }

        if (characterInputSystem != null && !characterInputSystem.enabled)
        {
            return;
        }

        float sqrDistance = (playerTarget.position - transform.position).sqrMagnitude;
        float promptRadiusSqr = promptRadius * promptRadius;
        if (sqrDistance > promptRadiusSqr)
        {
            return;
        }

        Scene2InteractionPromptUI.ConfigureStyle(promptFont, promptFontMaterial, promptFontSize, promptTextColor);

        string promptMessage = sqrDistance <= interactRadius * interactRadius
            ? interactPrompt
            : nearbyPrompt;

        Scene2InteractionPromptUI.Instance.RequestPrompt(promptMessage, sqrDistance);

        if (sqrDistance > interactRadius * interactRadius)
        {
            return;
        }

        if (!Input.GetKeyDown(interactionKey))
        {
            return;
        }

        hasInteracted = true;
        onInteract.Invoke();

        if (onInteract.GetPersistentEventCount() == 0)
        {
            Debug.Log($"[Scene2InteractiveItem] '{name}' interacted. Placeholder event is currently empty.", this);
        }
    }

    private bool ResolvePlayerTarget()
    {
        if (playerTarget != null)
        {
            return true;
        }

        characterInputSystem = FindFirstObjectByType<CharacterInputSystem>();
        if (characterInputSystem != null)
        {
            playerTarget = characterInputSystem.transform;
            return true;
        }

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (taggedPlayer != null)
            {
                playerTarget = taggedPlayer.transform;
                return true;
            }
        }

        GameObject namedPlayer = GameObject.Find("Player");
        if (namedPlayer != null)
        {
            playerTarget = namedPlayer.transform;
            return true;
        }

        GameObject fallbackPlayer = GameObject.Find("Player (1)");
        if (fallbackPlayer != null)
        {
            playerTarget = fallbackPlayer.transform;
            return true;
        }

        return false;
    }

    private bool IsSceneAllowed()
    {
        if (!restrictToScene)
        {
            return true;
        }

        return SceneManager.GetActiveScene().name == sceneName;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, promptRadius);

        Gizmos.color = new Color(1f, 0.85f, 0.25f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
