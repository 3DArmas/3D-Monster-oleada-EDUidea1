using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tienda entre rondas (version con pestanas y tarjetas).
///
/// - Se abre SOLO cuando el jugador interactua con el cubo rojo (E o clic).
/// - Se cierra con el hueso de arriba a la derecha o con ESC.
/// - El boton LISTO arranca la siguiente ronda.
/// - Solo vende lo que existe de verdad; lo demas va con candado.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text infoText;
    [SerializeField] private Text titleText;

    [Tooltip("Etiqueta DINERO del sobre. Si esta asignada, moneyText solo muestra la cifra.")]
    [SerializeField] private Text moneyLabelText;

    [Header("Pestanas")]
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private Image[] tabFondos;
    [SerializeField] private Color tabOnColor = Color.white;
    [SerializeField] private Color tabOffColor = new Color(0.66f, 0.62f, 0.56f, 1f);
    [Tooltip("Escala de la pestana seleccionada (la cinta se levanta un poco).")]
    [SerializeField] private float tabOnScale = 1.06f;

    [Header("Tarjetas")]
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private ShopCard cardTemplate;

    [Header("Estado del personaje (orden: vida, velocidad, salto, dano, reserva, gastado)")]
    [SerializeField] private Text[] statusValues;

    [Header("Sistemas")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MoneySystem moneySystem;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Cursor")]
    [SerializeField] private Texture2D cursorMano;

    public static bool IsAnyOpen { get; private set; }
    public bool IsOpen => panel != null && panel.activeSelf;

    private readonly Dictionary<string, int> niveles = new Dictionary<string, int>();
    private readonly List<ShopCard> tarjetas = new List<ShopCard>();
    private readonly List<WeaponAmmo> armas = new List<WeaponAmmo>();
    private ShopCategory categoriaActual = ShopCategory.Movilidad;
    private FirstPersonController playerController;
    private float infoHideTime;
    private int gastado;
    /// <summary>Open() ya se llamo al menos una vez. Si este objeto empieza inactivo en la
    /// escena, su Start se ejecuta en el primer Open y no debe volver a ocultar el panel.</summary>
    private bool abiertaAlgunaVez;

    private void Awake()
    {
        IsAnyOpen = false;
    }

    private void Start()
    {
        // Si el primer Open() activo este objeto, Start llega DESPUES de abrir:
        // ocultar el panel aqui cerraba la tienda nada mas abrirla.
        if (panel != null && !abiertaAlgunaVez) panel.SetActive(false);
        if (infoText != null && !abiertaAlgunaVez) infoText.text = string.Empty;

        // Las pestanas se cablean solas por indice.
        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int indice = i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => SeleccionarCategoria(indice));
            }
        }

        if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
    }

    private void Update()
    {
        // OJO: el ESC lo gestiona PauseMenu (que cierra la tienda antes de pausar),
        // para que una sola tecla no haga dos cosas a la vez.

        if (infoText != null && infoText.text.Length > 0 && Time.time >= infoHideTime) infoText.text = string.Empty;
    }

    // ------------------------------------------------------------------
    // Abrir / cerrar
    // ------------------------------------------------------------------

    public void Open()
    {
        if (IsOpen) return;

        abiertaAlgunaVez = true;
        CachePlayer();
        if (panel != null) panel.SetActive(true);
        IsAnyOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (cursorMano != null) Cursor.SetCursor(cursorMano, new Vector2(58f, 6f), CursorMode.Auto);

        if (playerController != null) playerController.enabled = false;

        if (titleText != null) titleText.text = "TIENDA";
        SeleccionarCategoria((int)categoriaActual);
        RefrescarDinero();
        RefrescarEstado();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        IsAnyOpen = false;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerController != null) playerController.enabled = true;
    }

    /// <summary>Boton LISTO: cierra y arranca la siguiente ronda.</summary>
    public void Ready()
    {
        Close();
        if (gameManager != null) gameManager.ContinueToNextRound();
    }

    // ------------------------------------------------------------------
    // Pestanas y tarjetas
    // ------------------------------------------------------------------

    public void SeleccionarCategoria(int indice)
    {
        categoriaActual = (ShopCategory)Mathf.Clamp(indice, 0, 4);

        if (tabFondos != null)
        {
            for (int i = 0; i < tabFondos.Length; i++)
            {
                if (tabFondos[i] == null) continue;
                bool activa = i == indice;
                tabFondos[i].color = activa ? tabOnColor : tabOffColor;
                tabFondos[i].rectTransform.localScale = Vector3.one * (activa ? tabOnScale : 1f);
            }
        }

        ConstruirTarjetas();
    }

    private void ConstruirTarjetas()
    {
        foreach (ShopCard c in tarjetas) if (c != null) Destroy(c.gameObject);
        tarjetas.Clear();

        if (cardTemplate == null || cardContainer == null) return;

        List<ShopItem> items = ShopCatalog.DeCategoria(categoriaActual);
        foreach (ShopItem item in items)
        {
            ShopCard tarjeta = Instantiate(cardTemplate, cardContainer);
            tarjeta.gameObject.SetActive(true);
            tarjeta.name = "Card_" + item.id;
            int nivel = NivelDe(item.id);
            tarjeta.Configurar(this, item, nivel, PuedePagar(item.Precio(nivel)));
            tarjetas.Add(tarjeta);
        }
    }

    private int NivelDe(string id)
    {
        return niveles.TryGetValue(id, out int n) ? n : 0;
    }

    private bool PuedePagar(int precio) => moneySystem != null && moneySystem.Money >= precio;

    // ------------------------------------------------------------------
    // Comprar
    // ------------------------------------------------------------------

    public void Comprar(string id)
    {
        ShopItem item = null;
        foreach (ShopItem i in ShopCatalog.Items) if (i.id == id) { item = i; break; }
        if (item == null) return;

        int nivel = NivelDe(id);

        if (!item.SinLimite && nivel >= item.maxLevel)
        {
            Avisar("Ya lo tienes al maximo.");
            return;
        }

        // Comprobaciones ANTES de cobrar.
        if (item.efecto == ShopEffect.Municion)
        {
            WeaponAmmo arma = ArmaActiva();
            if (arma == null) { Avisar("No tienes arma equipada."); return; }
            if (arma.Reserve >= arma.MaxReserve) { Avisar("Ya llevas la municion al maximo."); return; }
        }
        if (item.efecto == ShopEffect.Botiquin && playerHealth != null && playerHealth.CurrentHealth >= playerHealth.MaxHealth)
        {
            Avisar("Ya tienes la vida al maximo.");
            return;
        }

        int precio = item.Precio(nivel);
        if (moneySystem == null || !moneySystem.TrySpend(precio))
        {
            Avisar("No te llega el dinero (cuesta $" + precio + ").");
            return;
        }

        gastado += precio;
        niveles[id] = nivel + 1;
        AplicarEfecto(item);
        Avisar(item.nombre + " comprado por $" + precio);

        RefrescarDinero();
        RefrescarEstado();
        ConstruirTarjetas();
    }

    private void AplicarEfecto(ShopItem item)
    {
        switch (item.efecto)
        {
            case ShopEffect.Municion:
                WeaponAmmo arma = ArmaActiva();
                if (arma != null) arma.AddReserve(Mathf.RoundToInt(item.valorEfecto));
                break;

            case ShopEffect.VidaMaxima:
                if (playerHealth != null) playerHealth.IncreaseMaxHealth(item.valorEfecto);
                break;

            case ShopEffect.Botiquin:
                if (playerHealth != null) playerHealth.Heal(item.valorEfecto);
                break;

            case ShopEffect.Velocidad:
                if (playerController != null) playerController.MejorarVelocidad(item.valorEfecto);
                break;

            case ShopEffect.Salto:
                if (playerController != null) playerController.MejorarSalto(item.valorEfecto);
                break;

            case ShopEffect.Dano:
                foreach (Shot armaDano in FindObjectsByType<Shot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    armaDano.MejorarDano(item.valorEfecto);
                break;

            case ShopEffect.RecargaRapida:
                foreach (WeaponAmmo w in FindObjectsByType<WeaponAmmo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    w.MejorarRecarga(item.valorEfecto);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Textos
    // ------------------------------------------------------------------

    public void Avisar(string texto)
    {
        if (infoText == null) return;
        infoText.text = texto;
        infoHideTime = Time.time + 3f;
    }

    private void RefrescarDinero()
    {
        if (moneyText == null || moneySystem == null) return;

        string cifra = "$" + moneySystem.Money.ToString("N0");
        if (moneyLabelText != null) moneyText.text = cifra;
        else moneyText.text = "DINERO  " + cifra;
    }

    private void RefrescarEstado()
    {
        if (statusValues == null) return;

        WeaponAmmo arma = ArmaActiva();
        string[] valores = new string[]
        {
            playerHealth != null ? Mathf.RoundToInt(playerHealth.MaxHealth).ToString() : "-",
            playerController != null ? playerController.VelocidadActual.ToString("F1") + " m/s" : "-",
            playerController != null ? playerController.AlturaSaltoActual.ToString("F1") + " m" : "-",
            "-",
            arma != null ? arma.Reserve.ToString() : "-",
            "$" + gastado.ToString("N0")
        };

        for (int i = 0; i < statusValues.Length && i < valores.Length; i++)
            if (statusValues[i] != null) statusValues[i].text = valores[i];
    }

    // ------------------------------------------------------------------

    private void CachePlayer()
    {
        if (playerController == null) playerController = FindFirstObjectByType<FirstPersonController>();

        armas.Clear();
        foreach (WeaponAmmo w in FindObjectsByType<WeaponAmmo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            armas.Add(w);
    }

    private WeaponAmmo ArmaActiva()
    {
        if (armas.Count == 0) CachePlayer();
        foreach (WeaponAmmo w in armas) if (w != null && w.gameObject.activeInHierarchy) return w;
        return null;
    }
}
