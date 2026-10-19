using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Menu de inicio del juego.
///
/// Tres botones: EMPEZAR (carga la escena del juego), CONFIGURACION (abre el panel de
/// opciones) y SALIDA (cierra el juego).
///
/// Las opciones se guardan en PlayerPrefs con las MISMAS claves que usa el menu de
/// pausa (op_sensibilidad, op_volumen), asi lo que se ajusta aqui ya viene aplicado
/// en la partida: el PauseMenu lee esas claves al arrancar la escena del juego.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject panelPrincipal;
    [SerializeField] private GameObject panelConfiguracion;
    [Tooltip("Titulo y subtitulo: se ocultan al abrir configuracion para que el panel se lea limpio.")]
    [SerializeField] private GameObject encabezado;

    [Header("Botones")]
    [SerializeField] private Button botonEmpezar;
    [SerializeField] private Button botonConfiguracion;
    [SerializeField] private Button botonSalir;
    [SerializeField] private Button botonVolver;

    [Header("Opciones")]
    [SerializeField] private Slider volumenSlider;
    [SerializeField] private Text volumenValor;
    [SerializeField] private Slider sensibilidadSlider;
    [SerializeField] private Text sensibilidadValor;
    [SerializeField] private Toggle pantallaCompletaToggle;

    [Header("Juego")]
    [Tooltip("Nombre de la escena que se carga al pulsar EMPEZAR.")]
    [SerializeField] private string escenaJuego = "Warehouse";
    [Tooltip("Version que se muestra abajo.")]
    [SerializeField] private Text versionText;

    private const string ClaveSensibilidad = "op_sensibilidad";
    private const string ClaveVolumen = "op_volumen";
    private const string ClavePantalla = "op_pantalla_completa";

    private void Awake()
    {
        // En el menu el raton manda: cursor libre y tiempo normal (por si se vuelve
        // de una partida que se quedo con el tiempo congelado).
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void Start()
    {
        if (versionText != null) versionText.text = "Project Outbreak · v0.3";

        if (botonEmpezar != null) botonEmpezar.onClick.AddListener(EmpezarPartida);
        if (botonConfiguracion != null) botonConfiguracion.onClick.AddListener(MostrarConfiguracion);
        if (botonSalir != null) botonSalir.onClick.AddListener(SalirDelJuego);
        if (botonVolver != null) botonVolver.onClick.AddListener(MostrarPrincipal);

        // Valores guardados de la ultima vez.
        float volumen = PlayerPrefs.GetFloat(ClaveVolumen, 1f);
        float sensibilidad = PlayerPrefs.GetFloat(ClaveSensibilidad, 0.15f);

        if (volumenSlider != null)
        {
            volumenSlider.minValue = 0f;
            volumenSlider.maxValue = 1f;
            volumenSlider.value = volumen;
            volumenSlider.onValueChanged.AddListener(CambiarVolumen);
        }

        if (sensibilidadSlider != null)
        {
            sensibilidadSlider.minValue = 0.03f;
            sensibilidadSlider.maxValue = 0.60f;
            sensibilidadSlider.value = sensibilidad;
            sensibilidadSlider.onValueChanged.AddListener(CambiarSensibilidad);
        }

        if (pantallaCompletaToggle != null)
        {
            pantallaCompletaToggle.isOn = PlayerPrefs.GetInt(ClavePantalla, Screen.fullScreen ? 1 : 0) == 1;
            pantallaCompletaToggle.onValueChanged.AddListener(CambiarPantallaCompleta);
        }

        // Se aplican al arrancar para que el menu ya suene y se vea como debe.
        CambiarVolumen(volumen);
        CambiarSensibilidad(sensibilidad);
        Screen.fullScreen = PlayerPrefs.GetInt(ClavePantalla, Screen.fullScreen ? 1 : 0) == 1;

        MostrarPrincipal();
    }

    private void Update()
    {
        // ESC dentro de configuracion vuelve a los botones principales.
        if (panelConfiguracion != null && panelConfiguracion.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            MostrarPrincipal();
    }

    // ------------------------------------------------------------------

    public void EmpezarPartida()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(escenaJuego)) SceneManager.LoadScene(escenaJuego);
    }

    public void MostrarConfiguracion()
    {
        if (panelPrincipal != null) panelPrincipal.SetActive(false);
        if (encabezado != null) encabezado.SetActive(false);
        if (panelConfiguracion != null) panelConfiguracion.SetActive(true);
    }

    public void MostrarPrincipal()
    {
        if (panelPrincipal != null) panelPrincipal.SetActive(true);
        if (encabezado != null) encabezado.SetActive(true);
        if (panelConfiguracion != null) panelConfiguracion.SetActive(false);
    }

    public void SalirDelJuego()
    {
        PlayerPrefs.Save();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------

    private void CambiarVolumen(float valor)
    {
        AudioListener.volume = valor;
        if (volumenValor != null) volumenValor.text = Mathf.RoundToInt(valor * 100f) + " %";
        PlayerPrefs.SetFloat(ClaveVolumen, valor);
    }

    private void CambiarSensibilidad(float valor)
    {
        if (sensibilidadValor != null) sensibilidadValor.text = valor.ToString("F2");
        PlayerPrefs.SetFloat(ClaveSensibilidad, valor);
    }

    private void CambiarPantallaCompleta(bool activo)
    {
        Screen.fullScreen = activo;
        PlayerPrefs.SetInt(ClavePantalla, activo ? 1 : 0);
    }
}
