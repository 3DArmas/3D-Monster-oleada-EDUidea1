using UnityEngine;

/// <summary>
/// Pega los pies del zombie al suelo de verdad.
///
/// El NavMeshAgent coloca el objeto a la altura del NavMesh, pero la animacion no
/// siempre deja los pies apoyados (y el suelo puede estar en pendiente o en una
/// plataforma). Este script lanza rayos hacia abajo desde los dos pies, mira donde
/// esta el suelo (ignorando a los propios zombies) y desplaza el modelo lo justo
/// para que el pie que apoya quede sobre la superficie.
///
/// Ademas deja publico EnSuelo y AlturaSobreSuelo, para que cualquier otro sistema
/// pueda validar si el zombie esta tocando el suelo.
/// </summary>
[DefaultExecutionOrder(200)]
public class ZombieGroundSnap : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto del modelo. Si se deja vacio se usa el primer hijo.")]
    [SerializeField] private Transform modelo;

    [Header("Ajustes")]
    [Tooltip("Distancia del tobillo a la planta del pie.")]
    [SerializeField] private float plantaDelPie = 0.10f;
    [Tooltip("Cada cuantos segundos se recalcula.")]
    [SerializeField] private float intervalo = 0.08f;
    [Tooltip("Cuanto se aplica de la correccion en cada paso (1 = instantaneo).")]
    [Range(0.05f, 1f)][SerializeField] private float suavizado = 0.4f;
    [Tooltip("Limite de la posicion local del modelo, para no dispararse.")]
    [SerializeField] private float limiteLocal = 1.2f;
    [Tooltip("Capas que cuentan como suelo.")]
    [SerializeField] private LayerMask capasSuelo = ~0;
    [Tooltip("Por debajo de esta diferencia se considera que esta apoyado.")]
    [SerializeField] private float margenApoyado = 0.12f;

    /// <summary>True si el pie que apoya esta practicamente en el suelo.</summary>
    public bool EnSuelo { get; private set; }
    /// <summary>Cuanto le sobra (+) o le falta (-) para apoyar.</summary>
    public float AlturaSobreSuelo { get; private set; }

    private Animator anim;
    private float siguiente;
    private float ultimoSuelo = float.NaN;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        if (modelo == null && transform.childCount > 0) modelo = transform.GetChild(0);
    }

    private void LateUpdate()
    {
        if (Time.time < siguiente) return;
        siguiente = Time.time + intervalo;
        if (modelo == null) return;

        float suelo = BuscarSuelo();
        if (!float.IsNaN(suelo)) ultimoSuelo = suelo;

        float pieApoya = PieMasBajo();
        if (float.IsNaN(pieApoya) || float.IsNaN(ultimoSuelo)) { EnSuelo = false; return; }

        // Cuanto habria que mover el modelo para que la planta toque el suelo.
        float correccion = (ultimoSuelo + plantaDelPie) - pieApoya;

        Vector3 local = modelo.localPosition;
        local.y = Mathf.Clamp(local.y + correccion * suavizado, -limiteLocal, limiteLocal);
        modelo.localPosition = local;

        AlturaSobreSuelo = (pieApoya + plantaDelPie) - ultimoSuelo;
        EnSuelo = Mathf.Abs(AlturaSobreSuelo) < margenApoyado;
    }

    /// <summary>Punto mas alto del suelo justo debajo del zombie (ignorando zombies y triggers).</summary>
    private float BuscarSuelo()
    {
        float mejor = float.NaN;
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 1.2f, Vector3.down, 4f, capasSuelo);
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.isTrigger) continue;
            if (h.collider.GetComponentInParent<ZombieController>() != null) continue;
            if (h.collider.GetComponentInParent<PlayerHealth>() != null) continue;
            if (float.IsNaN(mejor) || h.point.y > mejor) mejor = h.point.y;
        }
        return mejor;
    }

    /// <summary>Altura del tobillo mas bajo (con avatar humanoide).</summary>
    private float PieMasBajo()
    {
        if (anim != null && anim.isHuman)
        {
            float y = float.MaxValue;
            Transform izq = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform der = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (izq != null) y = Mathf.Min(y, izq.position.y);
            if (der != null) y = Mathf.Min(y, der.position.y);
            if (y < float.MaxValue) return y;
        }
        return float.NaN;
    }
}
