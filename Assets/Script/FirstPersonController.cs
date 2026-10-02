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

    // --- Multiplicadores que se compran en la tienda ---
    private float multiplicadorVelocidad = 1f;
    private float multiplicadorSalto = 1f;

    public float VelocidadActual => walkSpeed * multiplicadorVelocidad;
    public float AlturaSaltoActual => jumpHeight * multiplicadorSalto;

    /// <summary>Sube la velocidad un porcentaje (1.08 = +8 %).</summary>
    public void MejorarVelocidad(float factor) { multiplicadorVelocidad *= factor; }

    /// <summary>Sensibilidad del raton. La ajusta el menu de opciones.</summary>
    public float Sensibilidad
    {
        get => mouseSensitivity;
        set => mouseSensitivity = Mathf.Clamp(value, 0.02f, 1f);
    }

    /// <summary>Sube la altura de salto un porcentaje.</summary>
    public void MejorarSalto(float factor) { multiplicadorSalto *= factor; }

    // Referencias a las Input Actions (asignadas desde el PlayerInput component)
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        // El pitch de la mirada parte de la rotacion que tenga la camara colocada
        // en la escena (asi no hay saltos raros al empezar la partida).
        if (cameraHolder != null)
        {
            pitch = cameraHolder.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }

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
        // PROTECCIONES CONTRA PICOS DE LA MIRADA.
        // Esto era lo que dejaba la camara mirando al suelo al empezar: en el primer
        // frame llegaba un delta enorme (ventana sin foco, alt-tab, raton saltando) y
        // se aplicaba tal cual: 42 grados de golpe, y ahi se quedaba.

        // 1) Si el juego no tiene el foco, no se mira.
        if (!Application.isFocused) return;

        // 2) Con el cursor suelto (tienda, pausa, game over) tampoco se mira.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // 3) Se limita el delta por frame: los saltos absurdos se recortan, pero un
        //    giro rapido de verdad (hasta 60 unidades) se respeta entero.
        Vector2 mirada = Vector2.ClampMagnitude(lookInput, 60f);

        // Rotación horizontal (yaw) rota todo el cuerpo del jugador
        float yaw = mirada.x * mouseSensitivity;
        transform.Rotate(Vector3.up * yaw);

        // Rotación vertical (pitch) solo rota la cámara
        pitch -= mirada.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMove()
    {
        // Dirección relativa a hacia donde mira el jugador
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * walkSpeed * multiplicadorVelocidad * Time.deltaTime);

        // Gravedad
        if (controller.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f; // pequeño valor para mantenerlo pegado al suelo

        // Se usa IsPressed y no WasPressedThisFrame: asi el salto no se pierde si
        // pulsas un instante antes de tocar el suelo, que era el fallo que notaba el jugador.
        if (jumpAction != null && jumpAction.IsPressed() && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * multiplicadorSalto * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

}