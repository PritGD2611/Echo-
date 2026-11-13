using UnityEngine;
using UnityEngine.UI;

public class JournalTabAndInfoManager : MonoBehaviour
{
    [Header("Top Bookmark Tab Buttons")]
    public Button investigationTabButton;
    public Button informationTabButton;

    [Header("Investigation Tab Panels")]
    public GameObject leftPageEvidencesPanel;      // LeftPageEvidencesPanel
    public GameObject rightPageCaseTypePanel;      // RightPageCaseTypePanel

    [Header("Information Tab Panels")]
    public GameObject leftPage2InformationPanel;   // LeftPage2InformationPanel
    public GameObject rightPageCaseTypeInfoPanel;  // RightPageCaseTypeInfoPanel

    [Header("Auto-Populated Lists (21 Items Each)")]
    public GameObject[] caseInfoPanels = new GameObject[21]; // Case1Info to Case21Info
    public Button[] caseInfoButtons = new Button[21];        // Buttons 1 to 21 on Info right page

    // Right-click component header -> "Auto-Fetch 21 Info Panels & Buttons"
    [ContextMenu("Auto-Fetch 21 Info Panels & Buttons")]
    public void AutoFetchInfoElements()
    {
        if (leftPage2InformationPanel != null)
        {
            int count = Mathf.Min(21, leftPage2InformationPanel.transform.childCount);
            caseInfoPanels = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                caseInfoPanels[i] = leftPage2InformationPanel.transform.GetChild(i).gameObject;
            }
        }

        if (rightPageCaseTypeInfoPanel != null)
        {
            int count = Mathf.Min(21, rightPageCaseTypeInfoPanel.transform.childCount);
            caseInfoButtons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                Transform child = rightPageCaseTypeInfoPanel.transform.GetChild(i);
                caseInfoButtons[i] = child.GetComponentInChildren<Button>(true);
            }
        }

        Debug.Log("Auto-fetched 21 CaseInfo panels and 21 CaseInfo buttons!");
    }

    void Start()
    {
        // If you forgot to run the ContextMenu, auto-fetch on Start automatically
        if (caseInfoPanels.Length == 0 || caseInfoPanels[0] == null)
        {
            AutoFetchInfoElements();
        }

        // 1. Hook up Top Tab Buttons
        if (investigationTabButton != null)
            investigationTabButton.onClick.AddListener(OpenInvestigationTab);

        if (informationTabButton != null)
            informationTabButton.onClick.AddListener(OpenInformationTab);

        // 2. Hook up the 21 Case Info Buttons on the RightPageCaseTypeInfoPanel
        for (int i = 0; i < caseInfoButtons.Length; i++)
        {
            int index = i; // Local copy for closure
            if (caseInfoButtons[i] != null)
            {
                caseInfoButtons[i].onClick.AddListener(() => ShowCaseInfo(index));
            }
        }

        // Default to showing Case1Info when the Information tab is opened (like Phasmophobia)
        ShowCaseInfo(0);

        // Start the journal on the Investigation Tab (or call OpenInformationTab() if testing Info)
        OpenInvestigationTab();
    }

    // Switches book to "Investigation" mode
    public void OpenInvestigationTab()
    {
        if (leftPageEvidencesPanel != null) leftPageEvidencesPanel.SetActive(true);
        if (rightPageCaseTypePanel != null) rightPageCaseTypePanel.SetActive(true);

        if (leftPage2InformationPanel != null) leftPage2InformationPanel.SetActive(false);
        if (rightPageCaseTypeInfoPanel != null) rightPageCaseTypeInfoPanel.SetActive(false);
    }

    // Switches book to "Information" mode
    public void OpenInformationTab()
    {
        if (leftPageEvidencesPanel != null) leftPageEvidencesPanel.SetActive(false);
        if (rightPageCaseTypePanel != null) rightPageCaseTypePanel.SetActive(false);

        if (leftPage2InformationPanel != null) leftPage2InformationPanel.SetActive(true);
        if (rightPageCaseTypeInfoPanel != null) rightPageCaseTypeInfoPanel.SetActive(true);
    }

    // Activates ONLY the clicked CaseInfo panel (e.g., Case1Info) and hides the other 20
    public void ShowCaseInfo(int selectedIndex)
    {
        for (int i = 0; i < caseInfoPanels.Length; i++)
        {
            if (caseInfoPanels[i] != null)
            {
                bool shouldBeActive = (i == selectedIndex);
                caseInfoPanels[i].SetActive(shouldBeActive);
            }
        }

        Debug.Log("Showing Info Panel: Case" + (selectedIndex + 1) + "Info");
    }
}