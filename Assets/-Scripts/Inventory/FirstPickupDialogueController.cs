using UnityEngine;

public class FirstPickupDialogueController : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private string dialogueObjectName = "Dialogue";

    [Header("Close")]
    [SerializeField] private KeyCode closeKey = KeyCode.Space;
    [SerializeField] private bool allowMouseClickToClose = true;
    [SerializeField] private bool hideDialogueOnStart = true;

    private bool hasShownDialogue;
    private bool isDialogueOpen;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        if (dialogueRoot == null)
        {
            GameObject foundDialogue = GameObject.Find(dialogueObjectName);
            if (foundDialogue != null)
            {
                dialogueRoot = foundDialogue;
            }
        }

        if (hideDialogueOnStart)
        {
            SetDialogueVisible(false);
        }
    }

    private void OnEnable()
    {
        InventoryManager.ItemAdded += HandleItemPickedUp;
    }

    private void OnDisable()
    {
        InventoryManager.ItemAdded -= HandleItemPickedUp;

        if (isDialogueOpen)
        {
            ResumeGame();
        }
    }

    private void Update()
    {
        if (!isDialogueOpen)
        {
            return;
        }

        if (Input.GetKeyDown(closeKey) || (allowMouseClickToClose && Input.GetMouseButtonDown(0)))
        {
            CloseDialogue();
        }
    }

    public void CloseDialogue()
    {
        if (!isDialogueOpen)
        {
            return;
        }

        SetDialogueVisible(false);
        ResumeGame();
    }

    private void HandleItemPickedUp(string itemId)
    {
        if (hasShownDialogue)
        {
            return;
        }

        hasShownDialogue = true;
        OpenDialogue();
    }

    private void OpenDialogue()
    {
        if (dialogueRoot == null)
        {
            Debug.LogWarning("[FirstPickupDialogueController] Dialogue object was not assigned or found.");
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isDialogueOpen = true;
        SetDialogueVisible(true);
    }

    private void ResumeGame()
    {
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        isDialogueOpen = false;
    }

    private void SetDialogueVisible(bool visible)
    {
        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(visible);
        }
    }
}
