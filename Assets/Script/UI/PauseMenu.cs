using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menu de pausa con ESC.
///
/// Prioridad de la tecla ESC:
///   1) Si la tienda esta abierta, la cierra (para poder seguir comprando).
///   2) Si la partida ha terminado, no hace nada (la pantalla de game over manda).
///   3) Si no, pausa o reanuda.
///
/// Al pausar se congela el tiempo (Time.timeScale = 0) y se libera el cursor.
/// Las opciones (sensibilidad, volumen y pantalla completa) se guardan en PlayerPrefs.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject botonesPrincipales;
    [SerializeField] private GameObject panelOpciones;
    [SerializeField] private Text versionText;

    [Header("Opciones")]
    [SerializeField] private Slider sensibilidadSlider;
    [SerializeField] private Text sensibilidadValor;
    [SerializeField] private Slider volumenSlider;
    [SerializeField] private Text volumenValor;
    [SerializeField] private Toggle pantallaCompletaToggle;

    [Header("Sistemas")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ShopUI shopUI;
    [SerializeField] private Texture2D cursorMano;

    private const string ClaveSensibilidad = "op_sensibilidad";
    private const string ClaveVolumen = "op_volumen";

    public static bool IsPaused { get; private set; }

    private FirstPersonController playerController;
    private bool opcionesAbiertas;
    private int fotogramasIgnorados;
    private bool escPulsadoAntes;

    private void Awake()
    {
        IsPaused = false;
    }

    private void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (panelOpciones != null) panelOpciones.SetActive(false);
        if (botonesPrincipales != null) botonesPrincipales.SetActive(true);
        if (versionText != null) versionText.text = "Project Outbreak · v0.3";

        CachePlayer();

        // Cargar opciones guardadas
        float sens = PlayerPrefs.GetFloat(ClaveSensibilidad, playerController != null ? playerController.Sensibilidad : 0.15f);
        float vol = PlayerPrefs.GetFloat(ClaveVolumen, 1f);

        if (sensibilidadSlider != null)
        {
            sensibilidadSlider.minValue = 0.03f;
            sensibilidadSlider.maxValue = 0.60f;
            sensibilidadSlider.value = sens;
            sensibilidadSlider.onValueChanged.AddListener(CambiarSensibilidad);
        }
        if (volumenSlider != null)
        {
            volumenSlider.minValue = 0f;
            volumenSlider.maxValue = 1f;
            volumenSlider.value = vol;
            volumenSlider.onValueChanged.AddListener(CambiarVolumen);
        }
        if (pantallaCompletaToggle != null)
        {
            pantallaCompletaToggle.isOn = Screen.fullScreen;
            pantallaCompletaToggle.onValueChanged.AddListener(CambiarPantallaCompleta);
        }

        CambiarSensibilidad(sens);
        CambiarVolumen(vol);
    }

    private void OnDestroy()
    {
        // Si se recarga la escena estando en pausa, el tiempo debe volver a la normalidad.
        if (IsPaused) Time.timeScale = 1f;
        IsPaused = false;
    }

    private void Update()
    {
        // Un fotograma de margen: asi el ESC que cierra la tienda no pausa el juego a la vez.
        if (fotogramasIgnorados > 0) { fotogramasIgnorados--; return; }

        if (Keyboard.current == null) return;

        // Deteccion de flanco propia en vez de wasPressedThisFrame: es igual de correcta
        // con teclado real y ademas funciona con teclados simulados (util para probar).
        bool pulsado = Keyboard.current.escapeKey.isPressed;
        bool recienPulsado = pulsado && !escPulsadoAntes;
        escPulsadoAntes = pulsado;
        if (!recienPulsado) return;

        // 1) La tienda tiene prioridad: ESC la cierra.
        if (ShopUI.IsAnyOpen)
        {
            if (shopUI != null) shopUI.Close();
            fotogramasIgnorados = 1;
            return;
        }

        // 2) Con la partida terminada manda la pantalla de game over.
        if (gameManager != null && gameManager.State == GameState.GameOver) return;

        // 3) Si estas en Opciones, ESC te devuelve a los botones de pausa.
        if (IsPaused && opcionesAbiertas) { MostrarBotones(); return; }

        // 4) Pausar o reanudar.
        if (IsPaused) Reanudar(); else Pausar();
    }

    // ------------------------------------------------------------------

    public void Pausar()
    {
        if (IsPaused) return;

        CachePlayer();
        IsPaused = true;
        Time.timeScale = 0f;

        if (panel != null) panel.SetActive(true);
        MostrarBotones();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (cursorMano != null) Cursor.SetCursor(cursorMano, new Vector2(58f, 6f), CursorMode.Auto);

        if (playerController != null) playerController.enabled = false;
    }

    public void Reanudar()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (panel != null) panel.SetActive(false);

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Solo se devuelve el control si la tienda no esta ocupando la pantalla.
        if (playerController != null && !ShopUI.IsAnyOpen) playerController.enabled = true;
    }

    public void MostrarBotones()
    {
        opcionesAbiertas = false;
        if (botonesPrincipales != null) botonesPrincipales.SetActive(true);
        if (panelOpciones != null) panelOpciones.SetActive(false);
    }

    public void MostrarOpciones()
    {
        opcionesAbiertas = true;
        if (botonesPrincipales != null) botonesPrincipales.SetActive(false);
        if (panelOpciones != null) panelOpciones.SetActive(true);
    }

    public void ReiniciarPartida()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        if (gameManager != null) gameManager.RestartGame();
    }

    public void SalirDelJuego()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------
    // Opciones
    // ------------------------------------------------------------------

    private void CambiarSensibilidad(float valor)
    {
        CachePlayer();
        if (playerController != null) playerController.Sensibilidad = valor;
        if (sensibilidadValor != null) sensibilidadValor.text = valor.ToString("F2");
        PlayerPrefs.SetFloat(ClaveSensibilidad, valor);
    }

    private void CambiarVolumen(float valor)
    {
        AudioListener.volume = valor;
        if (volumenValor != null) volumenValor.text = Mathf.RoundToInt(valor * 100f) + " %";
        PlayerPrefs.SetFloat(ClaveVolumen, valor);
    }

    private void CambiarPantallaCompleta(bool activo)
    {
        Screen.fullScreen = activo;
    }

    private void CachePlayer()
    {
        if (playerController == null) playerController = FindFirstObjectByType<FirstPersonController>();
    }
}
