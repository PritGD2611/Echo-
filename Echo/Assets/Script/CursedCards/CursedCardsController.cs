using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public sealed class CursedCardsController : MonoBehaviour
{
    [Serializable]
    private sealed class ChanceOutcome
    {
        [Range(0, 100)] public float chancePercent;
        [TextArea] public string textOnHit;
        [TextArea] public string textOnMiss;
        public UnityEvent onHit = new();
        public UnityEvent onMiss = new();

        public ChanceOutcome(float chancePercent)
        {
            this.chancePercent = chancePercent;
        }

        public void Resolve(CursedCardsView target)
        {
            float chance = Mathf.Clamp(chancePercent, 0, 100);
            bool hit = chance >= 100 || (chance > 0 && Random.value < chance / 100f);
            string message = hit ? textOnHit : textOnMiss;
            target.ShowMessage(string.IsNullOrWhiteSpace(message)
                ? $"{chance:0.#}% probability roll: {(hit ? "succeeded" : "did not succeed")}."
                : message);
            (hit ? onHit : onMiss)?.Invoke();
        }
    }

    public const int UsesPerInvestigation = 2;

    [Header("Scene References")]
    [SerializeField] private CursedCardsView view;
    [SerializeField] private AnomalyShowcaseManager investigation;
    [SerializeField] private GameObject journalPanel;
    [SerializeField] private GameObject cameraCanvas;

    [Header("Controls")]
    [SerializeField] private Key equipKey = Key.T;
    [SerializeField] private Key drawKey = Key.R;

    [Header("Reveal Timing")]
    [SerializeField, Min(0)] private float cardRevealDelay = .28f;
    [SerializeField, Min(0)] private float resultDelay = .38f;

    [Header("Queen Combination Outcomes")]
    [SerializeField] private ChanceOutcome twoJestersAndQueen = new(25);
    [SerializeField] private ChanceOutcome jesterAndTwoQueens = new(75);

    public bool IsEquipped { get; private set; }
    public bool IsReading { get; private set; }
    public int RemainingUses { get; private set; }
    public CursedCardOutcome? LastOutcome { get; private set; }

    private Coroutine reading;

    private bool IsBlocked => (journalPanel != null && journalPanel.activeInHierarchy) ||
        (cameraCanvas != null && cameraCanvas.activeInHierarchy);

    private void Awake()
    {
        if (view == null)
        {
            Debug.LogError("Assign a Cursed Cards View to the Cursed Cards Controller.", this);
            enabled = false;
            return;
        }

        BeginInvestigation();
    }

    private void OnEnable()
    {
        if (investigation != null) investigation.InvestigationStarted += BeginInvestigation;
        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (investigation != null) investigation.InvestigationStarted -= BeginInvestigation;
        if (IsReading)
        {
            CancelReading();
            view.ShowCardBacks();
            view.ShowMessage("Reading interrupted.");
            RefreshControls();
        }

        IsEquipped = false;
        if (view != null) view.SetVisibility(false, true);
    }

    private void LateUpdate()
    {
        if (IsBlocked)
        {
            IsEquipped = false;
            RefreshVisibility();
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (equipKey != Key.None && keyboard[equipKey].wasPressedThisFrame)
                ToggleEquipped();
            else if (drawKey != Key.None && keyboard[drawKey].wasPressedThisFrame)
                TryDrawCards();
        }

        RefreshVisibility();
    }

    public void ToggleEquipped()
    {
        if (!isActiveAndEnabled || IsBlocked) return;
        IsEquipped = !IsEquipped;
        RefreshVisibility();
    }

    public void DrawCards()
    {
        TryDrawCards();
    }

    public bool TryDrawCards()
    {
        if (!isActiveAndEnabled || !IsEquipped || IsBlocked || IsReading || RemainingUses == 0)
            return false;

        IsReading = true;
        RemainingUses--;
        LastOutcome = null;
        view.ShowCardBacks();
        view.ShowMessage("Reading the cards...");
        RefreshControls();

        var cards = new[] { CursedCardRules.Draw(), CursedCardRules.Draw(), CursedCardRules.Draw() };
        reading = StartCoroutine(RevealCards(cards));
        return true;
    }

    public void BeginInvestigation()
    {
        CancelReading();
        RemainingUses = UsesPerInvestigation;
        LastOutcome = null;
        if (view == null) return;
        view.ShowCardBacks();
        view.ShowMessage("Ready to draw three cards.");
        RefreshControls();
        RefreshVisibility();
    }

    private IEnumerator RevealCards(CursedCardType[] cards)
    {
        for (int index = 0; index < cards.Length; index++)
        {
            yield return new WaitForSecondsRealtime(cardRevealDelay);
            view.Reveal(index, cards[index]);
        }

        yield return new WaitForSecondsRealtime(resultDelay);
        reading = null;
        IsReading = false;
        LastOutcome = CursedCardRules.Evaluate(cards[0], cards[1], cards[2]);
        RefreshControls();
        ShowOutcome(LastOutcome.Value);
    }

    private void ShowOutcome(CursedCardOutcome outcome)
    {
        switch (outcome)
        {
            case CursedCardOutcome.TwoJestersAndQueenChance:
                twoJestersAndQueen.Resolve(view);
                break;
            case CursedCardOutcome.JesterAndTwoQueensChance:
                jesterAndTwoQueens.Resolve(view);
                break;
            default:
                view.ShowMessage(outcome switch
                {
                    CursedCardOutcome.OneAnomaly => "1 anomaly detected.",
                    CursedCardOutcome.TwoAnomalies => "2 anomalies detected.",
                    CursedCardOutcome.UnknownQuantity => "An anomaly exists. Quantity unknown.",
                    CursedCardOutcome.NoAnomaly => "No anomaly detected.",
                    _ => "No rule defined for this combination."
                });
                break;
        }
    }

    private void CancelReading()
    {
        if (reading != null) StopCoroutine(reading);
        reading = null;
        IsReading = false;
    }

    private void RefreshControls()
    {
        string equipLabel = equipKey == Key.None ? string.Empty : equipKey.ToString();
        string drawLabel = drawKey == Key.None ? string.Empty : drawKey.ToString();
        view.SetUses(RemainingUses, UsesPerInvestigation, equipLabel);
        view.SetControls(drawLabel, equipLabel, RemainingUses > 0 && !IsReading);
    }

    private void RefreshVisibility()
    {
        if (view != null) view.SetVisibility(IsEquipped, !isActiveAndEnabled || IsBlocked);
    }
}
