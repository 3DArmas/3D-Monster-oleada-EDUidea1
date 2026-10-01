using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interfaz de la tienda entre rondas: municion, mejora de vida y botiquin.
/// El boton LISTO cierra la tienda y arranca la siguiente ronda.
/// Mientras esta abierta se libera el raton y se desactiva el control del jugador.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text infoText;
    [SerializeField] private Text titleText;

    [Header("Sistemas")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MoneySystem moneySystem;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Precios")]
    [SerializeField] private int ammoPrice = 50;
    [SerializeField] private int ammoAmount = 12;
    [SerializeField] private int healthUpgradePrice = 500;
    [SerializeField] private int healthUpgradeAmount = 25;
    [SerializeField] private int medkitPrice = 250;
    [SerializeField] private int medkitHeal = 50;

    /// <summary>True mientras la tienda esta abierta (lo consulta Shot para no disparar).</summary>
    public static bool IsAnyOpen { get; private set; }

    public bool IsOpen => panel != null && panel.activeSelf;

    private readonly List<WeaponAmmo> weapons = new List<WeaponAmmo>();
    private FirstPersonController playerController;
    private float infoHideTime;

    private void Awake()
    {
        IsAnyOpen = false;
    }

    private void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (infoText != null) infoText.text = string.Empty;
    }

    private void Update()
    {
        if (infoText != null && infoText.text.Length > 0 && Time.time >= infoHideTime)
            infoText.text = string.Empty;
    }

    public void Open()
    {
        if (IsOpen) return;

        CachePlayer();
        if (panel != null) panel.SetActive(true);
        IsAnyOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null) playerController.enabled = false;

        if (titleText != null) titleText.text = "TIENDA";
        ShowInfo("Compra lo que necesites y pulsa LISTO para seguir.", 4f);
        RefreshMoney();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        IsAnyOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerController != null) playerController.enabled = true;
    }

    // ------------------------------------------------------------------
    // Botones de compra
    // ------------------------------------------------------------------

    public void BuyAmmo()
    {
        Buy(ammoPrice, () =>
        {
            WeaponAmmo weapon = GetActiveWeapon();
            if (weapon == null) { ShowInfo("No tienes arma equipada.", 2f); return; }
            int added = weapon.AddReserve(ammoAmount);
            ShowInfo(added > 0 ? "Municion: +" + added : "Ya llevas la municion al maximo.", 2f);
        });
    }

    public void BuyHealthUpgrade()
    {
        Buy(healthUpgradePrice, () =>
        {
            if (playerHealth != null) playerHealth.IncreaseMaxHealth(healthUpgradeAmount);
            ShowInfo("Vida maxima +" + healthUpgradeAmount, 2f);
        });
    }

    public void BuyMedkit()
    {
        Buy(medkitPrice, () =>
        {
            if (playerHealth != null) playerHealth.Heal(medkitHeal);
            ShowInfo("Curado " + medkitHeal + " de vida", 2f);
        });
    }

    /// <summary>Lo llama el boton LISTO.</summary>
    public void Ready()
    {
        Close();
        if (gameManager != null) gameManager.ContinueToNextRound();
    }

    // ------------------------------------------------------------------

    private void Buy(int price, System.Action apply)
    {
        if (moneySystem == null) return;

        if (!moneySystem.TrySpend(price))
        {
            ShowInfo("No tienes suficiente dinero (cuesta $" + price + ").", 2f);
            return;
        }

        apply?.Invoke();
        RefreshMoney();
    }

    private void RefreshMoney()
    {
        if (moneyText != null && moneySystem != null) moneyText.text = "DINERO  $" + moneySystem.Money;
    }

    private void ShowInfo(string text, float seconds)
    {
        if (infoText == null) return;
        infoText.text = text;
        infoHideTime = Time.time + seconds;
    }

    private void CachePlayer()
    {
        if (playerController == null) playerController = FindFirstObjectByType<FirstPersonController>();

        weapons.Clear();
        foreach (WeaponAmmo w in FindObjectsByType<WeaponAmmo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            weapons.Add(w);
    }

    private WeaponAmmo GetActiveWeapon()
    {
        if (weapons.Count == 0) CachePlayer();

        foreach (WeaponAmmo w in weapons)
            if (w != null && w.gameObject.activeInHierarchy) return w;

        return null;
    }
}
