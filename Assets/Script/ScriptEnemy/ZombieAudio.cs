using UnityEngine;

/// <summary>
/// Sonidos del zombi: gruñidos mientras ronda, ataque, daño al recibir un disparo y
/// muerte.
///
/// Sonido 3D: se oyen de donde viene cada zombi. Cada zombi tiene su propio tono y,
/// dentro de cada categoría, se elige un clip AL AZAR: en una horda de veinte zombis
/// no se oye el mismo gruñido repetido.
///
/// Los clips se generan con "Aura -> Generar audios de juego".
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ZombieAudio : MonoBehaviour
{
    [Header("Clips (opcionales: déjalos vacíos hasta tenerlos)")]
    [Tooltip("Gruñidos sueltos mientras vaga o te persigue. Se elige uno al azar.")]
    [SerializeField] private AudioClip[] grunidos;
    [Tooltip("Golpe: se reproduce al empezar cada ataque.")]
    [SerializeField] private AudioClip[] ataques;
    [Tooltip("Quejido al recibir un disparo.")]
    [SerializeField] private AudioClip[] quejidos;
    [Tooltip("Al morir.")]
    [SerializeField] private AudioClip[] muertes;

    [Header("Ajustes")]
    [Tooltip("Rango de tono aleatorio por zombi (voz distinta en cada uno).")]
    [SerializeField] private Vector2 tonoAleatorio = new Vector2(0.85f, 1.15f);
    [Range(0f, 1f)][SerializeField] private float volumen = 0.75f;
    [Tooltip("Cada cuánto suelta un gruñido de ambiente (mínimo y máximo).")]
    [SerializeField] private Vector2 cadaCuantoGrune = new Vector2(3.5f, 8f);
    [Tooltip("Cuanto más cerca está del jugador, más fuerte suena (0 = siempre igual).")]
    [Range(0f, 1f)][SerializeField] private float pesoDeLaDistancia = 0.5f;

    private AudioSource fuente;
    private ZombieBrain cerebro;
    private float siguienteGrunido;
    private float tonoBase;


    private void Awake()
    {
        fuente = GetComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;                     // 3D
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 2f;
        fuente.maxDistance = 32f;
        fuente.dopplerLevel = 0f;

        cerebro = GetComponent<ZombieBrain>();

        // Cada zombi arranca con un tono y un ritmo distintos.
        tonoBase = Random.Range(tonoAleatorio.x, tonoAleatorio.y);
        siguienteGrunido = Time.time + Random.Range(cadaCuantoGrune.x, cadaCuantoGrune.y);
    }

    private void Update()
    {
        if (cerebro == null || cerebro.EstadoActual == ZombieBrain.Estado.Muerto) return;

        if (Time.time < siguienteGrunido) return;
        siguienteGrunido = Time.time + Random.Range(cadaCuantoGrune.x, cadaCuantoGrune.y);

        // Gruñidos de ambiente salvo justo cuando está golpeando.
        if (cerebro.EstadoActual != ZombieBrain.Estado.Atacar) Reproducir(grunidos, 0.55f);
    }

    public void SonarAtaque() => Reproducir(ataques, 1f);
    public void SonarDano() => Reproducir(quejidos, 0.9f);
    public void SonarMuerte() => Reproducir(muertes, 1.15f);

    /// <summary>Elige un clip al azar del grupo y lo lanza con un tono distinto cada vez.</summary>
    private void Reproducir(AudioClip[] grupo, float factorVolumen = 1f)
    {
        if (grupo == null || grupo.Length == 0 || fuente == null) return;

        AudioClip clip = grupo.Length == 1 ? grupo[0] : grupo[Random.Range(0, grupo.Length)];
        if (clip == null) return;

        // Tono: el del zombi + una variación pequeña por sonido (no suena repetido).
        fuente.pitch = Mathf.Clamp(tonoBase * Random.Range(0.94f, 1.07f), 0.5f, 1.8f);

        float volumenFinal = volumen * factorVolumen;
        if (pesoDeLaDistancia > 0f && HordeDirector.Instancia != null && HordeDirector.Instancia.Jugador != null)
        {
            float distancia = Vector3.Distance(transform.position, HordeDirector.Instancia.Jugador.position);
            float cercania = Mathf.Clamp01(1f - distancia / 26f);
            volumenFinal *= Mathf.Lerp(1f, Mathf.Lerp(0.45f, 1f, cercania), pesoDeLaDistancia);
        }

        fuente.PlayOneShot(clip, volumenFinal);
    }
}
