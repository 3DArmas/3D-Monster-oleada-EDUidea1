using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps game identifiers (shop item ids, shop categories, weapon names and HUD icon keys) to the
/// white silhouette sprites of the apocalypse UI theme. The asset is filled by the editor tool
/// "Tools/Outbreak/Setup Apocalypse UI Assets"; at runtime it is read-only.
/// </summary>
[CreateAssetMenu(fileName = "ApocalypseIconLibrary", menuName = "Outbreak/Apocalypse Icon Library")]
public class ApocalypseIconLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("Lookup key. Weapons may list aliases separated by '|' (e.g. 'm1911|pistola').")]
        public string key;
        public Sprite sprite;
    }

    [Header("Shop items (ShopCatalog ids)")]
    [SerializeField] private List<Entry> items = new List<Entry>();

    [Header("Shop categories (armas, municion, movilidad, combate, objetos)")]
    [SerializeField] private List<Entry> categories = new List<Entry>();

    [Header("Weapons (WeaponAmmo.WeaponName, case-insensitive, aliases with '|')")]
    [SerializeField] private List<Entry> weapons = new List<Entry>();

    [Header("HUD (bullet, skull, health, crosshair, lock)")]
    [SerializeField] private List<Entry> hud = new List<Entry>();

    [NonSerialized] private Dictionary<string, Sprite> itemMap;
    [NonSerialized] private Dictionary<string, Sprite> categoryMap;
    [NonSerialized] private Dictionary<string, Sprite> weaponMap;
    [NonSerialized] private Dictionary<string, Sprite> hudMap;

    private void OnEnable() { InvalidateCache(); }

    private void OnValidate() { InvalidateCache(); }

    private void InvalidateCache()
    {
        itemMap = categoryMap = weaponMap = hudMap = null;
    }

    /// <summary>Icon for a ShopCatalog item id (e.g. "salto"). Null when unknown.</summary>
    public Sprite GetItemIcon(string itemId)
    {
        if (itemMap == null) itemMap = Build(items, false);
        return Lookup(itemMap, itemId);
    }

    /// <summary>Icon for a shop tab.</summary>
    public Sprite GetCategoryIcon(ShopCategory category)
    {
        if (categoryMap == null) categoryMap = Build(categories, false);
        return Lookup(categoryMap, category.ToString());
    }

    /// <summary>Weapon silhouette for WeaponAmmo.WeaponName ("AK74", "M1911"...). Null when unknown.</summary>
    public Sprite GetWeaponIcon(string weaponName)
    {
        if (weaponMap == null) weaponMap = Build(weapons, true);
        return Lookup(weaponMap, weaponName);
    }

    /// <summary>Generic HUD sprite: "bullet", "skull", "health", "crosshair" or "lock".</summary>
    public Sprite GetHudIcon(string key)
    {
        if (hudMap == null) hudMap = Build(hud, false);
        return Lookup(hudMap, key);
    }

    private static Sprite Lookup(Dictionary<string, Sprite> map, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return map.TryGetValue(key.Trim().ToLowerInvariant(), out Sprite sprite) ? sprite : null;
    }

    private static Dictionary<string, Sprite> Build(List<Entry> entries, bool splitAliases)
    {
        var map = new Dictionary<string, Sprite>();
        if (entries == null) return map;

        foreach (Entry e in entries)
        {
            if (string.IsNullOrEmpty(e.key) || e.sprite == null) continue;

            if (!splitAliases)
            {
                map[e.key.Trim().ToLowerInvariant()] = e.sprite;
                continue;
            }

            foreach (string alias in e.key.Split('|'))
                if (alias.Trim().Length > 0) map[alias.Trim().ToLowerInvariant()] = e.sprite;
        }

        return map;
    }
}
