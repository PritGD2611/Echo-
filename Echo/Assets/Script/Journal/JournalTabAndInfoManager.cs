using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JournalTabAndInfoManager : MonoBehaviour
{
    [Header("Top Bookmark Tab Buttons")]
    public Button investigationTabButton;
    public Button informationTabButton;
    public Button mediaTabButton;

    [Header("Investigation Tab Panels")]
    public GameObject leftPageEvidencesPanel;
    public GameObject rightPageCaseTypePanel;

    [Header("Information Tab Panels")]
    public GameObject leftPage2InformationPanel;
    public GameObject rightPageCaseTypeInfoPanel;

    [Header("Media Tab Panels")]
    public GameObject leftPageMediaPanel;   // Drag 'LeftPageMedia' here
    public GameObject rightPageMediaPanel;  // Drag 'RightPageMedia' here

    [Header("Media Photo Slots (6 Slots: 3 Left, 3 Right)")]
    public GameObject[] photoSlotFrames = new GameObject[6];
    public Image[] photoSlotImages = new Image[6];

    [Header("Auto-Populated Lists (21 Items Each)")]
    public GameObject[] caseInfoPanels = new GameObject[21];
    public Button[] caseInfoButtons = new Button[21];

    private int currentPhotoIndex = 0;
    private bool isInitialized = false;

    [ContextMenu("Auto-Fetch Media Photo Slots")]
    public void AutoFetchMediaSlots()
    {
        List<GameObject> foundFrames = new List<GameObject>();
        List<Image> foundImages = new List<Image>();

        CollectSlotsFromPage(leftPageMediaPanel, foundFrames, foundImages);
        CollectSlotsFromPage(rightPageMediaPanel, foundFrames, foundImages);

        if (foundFrames.Count > 0)
        {
            photoSlotFrames = foundFrames.ToArray();
            photoSlotImages = foundImages.ToArray();
        }
    }

    private void CollectSlotsFromPage(GameObject pagePanel, List<GameObject> frames, List<Image> images)
    {
        if (pagePanel == null) return;

        for (int i = 0; i < pagePanel.transform.childCount; i++)
        {
            Transform child = pagePanel.transform.GetChild(i);
            // Skip TitleText (no Image component) and grab only the Polaroid frames with a child photo Image
            if (child.GetComponent<Image>() != null && child.childCount > 0)
            {
                Image innerPhoto = child.GetChild(0).GetComponent<Image>();
                if (innerPhoto != null)
                {
                    frames.Add(child.gameObject);
                    images.Add(innerPhoto);
                }
            }
        }
    }

    [ContextMenu("Auto-Fetch 21 Info Panels & Buttons")]
    public void AutoFetchInfoElements()
    {
        if (leftPage2InformationPanel != null)
        {
            int count = Mathf.Min(21, leftPage2InformationPanel.transform.childCount);
            caseInfoPanels = new GameObject[count];
            for (int i = 0; i < count; i++)
                caseInfoPanels[i] = leftPage2InformationPanel.transform.GetChild(i).gameObject;
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
    }

    void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (photoSlotFrames == null || photoSlotFrames.Length == 0 || photoSlotFrames[0] == null)
            AutoFetchMediaSlots();

        if (caseInfoPanels == null || caseInfoPanels.Length == 0 || caseInfoPanels[0] == null)
            AutoFetchInfoElements();

        for (int i = 0; i < photoSlotFrames.Length; i++)
        {
            if (photoSlotFrames[i] != null)
                photoSlotFrames[i].SetActive(false);
        }
    }

    void Start()
    {
        EnsureInitialized();

        if (investigationTabButton != null)
            investigationTabButton.onClick.AddListener(OpenInvestigationTab);

        if (informationTabButton != null)
            informationTabButton.onClick.AddListener(OpenInformationTab);

        if (mediaTabButton != null)
            mediaTabButton.onClick.AddListener(OpenMediaTab);

        for (int i = 0; i < caseInfoButtons.Length; i++)
        {
            int index = i;
            if (caseInfoButtons[i] != null)
                caseInfoButtons[i].onClick.AddListener(() => ShowCaseInfo(index));
        }

        ShowCaseInfo(0);
        OpenInvestigationTab();
    }

    public void OpenInvestigationTab()
    {
        SetAllTabsInactive();
        if (leftPageEvidencesPanel != null) leftPageEvidencesPanel.SetActive(true);
        if (rightPageCaseTypePanel != null) rightPageCaseTypePanel.SetActive(true);
    }

    public void OpenInformationTab()
    {
        SetAllTabsInactive();
        if (leftPage2InformationPanel != null) leftPage2InformationPanel.SetActive(true);
        if (rightPageCaseTypeInfoPanel != null) rightPageCaseTypeInfoPanel.SetActive(true);
    }

    public void OpenMediaTab()
    {
        SetAllTabsInactive();
        if (leftPageMediaPanel != null) leftPageMediaPanel.SetActive(true);
        if (rightPageMediaPanel != null) rightPageMediaPanel.SetActive(true);
    }

    private void SetAllTabsInactive()
    {
        if (leftPageEvidencesPanel != null) leftPageEvidencesPanel.SetActive(false);
        if (rightPageCaseTypePanel != null) rightPageCaseTypePanel.SetActive(false);

        if (leftPage2InformationPanel != null) leftPage2InformationPanel.SetActive(false);
        if (rightPageCaseTypeInfoPanel != null) rightPageCaseTypeInfoPanel.SetActive(false);

        if (leftPageMediaPanel != null) leftPageMediaPanel.SetActive(false);
        if (rightPageMediaPanel != null) rightPageMediaPanel.SetActive(false);
    }

    public void ShowCaseInfo(int selectedIndex)
    {
        for (int i = 0; i < caseInfoPanels.Length; i++)
        {
            if (caseInfoPanels[i] != null)
                caseInfoPanels[i].SetActive(i == selectedIndex);
        }
    }

    public bool AddPhotoToJournal(Sprite newPhotoSprite)
    {
        EnsureInitialized();

        if (photoSlotFrames == null || photoSlotFrames.Length == 0)
        {
            Debug.LogWarning("No Media photo slots found! Assign LeftPageMedia and RightPageMedia on MainPanel(BG).");
            return false;
        }

        if (currentPhotoIndex >= photoSlotFrames.Length)
        {
            Debug.Log("Journal Media page is full (6/6 photos)!");
            return false;
        }

        if (photoSlotImages[currentPhotoIndex] != null)
        {
            photoSlotImages[currentPhotoIndex].sprite = newPhotoSprite;
            photoSlotImages[currentPhotoIndex].color = Color.white;
        }

        if (photoSlotFrames[currentPhotoIndex] != null)
        {
            photoSlotFrames[currentPhotoIndex].SetActive(true);
        }

        currentPhotoIndex++;
        Debug.Log($"Saved photo {currentPhotoIndex}/{photoSlotFrames.Length} into Journal Media tab.");
        return true;
    }
}