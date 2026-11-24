using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public sealed class StorageShedDoor : MonoBehaviour
{
    [SerializeField] private float reach = 2.4f;
    [SerializeField] private float swingAngle = 94f;
    [SerializeField] private float angularSpeed = 170f;

    private BoxCollider doorCollider;
    private Quaternion closedRotation;
    private Camera playerCamera;
    private CharacterController player;
    private float angle;
    private float targetAngle;
    private bool canInteract;

    private void Awake()
    {
        doorCollider = GetComponent<BoxCollider>();
        closedRotation = transform.localRotation;
    }

    private void Update()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera != null) player = playerCamera.GetComponentInParent<CharacterController>();
        }

        canInteract = playerCamera != null &&
            Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out var hit,
                reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            hit.collider == doorCollider;

        if (canInteract && (Keyboard.current?.eKey.wasPressedThisFrame == true ||
                            Gamepad.current?.buttonWest.wasPressedThisFrame == true))
        {
            if (targetAngle != 0)
            {
                targetAngle = 0;
            }
            else
            {
                Vector3 offset = playerCamera.transform.position - transform.position;
                Vector3 closedNormal = transform.parent.TransformDirection(closedRotation * Vector3.right);
                targetAngle = Vector3.Dot(offset, closedNormal) > 0 ? -swingAngle : swingAngle;
            }
        }

        float nextAngle = Mathf.MoveTowards(angle, targetAngle, angularSpeed * Time.deltaTime);
        if (Mathf.Approximately(angle, nextAngle)) return;
        var nextRotation = closedRotation * Quaternion.Euler(0, nextAngle, 0);
        if (WouldHitPlayer(nextRotation)) return;

        angle = nextAngle;
        transform.localRotation = nextRotation;
    }

    private bool WouldHitPlayer(Quaternion rotation)
    {
        if (player == null || !player.enabled) return false;
        Vector3 center = transform.parent.InverseTransformPoint(player.bounds.center) - transform.localPosition;
        Vector3 offset = Quaternion.Inverse(rotation) * center - doorCollider.center;
        Vector3 half = doorCollider.size * .5f;
        if (Mathf.Abs(offset.y) > half.y + player.bounds.extents.y) return false;

        float dx = Mathf.Max(0, Mathf.Abs(offset.x) - half.x);
        float dz = Mathf.Max(0, Mathf.Abs(offset.z) - half.z);
        float radius = player.radius - player.skinWidth;
        return dx * dx + dz * dz < radius * radius;
    }

    private void OnGUI()
    {
        if (!canInteract) return;
        string key = Gamepad.current != null && Keyboard.current == null ? "X / Square" : "E";
        string action = targetAngle == 0 ? "Open storage door" : "Close storage door";
        GUI.Box(new Rect((Screen.width - 208) * .5f, Screen.height * .58f, 208, 26), key + "   " + action);
    }
}
