using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cambio de arma de la nueva generacion.
///
/// Las armas son PREFABS que se instancian UNA sola vez bajo el ArmaSocket (el socket
/// vive en el hueso de la mano derecha de los brazos FPS), de modo que siguen a las
/// manos al mirar, caminar y disparar. Cambiar de arma solo ACTIVA y DESACTIVA: no se
/// destruye nada, asi cada arma conserva su municion y las mejoras compradas en la
/// tienda, y el HUD y la tienda no pierden sus referencias.
///
/// Un slot con 'desbloqueada' a false no se puede equipar hasta que la tienda lo
/// desbloquee por su id.
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class SlotArma
    {
        [Tooltip("Identificador que usa la tienda para desbloquearlo (ak74, escopeta...).")]
        public string id = "ak74";
        [Tooltip("Nombre que se muestra en el HUD.")]
        public string nombre = "AK74";
        [Tooltip("Prefab del arma (modelo + Shot + WeaponAmmo + fogonazo).")]
        public GameObject prefab;
        [Tooltip("Si esta a false, el arma aparece bloqueada hasta comprarla.")]
        public bool desbloqueada = true;
        [Tooltip("Icono opcional para la UI.")]
        public Sprite icono;
        [Header("Encaje en la mano")]
        public Vector3 posicionEnMano = Vector3.zero;
        public Vector3 rotacionEnMano = Vector3.zero;
        public float escalaEnMano = 0.55f;
    }

    [Header("Socket")]
    [Tooltip("Transform en la mano donde se cuelgan las armas (ArmaSocket).")]
    [SerializeField] private Transform socket;

    [Header("Armas (en orden: slot 1, slot 2, ...)")]
    [SerializeField] private SlotArma[] slots;

    [Header("UI")]
    [Tooltip("Opcional: el HUD ya dibuja la silueta del arma activa por su nombre.")]
    [SerializeField] private Image weaponIconDisplay;

    [Header("Sonido")]
    [SerializeField] private AudioClip switchSound;
    [SerializeField] private AudioSource audioSource;

    private readonly System.Collections.Generic.List<GameObject> instancias = new System.Collections.Generic.List<GameObject>();
    private int indiceActual = -1;
    private InputAction[] accionesSlot;

    /// <summary>Se dispara con (indice, arma) cada vez que se equipa una.</summary>
    public event System.Action<int, GameObject> OnArmaCambiada;

    public int IndiceActual => indiceActual;
    public GameObject ArmaActual => (indiceActual >= 0 && indiceActual < instancias.Count) ? instancias[indiceActual] : null;
    public int NumeroDeSlots => slots != null ? slots.Length : 0;
    public string IdActual => (slots != null && indiceActual >= 0 && indiceActual < slots.Length) ? slots[indiceActual].id : "";

    private void Awake()
    {
        var playerInput = GetComponentInParent<PlayerInput>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (playerInput != null && slots != null)
        {
            accionesSlot = new InputAction[slots.Length];
            for (int i = 0; i < slots.Length; i++) accionesSlot[i] = playerInput.actions["Slot" + (i + 1)];
        }

        InstanciarTodas();
    }

    private void Start()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].desbloqueada) { Equipar(i); break; }
    }

    private void Update()
    {
        if (accionesSlot == null) return;
        for (int i = 0; i < accionesSlot.Length; i++)
            if (accionesSlot[i] != null && accionesSlot[i].WasPressedThisFrame()) Equipar(i);
    }

    /// <summary>Crea todas las armas bajo el socket, desactivadas. Se hace una sola vez.</summary>
    private void InstanciarTodas()
    {
        instancias.Clear();
        if (slots == null) return;

        Transform padre = socket != null ? socket : transform;
        for (int i = 0; i < slots.Length; i++)
        {
            SlotArma s = slots[i];
            if (s == null || s.prefab == null) { instancias.Add(null); continue; }

            GameObject go = Instantiate(s.prefab, padre);
            go.name = string.IsNullOrEmpty(s.nombre) ? s.prefab.name : s.nombre;
            go.transform.localPosition = s.posicionEnMano;
            go.transform.localRotation = Quaternion.Euler(s.rotacionEnMano);
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, s.escalaEnMano);
            go.SetActive(false);
            instancias.Add(go);
        }
    }

    public bool EstaDesbloqueada(int index)
        => slots != null && index >= 0 && index < slots.Length && slots[index] != null && slots[index].desbloqueada;

    /// <summary>La tienda llama a esto al comprar un arma. Devuelve true si existia el id.</summary>
    public bool Desbloquear(string id)
    {
        if (slots == null || string.IsNullOrEmpty(id)) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (!string.Equals(slots[i].id, id, System.StringComparison.OrdinalIgnoreCase)) continue;
            slots[i].desbloqueada = true;
            return true;
        }
        return false;
    }

    public void Equipar(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return;
        if (!EstaDesbloqueada(index)) return;
        if (index == indiceActual) return;

        for (int i = 0; i < instancias.Count; i++)
            if (instancias[i] != null) instancias[i].SetActive(i == index);

        indiceActual = index;
        SlotArma s = slots[index];

        if (switchSound != null && audioSource != null) audioSource.PlayOneShot(switchSound);
        if (weaponIconDisplay != null && s != null && s.icono != null) weaponIconDisplay.sprite = s.icono;

        OnArmaCambiada?.Invoke(index, ArmaActual);
    }
}