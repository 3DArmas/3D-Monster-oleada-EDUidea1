using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Armas (en orden: slot 1, slot 2, ...)")]
    [SerializeField] private GameObject[] weapons;

    [Header("Íconos de UI (mismo orden que 'weapons')")]
    [SerializeField] private Sprite[] weaponIcons;
    [SerializeField] private Image weaponIconDisplay; // el Image del Canvas donde se muestra el arma actual

    [Header("Sonido")]
    [SerializeField] private AudioClip switchSound;
    [SerializeField] private AudioSource audioSource;

    private int currentWeaponIndex = 0;
    private InputAction[] slotActions;

    private void Awake()
    {
        PlayerInput playerInput = GetComponentInParent<PlayerInput>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Busca las acciones "Slot1", "Slot2", "Slot3"... según cuántas armas tengas
        slotActions = new InputAction[weapons.Length];
        for (int i = 0; i < weapons.Length; i++)
        {
            slotActions[i] = playerInput.actions[$"Slot{i + 1}"];
        }
    }

    private void Start()
    {
        EquipWeapon(0); // empieza con la primera arma equipada
    }

    private void Update()
    {
        for (int i = 0; i < slotActions.Length; i++)
        {
            if (slotActions[i] != null && slotActions[i].WasPressedThisFrame())
            {
                EquipWeapon(i);
            }
        }
    }

    private void EquipWeapon(int index)
    {
        if (index == currentWeaponIndex && weapons[index].activeSelf) return;

        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i].SetActive(i == index);
        }

        currentWeaponIndex = index;

        // Sonido de cambio de arma
        if (switchSound != null && audioSource != null)
            audioSource.PlayOneShot(switchSound);

        // Actualizar el ícono en la UI
        if (weaponIconDisplay != null && weaponIcons != null && index < weaponIcons.Length && weaponIcons[index] != null)
        {
            weaponIconDisplay.sprite = weaponIcons[index];
        }
    }
}