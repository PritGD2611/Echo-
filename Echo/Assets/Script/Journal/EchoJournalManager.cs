using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EchoJournalManager : MonoBehaviour
{
    public enum AnomalyType
    {
        VoicesSounds = 0,
        ObjectsBreaking = 1,
        ImpossibleObjectPlacement = 2,
        ObjectAppearance = 3,
        DisturbanceInAction = 4,
        RadioElectricalDisturbance = 5,
        EnvironmentalDisturbance = 6,
        UnexplainedPresence = 7
    }

    [System.Serializable]
    public class AnomalyUI
    {
        public string name;
        public AnomalyType type;
        public Toggle toggle;               // Your UI Toggle on the left page
        public TMP_Text labelText;          // Text next to the Toggle (optional, to fade out)
    }

    [System.Serializable]
    public class CaseEntry
    {
        public string caseName;
        public AnomalyType[] requiredAnomalies = new AnomalyType[3];
        public Button caseButton;           // Button on Case1..Case21
        public TMP_Text caseText;           // Text of Case1..Case21 (fades when ruled out)
        public GameObject selectionBox;     // The circle/box image child
    }

    [Header("Left Page: 8 Anomaly Checkboxes")]
    public AnomalyUI[] anomalies = new AnomalyUI[8];

    [Header("Right Page: 21 Cases")]
    public CaseEntry[] cases = new CaseEntry[21];

    [Header("Visual Settings")]
    [Range(0.1f, 1f)] public float fadedAlpha = 0.2f; // How dim ruled-out cases look
    public string currentSelectedCase = "";
    private int selectedCaseIndex = -1;

    // Right-click the script in the Inspector and click "Auto-Fill 21 Combinations"
    [ContextMenu("Auto-Fill 21 Combinations")]
    public void AutoFillCombinations()
    {
        int[,] combos = new int[21, 3]
        {
            {0, 1, 2}, {0, 1, 3}, {0, 2, 4}, {0, 3, 5}, {0, 4, 6}, {0, 5, 7}, {0, 6, 7},
            {1, 2, 4}, {1, 3, 6}, {1, 4, 5}, {1, 5, 7}, {1, 6, 7}, {2, 3, 5}, {2, 3, 7},
            {2, 4, 6}, {2, 5, 6}, {2, 4, 7}, {3, 4, 5}, {3, 4, 7}, {3, 6, 7}, {4, 5, 6}
        };

        for (int i = 0; i < 8 && i < anomalies.Length; i++)
        {
            anomalies[i].type = (AnomalyType)i;
            anomalies[i].name = ((AnomalyType)i).ToString();
        }

        for (int i = 0; i < 21 && i < cases.Length; i++)
        {
            cases[i].caseName = "Case" + (i + 1);
            cases[i].requiredAnomalies = new AnomalyType[3]
            {
                (AnomalyType)combos[i, 0],
                (AnomalyType)combos[i, 1],
                (AnomalyType)combos[i, 2]
            };
        }
        Debug.Log("Successfully auto-filled all 21 Case combinations!");
    }

    void Start()
    {
        // Hook up the 8 Anomaly Toggles
        for (int i = 0; i < anomalies.Length; i++)
        {
            if (anomalies[i].toggle != null)
            {
                anomalies[i].toggle.isOn = false;
                anomalies[i].toggle.onValueChanged.AddListener((_) => EvaluateJournal());
            }
        }

        // Hook up the 21 Case Buttons
        for (int i = 0; i < cases.Length; i++)
        {
            int index = i;
            if (cases[i].selectionBox != null)
                cases[i].selectionBox.SetActive(false);

            if (cases[i].caseButton != null)
                cases[i].caseButton.onClick.AddListener(() => OnCaseClicked(index));
        }

        EvaluateJournal();
    }

    public void EvaluateJournal()
    {
        // 1. Gather all currently checked anomalies
        List<AnomalyType> selectedAnomalies = new List<AnomalyType>();
        for (int i = 0; i < anomalies.Length; i++)
        {
            if (anomalies[i].toggle != null && anomalies[i].toggle.isOn)
            {
                selectedAnomalies.Add(anomalies[i].type);
            }
        }

        // Track which anomalies are still possible across remaining valid cases
        HashSet<AnomalyType> possibleRemainingAnomalies = new HashSet<AnomalyType>();

        // 2. Check each of the 21 Cases
        for (int i = 0; i < cases.Length; i++)
        {
            bool isMatch = MatchesAllSelected(cases[i], selectedAnomalies);

            // Fade or highlight the Case text
            if (cases[i].caseText != null)
            {
                Color c = cases[i].caseText.color;
                c.a = isMatch ? 1f : fadedAlpha;
                cases[i].caseText.color = c;
            }

            // Disable clicking on ruled-out cases & remove selection box if it got ruled out
            if (cases[i].caseButton != null)
                cases[i].caseButton.interactable = isMatch;

            if (!isMatch && selectedCaseIndex == i)
            {
                if (cases[i].selectionBox != null)
                    cases[i].selectionBox.SetActive(false);
                selectedCaseIndex = -1;
                currentSelectedCase = "";
            }

            // If this case is still possible, record its anomalies as valid
            if (isMatch)
            {
                foreach (var anom in cases[i].requiredAnomalies)
                    possibleRemainingAnomalies.Add(anom);
            }
        }

        // 3. Fade out impossible checkboxes on the left page (just like Phasmophobia!)
        for (int i = 0; i < anomalies.Length; i++)
        {
            bool isChecked = anomalies[i].toggle != null && anomalies[i].toggle.isOn;
            bool isPossible = selectedAnomalies.Count == 0 || isChecked || possibleRemainingAnomalies.Contains(anomalies[i].type);

            if (anomalies[i].toggle != null)
                anomalies[i].toggle.interactable = isPossible;

            if (anomalies[i].labelText != null)
            {
                Color c = anomalies[i].labelText.color;
                c.a = isPossible ? 1f : fadedAlpha;
                anomalies[i].labelText.color = c;
            }
        }
    }

    private bool MatchesAllSelected(CaseEntry entry, List<AnomalyType> selected)
    {
        foreach (AnomalyType sel in selected)
        {
            bool hasAnomaly = false;
            for (int i = 0; i < entry.requiredAnomalies.Length; i++)
            {
                if (entry.requiredAnomalies[i] == sel)
                {
                    hasAnomaly = true;
                    break;
                }
            }
            if (!hasAnomaly) return false;
        }
        return true;
    }

    public void OnCaseClicked(int index)
    {
        // Un-select if clicking the already-selected case
        if (selectedCaseIndex == index)
        {
            if (cases[index].selectionBox != null)
                cases[index].selectionBox.SetActive(false);
            selectedCaseIndex = -1;
            currentSelectedCase = "";
            return;
        }

        // Hide all boxes first
        for (int i = 0; i < cases.Length; i++)
        {
            if (cases[i].selectionBox != null)
                cases[i].selectionBox.SetActive(false);
        }

        // Show box on the clicked case
        if (cases[index].selectionBox != null)
            cases[index].selectionBox.SetActive(true);

        selectedCaseIndex = index;
        currentSelectedCase = cases[index].caseName;
        Debug.Log("Locked in Classification: " + currentSelectedCase);
    }
}