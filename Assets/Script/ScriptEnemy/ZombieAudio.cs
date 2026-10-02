using UnityEngine;

/// <summary>
/// Sonidos del zombi: gruñidos mientras ronda, ataque, daño al recibir un disparo y
/// muerte. Los clips se dejan VACÍOS hasta que los tengas: en cuanto arrastres un
/// audio a cada hueco, ya suenan solos (no hay que tocar código).
///
/// Sonido 3D: se oyen de donde viene cada zombi, con tono aleatorio por zombi para
/// que la horda no suene clonada.
///
/// De dónde sacar los audios gratis: freesound.org, Pixabay (efectos), o el buscador
/// "Zombie" del Asset Store.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ZombieAudio : MonoBehaviour
{
    [Header("Clips (opcionales: déjalos vacíos hasta tenerlos)")]
    [Tooltip("Gruñido suelto mientras vaga o te persigue.")]
    [SerializeField] private AudioClip grunido;
    [Tooltip("Golpe: se reproduce al empezar cada ataque.")]
    [SerializeField] private AudioClip ataque;
    [Tooltip("Quejido al recibir un disparo.")]
    [SerializeField] private AudioClip dano;
    [Tooltip("Al morir.")]
    [SerializeField] private AudioClip muerte;

    [Header("Ajustes")]
    [Tooltip("Rango de tono aleatorio por zombi (voz distinta en cada uno).")]
    [SerializeField] private Vector2 tonoAleatorio = new Vector2(0.85f, 1.15f);
    [Range(0f, 1f)][SerializeField] private float volumen = 0.6f;
    [Tooltip("Cada cuánto suelta un gruñido de ambiente (mínimo y máximo).")]
    [SerializeField] private Vector2 cadaCuantoGrune = new Vector2(4f, 9f);

    private AudioSource fuente;
    private ZombieBrain cerebro;
    private float siguienteGrunido;

    private void Awake()
    {
        fuente = GetComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;                     // 3D
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 2f;
        fuente.maxDistance = 30f;

        cerebro = GetComponent<ZombieBrain>();

        // Cada zombi arranca con un tono y un ritmo distintos.
        fuente.pitch = Random.Range(tonoAleatorio.x, tonoAleatorio.y);
        siguienteGrunido = Time.time + Random.Range(cadaCuantoGrune.x, cadaCuantoGrune.y);
    }

    private void Update()
    {
        if (cerebro == null || cerebro.EstadoActual == ZombieBrain.Estado.Muerto) return;

        if (Time.time < siguienteGrunido) return;
        siguienteGrunido = Time.time + Random.Range(cadaCuantoGrune.x, cadaCuantoGrune.y);

        // Gruñidos de ambiente salvo justo cuando está golpeando.
        if (cerebro.EstadoActual != ZombieBrain.Estado.Atacar) Reproducir(grunido, 0.55f);
    }

    public void SonarAtaque() => Reproducir(ataque);
    public void SonarDano() => Reproducir(dano);
    public void SonarMuerte() => Reproducir(muerte);

    private void Reproducir(AudioClip clip, float factorVolumen = 1f)
    {
        if (clip == null || fuente == null) return;
        fuente.PlayOneShot(clip, volumen * factorVolumen);
    }
}
