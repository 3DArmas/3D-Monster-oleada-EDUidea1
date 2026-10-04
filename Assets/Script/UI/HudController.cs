using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD: vida, dinero, municion, arma, ronda y avisos centrales.
/// Se suscribe a los eventos de los sistemas de juego (no consulta nada cada frame,
/// salvo el cambio de arma activa y las animaciones cosmeticas).
///
/// Las referencias del bloque "Apocalypse HUD" son opcionales: si estan vacias el HUD
/// se comporta como el HUD minimo original.
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
    [SerializeField] private Color healthGood = new Color(0.96f, 0.93f, 0.86f);
    [SerializeField] private Color healthLow = new Color(1f, 0.82f, 0.25f);

    [Header("Apocalypse HUD (opcional)")]
    [Tooltip("Biblioteca de iconos: se usa para el icono del arma activa.")]
    [SerializeField] private ApocalypseIconLibrary iconLibrary;
    [Tooltip("Cinta roja rellena de izquierda a derecha segun la vida.")]
    [SerializeField] private Image healthFill;
    [Tooltip("Contenedor de la vida: late cuando la vida es baja.")]
    [SerializeField] private RectTransform healthBlock;
    [Tooltip("Silueta del arma activa.")]
    [SerializeField] private Image weaponIcon;
    [Tooltip("Fila donde se dibujan las balas del cargador.")]
    [SerializeField] private RectTransform bulletRow;
    [Tooltip("Plantilla (desactivada) de una bala.")]
    [SerializeField] private Image bulletTemplate;
    [Tooltip("Tecla del aviso, p. ej. la E de '[E] ABRIR TIENDA'.")]
    [SerializeField] private GameObject keyBadge;
    [SerializeField] private Text keyBadgeText;
    [Range(0.05f, 0.9f)]
    [SerializeField] private float lowHealthThreshold = 0.3f;

    private static readonly Color BulletFull = new Color(0.93f, 0.9f, 0.82f, 1f);
    private static readonly Color BulletEmpty = new Color(0.93f, 0.9f, 0.82f, 0.28f);
    private const float PunchDuration = 0.4f;
    private const float BulletRowHeight = 38f;
    private const float BulletMaxWidth = 16f;
    private const float BulletSpacing = 3f;

    private readonly List<WeaponAmmo> weapons = new List<WeaponAmmo>();
    private readonly List<Image> bulletSlots = new List<Image>();
    private WeaponAmmo activeWeapon;
    private float messageHideTime;
    private float healthRatio = 1f;
    private float roundPunchStart = -10f;
    private string lastMessage = string.Empty;

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
        ClearMessage();
        RefreshWeapon(force: true);
    }

    private void Update()
    {
        // El jugador cambia de arma: refrescamos el bloque de municion.
        WeaponAmmo current = FindActiveWeapon();
        if (current != activeWeapon) RefreshWeapon();

        if (messageText != null && messageText.text.Length > 0 && Time.time >= messageHideTime)
            ClearMessage();

        AnimateRoundPunch();
        AnimateHeartbeat();
    }

    // ------------------------------------------------------------------
    // Manejadores de eventos
    // ------------------------------------------------------------------

    private void HandleHealth(float current, float max)
    {
        healthRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (healthFill != null) healthFill.fillAmount = healthRatio;

        if (healthText == null) return;

        healthText.text = "HP  " + Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
        healthText.color = Color.Lerp(healthLow, healthGood, healthRatio);
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
        roundPunchStart = Time.unscaledTime;
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

        // Las armas ahora se INSTANCIAN en el Awake del WeaponSwitcher bajo la mano, asi
        // que la lista cacheada puede estar vacia o desfasada: se vuelve a buscar sola.
        RefrescarArmas();
        foreach (WeaponAmmo w in weapons)
            if (w != null && w.gameObject.activeInHierarchy) return w;

        return null;
    }

    /// <summary>Vuelve a engancharse a las armas que existan ahora mismo (HUD de municion).</summary>
    public void RefrescarArmas()
    {
        foreach (WeaponAmmo w in weapons)
            if (w != null) w.OnAmmoChanged -= HandleAmmoChanged;

        weapons.Clear();
        foreach (WeaponAmmo w in FindObjectsByType<WeaponAmmo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            weapons.Add(w);
            w.OnAmmoChanged += HandleAmmoChanged;
        }
    }

    private void RefreshWeapon(bool force = false)
    {
        WeaponAmmo current = FindActiveWeapon();
        if (!force && current == activeWeapon) return;

        activeWeapon = current;
        UpdateWeaponIcon();
        UpdateAmmoText();
    }

    /// <summary>
    /// Silueta del arma activa. Sustituye al WeaponSwitcher.weaponIconDisplay, que nunca estaba
    /// asignado en el prefab del jugador y por eso no se veia ningun icono.
    /// </summary>
    private void UpdateWeaponIcon()
    {
        if (weaponIcon == null) return;

        Sprite sprite = null;
        if (iconLibrary != null && activeWeapon != null) sprite = iconLibrary.GetWeaponIcon(activeWeapon.WeaponName);

        weaponIcon.sprite = sprite;
        weaponIcon.enabled = sprite != null;
    }

    /// <summary>Escribe la municion del arma activa. Se llama en cada cambio de balas.</summary>
    private void UpdateAmmoText()
    {
        if (activeWeapon == null)
        {
            if (ammoText != null) ammoText.text = "-- / --";
            if (weaponText != null) weaponText.text = string.Empty;
            UpdateBullets(0, 0);
            return;
        }

        if (ammoText != null) ammoText.text = activeWeapon.Magazine + " / " + activeWeapon.Reserve;
        if (weaponText != null)
            weaponText.text = activeWeapon.IsReloading ? "RECARGANDO..." : activeWeapon.WeaponName;

        UpdateBullets(activeWeapon.Magazine, activeWeapon.MagazineSize);
    }

    /// <summary>Dibuja una bala por cada hueco del cargador; las gastadas quedan apagadas.</summary>
    private void UpdateBullets(int magazine, int magazineSize)
    {
        if (bulletRow == null || bulletTemplate == null) return;

        magazineSize = Mathf.Clamp(magazineSize, 0, 60);
        while (bulletSlots.Count < magazineSize)
        {
            Image slot = Instantiate(bulletTemplate, bulletRow);
            slot.gameObject.SetActive(true);
            slot.name = "Bullet_" + bulletSlots.Count;
            bulletSlots.Add(slot);
        }

        float width = bulletRow.rect.width;
        float slotWidth = magazineSize > 0
            ? Mathf.Min(BulletMaxWidth, (width - BulletSpacing * (magazineSize - 1)) / magazineSize)
            : BulletMaxWidth;
        float pitch = slotWidth + BulletSpacing;

        for (int i = 0; i < bulletSlots.Count; i++)
        {
            Image slot = bulletSlots[i];
            bool used = i < magazineSize;
            slot.gameObject.SetActive(used);
            if (!used) continue;

            // Alineadas a la derecha: la primera bala gastada es la de la izquierda.
            var rt = (RectTransform)slot.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(slotWidth, BulletRowHeight);
            rt.anchoredPosition = new Vector2(-(magazineSize - 1 - i) * pitch, 0f);

            // Las balas que quedan ocupan los huecos de la derecha.
            slot.color = i >= magazineSize - magazine ? BulletFull : BulletEmpty;
        }
    }

    // ------------------------------------------------------------------
    // Avisos
    // ------------------------------------------------------------------

    public void ShowMessage(string text, float duration)
    {
        if (messageText == null) return;

        messageHideTime = Time.time + duration;
        if (text == lastMessage && messageText.text.Length > 0) return;

        lastMessage = text;
        ApplyMessage(text);
    }

    /// <summary>Si el aviso empieza por "[X]" la tecla se dibuja en un recuadro aparte.</summary>
    private void ApplyMessage(string text)
    {
        string body = text;
        string key = null;

        if (keyBadge != null && !string.IsNullOrEmpty(text) && text[0] == '[')
        {
            int close = text.IndexOf(']');
            if (close > 1 && close <= 4)
            {
                key = text.Substring(1, close - 1);
                body = text.Substring(close + 1).Trim();
            }
        }

        messageText.text = body;

        if (keyBadge != null)
        {
            keyBadge.SetActive(key != null);
            if (key != null && keyBadgeText != null) keyBadgeText.text = key;
        }
    }

    private void ClearMessage()
    {
        lastMessage = string.Empty;
        if (messageText != null) messageText.text = string.Empty;
        if (keyBadge != null) keyBadge.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Animaciones cosmeticas
    // ------------------------------------------------------------------

    /// <summary>"RONDA N" entra con un golpe: empieza grande y se asienta.</summary>
    private void AnimateRoundPunch()
    {
        if (roundText == null) return;

        float t = (Time.unscaledTime - roundPunchStart) / PunchDuration;
        if (t >= 1f)
        {
            if (roundText.rectTransform.localScale != Vector3.one) roundText.rectTransform.localScale = Vector3.one;
            return;
        }

        float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        roundText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, eased);
    }

    /// <summary>Con la vida baja el bloque de vida late.</summary>
    private void AnimateHeartbeat()
    {
        if (healthBlock == null) return;

        if (healthRatio > lowHealthThreshold || healthRatio <= 0f)
        {
            if (healthBlock.localScale != Vector3.one) healthBlock.localScale = Vector3.one;
            return;
        }

        float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 7f)), 6f);
        healthBlock.localScale = Vector3.one * (1f + 0.05f * beat);
    }
}
