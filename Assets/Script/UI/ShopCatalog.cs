using System.Collections.Generic;
using UnityEngine;

/// <summary>Categoria de la pestana en la que aparece el articulo.</summary>
public enum ShopCategory { Armas, Municion, Movilidad, Combate, Objetos }

/// <summary>Que hace el articulo al comprarlo.</summary>
public enum ShopEffect
{
    Ninguno,        // bloqueado / proximamente
    Municion,       // + reserva al arma activa
    VidaMaxima,     // + vida maxima
    Botiquin,       // cura al instante
    Velocidad,      // multiplica la velocidad de movimiento
    Salto,          // multiplica la altura de salto
    Dano,           // + dano de las armas
    RecargaRapida,  // recarga mas rapido
    DesbloquearArma // desbloquea en el WeaponSwitcher el arma cuyo id coincide
}

/// <summary>
/// Definicion de un articulo de la tienda. Los precios suben con el nivel:
/// precio = basePrice + priceStep * nivelActual.
/// maxLevel = 0 significa sin limite (se puede comprar siempre).
/// </summary>
[System.Serializable]
public class ShopItem
{
    public string id;
    public string nombre;
    public string descripcion;
    public ShopCategory categoria;
    public ShopEffect efecto;
    public int basePrice;
    public int priceStep;
    public int maxLevel;
    public float valorEfecto;
    public bool bloqueado;

    public ShopItem(string id, string nombre, string descripcion, ShopCategory categoria, ShopEffect efecto,
                    int basePrice, int priceStep, int maxLevel, float valorEfecto, bool bloqueado = false)
    {
        this.id = id; this.nombre = nombre; this.descripcion = descripcion; this.categoria = categoria;
        this.efecto = efecto; this.basePrice = basePrice; this.priceStep = priceStep;
        this.maxLevel = maxLevel; this.valorEfecto = valorEfecto; this.bloqueado = bloqueado;
    }

    public int Precio(int nivel) => basePrice + priceStep * nivel;

    public bool SinLimite => maxLevel <= 0;
}

/// <summary>
/// Catalogo de la tienda. La tienda SOLO vende lo que existe de verdad:
/// lo que aun no esta programado aparece con candado y la etiqueta PROXIMAMENTE.
/// </summary>
public static class ShopCatalog
{
    public static readonly List<ShopItem> Items = new List<ShopItem>
    {
        // ---------------- MOVILIDAD (lo que pide el jugador) ----------------
        new ShopItem("salto", "SALTO MÁS ALTO", "+12 % de altura de salto.", ShopCategory.Movilidad, ShopEffect.Salto, 400, 300, 3, 1.12f),
        new ShopItem("velocidad", "+ VELOCIDAD", "+8 % de velocidad al moverte y correr.", ShopCategory.Movilidad, ShopEffect.Velocidad, 600, 300, 3, 1.08f),
        new ShopItem("doble_salto", "DOBLE SALTO", "Un segundo salto en el aire.", ShopCategory.Movilidad, ShopEffect.Ninguno, 900, 0, 1, 0f, true),
        new ShopItem("dash", "DASH", "Impulso corto hacia donde miras.", ShopCategory.Movilidad, ShopEffect.Ninguno, 800, 0, 1, 0f, true),
        new ShopItem("aguante", "+ AGUANTE", "Más tiempo corriendo sin cansarte.", ShopCategory.Movilidad, ShopEffect.Ninguno, 500, 0, 1, 0f, true),
        new ShopItem("slide", "SLIDE MEJORADO", "Deslizarte más lejos y más rápido.", ShopCategory.Movilidad, ShopEffect.Ninguno, 500, 0, 1, 0f, true),
        new ShopItem("wallrun", "WALL RUN", "Correr por las paredes.", ShopCategory.Movilidad, ShopEffect.Ninguno, 1200, 0, 1, 0f, true),
        new ShopItem("mantle", "MANTLE", "Subirte a las plataformas de un salto.", ShopCategory.Movilidad, ShopEffect.Ninguno, 600, 0, 1, 0f, true),

        // ---------------- MUNICION ----------------
        new ShopItem("municion", "MUNICIÓN (+12)", "Rellenas la reserva del arma que llevas.", ShopCategory.Municion, ShopEffect.Municion, 50, 0, 0, 12f),
        new ShopItem("municion_grande", "CAJA DE MUNICIÓN (+48)", "Reserva completa de golpe.", ShopCategory.Municion, ShopEffect.Municion, 180, 0, 0, 48f),

        // ---------------- COMBATE ----------------
        new ShopItem("dano", "+10 % DAÑO", "Tus balas hacen más daño.", ShopCategory.Combate, ShopEffect.Dano, 700, 400, 3, 1.10f),
        new ShopItem("recarga", "RECARGA RÁPIDA", "Recargas un 25 % más rápido.", ShopCategory.Combate, ShopEffect.RecargaRapida, 650, 0, 1, 0.75f),
        new ShopItem("cargador", "CARGADOR AMPLIADO", "Más balas por cargador.", ShopCategory.Combate, ShopEffect.Ninguno, 800, 0, 1, 0f, true),
        // Las armas se desbloquean en el WeaponSwitcher: el id del articulo tiene que
        // coincidir con el id del slot (c1911, mk18, escopeta, subfusil, rifle).
        new ShopItem("c1911", "C1911", "Pistola .45 ligera y precisa.", ShopCategory.Armas, ShopEffect.DesbloquearArma, 700, 0, 1, 0f),
        new ShopItem("mk18", "MK18", "Rifle de asalto rapido y equilibrado.", ShopCategory.Armas, ShopEffect.DesbloquearArma, 1000, 0, 1, 0f),
        new ShopItem("escopeta", "ESCOPETA 590A1", "Devastadora de cerca, bombeo lento.", ShopCategory.Armas, ShopEffect.DesbloquearArma, 1500, 0, 1, 0f),
        new ShopItem("subfusil", "SUBFUSIL SMG5", "Cadencia altisima y cargador grande.", ShopCategory.Armas, ShopEffect.DesbloquearArma, 1800, 0, 1, 0f),
        new ShopItem("rifle", "R90", "Compacta de calibre 5.7, mucho dano por disparo.", ShopCategory.Armas, ShopEffect.DesbloquearArma, 2500, 0, 1, 0f),

        // ---------------- OBJETOS ----------------
        new ShopItem("botiquin", "BOTIQUÍN", "Cura 50 de vida al instante.", ShopCategory.Objetos, ShopEffect.Botiquin, 250, 0, 0, 50f),
        new ShopItem("vida", "VIDA MÁXIMA (+25)", "Sube tu vida máxima para siempre.", ShopCategory.Objetos, ShopEffect.VidaMaxima, 500, 250, 3, 25f),
        new ShopItem("granada", "GRANADA", "Explosivo para lanzar con G.", ShopCategory.Objetos, ShopEffect.Ninguno, 300, 0, 1, 0f, true),
        new ShopItem("mina", "MINA EXPLOSIVA", "Se coloca en el suelo.", ShopCategory.Objetos, ShopEffect.Ninguno, 400, 0, 1, 0f, true)
    };

    public static string NombreCategoria(ShopCategory c)
    {
        switch (c)
        {
            case ShopCategory.Armas: return "ARMAS";
            case ShopCategory.Municion: return "MUNICIÓN";
            case ShopCategory.Movilidad: return "MOVILIDAD";
            case ShopCategory.Combate: return "COMBATE";
            default: return "OBJETOS";
        }
    }

    public static string SubtituloCategoria(ShopCategory c)
    {
        switch (c)
        {
            case ShopCategory.Armas: return "pistolas · rifles · escopeta";
            case ShopCategory.Municion: return "por arma";
            case ShopCategory.Movilidad: return "saltos · velocidad · dash";
            case ShopCategory.Combate: return "daño · recarga · cargador";
            default: return "botiquín · granadas · minas";
        }
    }

    public static List<ShopItem> DeCategoria(ShopCategory c)
    {
        var lista = new List<ShopItem>();
        foreach (ShopItem i in Items) if (i.categoria == c) lista.Add(i);
        return lista;
    }
}
