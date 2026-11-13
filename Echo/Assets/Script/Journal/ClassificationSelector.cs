using UnityEngine;
using UnityEngine.UI;

public class ClassificationSelector : MonoBehaviour
{
    [System.Serializable]
    public class ClassificationOption
    {
        public string eventName;          // e.g., "Disturbance Event", "Time Loop", etc.
        public Button textButton;         // The Button component on your Text object
        public GameObject selectionBox;   // The SelectionBox UI Image child
    }

    [Header("All Classification Options on the Page")]
    public ClassificationOption[] options;

    [Header("Current Player Selection")]
    public string currentSelectedEvent = "";

    private int selectedIndex = -1;

    void Start()
    {
        // Automatically link every button click and hide all boxes at the start
        for (int i = 0; i < options.Length; i++)
        {
            int index = i; // Local copy required for button listener
            options[i].selectionBox.SetActive(false);
            options[i].textButton.onClick.AddListener(() => OnOptionClicked(index));
        }
    }

    public void OnOptionClicked(int index)
    {
        // If clicking the SAME option that is already boxed -> un-box it
        if (selectedIndex == index)
        {
            options[index].selectionBox.SetActive(false);
            selectedIndex = -1;
            currentSelectedEvent = "";
            Debug.Log("Cleared classification selection.");
            return;
        }

        // Turn OFF all boxes first so only one can be active
        for (int i = 0; i < options.Length; i++)
        {
            options[i].selectionBox.SetActive(false);
        }

        // Turn ON the box for the one we just clicked
        options[index].selectionBox.SetActive(true);
        selectedIndex = index;
        currentSelectedEvent = options[index].eventName;

        Debug.Log("Selected Echo Event Classification: " + currentSelectedEvent);
    }
}