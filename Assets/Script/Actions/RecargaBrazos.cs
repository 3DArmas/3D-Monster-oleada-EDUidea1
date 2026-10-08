using UnityEngine;
using UnityEngine.InputSystem;

public class RecargaBrazos : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Parte final del nombre del clip de recarga.")]
    [SerializeField] private string nombreClipRecarga = "RELOAD1";
    [Tooltip("Se usa solo si no se encuentra el clip.")]
    [SerializeField] private float duracionRecarga = 2.2f;

    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private InputAction reloadAction;
    private float recargaTerminaEn = -1f;

    public bool Recargando => Time.time < recargaTerminaEn;

    private void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        var playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
            reloadAction = playerInput.actions.FindAction("Reload", false);

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name.EndsWith(nombreClipRecarga)) { duracionRecarga = clip.length; break; }
        }
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        bool pulsada = reloadAction != null
            ? reloadAction.WasPressedThisFrame()
            : (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);

        if (pulsada) Recargar();
    }

    public void Recargar()
    {
        if (animator == null || Recargando) return;
        recargaTerminaEn = Time.time + duracionRecarga;
        animator.SetTrigger(ReloadHash);
    }

    private void OnDisable()
    {
        recargaTerminaEn = -1f;
    }
}