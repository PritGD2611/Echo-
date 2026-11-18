using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PhotoCapture : MonoBehaviour
{
    [Header("Camera Equipment")]
    [SerializeField] private GameObject cameraModel3D;       // Drag your 3D Camera model here
    [SerializeField] private Camera viewfinderCamera;       // Drag the ViewfinderCamera here
    [SerializeField] private GameObject journalPanel;       // Drag MainPanel(BG) here

    [Header("UI & Polaroids")]
    [SerializeField] private GameObject cameraCanvas;       // UI overlay (lines, stickers, etc.)
    [SerializeField] private Image photoDisplayArea;
    [SerializeField] private GameObject photoFrame;

    [Header("Flash & Fader")]
    [SerializeField] private GameObject cameraFlash;
    [SerializeField] private float flashTime = 0.2f;
    [SerializeField] private Animator fadingAnimation;

    private bool isCameraEquipped = false;
    private bool viewingPhoto = false;
    private Texture2D screenCapture;

    void Start()
    {
        screenCapture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        // Start unequipped
        SetCameraEquipped(false);
        if (photoFrame != null) photoFrame.SetActive(false);
    }

    void Update()
    {
        // 1. Toggle Camera Equip state with 'P'
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            // Don't equip/unequip if journal is open
            if (journalPanel == null || !journalPanel.activeInHierarchy)
            {
                SetCameraEquipped(!isCameraEquipped);
            }
        }

        if (!isCameraEquipped) return;

        // Block taking photos if journal is open
        if (journalPanel != null && journalPanel.activeInHierarchy) return;

        // 2. Left Click to Take Photo or Dismiss Polaroid
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!viewingPhoto)
            {
                StartCoroutine(CapturePhoto());
            }
            else
            {
                RemovePhoto();
            }
        }
    }

    private void SetCameraEquipped(bool equipped)
    {
        isCameraEquipped = equipped;

        if (cameraModel3D != null) cameraModel3D.SetActive(equipped);
        if (viewfinderCamera != null) viewfinderCamera.gameObject.SetActive(equipped); // Fixed here!
        if (cameraCanvas != null) cameraCanvas.SetActive(equipped);

        if (!equipped)
        {
            StopAllCoroutines();
            if (cameraFlash != null) cameraFlash.SetActive(false);
            RemovePhoto();
        }

        Debug.Log("Camera Equipped: " + isCameraEquipped);
    }

    IEnumerator CapturePhoto()
    {
        viewingPhoto = true;

        if (cameraFlash != null)
            cameraFlash.SetActive(true);

        yield return new WaitForEndOfFrame();

        // 1. Get the Render Texture from the Viewfinder Camera
        RenderTexture activeRT = RenderTexture.active;
        RenderTexture.active = viewfinderCamera.targetTexture;

        // 2. Render what the viewfinder sees into a new Texture2D
        Texture2D camTexture = new Texture2D(viewfinderCamera.targetTexture.width, viewfinderCamera.targetTexture.height, TextureFormat.RGB24, false);
        camTexture.ReadPixels(new Rect(0, 0, viewfinderCamera.targetTexture.width, viewfinderCamera.targetTexture.height), 0, 0);
        camTexture.Apply();

        // 3. Restore previous render texture
        RenderTexture.active = activeRT;

        // 4. Assign this captured viewfinder texture to our screenCapture variable
        screenCapture = camTexture;

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

        if (photoDisplayArea != null) photoDisplayArea.sprite = photoSprite;
        if (photoFrame != null) photoFrame.SetActive(true);
        if (fadingAnimation != null) fadingAnimation.Play("PhotoFade");
    }

    void RemovePhoto()
    {
        if (photoDisplayArea != null) photoDisplayArea.sprite = null;
        if (photoFrame != null) photoFrame.SetActive(false);
        viewingPhoto = false;
    }
}