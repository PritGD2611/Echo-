using UnityEngine;

public class AnomalyShowcaseManager : MonoBehaviour
{
    [Header("Drag the 8 Anomaly Objects (Sitting on Your 8 Cylinders) in Order 0 to 7")]
    [Tooltip("0: Voices/Sounds | 1: Objects Breaking | 2: Impossible Placement | 3: Object Appearance | 4: Disturbance in Action | 5: Radio/Electrical | 6: Environmental | 7: Unexplained Presence")]
    public GameObject[] anomalyDisplays = new GameObject[8];

    [Header("Current Active Showcase Info (For Your Reference)")]
    public string activeCaseName;
    public string[] activeAnomalies = new string[3];

    public event System.Action InvestigationStarted;

    // Exact same 21 combinations as EchoJournalManager
    private readonly int[,] combos = new int[21, 3]
    {
        {0, 1, 2}, {0, 1, 3}, {0, 2, 4}, {0, 3, 5}, {0, 4, 6}, {0, 5, 7}, {0, 6, 7},
        {1, 2, 4}, {1, 3, 6}, {1, 4, 5}, {1, 5, 7}, {1, 6, 7}, {2, 3, 5}, {2, 3, 7},
        {2, 4, 6}, {2, 5, 6}, {2, 4, 7}, {3, 4, 5}, {3, 4, 7}, {3, 6, 7}, {4, 5, 6}
    };

    private readonly string[] anomalyNames = new string[8]
    {
        "0: Voices / Sounds",
        "1: Objects Breaking",
        "2: Impossible Object Placement",
        "3: Object Appearance",
        "4: Disturbance in Action",
        "5: Radio / Electrical Disturbance",
        "6: Environmental Disturbance",
        "7: Unexplained Presence"
    };

    void Start()
    {
        GenerateNewCaseShowcase();
    }

    public void GenerateNewCaseShowcase()
    {
        // 1. Hide all 8 anomaly objects on the cylinders first
        for (int i = 0; i < anomalyDisplays.Length; i++)
        {
            if (anomalyDisplays[i] != null)
                anomalyDisplays[i].SetActive(false);
        }

        // 2. Pick a random Case (0 to 20) that is DIFFERENT from the last Play session
        int lastCase = PlayerPrefs.GetInt("LastShowcaseCase", -1);
        int chosenCase = Random.Range(0, 21);

        if (chosenCase == lastCase)
        {
            chosenCase = (chosenCase + Random.Range(1, 21)) % 21;
        }

        PlayerPrefs.SetInt("LastShowcaseCase", chosenCase);
        PlayerPrefs.Save();

        // 3. Activate ONLY the 3 anomalies for this Case on their cylinders
        activeCaseName = "Case " + (chosenCase + 1);

        for (int i = 0; i < 3; i++)
        {
            int anomalyIndex = combos[chosenCase, i];
            activeAnomalies[i] = anomalyNames[anomalyIndex];

            if (anomalyIndex < anomalyDisplays.Length && anomalyDisplays[anomalyIndex] != null)
            {
                anomalyDisplays[anomalyIndex].SetActive(true);
            }
        }

        Debug.Log($"[SHOWCASE] Active: {activeCaseName} -> {activeAnomalies[0]}, {activeAnomalies[1]}, {activeAnomalies[2]}");
        InvestigationStarted?.Invoke();
    }
}
