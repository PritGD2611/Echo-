using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;
public class PhotoCapture : MonoBehaviour
{
    [Header("Photo Taker")]
    [SerializeField] private Image photoDisplayArea;
    [SerializeField] private GameObject photoFrame;
    private Texture2D screenCapture;
    private bool viewingPhoto ;

    [Header("Flash Effects")]
    [SerializeField] private GameObject cameraFlash;
    [SerializeField] private float flashTime ;

    [Header("Photo Fader Effects")]
    [SerializeField] private Animator fadingAnimation;

    void Start()
    {
        screenCapture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if(!viewingPhoto)
            {
                viewingPhoto = true;
                // Take a screenshot when the left mouse button is clicked
                StartCoroutine(CapturePhoto());
            }
            else
            {
                RemovePhoto();
            }
        }
    }

    IEnumerator CapturePhoto()
    {

        viewingPhoto = true;


        yield return new WaitForEndOfFrame();

        Rect regionToRead = new Rect(0, 0, Screen.width, Screen.height);

        screenCapture.ReadPixels(regionToRead, 0, 0, false);
        screenCapture.Apply();
        ShowPhoto();

    }

    void ShowPhoto()
    {
        Sprite photoSprite = Sprite.Create(screenCapture, new Rect(0.0f, 0.0f, screenCapture.width, screenCapture.height), new Vector2(0.5f,0.5f),100.0f );
        // photo area = photosprite
        photoDisplayArea.sprite = photoSprite;

        photoFrame.SetActive(true);
        //Do Flash
        StartCoroutine(CameraFlashEffect());
        fadingAnimation.Play("PhotoFade");
    }

    IEnumerator CameraFlashEffect()
    {
        cameraFlash.SetActive(true);
        yield return new WaitForSeconds(flashTime);
        cameraFlash.SetActive(false);
    }

    void RemovePhoto()
    {
        photoDisplayArea.sprite = null;
        photoFrame.SetActive(false);
        viewingPhoto = false;
    }
}
