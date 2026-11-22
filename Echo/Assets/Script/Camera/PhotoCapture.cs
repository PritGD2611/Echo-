using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PhotoCapture : MonoBehaviour
{
    [Header("Camera Equipment")]
    [SerializeField] private GameObject cameraModel3D;
    [SerializeField] private Camera viewfinderCamera;
    [SerializeField] private GameObject journalPanel;
    [SerializeField] private JournalTabAndInfoManager journalManager;

    [Header("UI & Polaroids")]
    [SerializeField] private GameObject cameraCanvas;
    [SerializeField] private Image photoDisplayArea;
    [SerializeField] private GameObject photoFrame;

    [Header("Flash & Fader")]
    [SerializeField] private GameObject cameraFlash;
    [SerializeField] private float flashTime = 0.2f;
    [SerializeField] private Animator fadingAnimation;

    [Header("Save Settings")]
    [SerializeField] private string saveFolderName = "CapturedPhotos";
    private int photoCounter = 1;

    private bool isCameraEquipped = false;
    private bool viewingPhoto = false;
    private Texture2D screenCapture;

    void Start()
    {
        screenCapture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        SetCameraEquipped(false);
        if (photoFrame != null) photoFrame.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (journalPanel == null || !journalPanel.activeInHierarchy)
            {
                SetCameraEquipped(!isCameraEquipped);
            }
        }

        if (!isCameraEquipped) return;
        if (journalPanel != null && journalPanel.activeInHierarchy) return;

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
        if (viewfinderCamera != null) viewfinderCamera.gameObject.SetActive(equipped);
        if (cameraCanvas != null) cameraCanvas.SetActive(equipped);

        if (!equipped)
        {
            StopAllCoroutines();
            if (cameraFlash != null) cameraFlash.SetActive(false);
            RemovePhoto();
        }
    }

    IEnumerator CapturePhoto()
    {
        viewingPhoto = true;

        if (cameraFlash != null)
            cameraFlash.SetActive(true);

        yield return new WaitForEndOfFrame();

        RenderTexture activeRT = RenderTexture.active;
        RenderTexture.active = viewfinderCamera.targetTexture;

        Texture2D camTexture = new Texture2D(
            viewfinderCamera.targetTexture.width,
            viewfinderCamera.targetTexture.height,
            TextureFormat.RGB24,
            false
        );
        camTexture.ReadPixels(
            new Rect(0, 0, viewfinderCamera.targetTexture.width, viewfinderCamera.targetTexture.height),
            0,
            0
        );
        camTexture.Apply();

        RenderTexture.active = activeRT;
        screenCapture = camTexture;

        SavePhotoToProject();
        ShowPhoto();

        yield return new WaitForSeconds(flashTime);
        if (cameraFlash != null)
            cameraFlash.SetActive(false);
    }

    private void SavePhotoToProject()
    {
        if (screenCapture == null) return;

        string folderPath = Path.Combine(Application.dataPath, saveFolderName);
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string fileName = $"EchoPhoto_{System.DateTime.Now:yyyyMMdd_HHmmss}_{photoCounter}.png";
        string fullPath = Path.Combine(folderPath, fileName);

        byte[] pngBytes = screenCapture.EncodeToPNG();
        File.WriteAllBytes(fullPath, pngBytes);

        photoCounter++;

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
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

        if (journalManager == null)
            journalManager = FindFirstObjectByType<JournalTabAndInfoManager>(FindObjectsInactive.Include);

        if (journalManager != null)
            journalManager.AddPhotoToJournal(photoSprite);
        else
            Debug.LogWarning("PhotoCapture could not find JournalTabAndInfoManager in the scene!");
    }

    void RemovePhoto()
    {
        if (photoDisplayArea != null) photoDisplayArea.sprite = null;
        if (photoFrame != null) photoFrame.SetActive(false);
        viewingPhoto = false;
    }
}