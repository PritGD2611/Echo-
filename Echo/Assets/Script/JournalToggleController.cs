using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class JournalToggleController : MonoBehaviour
{
    [Header("Drag MainPanel(BG) Here")]
    public GameObject journalObject;

    [Header("First-Person Cursor Settings")]
    public bool manageCursor = true;

    private bool isJournalOpen = false;

    void Start()
    {
        // Start with the journal closed when the game begins
        CloseJournal();
    }

    void Update()
    {
        bool jPressed = false;
        bool escPressed = false;

        // Works automatically with Unity 6's New Input System AND old Input Manager
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

        // Pressing 'J' toggles the journal Open or Closed
        if (jPressed)
        {
            if (isJournalOpen)
                CloseJournal();
            else
                OpenJournal();
        }
        // Pressing 'Esc' ONLY closes the journal if it is currently open
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

        if (manageCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}