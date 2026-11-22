using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class JournalToggleController : MonoBehaviour
{
    [Header("Drag MainPanel(BG) Here")]
    public GameObject journalObject;

    [Header("Player Look / Movement Control")]
    [Tooltip("Drag your Player Capsule (with FirstPersonController) here")]
    public MonoBehaviour firstPersonController; // Drag FirstPersonController component here
    public bool manageCursor = true;

    private bool isJournalOpen = false;

    void Start()
    {
        CloseJournal();
    }

    void Update()
    {
        bool jPressed = false;
        bool escPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            jPressed = Keyboard.current.jKey.wasPressedThisFrame;
            escPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
        }
#else
        jPressed = Input.GetKeyDown(KeyCode.J);
        escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif

        if (jPressed)
        {
            if (isJournalOpen)
                CloseJournal();
            else
                OpenJournal();
        }
        else if (escPressed && isJournalOpen)
        {
            CloseJournal();
        }
    }

    public void OpenJournal()
    {
        isJournalOpen = true;
        if (journalObject != null)
            journalObject.SetActive(true);

        // Freeze camera rotation & movement while reading the journal
        if (firstPersonController != null)
            firstPersonController.enabled = false;

        if (manageCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void CloseJournal()
    {
        isJournalOpen = false;
        if (journalObject != null)
            journalObject.SetActive(false);

        // Restore camera rotation & movement
        if (firstPersonController != null)
            firstPersonController.enabled = true;

        if (manageCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}