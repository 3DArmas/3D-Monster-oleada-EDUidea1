using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Construye la escena del menu de inicio (Assets/Scenes/MainMenu.unity) con el mismo
/// estilo que el HUD del juego: tipografia Black Ops One / Permanent Marker, hueso,
/// tinta y rojo spray.
/// </summary>
public static class CrearMenuInicio
{
    private static readonly Color Bone = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color Ink = new Color(0.10f, 0.075f, 0.055f, 1f);
    private static readonly Color Red = new Color(0.74f, 0.05f, 0.04f, 1f);
    private static readonly Color Amber = new Color(0.96f, 0.76f, 0.25f, 1f);
    private static readonly Color Dark = new Color(0.045f, 0.04f, 0.04f, 1f);

    private static Font stencil;
    private static Font marker;

    public static string Execute()
    {
        stencil = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/BlackOpsOne-Regular.ttf");
        marker = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/PermanentMarker-Regular.ttf");

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------------- Camara de fondo ----------------
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Dark;
        cam.cullingMask = 0;
        cam.orthographic = true;
        camGo.AddComponent<AudioListener>();

        // ---------------- EventSystem (Input System, no el antiguo) ----------------
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        var modulo = esGo.AddComponent<InputSystemUIInputModule>();
        modulo.AssignDefaultActions();

        // ---------------- Canvas ----------------
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var canvasRt = (RectTransform)canvasGo.transform;

        // Fondo + bandas rojas arriba y abajo
        Estirar(Img(canvasRt, "Fondo", Dark).rectTransform);
        Banda(Img(canvasRt, "BandaSuperior", Red).rectTransform, true);
        Banda(Img(canvasRt, "BandaInferior", Red).rectTransform, false);

        // Titulo
        var titulo = Txt(canvasRt, "Titulo", stencil, 118, Bone, TextAnchor.MiddleCenter, "PROJECT OUTBREAK");
        Colocar(titulo.rectTransform, new Vector2(0f, 300f), new Vector2(1600f, 160f));

        var linea = Img(canvasRt, "LineaTitulo", Red);
        Colocar(linea.rectTransform, new Vector2(0f, 205f), new Vector2(520f, 6f));

        var subtitulo = Txt(canvasRt, "Subtitulo", marker, 40, Amber, TextAnchor.MiddleCenter, "OLEADA DE ZOMBIS");
        Colocar(subtitulo.rectTransform, new Vector2(0f, 155f), new Vector2(1200f, 60f));

        // ---------------- Panel principal (botones) ----------------
        var panelPrincipal = RT(canvasRt, "PanelPrincipal");
        Colocar(panelPrincipal, new Vector2(0f, -110f), new Vector2(700f, 420f));

        Text texto;
        var btnEmpezar = Boton(panelPrincipal, "BotonEmpezar", "EMPEZAR", 120f, out texto);
        var btnConfig = Boton(panelPrincipal, "BotonConfiguracion", "CONFIGURACION", 0f, out texto);
        var btnSalir = Boton(panelPrincipal, "BotonSalir", "SALIDA", -120f, out texto);

        // ---------------- Panel de configuracion ----------------
        var panelConfig = Img(canvasRt, "PanelConfiguracion", new Color(Ink.r, Ink.g, Ink.b, 0.94f));
        Colocar(panelConfig.rectTransform, new Vector2(0f, -60f), new Vector2(920f, 680f));

        var cfgRt = panelConfig.rectTransform;

        var tituloCfg = Txt(cfgRt, "TituloConfiguracion", stencil, 62, Bone, TextAnchor.MiddleCenter, "CONFIGURACION");
        Colocar(tituloCfg.rectTransform, new Vector2(0f, 262f), new Vector2(800f, 80f));

        var lineaCfg = Img(cfgRt, "LineaConfiguracion", Red);
        Colocar(lineaCfg.rectTransform, new Vector2(0f, 214f), new Vector2(300f, 5f));

        var etiquetaVolumen = Txt(cfgRt, "EtiquetaVolumen", marker, 32, Bone, TextAnchor.MiddleLeft, "VOLUMEN");
        Colocar(etiquetaVolumen.rectTransform, new Vector2(-190f, 148f), new Vector2(400f, 44f));

        Text valorVolumen;
        var sliderVolumen = CrearSlider(cfgRt, "SliderVolumen", new Vector2(80f, 148f), 480f, out valorVolumen);

        var etiquetaSens = Txt(cfgRt, "EtiquetaSensibilidad", marker, 32, Bone, TextAnchor.MiddleLeft, "SENSIBILIDAD");
        Colocar(etiquetaSens.rectTransform, new Vector2(-160f, 44f), new Vector2(460f, 44f));

        Text valorSensibilidad;
        var sliderSensibilidad = CrearSlider(cfgRt, "SliderSensibilidad", new Vector2(80f, 44f), 480f, out valorSensibilidad);

        var togglePantalla = CrearToggle(cfgRt, "TogglePantallaCompleta", "PANTALLA COMPLETA", new Vector2(0f, -60f));

        var btnVolver = Boton(cfgRt, "BotonVolver", "VOLVER", -232f, out texto);
        btnVolver.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 84f);

        panelConfig.gameObject.SetActive(false);

        // Version
        var version = Txt(canvasRt, "Version", marker, 24, new Color(Bone.r, Bone.g, Bone.b, 0.6f), TextAnchor.LowerLeft, "Project Outbreak · v0.3");
        var versionRt = version.rectTransform;
        versionRt.anchorMin = versionRt.anchorMax = new Vector2(0f, 0f);
        versionRt.pivot = new Vector2(0f, 0f);
        versionRt.anchoredPosition = new Vector2(40f, 28f);
        versionRt.sizeDelta = new Vector2(600f, 40f);

        // ---------------- Componente que da vida al menu ----------------
        var managerGo = new GameObject("MenuManager");
        var menu = managerGo.AddComponent<MainMenu>();

        var so = new SerializedObject(menu);
        so.FindProperty("panelPrincipal").objectReferenceValue = panelPrincipal.gameObject;
        so.FindProperty("panelConfiguracion").objectReferenceValue = panelConfig.gameObject;
        so.FindProperty("botonEmpezar").objectReferenceValue = btnEmpezar;
        so.FindProperty("botonConfiguracion").objectReferenceValue = btnConfig;
        so.FindProperty("botonSalir").objectReferenceValue = btnSalir;
        so.FindProperty("botonVolver").objectReferenceValue = btnVolver;
        so.FindProperty("volumenSlider").objectReferenceValue = sliderVolumen;
        so.FindProperty("volumenValor").objectReferenceValue = valorVolumen;
        so.FindProperty("sensibilidadSlider").objectReferenceValue = sliderSensibilidad;
        so.FindProperty("sensibilidadValor").objectReferenceValue = valorSensibilidad;
        so.FindProperty("pantallaCompletaToggle").objectReferenceValue = togglePantalla;
        so.FindProperty("versionText").objectReferenceValue = version;
        so.FindProperty("escenaJuego").stringValue = "Warehouse";
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---------------- Guardar escena ----------------
        const string ruta = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena, ruta);

        // ---------------- Build settings: el menu primero ----------------
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ruta, true),
            new EditorBuildSettingsScene("Assets/Scenes/Warehouse.unity", true),
        };

        return "Menu creado en " + ruta + " (fuentes: " + (stencil != null ? "ok" : "FALTA") + "/" + (marker != null ? "ok" : "FALTA") + ")";
    }

    // ------------------------------------------------------------------
    // Utilidades de UI
    // ------------------------------------------------------------------

    private static RectTransform RT(Transform padre, string nombre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    private static Image Img(Transform padre, string nombre, Color color)
    {
        var rt = RT(padre, nombre);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text Txt(Transform padre, string nombre, Font fuente, int tamano, Color color, TextAnchor anclaje, string texto)
    {
        var rt = RT(padre, nombre);
        var txt = rt.gameObject.AddComponent<Text>();
        txt.font = fuente;
        txt.fontSize = tamano;
        txt.color = color;
        txt.alignment = anclaje;
        txt.text = texto;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        return txt;
    }

    /// <summary>Centra un elemento en su padre con un tamano fijo.</summary>
    private static void Colocar(RectTransform rt, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    /// <summary>Banda horizontal pegada arriba (o abajo) de la pantalla.</summary>
    private static void Banda(RectTransform rt, bool arriba)
    {
        float y = arriba ? 1f : 0f;
        rt.anchorMin = new Vector2(0f, y);
        rt.anchorMax = new Vector2(1f, y);
        rt.pivot = new Vector2(0.5f, y);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 8f);
    }

    private static Button Boton(Transform padre, string nombre, string etiqueta, float y, out Text texto)
    {
        var rt = RT(padre, nombre);
        Colocar(rt, new Vector2(0f, y), new Vector2(560f, 92f));

        var fondo = rt.gameObject.AddComponent<Image>();
        fondo.color = new Color(Ink.r, Ink.g, Ink.b, 0.9f);

        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = fondo;
        var colores = boton.colors;
        colores.normalColor = Color.white;
        colores.highlightedColor = new Color(1f, 0.78f, 0.72f, 1f);
        colores.pressedColor = new Color(1f, 0.55f, 0.5f, 1f);
        colores.selectedColor = Color.white;
        colores.fadeDuration = 0.08f;
        boton.colors = colores;

        // Acento rojo a la izquierda
        var acentoRt = Img(rt, "Acento", Red).rectTransform;
        acentoRt.anchorMin = new Vector2(0f, 0f);
        acentoRt.anchorMax = new Vector2(0f, 1f);
        acentoRt.pivot = new Vector2(0f, 0.5f);
        acentoRt.anchoredPosition = new Vector2(10f, 0f);
        acentoRt.sizeDelta = new Vector2(6f, -18f);

        texto = Txt(rt, "Texto", stencil, 46, Bone, TextAnchor.MiddleCenter, etiqueta);
        Estirar(texto.rectTransform);

        return boton;
    }

    private static Slider CrearSlider(Transform padre, string nombre, Vector2 posicion, float ancho, out Text valor)
    {
        var rt = RT(padre, nombre);
        Colocar(rt, posicion, new Vector2(ancho, 34f));

        var slider = rt.gameObject.AddComponent<Slider>();

        var fondoRt = Img(rt, "Fondo", new Color(1f, 1f, 1f, 0.16f)).rectTransform;
        fondoRt.anchorMin = new Vector2(0f, 0.35f);
        fondoRt.anchorMax = new Vector2(1f, 0.65f);
        fondoRt.sizeDelta = Vector2.zero;

        var areaRelleno = RT(rt, "Fill Area");
        areaRelleno.anchorMin = new Vector2(0f, 0.35f);
        areaRelleno.anchorMax = new Vector2(1f, 0.65f);
        areaRelleno.sizeDelta = Vector2.zero;

        var rellenoRt = Img(areaRelleno, "Fill", Red).rectTransform;
        rellenoRt.anchorMin = Vector2.zero;
        rellenoRt.anchorMax = Vector2.one;
        rellenoRt.sizeDelta = Vector2.zero;

        var areaMando = RT(rt, "Handle Slide Area");
        areaMando.anchorMin = Vector2.zero;
        areaMando.anchorMax = Vector2.one;
        areaMando.sizeDelta = new Vector2(-20f, 0f);

        var mando = Img(areaMando, "Handle", Bone);
        var mandoRt = mando.rectTransform;
        mandoRt.anchorMin = new Vector2(0f, 0f);
        mandoRt.anchorMax = new Vector2(0f, 1f);
        mandoRt.sizeDelta = new Vector2(20f, 6f);

        slider.fillRect = rellenoRt;
        slider.handleRect = mandoRt;
        slider.targetGraphic = mando;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;

        valor = Txt(rt, "Valor", marker, 28, Amber, TextAnchor.MiddleLeft, "");
        Colocar(valor.rectTransform, new Vector2(ancho * 0.5f + 80f, 0f), new Vector2(180f, 40f));

        return slider;
    }

    private static Toggle CrearToggle(Transform padre, string nombre, string etiqueta, Vector2 posicion)
    {
        var rt = RT(padre, nombre);
        Colocar(rt, posicion, new Vector2(620f, 52f));

        var toggle = rt.gameObject.AddComponent<Toggle>();

        var fondo = Img(rt, "Fondo", new Color(1f, 1f, 1f, 0.16f));
        var fondoRt = fondo.rectTransform;
        fondoRt.anchorMin = new Vector2(0f, 0.5f);
        fondoRt.anchorMax = new Vector2(0f, 0.5f);
        fondoRt.pivot = new Vector2(0f, 0.5f);
        fondoRt.anchoredPosition = Vector2.zero;
        fondoRt.sizeDelta = new Vector2(40f, 40f);

        var marcaRt = Img(fondoRt, "Checkmark", Red).rectTransform;
        marcaRt.anchorMin = Vector2.zero;
        marcaRt.anchorMax = Vector2.one;
        marcaRt.sizeDelta = new Vector2(-14f, -14f);
        marcaRt.anchoredPosition = Vector2.zero;

        var texto = Txt(rt, "Etiqueta", marker, 32, Bone, TextAnchor.MiddleLeft, etiqueta);
        var textoRt = texto.rectTransform;
        textoRt.anchorMin = new Vector2(0f, 0.5f);
        textoRt.anchorMax = new Vector2(1f, 0.5f);
        textoRt.pivot = new Vector2(0f, 0.5f);
        textoRt.anchoredPosition = new Vector2(62f, 0f);
        textoRt.sizeDelta = new Vector2(-62f, 44f);

        toggle.targetGraphic = fondo;
        toggle.graphic = marcaRt.GetComponent<Image>();
        toggle.isOn = true;

        return toggle;
    }
}
