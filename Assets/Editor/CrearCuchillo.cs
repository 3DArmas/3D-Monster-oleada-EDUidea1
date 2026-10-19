using UnityEngine;
using UnityEditor;

/// <summary>Crea el prefab del cuchillo (malla por primitivas) y sus materiales.</summary>
public static class CrearCuchillo
{
    public static string Execute()
    {
        // ---------- Materiales ----------
        var hoja = CrearMaterial("Assets/Material/CuchilloHoja.mat", new Color(0.62f, 0.65f, 0.69f),
                                 metallic: 1f, smoothness: 0.8f, emission: Color.black);
        var mango = CrearMaterial("Assets/Material/CuchilloMango.mat", new Color(0.13f, 0.09f, 0.06f),
                                  metallic: 0f, smoothness: 0.3f, emission: Color.black);

        // ---------- Jerarquia ----------
        // La hoja apunta hacia -Z local, igual que el canon del AK74 (SPANWPOINT en -0.46),
        // para que el WeaponSwitcher lo coloque en la mano sin rotaciones raras.
        var root = new GameObject("Cuchillo");

        CrearCaja(root, "Hoja", new Vector3(0f, 0f, -0.105f), new Vector3(0.012f, 0.042f, 0.19f), Vector3.zero, hoja);
        CrearCaja(root, "Punta", new Vector3(0f, 0.013f, -0.225f), new Vector3(0.012f, 0.042f, 0.07f), new Vector3(-28f, 0f, 0f), hoja);
        CrearCaja(root, "Guarda", new Vector3(0f, 0f, 0.002f), new Vector3(0.055f, 0.012f, 0.016f), Vector3.zero, hoja);
        CrearCaja(root, "Mango", new Vector3(0f, 0f, 0.062f), new Vector3(0.026f, 0.032f, 0.115f), Vector3.zero, mango);
        CrearCaja(root, "Pomo", new Vector3(0f, 0f, 0.128f), new Vector3(0.032f, 0.036f, 0.018f), Vector3.zero, mango);

        // Ningun collider: el cuchillo va pegado a la camara y no debe frenar
        // ni las balas ni el propio raycast del cuchillo.
        foreach (var col in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        root.AddComponent<KnifeWeapon>();

        // ---------- Guardar prefab ----------
        const string ruta = "Assets/Character/Weapons/Cuchillo.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, ruta);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        return prefab != null ? "OK " + ruta : "FALLO al guardar el prefab";
    }

    private static Material CrearMaterial(string ruta, Color color, float metallic, float smoothness, Color emission)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, ruta);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void CrearCaja(GameObject padre, string nombre, Vector3 pos, Vector3 escala, Vector3 rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre.transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(rot);
        go.transform.localScale = escala;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }
}
