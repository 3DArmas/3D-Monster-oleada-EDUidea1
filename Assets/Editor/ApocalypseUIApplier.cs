using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Restyles the HUD and the shop of the OPEN scene with the apocalypse theme and wires every
/// serialized reference of HudController, ShopUI and ShopCard.
///
/// - Idempotent: objects are found by name (and created only when missing), every property is
///   rewritten on each run, so running it twice yields the same hierarchy.
/// - Never deletes gameplay objects: the legacy Text objects are kept, restyled and re-parented.
/// - Interactive mode marks the scene dirty and does not save; batch mode can open and save scenes.
/// </summary>
public static class ApocalypseUIApplier
{
    private const string Tag = "[ApocalypseUI] ";
    private const string FontsDir = "Assets/UI/Fonts/";
    private const string TexturesDir = ApocalypseAssetSetup.TexturesDir + "/";
    private static readonly string[] DefaultScenes = { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Warehouse.unity" };

    // ---------------- palette ----------------
    private static readonly Color Bone = new Color(0.93f, 0.9f, 0.82f, 1f);
    private static readonly Color Ink = new Color(0.1f, 0.075f, 0.055f, 1f);
    private static readonly Color SprayRed = new Color(0.74f, 0.05f, 0.04f, 1f);
    private static readonly Color MoneyGreen = new Color(0.27f, 0.95f, 0.42f, 1f);
    private static readonly Color Black70 = new Color(0f, 0f, 0f, 0.7f);
    private static readonly Color Black55 = new Color(0.03f, 0.03f, 0.03f, 0.62f);
    private static readonly Color Amber = new Color(0.96f, 0.76f, 0.25f, 1f);
    private static readonly Color White = Color.white;

    private class Ctx
    {
        public Font stencil, marker, spray;
        public ApocalypseIconLibrary library;
        public readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public readonly List<string> log = new List<string>();
        public int warnings;

        public Sprite S(string name)
        {
            if (!sprites.TryGetValue(name, out Sprite s))
            {
                s = AssetDatabase.LoadAssetAtPath<Sprite>(TexturesDir + name + ".png");
                sprites[name] = s;
                if (s == null) Warn("missing sprite " + name);
            }
            return s;
        }

        public void Warn(string msg) { warnings++; log.Add("WARN " + msg); Debug.LogWarning(Tag + msg); }
        public void Info(string msg) { log.Add(msg); }
    }

    // ------------------------------------------------------------------
    // Entry points
    // ------------------------------------------------------------------

    [MenuItem("Tools/Outbreak/Apply Apocalypse UI")]
    public static void ApplyMenu()
    {
        int warnings = Apply();
        if (warnings < 0) return;

        EditorUtility.DisplayDialog("Apocalypse UI",
            "UI applied to the open scene (" + SceneManager.GetActiveScene().name + ").\n" +
            warnings + " warning(s). Save the scene with Ctrl+S.\n" +
            "Run this menu again on every scene that has a Canvas (SampleScene, Warehouse).", "OK");
    }

    /// <summary>
    /// Batch mode: -executeMethod ApocalypseUIApplier.ApplyBatch [-apocalypseScenes "a.unity;b.unity"].
    /// Opens each scene, applies the UI and saves it.
    /// </summary>
    public static void ApplyBatch()
    {
        string[] scenes = DefaultScenes;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-apocalypseScenes") scenes = args[i + 1].Split(';');

        int failures = 0;
        foreach (string path in scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int warnings = Apply();
            if (warnings < 0) { failures++; continue; }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log(Tag + "saved " + path + " (" + warnings + " warnings)");
        }

        if (failures > 0) EditorApplication.Exit(1);
    }

    /// <summary>
    /// Applies the theme to the active scene. Returns the number of warnings (0 = clean) or -1
    /// when the scene has no HUD/shop to restyle.
    /// </summary>
    public static int Apply()
    {
        Debug.Log(Tag + AssetSetupReport());

        var ctx = new Ctx
        {
            stencil = AssetDatabase.LoadAssetAtPath<Font>(FontsDir + "BlackOpsOne-Regular.ttf"),
            marker = AssetDatabase.LoadAssetAtPath<Font>(FontsDir + "PermanentMarker-Regular.ttf"),
            spray = AssetDatabase.LoadAssetAtPath<Font>(FontsDir + "RubikWetPaint-Regular.ttf"),
            library = AssetDatabase.LoadAssetAtPath<ApocalypseIconLibrary>(ApocalypseAssetSetup.LibraryPath)
        };

        if (ctx.stencil == null || ctx.marker == null || ctx.spray == null) ctx.Warn("one or more fonts are missing in " + FontsDir);
        if (ctx.library == null) { Debug.LogError(Tag + "icon library missing"); return -1; }

        HudController hud = Object.FindFirstObjectByType<HudController>(FindObjectsInactive.Include);
        ShopUI shop = Object.FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
        if (hud == null && shop == null)
        {
            Debug.LogError(Tag + "no HudController or ShopUI in the open scene.");
            return -1;
        }

        if (hud != null) BuildHud(ctx, hud);
        if (shop != null) BuildShop(ctx, shop);

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        foreach (string line in ctx.log) Debug.Log(Tag + line);
        Debug.Log(Tag + "applied to '" + scene.name + "' with " + ctx.warnings + " warning(s).");
        return ctx.warnings;
    }

    private static string AssetSetupReport()
    {
        return "assets: " + ApocalypseAssetSetup.EnsureAssets();
    }

    // ------------------------------------------------------------------
    // HUD
    // ------------------------------------------------------------------

    private static void BuildHud(Ctx c, HudController hudCtrl)
    {
        Transform hud = hudCtrl.transform;
        var so = new SerializedObject(hudCtrl);

        // Legacy texts referenced by HudController (kept, restyled, re-parented).
        RectTransform vida = Find(hud, "Vida"), arma = Find(hud, "Arma"), municion = Find(hud, "Municion");
        RectTransform dinero = Find(hud, "Dinero"), ronda = Find(hud, "Ronda");
        RectTransform restantes = Find(hud, "Restantes"), aviso = Find(hud, "Aviso"), cross = Find(hud, "Crosshair");

        // --- grime vignette (first child, never blocks clicks) ---
        RectTransform grime = Get(hud, hud, "GrimeVignette");
        Stretch(grime, 0, 0, 0, 0);
        Img(grime, c.S("hud_grime_vignette"), new Color(1f, 1f, 1f, 0.85f), Image.Type.Simple);
        grime.SetAsFirstSibling();

        // --- round title + remaining pill (top centre) ---
        RectTransform roundBlock = Get(hud, hud, "RondaBlock");
        Anchor(roundBlock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(760f, 190f));

        if (ronda != null)
        {
            Reparent(ronda, roundBlock);
            Anchor(ronda, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(760f, 124f));
            Txt(ronda, c.spray, 96, Bone, TextAnchor.MiddleCenter, null, true, 40);
            Outline(ronda, new Color(0.02f, 0.02f, 0.02f, 0.9f), new Vector2(3f, -3f));
        }

        RectTransform pill = Get(roundBlock, roundBlock, "RestantesPill");
        Anchor(pill, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(340f, 54f));
        Img(pill, null, Black55, Image.Type.Simple);

        RectTransform skull = Get(pill, pill, "SkullIcon");
        Anchor(skull, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(40f, 40f));
        Img(skull, c.library.GetHudIcon("skull"), Bone, Image.Type.Simple, true);

        if (restantes != null)
        {
            Reparent(restantes, pill);
            Stretch(restantes, 62, 0, 12, 0);
            Txt(restantes, c.stencil, 29, Bone, TextAnchor.MiddleCenter, null, true, 16);
            Outline(restantes, Color.clear, Vector2.zero);
        }

        // --- money on duct tape (top right) ---
        RectTransform moneyTape = Get(hud, hud, "DineroTape");
        Anchor(moneyTape, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(372f, 96f));
        Img(moneyTape, c.S("hud_duct_tape"), White, Image.Type.Simple);
        moneyTape.localRotation = Quaternion.Euler(0f, 0f, -2f);

        if (dinero != null)
        {
            Reparent(dinero, moneyTape);
            Stretch(dinero, 34, 6, 34, 6);
            Txt(dinero, c.stencil, 58, MoneyGreen, TextAnchor.MiddleCenter, null, true, 26);
            Outline(dinero, new Color(0.02f, 0.06f, 0.02f, 0.85f), new Vector2(2f, -2f));
        }

        // --- health on red medical tape (bottom left) ---
        RectTransform healthBlock = Get(hud, hud, "VidaBlock");
        Anchor(healthBlock, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(298f, 83f), new Vector2(540f, 109f));

        RectTransform tapeBase = Get(healthBlock, healthBlock, "TapeBase");
        Stretch(tapeBase, 0, 0, 0, 0);
        Img(tapeBase, c.S("hud_health_tape"), new Color(0.46f, 0.34f, 0.34f, 1f), Image.Type.Simple);

        RectTransform tapeFill = Get(healthBlock, healthBlock, "TapeFill");
        Stretch(tapeFill, 0, 0, 0, 0);
        Image fill = Img(tapeFill, c.S("hud_health_tape"), White, Image.Type.Filled);
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        RectTransform cross2 = Get(healthBlock, healthBlock, "CruzMedica");
        Anchor(cross2, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 2f), new Vector2(92f, 92f));
        Img(cross2, c.library.GetHudIcon("health"), new Color(0.96f, 0.94f, 0.9f, 1f), Image.Type.Simple, true);
        Outline(cross2, new Color(0.25f, 0f, 0f, 0.9f), new Vector2(2f, -2f));

        if (vida != null)
        {
            Reparent(vida, healthBlock);
            Stretch(vida, 104, 4, 22, 4);
            Txt(vida, c.stencil, 42, Bone, TextAnchor.MiddleCenter, null, true, 20);
            Outline(vida, new Color(0.12f, 0f, 0f, 0.95f), new Vector2(2.5f, -2.5f));
        }

        // --- ammo, weapon silhouette and bullets (bottom right) ---
        RectTransform ammoBlock = Get(hud, hud, "MunicionBlock");
        Anchor(ammoBlock, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 26f), new Vector2(660f, 200f));

        if (municion != null)
        {
            Reparent(municion, ammoBlock);
            Anchor(municion, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 38f), new Vector2(390f, 130f));
            Txt(municion, c.stencil, 100, Bone, TextAnchor.MiddleRight, null, true, 40);
            Outline(municion, new Color(0.02f, 0.02f, 0.02f, 0.9f), new Vector2(3f, -3f));
        }

        RectTransform weaponIcon = Get(ammoBlock, ammoBlock, "ArmaIcono");
        Anchor(weaponIcon, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(240f, 86f));
        Image weaponImg = Img(weaponIcon, null, Bone, Image.Type.Simple, true);
        Outline(weaponIcon, new Color(0.02f, 0.02f, 0.02f, 0.8f), new Vector2(2f, -2f));

        RectTransform bulletRow = Get(ammoBlock, ammoBlock, "Balas");
        Anchor(bulletRow, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 50f), new Vector2(240f, 38f));

        RectTransform bulletTemplate = Get(bulletRow, bulletRow, "BulletTemplate");
        Anchor(bulletTemplate, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(16f, 38f));
        Image bulletImg = Img(bulletTemplate, c.library.GetHudIcon("bullet"), Bone, Image.Type.Simple, true);
        bulletTemplate.gameObject.SetActive(false);

        if (arma != null)
        {
            Reparent(arma, ammoBlock);
            Anchor(arma, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(240f, 28f));
            Txt(arma, c.stencil, 19, new Color(Bone.r, Bone.g, Bone.b, 0.85f), TextAnchor.MiddleRight, null, true, 12);
            Outline(arma, new Color(0.02f, 0.02f, 0.02f, 0.8f), new Vector2(1.5f, -1.5f));
        }

        // --- centre message with key badge ("[E] ABRIR TIENDA") ---
        RectTransform row = Get(hud, hud, "AvisoFila");
        Anchor(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(300f, 64f));
        var hlg = Ensure<HorizontalLayoutGroup>(row.gameObject);
        hlg.spacing = 14f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var fitter = Ensure<ContentSizeFitter>(row.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform badge = Get(row, row, "KeyBadge");
        Anchor(badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
        Image badgeImg = Img(badge, c.S("hud_duct_tape"), White, Image.Type.Sliced);
        badgeImg.pixelsPerUnitMultiplier = 5f;
        var badgeLayout = Ensure<LayoutElement>(badge.gameObject);
        badgeLayout.preferredWidth = 64f;
        badgeLayout.preferredHeight = 64f;
        badge.SetAsFirstSibling();

        RectTransform badgeText = Get(badge, badge, "Tecla");
        Stretch(badgeText, 0, 0, 0, 0);
        Txt(badgeText, c.stencil, 38, Ink, TextAnchor.MiddleCenter, "E", false, 0);

        if (aviso != null)
        {
            Reparent(aviso, row);
            aviso.SetAsLastSibling();
            Anchor(aviso, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 64f));
            Text msg = Txt(aviso, c.stencil, 30, Bone, TextAnchor.MiddleCenter, null, false, 0);
            msg.horizontalOverflow = HorizontalWrapMode.Overflow;
            msg.verticalOverflow = VerticalWrapMode.Overflow;
            Outline(aviso, new Color(0.02f, 0.02f, 0.02f, 0.9f), new Vector2(2f, -2f));
        }

        // --- crosshair: same shape, bone colour ---
        if (cross != null)
            foreach (Image im in cross.GetComponentsInChildren<Image>(true))
                im.color = new Color(Bone.r, Bone.g, Bone.b, im.name == "Centro" ? 0.6f : 0.92f);

        // --- wiring ---
        SetRef(c, so, "iconLibrary", c.library);
        SetRef(c, so, "healthFill", fill);
        SetRef(c, so, "healthBlock", healthBlock);
        SetRef(c, so, "weaponIcon", weaponImg);
        SetRef(c, so, "bulletRow", bulletRow);
        SetRef(c, so, "bulletTemplate", bulletImg);
        SetRef(c, so, "keyBadge", badge.gameObject);
        SetRef(c, so, "keyBadgeText", badgeText.GetComponent<Text>());
        SetColor(so, "healthGood", new Color(0.96f, 0.93f, 0.86f, 1f));
        SetColor(so, "healthLow", new Color(1f, 0.82f, 0.25f, 1f));
        so.ApplyModifiedProperties();

        // The legacy refs must exist: log them so a missing one is visible.
        foreach (string p in new[] { "healthText", "moneyText", "ammoText", "weaponText", "roundText", "remainingText", "messageText", "playerHealth", "moneySystem", "gameManager" })
            CheckRef(c, so, p);

        weaponImg.enabled = false; // the HUD sets the sprite at runtime from the active weapon
        EditorUtility.SetDirty(hudCtrl);
    }

    // ------------------------------------------------------------------
    // Shop
    // ------------------------------------------------------------------

    private static void BuildShop(Ctx c, ShopUI shopUi)
    {
        Transform panel = shopUi.transform;
        var panelRt = (RectTransform)panel;
        var so = new SerializedObject(shopUi);

        // --- cork board ---
        Anchor(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1720f, 963f));
        Img(panelRt, null, Color.clear, Image.Type.Simple, false, false); // the board and its backdrop are children

        RectTransform backdrop = Get(panel, panel, "Fondo");
        Stretch(backdrop, -2400, -1400, -2400, -1400);
        Img(backdrop, null, new Color(0.05f, 0.04f, 0.03f, 1f), Image.Type.Simple, false, true);

        // Exactly one 1920x1080 canvas around the 1720x963 layout area.
        RectTransform board = Get(panel, panel, "Tablon");
        Stretch(board, -100, -58.5f, -100, -58.5f);
        Img(board, c.S("cork_board"), White, Image.Type.Simple, false, true);

        RectTransform titulo = Find(panel, "Titulo", 1);
        RectTransform dinero = Find(panel, "Dinero", 1);
        RectTransform info = Find(panel, "Info", 1);
        RectTransform hueso = Find(panel, "BotonHueso", 1);
        RectTransform grid = Find(panel, "Tarjetas", 1);
        RectTransform estado = Find(panel, "Estado", 1);
        RectTransform listo = Find(panel, "BotonListo", 1);

        // --- torn paper header with the spray title ---
        RectTransform header = Get(panel, panel, "HeaderPaper");
        TL(header, 470, 26, 640, 236);
        Img(header, c.S("paper_header"), White, Image.Type.Simple);

        if (titulo != null)
        {
            TL(titulo, 500, 52, 580, 184);
            Txt(titulo, c.spray, 130, SprayRed, TextAnchor.MiddleCenter, "TIENDA", true, 50);
        }

        // --- masking-tape tabs ---
        var tabFondos = new List<Image>();
        foreach (ShopCategory cat in Enum.GetValues(typeof(ShopCategory)))
        {
            RectTransform tab = Find(panel, "Tab_" + cat, 1);
            if (tab == null) { c.Warn("tab missing: Tab_" + cat); continue; }

            int i = (int)cat;
            TL(tab, 56, 175 + i * 118, 340, 100);
            Image tabImg = Img(tab, c.S("masking_tape_label"), White, Image.Type.Sliced, false, true);
            tabImg.pixelsPerUnitMultiplier = 3f;
            tabFondos.Add(tabImg);

            RectTransform icon = Get(tab, tab, "Icono");
            TL(icon, 24, 22, 58, 58);
            Img(icon, c.library.GetCategoryIcon(cat), Ink, Image.Type.Simple, true);

            RectTransform nombre = Find(tab, "Nombre");
            if (nombre != null)
            {
                TL(nombre, 96, 14, 232, 44);
                Txt(nombre, c.marker, 33, Ink, TextAnchor.MiddleLeft, null, true, 18);
                Outline(nombre, Color.clear, Vector2.zero);
            }

            RectTransform sub = Find(tab, "Sub");
            if (sub != null)
            {
                TL(sub, 98, 58, 232, 24);
                Txt(sub, c.marker, 15, new Color(0.32f, 0.25f, 0.18f, 1f), TextAnchor.MiddleLeft, null, true, 9);
            }
        }

        // --- card grid ---
        if (grid != null)
        {
            TL(grid, 420, 285, 880, 640);
            var layout = Ensure<GridLayoutGroup>(grid.gameObject);
            layout.cellSize = new Vector2(204f, 280f);
            layout.spacing = new Vector2(14f, 16f);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            layout.padding = new RectOffset(4, 4, 8, 8);

            BuildCardTemplate(c, grid, so);
        }
        else
        {
            c.Warn("card container 'Tarjetas' missing");
        }

        // --- cash envelope: money ---
        RectTransform envelope = Get(panel, panel, "Sobre");
        TL(envelope, 1306, 128, 330, 200);
        Img(envelope, c.S("cash_envelope"), White, Image.Type.Simple);

        RectTransform label = Get(panel, panel, "SobreEtiqueta");
        TL(label, 1418, 164, 200, 38);
        Text labelText = Txt(label, c.marker, 28, Ink, TextAnchor.MiddleCenter, "DINERO", false, 0);

        if (dinero != null)
        {
            TL(dinero, 1406, 204, 224, 76);
            Txt(dinero, c.stencil, 48, Ink, TextAnchor.MiddleCenter, null, true, 22);
            Outline(dinero, Color.clear, Vector2.zero);
        }

        // --- notebook page: TU PERSONAJE ---
        if (estado != null)
        {
            TL(estado, 1312, 340, 344, 472);
            Img(estado, c.S("notebook_page"), White, Image.Type.Simple);

            RectTransform eTitulo = Find(estado, "Titulo");
            if (eTitulo != null)
            {
                TL(eTitulo, 74, 24, 250, 46);
                Txt(eTitulo, c.marker, 31, Ink, TextAnchor.MiddleLeft, "TU PERSONAJE", true, 18);
            }

            string[] rowIcons = { "vida", "velocidad", "salto", "dano", "municion", "objetos" };
            for (int i = 0; i < 6; i++)
            {
                float y = 100f + i * 56f;
                RectTransform et = Find(estado, "Et_" + i);
                RectTransform va = Find(estado, "Va_" + i);
                if (et != null)
                {
                    TL(et, 90, y, 160, 34);
                    Txt(et, c.marker, 19, Ink, TextAnchor.MiddleLeft, null, true, 12);
                }
                if (va != null)
                {
                    TL(va, 196, y, 126, 34);
                    Txt(va, c.stencil, 18, new Color(0.45f, 0.06f, 0.05f, 1f), TextAnchor.MiddleRight, null, true, 11);
                }

                RectTransform ic = Get(estado, estado, "Ic_" + i);
                TL(ic, 46, y + 1, 34, 34);
                Sprite sprite = i == 5 ? c.library.GetCategoryIcon(ShopCategory.Objetos) : c.library.GetItemIcon(rowIcons[i]);
                Img(ic, sprite, new Color(0.2f, 0.15f, 0.1f, 1f), Image.Type.Simple, true);
            }
        }

        // --- LISTO on the green wooden sign ---
        if (listo != null)
        {
            TL(listo, 1300, 822, 370, 116);
            Image signImg = Img(listo, c.S("green_sign"), White, Image.Type.Simple, false, true);
            var button = listo.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                button.targetGraphic = signImg;
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                colors.highlightedColor = White;
                colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                colors.selectedColor = colors.normalColor;
                colors.colorMultiplier = 1f;
                button.colors = colors;
            }

            RectTransform texto = Find(listo, "Texto");
            if (texto != null)
            {
                TL(texto, 26, 12, 240, 74);
                Txt(texto, c.marker, 60, Bone, TextAnchor.MiddleLeft, "LISTO", true, 24);
                Outline(texto, new Color(0.02f, 0.08f, 0.02f, 0.8f), new Vector2(2f, -2f));
            }

            RectTransform sub = Get(listo, listo, "Sub");
            TL(sub, 30, 80, 240, 26);
            Txt(sub, c.marker, 19, new Color(Bone.r, Bone.g, Bone.b, 0.9f), TextAnchor.MiddleLeft, "SIGUIENTE RONDA", true, 11);
        }

        // --- info line and bone close button ---
        if (info != null)
        {
            TL(info, 420, 884, 880, 44);
            Txt(info, c.marker, 26, new Color(1f, 0.93f, 0.7f, 1f), TextAnchor.MiddleLeft, null, true, 14);
            Outline(info, new Color(0.02f, 0.02f, 0.02f, 0.95f), new Vector2(2f, -2f));
        }

        if (hueso != null)
        {
            var boneBg = hueso.GetComponent<Image>();
            if (boneBg != null) boneBg.color = new Color(0.06f, 0.04f, 0.03f, 0.45f);
            RectTransform esc = Find(hueso, "Esc");
            if (esc != null) Txt(esc, c.stencil, 15, Bone, TextAnchor.MiddleCenter, null, false, 0);
        }

        // --- draw order ---
        var order = new List<RectTransform> { backdrop, board, header, titulo };
        foreach (Image t in tabFondos) order.Add(t.rectTransform);
        order.Add(grid); order.Add(envelope); order.Add(label); order.Add(dinero);
        order.Add(estado); order.Add(listo); order.Add(info); order.Add(hueso);
        foreach (RectTransform rt in order)
            if (rt != null) rt.SetAsLastSibling();

        // --- wiring ---
        SetRef(c, so, "moneyLabelText", labelText);
        SetColor(so, "tabOnColor", White);
        SetColor(so, "tabOffColor", new Color(0.66f, 0.62f, 0.56f, 1f));
        SetFloat(so, "tabOnScale", 1.06f);
        so.ApplyModifiedProperties();

        foreach (string p in new[] { "panel", "moneyText", "infoText", "titleText", "cardContainer", "cardTemplate", "gameManager", "moneySystem", "playerHealth", "cursorMano" })
            CheckRef(c, so, p);
        CheckArray(c, so, "tabButtons", 5);
        CheckArray(c, so, "tabFondos", 5);
        CheckArray(c, so, "statusValues", 6);

        EditorUtility.SetDirty(shopUi);
    }

    private static void BuildCardTemplate(Ctx c, RectTransform grid, SerializedObject shopSo)
    {
        RectTransform template = Find(grid, "CardTemplate");
        if (template == null) { c.Warn("CardTemplate missing"); return; }

        var card = template.GetComponent<ShopCard>();
        if (card == null) { c.Warn("CardTemplate has no ShopCard"); return; }

        template.localScale = Vector3.one;
        template.sizeDelta = new Vector2(204f, 280f);
        Image fondo = Img(template, c.S("polaroid_frame"), White, Image.Type.Simple, false, true);

        RectTransform icono = Get(template, template, "Icono");
        TL(icono, 52, 54, 100, 100);
        Image iconoImg = Img(icono, null, White, Image.Type.Simple, true);

        RectTransform padlock = Get(template, template, "Candado");
        TL(padlock, 72, 34, 60, 60);
        Image padlockImg = Img(padlock, c.library.GetHudIcon("lock"), White, Image.Type.Simple, true);

        RectTransform nivel = Find(template, "Nivel");
        if (nivel != null)
        {
            TL(nivel, 13, 27, 178, 22);
            Txt(nivel, c.stencil, 14, Amber, TextAnchor.MiddleCenter, null, true, 9);
            Outline(nivel, Color.clear, Vector2.zero);
        }

        RectTransform desc = Find(template, "Desc");
        if (desc != null)
        {
            TL(desc, 20, 160, 164, 58);
            Txt(desc, c.marker, 13, new Color(0.86f, 0.83f, 0.76f, 1f), TextAnchor.UpperCenter, null, true, 9);
            Outline(desc, Color.clear, Vector2.zero);
        }

        RectTransform nombre = Find(template, "Nombre");
        if (nombre != null)
        {
            TL(nombre, 12, 222, 180, 34);
            Txt(nombre, c.marker, 17, Ink, TextAnchor.MiddleCenter, null, true, 10);
            Outline(nombre, Color.clear, Vector2.zero);
        }

        RectTransform precio = Find(template, "Precio");
        if (precio != null)
        {
            TL(precio, 12, 250, 180, 28);
            Txt(precio, c.stencil, 22, new Color(0.1f, 0.42f, 0.14f, 1f), TextAnchor.MiddleCenter, null, true, 12);
            Outline(precio, Color.clear, Vector2.zero);
        }

        // Red tape PROXIMAMENTE across the photo (locked items).
        RectTransform tape = Get(template, template, "Cinta");
        TLc(tape, 102, 126, 218, 44);
        Image tapeImg = Img(tape, c.S("red_tape"), White, Image.Type.Sliced);
        tapeImg.pixelsPerUnitMultiplier = 3f;
        tape.localRotation = Quaternion.Euler(0f, 0f, -4f);

        RectTransform tapeText = Get(tape, tape, "Texto");
        Stretch(tapeText, 12, 2, 12, 2);
        Txt(tapeText, c.stencil, 21, new Color(0.98f, 0.95f, 0.9f, 1f), TextAnchor.MiddleCenter, "PRÓXIMAMENTE", true, 11);
        Outline(tapeText, new Color(0.25f, 0f, 0f, 0.8f), new Vector2(1.5f, -1.5f));

        // Stamp for items bought at max level.
        RectTransform stamp = Get(template, template, "Sello");
        TLc(stamp, 102, 130, 176, 50);
        stamp.localRotation = Quaternion.Euler(0f, 0f, 10f);
        Txt(stamp, c.stencil, 30, new Color(0.78f, 0.1f, 0.08f, 0.95f), TextAnchor.MiddleCenter, "COMPRADO", true, 16);
        Outline(stamp, new Color(0.78f, 0.1f, 0.08f, 0.5f), new Vector2(1.5f, -1.5f));

        // Draw order inside the card.
        foreach (RectTransform rt in new[] { icono, padlock, nivel, desc, nombre, precio, tape, stamp })
            if (rt != null) rt.SetAsLastSibling();

        // Only the polaroid receives pointer events.
        foreach (Graphic g in template.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = g.gameObject == template.gameObject;

        tape.gameObject.SetActive(false);
        stamp.gameObject.SetActive(false);
        padlock.gameObject.SetActive(true);

        var cardSo = new SerializedObject(card);
        SetRef(c, cardSo, "fondo", fondo);
        SetRef(c, cardSo, "marcaCandado", padlockImg);
        SetRef(c, cardSo, "nombre", nombre != null ? nombre.GetComponent<Text>() : null);
        SetRef(c, cardSo, "descripcion", desc != null ? desc.GetComponent<Text>() : null);
        SetRef(c, cardSo, "precio", precio != null ? precio.GetComponent<Text>() : null);
        SetRef(c, cardSo, "nivel", nivel != null ? nivel.GetComponent<Text>() : null);
        SetRef(c, cardSo, "iconLibrary", c.library);
        SetRef(c, cardSo, "icono", iconoImg);
        SetRef(c, cardSo, "cintaBloqueado", tape.gameObject);
        SetRef(c, cardSo, "sello", stamp.gameObject);
        cardSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(card);

        // Safety: the template stays inactive (ShopUI instantiates it per item).
        template.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Hierarchy helpers
    // ------------------------------------------------------------------

    /// <summary>Finds a descendant by name (depth-first). maxDepth 0 = unlimited, 1 = direct children.</summary>
    private static RectTransform Find(Transform scope, string name, int maxDepth = 0)
    {
        return FindRec(scope, name, maxDepth, 1);
    }

    private static RectTransform FindRec(Transform scope, string name, int maxDepth, int depth)
    {
        foreach (Transform child in scope)
        {
            if (child.name == name) return child as RectTransform;
            if (maxDepth == 0 || depth < maxDepth)
            {
                RectTransform deeper = FindRec(child, name, maxDepth, depth + 1);
                if (deeper != null) return deeper;
            }
        }
        return null;
    }

    /// <summary>Finds a named descendant of scope or creates it under parent.</summary>
    private static RectTransform Get(Transform scope, Transform parent, string name)
    {
        RectTransform existing = Find(scope, name);
        if (existing != null)
        {
            if (existing.parent != parent) existing.SetParent(parent, false);
            return existing;
        }

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Reparent(RectTransform rt, Transform parent)
    {
        if (rt.parent != parent) rt.SetParent(parent, false);
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        return comp != null ? comp : go.AddComponent<T>();
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    /// <summary>Top-left anchored: x,y measured from the parent's top-left corner (y grows downwards).</summary>
    private static void TL(RectTransform rt, float x, float y, float w, float h)
    {
        Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
    }

    /// <summary>Same as TL but x,y is the CENTER of the element (pivot centre, handy for rotated pieces).</summary>
    private static void TLc(RectTransform rt, float cx, float cy, float w, float h)
    {
        Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cx, -cy), new Vector2(w, h));
    }

    private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    // ------------------------------------------------------------------
    // Graphic helpers
    // ------------------------------------------------------------------

    private static Image Img(RectTransform rt, Sprite sprite, Color color, Image.Type type, bool preserveAspect = false, bool raycast = false)
    {
        Image img = Ensure<Image>(rt.gameObject);
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.preserveAspect = preserveAspect;
        img.raycastTarget = raycast;
        img.pixelsPerUnitMultiplier = 1f;
        img.enabled = sprite != null || color.a > 0f;
        return img;
    }

    private static Text Txt(RectTransform rt, Font font, int size, Color color, TextAnchor anchor, string text, bool bestFit, int minSize)
    {
        Text t = Ensure<Text>(rt.gameObject);
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Normal;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = bestFit ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = bestFit;
        t.resizeTextMinSize = bestFit ? minSize : 10;
        t.resizeTextMaxSize = size;
        t.lineSpacing = 0.9f;
        if (text != null) t.text = text;
        return t;
    }

    private static void Outline(RectTransform rt, Color color, Vector2 distance)
    {
        Outline outline = rt.GetComponent<Outline>();
        if (color.a <= 0f)
        {
            if (outline != null) outline.enabled = false;
            return;
        }

        if (outline == null) outline = rt.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    // ------------------------------------------------------------------
    // Serialized wiring
    // ------------------------------------------------------------------

    private static void SetRef(Ctx c, SerializedObject so, string prop, Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        string owner = so.targetObject.GetType().Name;
        if (p == null) { c.Warn(owner + "." + prop + " does not exist"); return; }

        p.objectReferenceValue = value;
        if (value == null) c.Warn(owner + "." + prop + " = null");
        else c.Info(owner + "." + prop + " = " + value.name + " (" + value.GetType().Name + ")");
    }

    private static void SetColor(SerializedObject so, string prop, Color color)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.colorValue = color;
    }

    private static void SetFloat(SerializedObject so, string prop, float value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.floatValue = value;
    }

    private static void CheckRef(Ctx c, SerializedObject so, string prop)
    {
        SerializedProperty p = so.FindProperty(prop);
        string owner = so.targetObject.GetType().Name;
        if (p == null) { c.Warn(owner + "." + prop + " does not exist"); return; }

        Object v = p.objectReferenceValue;
        if (v == null) c.Warn(owner + "." + prop + " is NULL");
        else c.Info(owner + "." + prop + " ok -> " + v.name);
    }

    private static void CheckArray(Ctx c, SerializedObject so, string prop, int expected)
    {
        SerializedProperty p = so.FindProperty(prop);
        string owner = so.targetObject.GetType().Name;
        if (p == null || !p.isArray) { c.Warn(owner + "." + prop + " is not an array"); return; }

        int nulls = 0;
        for (int i = 0; i < p.arraySize; i++)
            if (p.GetArrayElementAtIndex(i).objectReferenceValue == null) nulls++;

        if (p.arraySize != expected || nulls > 0) c.Warn(owner + "." + prop + " size=" + p.arraySize + " nulls=" + nulls);
        else c.Info(owner + "." + prop + " ok (" + expected + " refs)");
    }
}
