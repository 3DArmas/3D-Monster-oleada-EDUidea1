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
    private bool visible;
    private bool aiming;
    /// <summary>Hay que soltar el boton antes de poder volver a abrir. Si no, con el
    /// clic mantenido la tienda se reabria sola justo despues de cerrarla.</summary>
    private bool esperandoSoltar;
    private bool estabaAbierta;

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
        // Si la tienda se acaba de cerrar, exigir soltar el boton antes de reabrirla.
        bool abiertaAhora = shopUI != null && shopUI.IsOpen;
        if (estabaAbierta && !abiertaAhora) esperandoSoltar = true;
        estabaAbierta = abiertaAhora;

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

        if (hud != null) hud.ShowMessage("[E]  ABRIR TIENDA", 0.2f);

        // Si acaba de abrir o cerrar, hay que soltar el boton antes de poder abrir otra vez.
        if (esperandoSoltar)
        {
            bool suelto = (interactAction == null || !interactAction.IsPressed());
            if (suelto) esperandoSoltar = false;
            return;
        }

        // Solo con la tecla E (el clic ya no abre la tienda).
        if (interactAction != null && interactAction.IsPressed()) Open();
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
        esperandoSoltar = true;
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
            if (input != null) interactAction = input.actions.FindAction("Interact");
        }
    }
}
