using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CursedCardsView : MonoBehaviour
{
    [Serializable]
    private sealed class CardSlot
    {
        public Image face;
        public Image artwork;
        public TMP_Text rank;
        public TMP_Text title;
    }

    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject hud;
    [SerializeField] private TMP_Text panelUses;
    [SerializeField] private TMP_Text hudUses;
    [SerializeField] private TMP_Text result;
    [SerializeField] private TMP_Text controls;
    [SerializeField] private CardSlot[] slots = new CardSlot[3];

    [Header("Optional Card Artwork")]
    [SerializeField] private Sprite jesterArtwork;
    [SerializeField] private Sprite kingArtwork;
    [SerializeField] private Sprite queenArtwork;

    internal void SetVisibility(bool equipped, bool blocked)
    {
        Show(panel, equipped && !blocked);
        Show(hud, !equipped && !blocked);
    }

    private static void Show(GameObject item, bool visible)
    {
        if (item.activeSelf != visible) item.SetActive(visible);
    }

    internal void SetUses(int remaining, int maximum, string equipKey)
    {
        panelUses.text = $"{remaining} / {maximum} USES LEFT";
        hudUses.text = $"Cursed Cards   {remaining}/{maximum}";
        if (equipKey.Length > 0) hudUses.text += $"   [{equipKey}]";
    }

    internal void SetControls(string readingKey, string equipKey, bool canDraw)
    {
        string drawHint = canDraw && readingKey.Length > 0 ? $"[{readingKey}] Draw cards" : string.Empty;
        string equipHint = equipKey.Length > 0 ? $"[{equipKey}] Unequip" : string.Empty;
        controls.text = (drawHint + "     " + equipHint).Trim();
    }

    internal void ShowMessage(string message)
    {
        result.text = message;
    }

    internal void ShowCardBacks()
    {
        foreach (var slot in slots)
        {
            slot.face.color = new Color32(50, 39, 43, 255);
            slot.artwork.gameObject.SetActive(false);
            slot.rank.gameObject.SetActive(true);
            slot.rank.color = new Color32(213, 175, 112, 255);
            slot.rank.text = "?";
            slot.title.text = string.Empty;
        }
    }

    internal void Reveal(int index, CursedCardType card)
    {
        var slot = slots[index];
        var (label, ink, art) = card switch
        {
            CursedCardType.Jester => ("J", new Color32(124, 53, 62, 255), jesterArtwork),
            CursedCardType.King => ("K", new Color32(83, 64, 43, 255), kingArtwork),
            _ => ("Q", new Color32(48, 75, 75, 255), queenArtwork)
        };

        slot.face.color = new Color32(229, 211, 173, 255);
        slot.rank.text = label;
        slot.rank.color = ink;
        slot.title.text = card.ToString();
        slot.title.color = ink;
        slot.artwork.sprite = art;
        slot.artwork.gameObject.SetActive(art != null);
        slot.rank.gameObject.SetActive(art == null);
    }
}
