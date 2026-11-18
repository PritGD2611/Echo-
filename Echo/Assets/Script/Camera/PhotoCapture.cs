using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PhotoCapture : MonoBehaviour
{
    [Header("Camera Mode Settings")]
    [SerializeField] private GameObject cameraCanvas; // Drag 'Camera - Canvas' here
    [SerializeField] private GameObject journalPanel; // Optional: Drag 'MainPanel(BG)' here so camera won't shoot while journal is open
    private bool isCameraModeActive = false;

    [Header("Photo Taker")]
    [SerializeField] private Image photoDisplayArea;  // Drag 'PhotoDisplayArea' here
    [SerializeField] private GameObject photoFrame;   // Drag 'PhotoFrameBG' here
    private Texture2D screenCapture;
    private bool viewingPhoto;

    [Header("Flash Effects")]
    [SerializeField] private GameObject cameraFlash;
    [SerializeField] private float flashTime = 0.2f;

    [Header("Photo Fader Effects")]
    [SerializeField] private Animator fadingAnimation;

    void Start()
    {
        screenCapture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        // Start with Camera Mode and PhotoFrameBG turned off
        isCameraModeActive = false;
        if (photoFrame != null) photoFrame.SetActive(false);
        if (cameraCanvas != null) cameraCanvas.SetActive(false);
    }

    void Update()
    {
        // Block camera inputs if the Journal is currently open
        if (journalPanel != null && journalPanel.activeInHierarchy)
            return;

        // 1. Toggle Camera Mode ON/OFF when pressing 'P'
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            ToggleCameraMode();
        }

        // 2. Only allow Left-Click photo capture if Camera Mode is ACTIVE
        if (!isCameraModeActive)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!viewingPhoto)
            {
                viewingPhoto = true;
                StartCoroutine(CapturePhoto());
            }
            else
            {
                RemovePhoto();
            }
        }
    }

    private void ToggleCameraMode()
    {
        isCameraModeActive = !isCameraModeActive;

        if (cameraCanvas != null)
            cameraCanvas.SetActive(isCameraModeActive);

        // If the player turns off Camera Mode while looking at a photo, clean it up
        if (!isCameraModeActive)
        {
            StopAllCoroutines();
            if (cameraFlash != null) cameraFlash.SetActive(false);
            RemovePhoto();
        }

        Debug.Log("Camera Mode Active: " + isCameraModeActive);
    }

    IEnumerator CapturePhoto()
    {
        viewingPhoto = true;

        if (cameraFlash != null)
            cameraFlash.SetActive(true);

        yield return new WaitForEndOfFrame();

        Rect regionToRead = new Rect(0, 0, Screen.width, Screen.height);
        screenCapture.ReadPixels(regionToRead, 0, 0, false);
        screenCapture.Apply();

        ShowPhoto();

        yield return new WaitForSeconds(flashTime);
        if (cameraFlash != null)
            cameraFlash.SetActive(false);
    }

    void ShowPhoto()
    {
        Sprite photoSprite = Sprite.Create(
            screenCapture,
            new Rect(0.0f, 0.0f, screenCapture.width, screenCapture.height),
            new Vector2(0.5f, 0.5f),
            100.0f
        );

        photoDisplayArea.sprite = photoSprite;
        photoFrame.SetActive(true);
        fadingAnimation.Play("PhotoFade");
    }

    void RemovePhoto()
    {
        if (photoDisplayArea != null) photoDisplayArea.sprite = null;
        if (photoFrame != null) photoFrame.SetActive(false);
        viewingPhoto = false;
    }
}