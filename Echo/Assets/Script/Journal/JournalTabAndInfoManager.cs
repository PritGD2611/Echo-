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
    public GameObject leftPageMediaPanel;
    public GameObject rightPageMediaPanel;

    [Header("Media Photo Slots (6 Slots: 3 Left, 3 Right)")]
    public GameObject[] photoSlotFrames = new GameObject[6];
    public Image[] photoSlotImages = new Image[6];
    public Button[] photoDeleteButtons = new Button[6]; // Drag the 6 DeleteButtons here

    [Header("Auto-Populated Lists (21 Items Each)")]
    public GameObject[] caseInfoPanels = new GameObject[21];
    public Button[] caseInfoButtons = new Button[21];

    private List<Sprite> savedPhotos = new List<Sprite>();
    private bool isInitialized = false;

    [ContextMenu("Auto-Fetch Media Photo Slots")]
    public void AutoFetchMediaSlots()
    {
        List<GameObject> foundFrames = new List<GameObject>();
        List<Image> foundImages = new List<Image>();
        List<Button> foundDeleteBtns = new List<Button>();

        CollectSlotsFromPage(leftPageMediaPanel, foundFrames, foundImages, foundDeleteBtns);
        CollectSlotsFromPage(rightPageMediaPanel, foundFrames, foundImages, foundDeleteBtns);

        if (foundFrames.Count > 0)
        {
            photoSlotFrames = foundFrames.ToArray();
            photoSlotImages = foundImages.ToArray();
            photoDeleteButtons = foundDeleteBtns.ToArray();
        }
    }

    private void CollectSlotsFromPage(GameObject pagePanel, List<GameObject> frames, List<Image> images, List<Button> deleteBtns)
    {
        if (pagePanel == null) return;

        Transform slotsContainer = pagePanel.transform.Find("PhotoSlots");
        if (slotsContainer == null)
            slotsContainer = pagePanel.transform;

        for (int i = 0; i < slotsContainer.childCount; i++)
        {
            Transform slotFrame = slotsContainer.GetChild(i);
            if (slotFrame.childCount > 0)
            {
                Image innerPhoto = slotFrame.GetChild(0).GetComponent<Image>();
                Button delBtn = slotFrame.GetComponentInChildren<Button>(true);

                if (innerPhoto != null)
                {
                    frames.Add(slotFrame.gameObject);
                    images.Add(innerPhoto);
                    deleteBtns.Add(delBtn);
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

        // Hook up Delete Buttons for each of the 6 slots
        for (int i = 0; i < photoDeleteButtons.Length; i++)
        {
            int slotIndex = i;
            if (photoDeleteButtons[i] != null)
            {
                photoDeleteButtons[i].onClick.RemoveAllListeners();
                photoDeleteButtons[i].onClick.AddListener(() => DeletePhotoAtSlot(slotIndex));
            }
        }

        RefreshMediaUI();
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

        if (savedPhotos.Count >= photoSlotFrames.Length)
        {
            Debug.Log("Journal Media page is full (6/6 photos)! Delete a photo to make room.");
            return false;
        }

        savedPhotos.Add(newPhotoSprite);
        RefreshMediaUI();

        Debug.Log($"Saved photo {savedPhotos.Count}/{photoSlotFrames.Length} into Journal Media tab.");
        return true;
    }

    public void DeletePhotoAtSlot(int index)
    {
        if (index < 0 || index >= savedPhotos.Count) return;

        // Clean up texture memory for the deleted photo
        Sprite removed = savedPhotos[index];
        if (removed != null && removed.texture != null)
            Destroy(removed.texture);

        savedPhotos.RemoveAt(index);
        RefreshMediaUI();

        Debug.Log($"Deleted photo at slot {index + 1}. Remaining photos: {savedPhotos.Count}/{photoSlotFrames.Length}");
    }

    private void RefreshMediaUI()
    {
        for (int i = 0; i < photoSlotFrames.Length; i++)
        {
            if (i < savedPhotos.Count)
            {
                if (photoSlotImages[i] != null)
                {
                    photoSlotImages[i].sprite = savedPhotos[i];
                    photoSlotImages[i].color = Color.white;
                }
                if (photoSlotFrames[i] != null)
                    photoSlotFrames[i].SetActive(true);
            }
            else
            {
                if (photoSlotImages[i] != null)
                    photoSlotImages[i].sprite = null;

                if (photoSlotFrames[i] != null)
                    photoSlotFrames[i].SetActive(false);
            }
        }
    }
}