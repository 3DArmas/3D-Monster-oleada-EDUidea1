using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Imports the apocalypse UI textures as UI sprites, slices the two 4x4 icon sheets into named
/// sprites and (re)builds the <see cref="ApocalypseIconLibrary"/> asset. Idempotent: running it
/// again only touches what differs.
/// </summary>
public static class ApocalypseAssetSetup
{
    public const string Root = "Assets/UI/Apocalypse";
    public const string TexturesDir = Root + "/Textures";
    public const string IconsDir = Root + "/Icons";
    public const string LibraryPath = Root + "/ApocalypseIconLibrary.asset";

    private const string ItemsSheet = IconsDir + "/icons_sheet_items.png";
    private const string WeaponsSheet = IconsDir + "/icons_sheet_weapons.png";
    private const int Cell = 256;

    // file name -> 9-slice border (left, bottom, right, top) in source pixels.
    private static readonly Dictionary<string, Vector4> Textures = new Dictionary<string, Vector4>
    {
        { "cork_board", Vector4.zero },
        { "paper_header", new Vector4(90, 60, 90, 60) },
        { "masking_tape_label", new Vector4(50, 0, 50, 0) },
        { "polaroid_frame", Vector4.zero },
        { "red_tape", new Vector4(50, 0, 50, 0) },
        { "notebook_page", Vector4.zero },
        { "cash_envelope", Vector4.zero },
        { "green_sign", new Vector4(40, 40, 40, 40) },
        { "tape_piece", Vector4.zero },
        { "hud_health_tape", new Vector4(60, 0, 60, 0) },
        { "hud_duct_tape", new Vector4(90, 90, 90, 90) },
        { "hud_grime_vignette", Vector4.zero }
    };

    private struct SheetSprite
    {
        public string name;
        public bool trim;
        public SheetSprite(string name, bool trim) { this.name = name; this.trim = trim; }
    }

    // Row-major order of the generated sheets.
    private static readonly SheetSprite[] ItemsLayout =
    {
        new SheetSprite("icon_salto", false), new SheetSprite("icon_velocidad", false),
        new SheetSprite("icon_doble_salto", false), new SheetSprite("icon_dash", false),
        new SheetSprite("icon_aguante", false), new SheetSprite("icon_slide", false),
        new SheetSprite("icon_wallrun", false), new SheetSprite("icon_mantle", false),
        new SheetSprite("icon_municion", false), new SheetSprite("icon_municion_grande", false),
        new SheetSprite("icon_dano", false), new SheetSprite("icon_recarga", false),
        new SheetSprite("icon_cargador", false), new SheetSprite("icon_botiquin", false),
        new SheetSprite("icon_vida", false), new SheetSprite("icon_granada", false)
    };

    private static readonly SheetSprite[] WeaponsLayout =
    {
        new SheetSprite("icon_mina", false), new SheetSprite("icon_escopeta", true),
        new SheetSprite("icon_subfusil", true), new SheetSprite("icon_rifle", true),
        new SheetSprite("weapon_pistol", true), new SheetSprite("weapon_ak", true),
        new SheetSprite("weapon_knife", true), new SheetSprite("hud_bullet", true),
        new SheetSprite("hud_skull", false), new SheetSprite("hud_health", false),
        new SheetSprite("hud_crosshair", false), new SheetSprite("cat_municion", false),
        new SheetSprite("cat_movilidad", false), new SheetSprite("cat_combate", false),
        new SheetSprite("cat_objetos", false), new SheetSprite("icon_locked", false)
    };

    [MenuItem("Tools/Outbreak/Setup Apocalypse UI Assets")]
    public static void SetupMenu()
    {
        string report = EnsureAssets();
        Debug.Log("[ApocalypseAssets] " + report);
    }

    /// <summary>Batch entry point: -executeMethod ApocalypseAssetSetup.SetupBatch</summary>
    public static void SetupBatch()
    {
        Debug.Log("[ApocalypseAssets] " + EnsureAssets());
    }

    /// <summary>Imports/slices everything and returns a short report. Safe to call repeatedly.</summary>
    public static string EnsureAssets()
    {
        int changed = 0;
        foreach (KeyValuePair<string, Vector4> kv in Textures)
            changed += ConfigureSingle(TexturesDir + "/" + kv.Key + ".png", kv.Value) ? 1 : 0;

        changed += SliceSheet(ItemsSheet, ItemsLayout) ? 1 : 0;
        changed += SliceSheet(WeaponsSheet, WeaponsLayout) ? 1 : 0;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        bool libraryChanged = BuildLibrary();
        AssetDatabase.SaveAssets();
        return "textures/sheets reconfigured: " + changed + ", library " + (libraryChanged ? "updated" : "unchanged");
    }

    // ------------------------------------------------------------------
    // Single sprites
    // ------------------------------------------------------------------

    private static TextureImporter GetImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) Debug.LogError("[ApocalypseAssets] missing texture: " + path);
        return importer;
    }

    private static bool ApplyCommon(TextureImporter importer, SpriteImportMode mode)
    {
        bool dirty = false;

        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.spriteImportMode != mode) { importer.spriteImportMode = mode; dirty = true; }
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
        if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
        if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
        if (importer.maxTextureSize != 2048) { importer.maxTextureSize = 2048; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            dirty = true;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            dirty = true;
        }

        return dirty;
    }

    private static bool ConfigureSingle(string path, Vector4 border)
    {
        TextureImporter importer = GetImporter(path);
        if (importer == null) return false;

        bool dirty = ApplyCommon(importer, SpriteImportMode.Single);
        if (importer.spriteBorder != border) { importer.spriteBorder = border; dirty = true; }

        if (dirty) importer.SaveAndReimport();
        return dirty;
    }

    // ------------------------------------------------------------------
    // Icon sheets
    // ------------------------------------------------------------------

    private static bool SliceSheet(string path, SheetSprite[] layout)
    {
        TextureImporter importer = GetImporter(path);
        if (importer == null) return false;

        bool dirty = ApplyCommon(importer, SpriteImportMode.Multiple);
        if (dirty)
        {
            importer.SaveAndReimport();
            importer = GetImporter(path);
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Dictionary<string, SpriteRect> existing = new Dictionary<string, SpriteRect>();
        foreach (SpriteRect r in provider.GetSpriteRects()) existing[r.name] = r;

        Texture2D readable = LoadReadable(path);
        var rects = new List<SpriteRect>();
        bool rectsChanged = existing.Count != layout.Length;

        for (int i = 0; i < layout.Length; i++)
        {
            int col = i % 4;
            int row = i / 4;
            Rect cell = new Rect(col * Cell, readable.height - (row + 1) * Cell, Cell, Cell);
            Rect rect = layout[i].trim ? TrimToAlpha(readable, cell, 6) : cell;

            SpriteRect sr;
            if (!existing.TryGetValue(layout[i].name, out sr))
            {
                sr = new SpriteRect { name = layout[i].name, spriteID = GUID.Generate() };
                rectsChanged = true;
            }
            else if (sr.rect != rect)
            {
                rectsChanged = true;
            }

            sr.rect = rect;
            sr.alignment = SpriteAlignment.Center;
            sr.pivot = new Vector2(0.5f, 0.5f);
            rects.Add(sr);
        }

        Object.DestroyImmediate(readable);

        if (rectsChanged)
        {
            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();
            importer.SaveAndReimport();
            dirty = true;
        }

        return dirty;
    }

    private static Texture2D LoadReadable(string assetPath)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(assetPath));
        return tex;
    }

    /// <summary>Alpha bounding box inside a cell (Unity texture space, origin bottom-left) plus padding.</summary>
    private static Rect TrimToAlpha(Texture2D tex, Rect cell, int pad)
    {
        int x0 = (int)cell.x, y0 = (int)cell.y, w = (int)cell.width, h = (int)cell.height;
        Color32[] px = tex.GetPixels32();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[(y0 + y) * tex.width + (x0 + x)].a <= 40) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0) return cell;

        minX = Mathf.Max(0, minX - pad);
        minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(w - 1, maxX + pad);
        maxY = Mathf.Min(h - 1, maxY + pad);
        return new Rect(x0 + minX, y0 + minY, maxX - minX + 1, maxY - minY + 1);
    }

    // ------------------------------------------------------------------
    // Library
    // ------------------------------------------------------------------

    private static Sprite Find(string sheetPath, string spriteName)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath))
            if (o is Sprite s && s.name == spriteName) return s;

        Debug.LogError("[ApocalypseAssets] sprite not found: " + spriteName + " in " + sheetPath);
        return null;
    }

    private static bool BuildLibrary()
    {
        var library = AssetDatabase.LoadAssetAtPath<ApocalypseIconLibrary>(LibraryPath);
        bool created = false;
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<ApocalypseIconLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            created = true;
        }

        var so = new SerializedObject(library);
        bool changed = created;

        changed |= Fill(so.FindProperty("items"), new[]
        {
            Pair("salto", ItemsSheet, "icon_salto"), Pair("velocidad", ItemsSheet, "icon_velocidad"),
            Pair("doble_salto", ItemsSheet, "icon_doble_salto"), Pair("dash", ItemsSheet, "icon_dash"),
            Pair("aguante", ItemsSheet, "icon_aguante"), Pair("slide", ItemsSheet, "icon_slide"),
            Pair("wallrun", ItemsSheet, "icon_wallrun"), Pair("mantle", ItemsSheet, "icon_mantle"),
            Pair("municion", ItemsSheet, "icon_municion"), Pair("municion_grande", ItemsSheet, "icon_municion_grande"),
            Pair("dano", ItemsSheet, "icon_dano"), Pair("recarga", ItemsSheet, "icon_recarga"),
            Pair("cargador", ItemsSheet, "icon_cargador"), Pair("botiquin", ItemsSheet, "icon_botiquin"),
            Pair("vida", ItemsSheet, "icon_vida"), Pair("granada", ItemsSheet, "icon_granada"),
            Pair("escopeta", WeaponsSheet, "icon_escopeta"), Pair("subfusil", WeaponsSheet, "icon_subfusil"),
            Pair("rifle", WeaponsSheet, "icon_rifle"), Pair("mina", WeaponsSheet, "icon_mina")
        });

        changed |= Fill(so.FindProperty("categories"), new[]
        {
            Pair("armas", WeaponsSheet, "weapon_pistol"), Pair("municion", WeaponsSheet, "cat_municion"),
            Pair("movilidad", WeaponsSheet, "cat_movilidad"), Pair("combate", WeaponsSheet, "cat_combate"),
            Pair("objetos", WeaponsSheet, "cat_objetos")
        });

        changed |= Fill(so.FindProperty("weapons"), new[]
        {
            Pair("m1911|pistola|pistol", WeaponsSheet, "weapon_pistol"),
            Pair("ak74|ak47|ak-47|ak", WeaponsSheet, "weapon_ak"),
            Pair("cuchillo|knife|navaja", WeaponsSheet, "weapon_knife"),
            Pair("escopeta|shotgun", WeaponsSheet, "icon_escopeta"),
            Pair("subfusil|smg", WeaponsSheet, "icon_subfusil"),
            Pair("rifle|francotirador|sniper", WeaponsSheet, "icon_rifle")
        });

        changed |= Fill(so.FindProperty("hud"), new[]
        {
            Pair("bullet", WeaponsSheet, "hud_bullet"), Pair("skull", WeaponsSheet, "hud_skull"),
            Pair("health", WeaponsSheet, "hud_health"), Pair("crosshair", WeaponsSheet, "hud_crosshair"),
            Pair("lock", WeaponsSheet, "icon_locked")
        });

        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
        }

        return changed;
    }

    private struct KeySprite
    {
        public string key;
        public Sprite sprite;
    }

    private static KeySprite Pair(string key, string sheet, string spriteName)
    {
        return new KeySprite { key = key, sprite = Find(sheet, spriteName) };
    }

    private static bool Fill(SerializedProperty list, KeySprite[] pairs)
    {
        bool changed = false;
        if (list.arraySize != pairs.Length) { list.arraySize = pairs.Length; changed = true; }

        for (int i = 0; i < pairs.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            SerializedProperty key = e.FindPropertyRelative("key");
            SerializedProperty sprite = e.FindPropertyRelative("sprite");

            if (key.stringValue != pairs[i].key) { key.stringValue = pairs[i].key; changed = true; }
            if (sprite.objectReferenceValue != pairs[i].sprite) { sprite.objectReferenceValue = pairs[i].sprite; changed = true; }
        }

        return changed;
    }
}
