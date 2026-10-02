using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tarjeta de un articulo de la tienda. La crea ShopUI desde la plantilla y
/// gestiona su propio clic (comprar) y el resaltado al pasar la mano.
/// </summary>
public class ShopCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image fondo;
    [SerializeField] private Image marcaCandado;
    [SerializeField] private Text nombre;
    [SerializeField] private Text descripcion;
    [SerializeField] private Text precio;
    [SerializeField] private Text nivel;

    private ShopUI dueno;
    private string idArticulo;
    private bool comprado;
    private bool bloqueado;

    private static readonly Color ColorNormal = new Color(1f, 1f, 1f, 1f);
    private static readonly Color ColorHover = new Color(1f, 0.72f, 0.68f, 1f);
    private static readonly Color ColorComprado = new Color(0.62f, 1f, 0.68f, 1f);
    private static readonly Color ColorBloqueado = new Color(0.72f, 0.70f, 0.66f, 1f);
    private static readonly Color ColorCaro = new Color(0.98f, 0.72f, 0.55f, 1f);

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
        else if (comprado) textoPrecio = "COMPRADO";
        else textoPrecio = "$" + item.Precio(nivelActual).ToString("N0");

        if (precio != null)
        {
            precio.text = textoPrecio;
            precio.color = bloqueado ? ColorBloqueado : (comprado ? ColorComprado : (alcanzable ? ColorComprado : ColorCaro));
        }

        if (nivel != null)
        {
            if (bloqueado) nivel.text = "PRÓXIMAMENTE";
            else if (item.SinLimite) nivel.text = nivelActual > 0 ? "COMPRADO " + nivelActual + " VECES" : "";
            else nivel.text = "NIVEL " + nivelActual + " / " + item.maxLevel;
        }

        if (marcaCandado != null) marcaCandado.enabled = bloqueado;
        if (fondo != null) fondo.color = bloqueado ? new Color(0.5f, 0.5f, 0.5f, 0.55f) : ColorNormal;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (bloqueado) return;
        if (fondo != null) fondo.color = ColorHover;
        if (dueno != null) dueno.Avisar(">> " + (nombre != null ? nombre.text : ""));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (bloqueado) return;
        if (fondo != null) fondo.color = ColorNormal;
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
