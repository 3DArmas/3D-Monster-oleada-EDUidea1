using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cierra los dedos para que las manos AGARREN el arma.
///
/// El modelo viene con las manos abiertas y no trae animaciones, asi que la pose se
/// hace por codigo: para cada dedo se calcula su eje de giro (perpendicular al dedo y
/// a la normal de la palma) y se reparten los grados entre sus falanges. Al ser un
/// calculo geometrico funciona igual en cualquier rig y en las dos manos, sin depender
/// de nombres de ejes ni de convenciones del exportador.
///
/// Se aplica solo (en Start) y es idempotente: guarda las rotaciones originales la
/// primera vez, asi se puede volver a aplicar sin que los dedos se cierren dos veces.
/// </summary>
public class PoseDeAgarre : MonoBehaviour
{
    [Header("Fuerza del agarre")]
    [Tooltip("Grados que se cierra cada falange. Entre 40 y 55 suele quedar bien.")]
    [Range(0f, 90f)][SerializeField] private float cierrePorFalange = 45f;
    [Tooltip("Reparto del cierre entre las 4 falanges: nudillo, media, punta, ultima.")]
    [SerializeField] private Vector4 reparto = new Vector4(1.05f, 1.25f, 1.0f, 0.55f);
    [Tooltip("Cierre del pulgar (cruza la palma, suele ser menor).")]
    [Range(0f, 90f)][SerializeField] private float cierrePulgar = 30f;
    [Tooltip("Marca esto si los dedos se ABREN en vez de cerrarse.")]
    [SerializeField] private bool invertir = false;
    [Tooltip("Aplicar automaticamente al empezar.")]
    [SerializeField] private bool aplicarAlIniciar = true;

    private static readonly string[] dedos = { "point", "middle", "ring", "pink" };

    private readonly Dictionary<Transform, Quaternion> rotacionesBase = new Dictionary<Transform, Quaternion>();
    private bool baseGuardada;

    private void Start()
    {
        if (aplicarAlIniciar) Aplicar();
    }

    /// <summary>Cierra las dos manos. Se puede llamar desde el menu del componente.</summary>
    [ContextMenu("Aplicar agarre")]
    public void Aplicar()
    {
        GuardarBase();
        foreach (string mano in new[] { "L", "R" }) AplicarMano(mano);
    }

    /// <summary>Devuelve las manos a la pose original del modelo.</summary>
    [ContextMenu("Abrir manos")]
    public void Abrir()
    {
        GuardarBase();
        foreach (var kv in rotacionesBase) if (kv.Key != null) kv.Key.rotation = kv.Value;
    }

    private void GuardarBase()
    {
        if (baseGuardada) return;
        baseGuardada = true;
        foreach (var t in GetComponentsInChildren<Transform>(true))
            rotacionesBase[t] = t.rotation;
    }

    private void AplicarMano(string mano)
    {
        // 1) Eje de giro de la mano: perpendicular al indice y al menique = normal de la palma
        Transform indice1 = Buscar(mano + "_point1");
        Transform indice2 = Buscar(mano + "_point2");
        Transform menique1 = Buscar(mano + "_pink1");
        if (indice1 == null || indice2 == null || menique1 == null) return;

        Vector3 dirIndice = (indice2.position - indice1.position).normalized;
        Vector3 dirMenique = (menique1.position - indice1.position).normalized;
        Vector3 normalPalma = Vector3.Cross(dirIndice, dirMenique).normalized;
        float signo = invertir ? -1f : 1f;

        // 2) Cada dedo gira sobre su propio eje (perpendicular al dedo y a la palma)
        foreach (string dedo in dedos)
        {
            Transform a = Buscar(mano + "_" + dedo + "1");
            Transform b = Buscar(mano + "_" + dedo + "2");
            if (a == null || b == null) continue;
            Vector3 eje = Vector3.Cross((b.position - a.position).normalized, normalPalma).normalized;
            RotarFalanges(mano + "_" + dedo, eje, cierrePorFalange * signo);
        }

        // 3) El pulgar cruza la palma: gira sobre la normal
        RotarFalanges(mano + "_thumb", normalPalma * signo, cierrePulgar);
    }

    private void RotarFalanges(string prefijo, Vector3 eje, float gradosBase)
    {
        if (eje.sqrMagnitude < 0.001f) return;
        float[] factores = { reparto.x, reparto.y, reparto.z, reparto.w };

        for (int i = 0; i < 4; i++)
        {
            Transform hueso = Buscar(prefijo + (i + 1));
            if (hueso == null) continue;

            Quaternion original = rotacionesBase.ContainsKey(hueso) ? rotacionesBase[hueso] : hueso.rotation;
            hueso.rotation = Quaternion.AngleAxis(gradosBase * factores[i], eje.normalized) * original;
        }
    }

    private Transform Buscar(string nombre)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }
}
