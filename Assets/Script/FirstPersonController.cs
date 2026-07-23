using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform cameraHolder; // Empty a la altura de los ojos
    [SerializeField] private CharacterController controller;

    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Cámara")]
    [SerializeField] private float mouseSensitivity = 0.15f; // ajustar a gusto
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalVelocity;
    private float pitch;

    // Referencias a las Input Actions (asignadas desde el PlayerInput component)
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        PlayerInput playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        jumpAction = playerInput.actions["Jump"];
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        ReadInput();
        HandleLook();
        HandleMove();
    }

    private void ReadInput()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        lookInput = lookAction.ReadValue<Vector2>();
    }

    private void HandleLook()
    {
        // Rotación horizontal (yaw) rota todo el cuerpo del jugador
        float yaw = lookInput.x * mouseSensitivity;
        transform.Rotate(Vector3.up * yaw);

        // Rotación vertical (pitch) solo rota la cámara
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMove()
    {
        // Dirección relativa a hacia donde mira el jugador
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * walkSpeed * Time.deltaTime);

        // Gravedad
        if (controller.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f; // pequeño valor para mantenerlo pegado al suelo

        if (jumpAction != null && jumpAction.WasPressedThisFrame() && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

}