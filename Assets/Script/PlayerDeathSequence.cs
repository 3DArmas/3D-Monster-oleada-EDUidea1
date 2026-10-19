using System.Collections;
using UnityEngine;

/// <summary>
/// SECUENCIA DE MUERTE DEL JUGADOR.
///
/// Cuando la vida llega a 0 no se corta el juego de golpe: la cámara cae al suelo, el
/// jugador se queda allí y suena un último respiro. Solo DESPUÉS sale el cartel de
/// GAME OVER (eso lo temporiza GameManager).
///
/// La caída es una animación por código de la cámara (no hay rig de cuerpo en primera
/// persona): baja acelerando como por gravedad, gira hasta quedar de lado y da un bote
/// pequeño al tocar el suelo. Mientras cae se ocultan las manos y el arma, porque si no
/// el arma se queda flotando delante de la cara.
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathSequence : MonoBehaviour
{
    [Header("Referencias (se buscan solas si se dejan vacías)")]
    [Tooltip("Cámara del jugador.")]
    [SerializeField] private Transform camara;
    [Tooltip("Manos + armas del viewmodel. Se ocultan al morir.")]
    [SerializeField] private GameObject vistaDeArmas;
    [Tooltip("Sonido del último respiro.")]
    [SerializeField] private AudioClip respiroFinal;
    [SerializeField] private AudioSource fuenteDeVoz;
    [Tooltip("Balanceo de armas. Escribe la posición de la cámara cada frame: hay que " +
             "apagarlo o deshace la caída al suelo (lo hace él solo si se deja vacío).")]
    [SerializeField] private MonoBehaviour balanceoDeArmas;

    [Header("Caída")]
    [Tooltip("Segundos que tarda en llegar al suelo.")]
    [SerializeField] private float duracionDeLaCaida = 0.95f;
    [Tooltip("Altura a la que se queda la cámara tumbada (metros).")]
    [SerializeField] private float alturaEnElSuelo = 0.28f;
    [Tooltip("Cuánto avanza el cuerpo al caer (metros).")]
    [SerializeField] private float avanceAlCaer = 0.35f;
    [Tooltip("Grados que gira la cabeza al quedar de lado.")]
    [SerializeField] private float giroAlCaer = 78f;
    [Tooltip("Inclinación final de la mirada (negativo = mira algo hacia arriba).")]
    [SerializeField] private float inclinacionFinal = -12f;
    [Tooltip("Rebote del golpe contra el suelo (metros).")]
    [SerializeField] private float bote = 0.05f;

    [Header("Respiro y final")]
    [Tooltip("Cuánto tarda en sonar el respiro desde que empieza a caer.")]
    [SerializeField] private float retrasoDelRespiro = 0.55f;
    [Range(0f, 1f)][SerializeField] private float volumenDelRespiro = 0.9f;
    [Tooltip("Temblor de la cámara mientras agoniza (grados).")]
    [SerializeField] private float temblorFinal = 0.6f;

    private PlayerHealth vida;
    private bool secuenciaEmpezada;
    private Quaternion rotacionTumbado;

    private void Awake()
    {
        vida = GetComponent<PlayerHealth>();

        if (camara == null)
        {
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam == null) cam = Camera.main;
            if (cam != null) camara = cam.transform;
        }

        if (vistaDeArmas == null && camara != null)
        {
            Transform manos = camara.Find("AK74U");
            if (manos != null) vistaDeArmas = manos.gameObject;
        }

        if (fuenteDeVoz == null) fuenteDeVoz = GetComponent<AudioSource>();
        if (fuenteDeVoz == null) fuenteDeVoz = gameObject.AddComponent<AudioSource>();
        fuenteDeVoz.playOnAwake = false;
        fuenteDeVoz.spatialBlend = 0f;          // es el jugador: suena "dentro de la cabeza"

        if (balanceoDeArmas == null)
        {
            WeaponFeel balanceo = UnityEngine.Object.FindFirstObjectByType<WeaponFeel>();
            if (balanceo != null) balanceoDeArmas = balanceo;
        }
    }

    private void OnEnable()
    {
        if (vida != null) vida.OnDied += AlMorir;
    }

    private void OnDisable()
    {
        if (vida != null) vida.OnDied -= AlMorir;
    }

    private void AlMorir()
    {
        if (secuenciaEmpezada) return;
        secuenciaEmpezada = true;
        StartCoroutine(CaerAlSuelo());
    }

    private IEnumerator CaerAlSuelo()
    {
        // Red de seguridad: en Awake la cámara principal puede no estar lista todavía.
        if (camara == null)
        {
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam == null) cam = Camera.main;
            if (cam != null) camara = cam.transform;
        }
        if (vistaDeArmas == null && camara != null)
        {
            Transform manos = camara.Find("AK74U");
            if (manos != null) vistaDeArmas = manos.gameObject;
        }

        // El balanceo de armas escribe camara.localPosition en cada Update y deshacía la
        // caída (la cámara se quedaba de pie, solo girada). Se apagan TODOS: hubo un caso
        // con el componente duplicado en WeaponHolder y solo apagar uno no bastaba.
        if (balanceoDeArmas != null) balanceoDeArmas.enabled = false;
        foreach (WeaponFeel balanceo in FindObjectsByType<WeaponFeel>(FindObjectsSortMode.None))
            balanceo.enabled = false;

        // Las manos y el arma desaparecen: nadie sostiene nada cuando estás en el suelo.
        if (vistaDeArmas != null) vistaDeArmas.SetActive(false);

        if (camara == null) yield break;

        Vector3 posicionInicial = camara.position;
        Quaternion rotacionInicial = camara.rotation;

        // Hacia dónde cae: siempre hacia delante, nunca hacia arriba.
        Vector3 adelante = Vector3.ProjectOnPlane(camara.forward, Vector3.up);
        if (adelante.sqrMagnitude < 0.01f) adelante = transform.forward;

        Vector3 posicionFinal = posicionInicial
            + Vector3.down * Mathf.Max(0f, posicionInicial.y - alturaEnElSuelo)
            + adelante.normalized * avanceAlCaer;

        // Queda tumbado de lado: mantiene el rumbo (yaw) pero se gira sobre su eje.
        rotacionTumbado = Quaternion.Euler(inclinacionFinal, camara.eulerAngles.y, giroAlCaer);

        float t = 0f;
        bool respiroSonado = false;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, duracionDeLaCaida);
            float u = Mathf.Clamp01(t);

            // Cae acelerando (u al cuadrado se siente como gravedad, no como ascensor).
            float caida = u * u;
            Vector3 p = Vector3.LerpUnclamped(posicionInicial, posicionFinal, caida);
            p.y += Mathf.Sin(u * Mathf.PI * 2.2f) * bote * (1f - u);   // bote del golpe

            camara.position = p;
            camara.rotation = Quaternion.Slerp(rotacionInicial, rotacionTumbado, Mathf.SmoothStep(0f, 1f, u));

            if (!respiroSonado && u * duracionDeLaCaida >= retrasoDelRespiro)
            {
                SonarRespiro();
                respiroSonado = true;
            }

            yield return null;
        }

        camara.position = posicionFinal;
        camara.rotation = rotacionTumbado;

        if (!respiroSonado) SonarRespiro();

        // Último temblor: la cámara se apaga poco a poco hasta quedarse quieta.
        float duracionTemblor = 1.6f;
        float t2 = 0f;
        while (t2 < 1f)
        {
            t2 += Time.deltaTime / duracionTemblor;
            float fuerza = Mathf.Lerp(temblorFinal, 0f, Mathf.Clamp01(t2));
            camara.rotation = rotacionTumbado * Quaternion.Euler(
                Random.Range(-fuerza, fuerza),
                0f,
                Random.Range(-fuerza, fuerza));
            yield return null;
        }

        camara.rotation = rotacionTumbado;
    }

    private void SonarRespiro()
    {
        if (respiroFinal == null || fuenteDeVoz == null) return;
        fuenteDeVoz.PlayOneShot(respiroFinal, volumenDelRespiro);
    }
}
