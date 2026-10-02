using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD minimo: vida, dinero, municion, arma, ronda y avisos centrales.
/// Se suscribe a los eventos de los sistemas de juego (no consulta nada cada frame,
/// salvo el cambio de arma activa).
/// </summary>
public class HudController : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private Text healthText;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text ammoText;
    [SerializeField] private Text weaponText;
    [SerializeField] private Text roundText;
    [SerializeField] private Text remainingText;
    [SerializeField] private Text messageText;

    [Header("Sistemas")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private MoneySystem moneySystem;
    [SerializeField] private GameManager gameManager;

    [Header("Ajustes")]
    [SerializeField] private Color healthGood = new Color(0.45f, 0.95f, 0.5f);
    [SerializeField] private Color healthLow = new Color(1f, 0.35f, 0.3f);

    private readonly List<WeaponAmmo> weapons = new List<WeaponAmmo>();
    private WeaponAmmo activeWeapon;
    private float messageHideTime;

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged += HandleHealth;
        if (moneySystem != null) moneySystem.OnMoneyChanged += HandleMoney;
        if (gameManager != null)
        {
            gameManager.OnRoundStarted += HandleRoundStarted;
            gameManager.OnRemainingChanged += HandleRemaining;
            gameManager.OnStateChanged += HandleState;
        }

        weapons.Clear();
        foreach (WeaponAmmo w in FindObjectsByType<WeaponAmmo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            weapons.Add(w);
            w.OnAmmoChanged += HandleAmmoChanged;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealth;
        if (moneySystem != null) moneySystem.OnMoneyChanged -= HandleMoney;
        if (gameManager != null)
        {
            gameManager.OnRoundStarted -= HandleRoundStarted;
            gameManager.OnRemainingChanged -= HandleRemaining;
            gameManager.OnStateChanged -= HandleState;
        }

        foreach (WeaponAmmo w in weapons)
            if (w != null) w.OnAmmoChanged -= HandleAmmoChanged;

        weapons.Clear();
    }

    private void Start()
    {
        if (playerHealth != null) HandleHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        if (moneySystem != null) HandleMoney(moneySystem.Money);
        if (messageText != null) messageText.text = string.Empty;
        RefreshWeapon(force: true);
    }

    private void Update()
    {
        // El jugador cambia de arma: refrescamos el bloque de municion.
        WeaponAmmo current = FindActiveWeapon();
        if (current != activeWeapon) RefreshWeapon();

        if (messageText != null && messageText.text.Length > 0 && Time.time >= messageHideTime)
            messageText.text = string.Empty;
    }

    // ------------------------------------------------------------------
    // Manejadores de eventos
    // ------------------------------------------------------------------

    private void HandleHealth(float current, float max)
    {
        if (healthText == null) return;

        healthText.text = "HP  " + Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
        float ratio = max > 0f ? current / max : 0f;
        healthText.color = Color.Lerp(healthLow, healthGood, Mathf.Clamp01(ratio));
    }

    private void HandleMoney(int money)
    {
        if (moneyText != null) moneyText.text = "$" + money;
    }

    private void HandleAmmoChanged(int magazine, int reserve)
    {
        // OJO: antes esto llamaba a RefreshWeapon(), que sale sin hacer nada si el arma
        // activa no ha cambiado... por eso el contador se quedaba clavado al disparar.
        UpdateAmmoText();
    }

    private void HandleRoundStarted(int round)
    {
        if (roundText != null) roundText.text = "RONDA " + round;
        ShowMessage("RONDA " + round, 2f);
    }

    private void HandleRemaining(int remaining, int total)
    {
        if (remainingText != null) remainingText.text = "QUEDAN  " + remaining + " / " + total;
    }

    private void HandleState(GameState state)
    {
        if (state == GameState.Shop) ShowMessage("TIENDA ABIERTA  -  [E] para comprar", 4f);
    }

    // ------------------------------------------------------------------
    // Municion y arma activa
    // ------------------------------------------------------------------

    private WeaponAmmo FindActiveWeapon()
    {
        foreach (WeaponAmmo w in weapons)
            if (w != null && w.gameObject.activeInHierarchy) return w;

        return null;
    }

    private void RefreshWeapon(bool force = false)
    {
        WeaponAmmo current = FindActiveWeapon();
        if (!force && current == activeWeapon) return;

        activeWeapon = current;
        UpdateAmmoText();
    }

    /// <summary>Escribe la municion del arma activa. Se llama en cada cambio de balas.</summary>
    private void UpdateAmmoText()
    {
        if (activeWeapon == null)
        {
            if (ammoText != null) ammoText.text = "-- / --";
            if (weaponText != null) weaponText.text = string.Empty;
            return;
        }

        if (ammoText != null) ammoText.text = activeWeapon.Magazine + " / " + activeWeapon.Reserve;
        if (weaponText != null)
            weaponText.text = activeWeapon.IsReloading ? "RECARGANDO..." : activeWeapon.WeaponName;
    }

    public void ShowMessage(string text, float duration)
    {
        if (messageText == null) return;

        messageText.text = text;
        messageHideTime = Time.time + duration;
    }
}
