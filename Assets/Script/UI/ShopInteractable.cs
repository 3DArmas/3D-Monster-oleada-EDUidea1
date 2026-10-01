using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cubo rojo de la tienda. Aparece delante del jugador al superar la ronda y se
/// abre con la tecla E o con el clic del raton apuntandole con la mira.
///
/// OJO: este objeto se queda SIEMPRE activo (si se desactivara, su OnEnable no
/// se ejecutaria y no recibiria el cambio de estado). Lo que se enciende y apaga
/// es el aspecto fisico: el render y el collider.
/// </summary>
public class ShopInteractable : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ShopUI shopUI;
    [SerializeField] private HudController hud;

    [Header("Ajustes")]
    [Tooltip("Distancia maxima para poder interactuar.")]
    [SerializeField] private float interactRange = 4f;
    [Tooltip("Distancia a la que aparece delante del jugador.")]
    [SerializeField] private float spawnDistance = 3.5f;
    [SerializeField] private float hoverHeight = 1.5f;
    [SerializeField] private float spinSpeed = 45f;

    /// <summary>True si el jugador esta apuntando al cubo con la tienda disponible.</summary>
    public static bool PointerOnShop { get; private set; }

    private Renderer cubeRenderer;
    private Collider cubeCollider;
    private Camera playerCamera;
    private Transform player;
    private InputAction interactAction;
    private InputAction fireAction;
    private bool visible;
    private bool aiming;

    private void Awake()
    {
        cubeRenderer = GetComponent<Renderer>();
        cubeCollider = GetComponent<Collider>();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.OnStateChanged += HandleState;
        PointerOnShop = false;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.OnStateChanged -= HandleState;
        PointerOnShop = false;
    }

    private void Start()
    {
        CachePlayer();
    }

    private void Update()
    {
        if (!visible)
        {
            aiming = false;
            PointerOnShop = false;
            return;
        }

        if (playerCamera == null || player == null) CachePlayer();
        if (playerCamera == null) return;

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        aiming = IsBeingAimedAt();
        PointerOnShop = aiming;

        if (!aiming) return;

        if (hud != null) hud.ShowMessage("PULSA  [E]  O  CLIC  PARA ABRIR LA TIENDA", 0.2f);

        // Abrir con E o con clic (el mismo boton que dispara). Se usa IsPressed y no
        // WasPressedThisFrame: asi basta con mantener pulsado y no depende de que la
        // pulsacion caiga exactamente en el frame del Update.
        if (interactAction != null && interactAction.IsPressed()) { Open(); return; }
        if (fireAction != null && fireAction.IsPressed()) { Open(); return; }
    }

    private bool IsBeingAimedAt()
    {
        if (shopUI != null && shopUI.IsOpen) return false;
        if (cubeCollider == null || !cubeCollider.enabled) return false;

        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, interactRange)) return false;

        return hit.collider != null && hit.collider.transform.IsChildOf(transform);
    }

    private void Open()
    {
        if (shopUI == null) return;
        shopUI.Open();
        PointerOnShop = false;
    }

    private void HandleState(GameState state)
    {
        if (state == GameState.Shop)
        {
            PlaceInFrontOfPlayer();
            SetVisible(true);
        }
        else
        {
            SetVisible(false);
        }
    }

    private void SetVisible(bool value)
    {
        visible = value;
        if (cubeRenderer != null) cubeRenderer.enabled = value;
        if (cubeCollider != null) cubeCollider.enabled = value;
    }

    private void PlaceInFrontOfPlayer()
    {
        if (player == null) CachePlayer();
        if (player == null) return;

        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 position = player.position + forward * spawnDistance;
        position.y = hoverHeight;
        transform.position = position;
    }

    private void CachePlayer()
    {
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) player = health.transform;

        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) playerCamera = FindFirstObjectByType<Camera>();

        if (playerCamera != null && interactAction == null)
        {
            PlayerInput input = playerCamera.GetComponentInParent<PlayerInput>();
            if (input != null)
            {
                interactAction = input.actions.FindAction("Interact");
                fireAction = input.actions.FindAction("Fire");
            }
        }
    }
}
