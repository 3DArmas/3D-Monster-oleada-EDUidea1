using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>Ojos blancos brillantes (suaves) del zombi + mira verde simple.</summary>
public static class ConfigOjosYMira
{
    public static string Execute()
    {
        string log = "";

        // ---------------- Material de los ojos ----------------
        const string matPath = "Assets/Material/OjosZombie.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetColor("_BaseColor", Color.black);          // el color lo pone la emision
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.5f);
        mat.EnableKeyword("_EMISSION");
        // Blanco suave: 1.05 queda justo por encima del umbral de bloom (0.9) del
        // perfil de post-proceso, asi brilla sin deslumbrar.
        mat.SetColor("_EmissionColor", new Color(0.95f, 0.97f, 1f) * 1.05f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        log += "material ojos OK; ";

        // ---------------- Ojos en el prefab del zombi ----------------
        const string zombiePath = "Assets/Character/Zombie/Prefabs/Zombie.prefab";
        var mallaEsfera = ObtenerMallaEsfera();

        var contenido = PrefabUtility.LoadPrefabContents(zombiePath);
        try
        {
            Transform head = Buscar(contenido.transform, "mixamorig:Head");
            Transform ojoIzq = Buscar(contenido.transform, "mixamorig:LeftEye");
            Transform ojoDer = Buscar(contenido.transform, "mixamorig:RightEye");

            if (head == null)
            {
                log += "NO se encontro el hueso de la cabeza; ";
            }
            else
            {
                // Si ya existen (volver a ejecutar el script) se borran para no duplicar.
                Borrar(contenido.transform, "OjoBrillante_L");
                Borrar(contenido.transform, "OjoBrillante_R");

                // La posicion de cada hueso de ojo esta expresada en el espacio de la
                // cabeza, asi que sirve tal cual como localPosition de la esfera.
                Vector3 posIzq = ojoIzq != null ? ojoIzq.localPosition : new Vector3(-0.04f, 0.07f, 0.08f);
                Vector3 posDer = ojoDer != null ? ojoDer.localPosition : new Vector3(0.04f, 0.07f, 0.08f);

                // Un pelin por delante para que la esfera asome y se vea desde fuera.
                posIzq.z += 0.012f;
                posDer.z += 0.012f;

                CrearOjo(contenido.transform, head, "OjoBrillante_L", posIzq, mallaEsfera, mat);
                CrearOjo(contenido.transform, head, "OjoBrillante_R", posDer, mallaEsfera, mat);

                PrefabUtility.SaveAsPrefabAsset(contenido, zombiePath);
                log += "dos ojos creados; ";
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }

        // ---------------- Mira verde simple ----------------
        var crosshair = GameObject.Find("Canvas/HUD/Crosshair");
        if (crosshair == null)
        {
            log += "NO se encontro el Canvas/HUD/Crosshair; ";
        }
        else
        {
            Color verde = new Color(0.22f, 0.95f, 0.35f, 0.95f);
            foreach (var img in crosshair.GetComponentsInChildren<Image>(true))
            {
                img.color = verde;
                EditorUtility.SetDirty(img);
                log += img.name + " verde; ";
            }
            EditorUtility.SetDirty(crosshair);
        }

        return log;
    }

    // ------------------------------------------------------------------

    private static Mesh ObtenerMallaEsfera()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh malla = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return malla;
    }

    private static void CrearOjo(Transform raiz, Transform padre, string nombre, Vector3 localPos, Mesh malla, Material mat)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.022f;   // ~2 cm de diametro

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = malla;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
    }

    private static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }

    private static void Borrar(Transform raiz, string nombre)
    {
        var t = Buscar(raiz, nombre);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }
}
