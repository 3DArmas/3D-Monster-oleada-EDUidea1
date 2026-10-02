using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tarjeta de un articulo de la tienda (una polaroid en el tablon de corcho). La crea ShopUI
/// desde la plantilla y gestiona su propio clic (comprar) y el resaltado al pasar la mano.
///
/// Las referencias del bloque "Apocalypse" son opcionales: si faltan, la tarjeta funciona igual
/// que la version anterior (solo textos y fondo).
/// </summary>
public class ShopCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image fondo;
    [SerializeField] private Image marcaCandado;
    [SerializeField] private Text nombre;
    [SerializeField] private Text descripcion;
    [SerializeField] private Text precio;
    [SerializeField] private Text nivel;

    [Header("Apocalypse (opcional)")]
    [SerializeField] private ApocalypseIconLibrary iconLibrary;
    [Tooltip("Silueta blanca del articulo, dentro de la foto.")]
    [SerializeField] private Image icono;
    [Tooltip("Cinta roja PROXIMAMENTE que tapa los articulos bloqueados.")]
    [SerializeField] private GameObject cintaBloqueado;
    [Tooltip("Sello que aparece cuando el articulo ya esta al maximo.")]
    [SerializeField] private GameObject sello;
    [SerializeField] private float inclinacionMaxima = 1.6f;
    [SerializeField] private float escalaHover = 1.05f;

    private ShopUI dueno;
    private string idArticulo;
    private bool comprado;
    private bool bloqueado;

    private static readonly Color ColorNormal = new Color(1f, 1f, 1f, 1f);
    private static readonly Color ColorHover = new Color(1f, 0.93f, 0.8f, 1f);
    private static readonly Color ColorComprado = new Color(0.82f, 0.92f, 0.82f, 1f);
    private static readonly Color ColorBloqueado = new Color(0.72f, 0.7f, 0.66f, 1f);

    // Precio escrito a rotulador sobre papel claro: verde si llega el dinero, rojo si no.
    private static readonly Color PrecioAlcanzable = new Color(0.1f, 0.42f, 0.14f, 1f);
    private static readonly Color PrecioCaro = new Color(0.72f, 0.1f, 0.08f, 1f);
    private static readonly Color PrecioBloqueado = new Color(0.38f, 0.35f, 0.32f, 1f);

    public string IdArticulo => idArticulo;

    public void Configurar(ShopUI dueno, ShopItem item, int nivelActual, bool alcanzable)
    {
        this.dueno = dueno;
        idArticulo = item.id;
        bloqueado = item.bloqueado;
        comprado = !item.SinLimite && nivelActual >= item.maxLevel;

        if (nombre != null) nombre.text = item.nombre;
        if (descripcion != null) descripcion.text = item.descripcion;

        string textoPrecio;
        if (bloqueado) textoPrecio = "$" + item.Precio(nivelActual).ToString("N0");
        else if (comprado) textoPrecio = string.Empty; // el sello ya lo dice
        else textoPrecio = "$" + item.Precio(nivelActual).ToString("N0");

        if (precio != null)
        {
            // Sin sello (escena antigua) mantenemos el texto COMPRADO de siempre.
            precio.text = comprado && sello == null ? "COMPRADO" : textoPrecio;
            precio.color = bloqueado ? PrecioBloqueado : (comprado || alcanzable ? PrecioAlcanzable : PrecioCaro);
        }

        if (nivel != null)
        {
            if (bloqueado) nivel.text = "PRÓXIMAMENTE";
            else if (item.SinLimite) nivel.text = nivelActual > 0 ? "COMPRADO " + nivelActual + " VECES" : "";
            else nivel.text = "NIVEL " + nivelActual + " / " + item.maxLevel;
        }

        if (marcaCandado != null)
        {
            marcaCandado.enabled = bloqueado;
            if (bloqueado && iconLibrary != null && marcaCandado.sprite == null)
                marcaCandado.sprite = iconLibrary.GetHudIcon("lock");
        }

        ConfigurarIcono(item);

        if (cintaBloqueado != null) cintaBloqueado.SetActive(bloqueado);
        if (sello != null) sello.SetActive(comprado && !bloqueado);
        if (nivel != null && cintaBloqueado != null) nivel.gameObject.SetActive(!bloqueado);

        AplicarColorBase();
        Inclinar(item.id);
    }

    private void ConfigurarIcono(ShopItem item)
    {
        if (icono == null) return;

        Sprite sprite = iconLibrary != null ? iconLibrary.GetItemIcon(item.id) : null;
        icono.sprite = sprite;
        icono.enabled = sprite != null;
        icono.color = bloqueado ? new Color(1f, 1f, 1f, 0.32f) : Color.white;
    }

    private void AplicarColorBase()
    {
        if (fondo == null) return;
        fondo.color = bloqueado ? ColorBloqueado : (comprado ? ColorComprado : ColorNormal);
    }

    /// <summary>Cada polaroid cuelga un poco torcida; el giro sale del id para que sea estable.</summary>
    private void Inclinar(string id)
    {
        if (inclinacionMaxima <= 0f) return;

        int hash = 17;
        foreach (char c in id) hash = hash * 31 + c;
        float t = ((hash & 0xFFFF) / 65535f) * 2f - 1f;
        transform.localRotation = Quaternion.Euler(0f, 0f, t * inclinacionMaxima);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (bloqueado) return;
        if (fondo != null) fondo.color = ColorHover;
        transform.localScale = Vector3.one * escalaHover;
        if (dueno != null) dueno.Avisar(">> " + (nombre != null ? nombre.text : ""));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (bloqueado) return;
        AplicarColorBase();
        transform.localScale = Vector3.one;
        if (dueno != null) dueno.Avisar("");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (bloqueado)
        {
            if (dueno != null) dueno.Avisar("Eso todavía no está disponible.");
            return;
        }
        if (dueno != null) dueno.Comprar(idArticulo);
    }
}
