using UnityEngine;

/// <summary>
/// Salpicaduras de sangre al recibir un disparo, EN EL PUNTO DEL IMPACTO.
///
/// Detalles importantes de rendimiento: hay UN UNICO sistema de particulas compartido
/// por todos los zombis y se emite con Emit (no se instancia ningun objeto por
/// impacto, asi que no genera basura para el recolector). La textura de la gota se
/// genera por codigo, de modo que no hace falta ningun asset de arte.
/// </summary>
public class SangreDeZombi : MonoBehaviour
{
    private static SangreDeZombi instancia;

    [Header("Ajustes")]
    [Tooltip("Cuantas gotas salen en cada impacto.")]
    [SerializeField] private int gotasPorImpacto = 9;
    [Tooltip("Velocidad inicial de las gotas (m/s).")]
    [SerializeField] private float velocidad = 3.2f;
    [Tooltip("Cuanto se abre el chorro respecto a la direccion del disparo.")]
    [Range(0f, 1.5f)][SerializeField] private float dispersion = 0.55f;
    [SerializeField] private float tamano = 0.055f;
    [Tooltip("Cuanto dura cada gota (segundos).")]
    [SerializeField] private float duracion = 0.7f;
    [SerializeField] private Color colorSangre = new Color(0.45f, 0.03f, 0.03f, 1f);

    private ParticleSystem sistema;

    private void Awake()
    {
        instancia = this;
        CrearSistema();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    /// <summary>Salpica sangre en un punto, alejandose en la direccion de la normal.</summary>
    public static void Salpicar(Vector3 punto, Vector3 normal)
    {
        // La referencia estatica se puede perder (recarga de dominio, cambio de escena):
        // se vuelve a buscar sola, como el director de la horda. Si no, no salpicaria.
        if (instancia == null) instancia = FindFirstObjectByType<SangreDeZombi>(FindObjectsInactive.Include);
        if (instancia == null || instancia.sistema == null) return;
        instancia.Emitir(punto, normal);
    }

    private void Emitir(Vector3 punto, Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;

        // Emitir sobre un sistema PARADO no hace nada: si se ha detenido (por ejemplo tras
        // una recarga de dominio) se vuelve a arrancar antes de emitir.
        if (!sistema.isPlaying) sistema.Play();

        var em = new ParticleSystem.EmitParams();
        for (int i = 0; i < gotasPorImpacto; i++)
        {
            Vector3 direccion = (normal.normalized + Random.insideUnitSphere * dispersion).normalized;
            em.position = punto + direccion * 0.04f;
            em.velocity = direccion * velocidad * Random.Range(0.5f, 1.4f);
            em.startSize = tamano * Random.Range(0.7f, 1.6f);
            em.startLifetime = duracion * Random.Range(0.7f, 1.2f);
            em.startColor = colorSangre;
            em.applyShapeToPosition = false;
            sistema.Emit(em, 1);
        }
    }

    private void CrearSistema()
    {
        var go = new GameObject("Sangre");
        go.transform.SetParent(transform, false);

        sistema = go.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = sistema.main;
        main.playOnAwake = false;
        main.loop = true;                     // el sistema vive siempre; se emite a mano
        main.startLifetime = duracion;
        main.startSpeed = 0f;                 // la velocidad la pone cada gota con Emit
        main.startSize = tamano;
        main.gravityModifier = 1.1f;          // caen hacia el suelo
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;

        var emision = sistema.emission;
        emision.enabled = false;              // solo se emite a mano al impactar

        var forma = sistema.shape;
        forma.enabled = false;

        // Se desvanecen y encogen mientras caen
        var colorVida = sistema.colorOverLifetime;
        colorVida.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorVida.color = new ParticleSystem.MinMaxGradient(gradiente);

        var tamanoVida = sistema.sizeOverLifetime;
        tamanoVida.enabled = true;
        tamanoVida.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.35f));

        // Material: en URP el de particulas Unlit; si no, los de reserva
        var render = sistema.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        render.material = new Material(shader);
        render.material.mainTexture = TexturaGota();
        render.renderMode = ParticleSystemRenderMode.Billboard;

        sistema.Play();
    }

    /// <summary>Textura de gota redonda y suave, generada por codigo (sin assets).</summary>
    private static Texture2D TexturaGota()
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                       // borde suave
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
