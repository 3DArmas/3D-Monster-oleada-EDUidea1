using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outbreak.EditorTools
{
    /// <summary>
    /// Builds the night-time industrial warehouse arena (Warehouse.unity) from the
    /// RPG_FPS_game_assets_industrial pack plus a few primitive-built structures.
    ///
    /// The builder is idempotent: it deletes and rebuilds the "Warehouse_Map" root
    /// every time. Run it from the menu (Tools/Outbreak/Build Warehouse Map) or from
    /// batch mode: -executeMethod Outbreak.EditorTools.WarehouseMapBuilder.BuildFromCommandLine
    ///
    /// Layout (X = east, Z = north, player start near the origin, metres):
    ///   - Interior hall 34 x 34 (X, Z in [-17, 17]), perimeter walls 6.5 m high.
    ///   - North: steel mezzanine (deck at 3.1 m) with two staircases and a ladder.
    ///   - West: two long containers + a railed platform joined to the mezzanine.
    ///   - East: loading door (north-east), partition wall, merchant room (south-east).
    ///   - South-west: small walled room.
    ///   - Six wall breaches (zombie spawns) and a partial roof with a crane.
    /// The class is split in partial files: Shell (walls/roof), Interior, Lighting.
    /// </summary>
    public static partial class WarehouseMapBuilder
    {
        // ------------------------------------------------------------------
        // Layout constants (tweak here)
        // ------------------------------------------------------------------
        public const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
        public const string ScenePath = "Assets/Scenes/Warehouse.unity";
        public const string NavMeshPath = "Assets/Scenes/Warehouse/NavMesh-Warehouse.asset";
        public const string RootName = "Warehouse_Map";

        const string PackRoot = "Assets/RPG_FPS_game_assets_industrial/";
        const string MatDir = "Assets/Materials/Warehouse";
        const string TexDir = "Assets/Materials/Warehouse/Textures";

        const float Half = 17f;            // interior half size (hall is 34 x 34)
        const float WallH = 6.5f;          // perimeter wall height
        const float WallT = 0.6f;          // perimeter wall thickness
        const float FloorHalf = 23f;       // floor slab half size (includes the exterior yard)
        const float BreachW = 3.6f;        // width of a wall breach
        const float BreachH = 3.4f;        // height of the opening under the broken lintel

        const float DeckY = 3.1f;          // mezzanine / platform deck top
        const float MezzZ0 = 12f, MezzZ1 = 16f, MezzX0 = -13.6f, MezzX1 = 12.5f;
        const float ContainerZ0 = -9f, ContainerZ1 = 11f;   // long containers N-S span
        const float PartitionX = 11.9f;    // east partition / merchant room west wall
        const float PartitionH = 4.0f;
        const float MerchantNorthZ = -10.5f;
        const float SwRoomEastX = -9.5f, SwRoomNorthZ = -11f;

        // Player start and the six spawn points. Ground level, just inside the breaches.
        static readonly Vector3 PlayerStart = new Vector3(0f, 1.1f, -2f);
        static readonly Vector3[] SpawnPoints =
        {
            new Vector3(-14f, 0.05f, 16.2f),    // 1 NW corner breach (north wall)
            new Vector3(0f, 0.05f, 16.2f),      // 2 north wall centre (behind the mezzanine)
            new Vector3(-16.2f, 0.05f, -1.5f),  // 3 west wall middle
            new Vector3(16.2f, 0.05f, -3.5f),   // 4 east wall middle
            new Vector3(-1f, 0.05f, -16.2f),    // 5 south wall centre
            new Vector3(-13.2f, 0.05f, -15.2f)  // 6 south-west room
        };
        static readonly Vector3 ShopPosition = new Vector3(14.6f, 1.25f, -13.9f);

        static Transform _root;
        static Material _brick, _concrete, _steel, _steelDark, _yellow, _rust, _wood, _glass, _tarp, _floorMat;
        static Material _lampMat, _redMat, _tealMat, _grating, _hazard, _slats, _black;
        static Texture2D _brickTex, _concTex;
        static System.Random _rng;

        // ------------------------------------------------------------------
        // Entry points
        // ------------------------------------------------------------------
        [MenuItem("Tools/Outbreak/Build Warehouse Map")]
        public static void BuildFromMenu() { Build(); }

        /// <summary>Batch-mode entry point (-executeMethod).</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("[WarehouseMapBuilder] BUILD OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[WarehouseMapBuilder] BUILD FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the map from edit mode.");

            _rng = new System.Random(20261002);
            EnsureFolder("Assets/Materials");
            EnsureFolder(MatDir);
            EnsureFolder(TexDir);
            EnsureFolder("Assets/Scenes/Warehouse");

            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                    throw new InvalidOperationException("Could not copy " + SourceScenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Rebuild from scratch: remove the previous root.
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == RootName) UnityEngine.Object.DestroyImmediate(go);

            CreateMaterials();

            var rootGo = new GameObject(RootName);
            _root = rootGo.transform;

            BuildFloorAndShell();        // T1
            BuildRoofAndCrane();         // T1/T4
            BuildInterior();             // T2
            BuildLightsAndAtmosphere();  // T4 (lights, fog, volume, sky)

            MarkStatic(_root);

            WireGameplay(scene);         // T3 (spawn points, shop, player, old arena)
            BakeNavMesh();               // T3

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[WarehouseMapBuilder] Scene saved: " + ScenePath);
        }

        // ------------------------------------------------------------------
        // Helpers: folders, materials, textures
        // ------------------------------------------------------------------
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Texture2D LoadTex(string packRelative)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(PackRoot + packRelative);
            if (t == null) Debug.LogWarning("[WarehouseMapBuilder] Missing texture " + packRelative);
            return t;
        }

        static Material M(string name, Color color, float metallic, float smoothness,
                          Color? emission = null, Texture tex = null, Vector2? tiling = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.SetTexture("_BaseMap", tex);
            if (tex != null)
            {
                Vector2 t = tiling ?? Vector2.one;
                m.SetTextureScale("_BaseMap", t);
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static string Key(float v) { return (Mathf.Round(v * 2f) / 2f).ToString("0.0", CultureInfo.InvariantCulture); }

        /// <summary>A tinted, tiled copy of a pack texture (one asset per distinct tiling).</summary>
        static Material Tiled(string baseName, Texture2D tex, Color tint, float sx, float sy, float smooth)
        {
            sx = Mathf.Max(0.5f, Mathf.Round(sx * 2f) / 2f);
            sy = Mathf.Max(0.5f, Mathf.Round(sy * 2f) / 2f);
            return M(baseName + "_" + Key(sx) + "x" + Key(sy), tint, 0f, smooth, null, tex, new Vector2(sx, sy));
        }

        static Texture2D GenTexture(string fileName, int size, Func<int, int, Color> pixel)
        {
            string path = TexDir + "/" + fileName;
            if (!File.Exists(path))
            {
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        t.SetPixel(x, y, pixel(x, y));
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void CreateMaterials()
        {
            _brickTex = LoadTex("Textures/Concrete_wall/UNIConcrete_walls/UNIConcrete_wall_v3/UNIConcrete_wall_v3.tga");
            _concTex = LoadTex("Textures/Concrete_wall/UNIConcrete_walls/UNIConcrete_wall_v1/UNIConcrete_wall_v1.tga");
            Texture2D asphaltTex = LoadTex("Textures/Asphalt/Seamless_asphalt_v1/Seamless_asphalt_v1.tga");

            _brick = Tiled("WH_Brick", _brickTex, new Color(0.62f, 0.55f, 0.52f), 8, 2, 0.1f);
            _concrete = Tiled("WH_Concrete", _concTex, new Color(0.55f, 0.55f, 0.55f), 4, 1, 0.1f);
            _floorMat = Tiled("WH_Floor", asphaltTex, new Color(1f, 1f, 1f), 9, 9, 0.62f);

            _steel = M("WH_Steel", new Color(0.22f, 0.24f, 0.27f), 0.7f, 0.35f);
            _steelDark = M("WH_SteelDark", new Color(0.08f, 0.09f, 0.1f), 0.7f, 0.3f);
            _yellow = M("WH_SafetyYellow", new Color(0.85f, 0.62f, 0.05f), 0.2f, 0.35f);
            _rust = M("WH_Rust", new Color(0.33f, 0.16f, 0.09f), 0.5f, 0.2f);
            _wood = M("WH_Wood", new Color(0.28f, 0.19f, 0.11f), 0f, 0.15f);
            _black = M("WH_Black", new Color(0.03f, 0.03f, 0.035f), 0.2f, 0.2f);
            _glass = M("WH_Glass", new Color(0.04f, 0.07f, 0.1f), 0.1f, 0.9f, new Color(0.02f, 0.05f, 0.09f));
            _tarp = M("WH_Tarp", new Color(0.12f, 0.2f, 0.17f), 0f, 0.1f);
            _lampMat = M("WH_LampOrange", new Color(1f, 0.55f, 0.15f), 0f, 0.5f, new Color(2.6f, 1.15f, 0.25f));
            _redMat = M("WH_LampRed", new Color(1f, 0.1f, 0.08f), 0f, 0.5f, new Color(3f, 0.1f, 0.05f));
            _tealMat = M("WH_LampTeal", new Color(0.1f, 0.9f, 0.8f), 0f, 0.5f, new Color(0.2f, 2.6f, 2.3f));

            Texture2D gratingTex = GenTexture("WH_Grating.png", 64, (x, y) =>
            {
                bool line = (x % 8 < 2) || (y % 8 < 2);
                float n = 0.9f + 0.1f * (float)Math.Sin(x * 12.9898 + y * 78.233);
                return line ? new Color(0.30f, 0.30f, 0.32f) * n : new Color(0.07f, 0.07f, 0.08f);
            });
            _grating = M("WH_Grating", Color.white, 0.6f, 0.3f, null, gratingTex, new Vector2(1, 1));

            Texture2D hazardTex = GenTexture("WH_Hazard.png", 64, (x, y) =>
                ((x + y) / 16) % 2 == 0 ? new Color(0.9f, 0.68f, 0.05f) : new Color(0.04f, 0.04f, 0.04f));
            _hazard = M("WH_Hazard", Color.white, 0f, 0.3f, null, hazardTex, new Vector2(1, 1));

            Texture2D slatTex = GenTexture("WH_Slats.png", 32, (x, y) =>
                (y % 8 < 2) ? new Color(0.05f, 0.05f, 0.05f) : new Color(0.32f, 0.28f, 0.25f));
            _slats = M("WH_Slats", Color.white, 0.6f, 0.3f, null, slatTex, new Vector2(1, 1));
        }

        // ------------------------------------------------------------------
        // Helpers: groups, primitives, prefabs
        // ------------------------------------------------------------------
        static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            return go.transform;
        }

        /// <summary>Box primitive. Colliders are off by default (decor); pass collider = true for blockers.</summary>
        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat,
                              bool collider = false, Vector3? euler = null, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Cylinder primitive (vertical by default). Solid cylinders get a convex MeshCollider.</summary>
        static GameObject Cyl(Transform parent, string name, Vector3 pos, float radius, float height, Material mat,
                              bool collider = false, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = true;
            }
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            bool init = false;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!init) { b = r.bounds; init = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Bounds of all renderers under root, expressed in root local space.</summary>
        static Bounds LocalBounds(Transform root)
        {
            bool init = false;
            var b = new Bounds();
            Matrix4x4 w2l = root.worldToLocalMatrix;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                Bounds lb = r.localBounds;
                Matrix4x4 m = w2l * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = lb.center + Vector3.Scale(lb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (!init) { b = new Bounds(p, Vector3.zero); init = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Replaces all colliders under root with a single BoxCollider fitted to the renderers.</summary>
        static void FitBoxCollider(GameObject root)
        {
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            Bounds b = LocalBounds(root.transform);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
        }

        static readonly Dictionary<string, string> Prefabs = new Dictionary<string, string>
        {
            { "cont", "Containers/Cargo_container_v1/Cargo_container_v1_LD1close" },
            { "cont2", "Containers/Cargo_container_v1/Cargo_container_v1_LD2close" },
            { "barrel_rust", "Barrels/Barrel_v1/Barrel_v1_LD1" },
            { "barrel_blue4", "Barrels/Barrel_v2/Barrel_v2_quadro" },
            { "barrel_blue1", "Barrels/Barrel_v2/Barrel_v2_single" },
            { "barrel_v3_4", "Barrels/Barrel_v3/Barrel_v3_quadro" },
            { "box_long", "Boxes/Wooden_box_v1/Wooden_box_v1_LD1" },
            { "box", "Boxes/Wooden_box_v1/Wooden_box_v1_LD1square" },
            { "box2", "Boxes/Wooden_box_v1/Wooden_box_v1_LD2square" },
            { "pallet_set", "Other_props/Palets/Palet_v1/Palet_v1_set" },
            { "pallet", "Other_props/Palets/Palet_v1/Palet_v1_single" },
            { "bags", "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_1" },
            { "bags2", "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_2" },
            { "dumpster", "Dumpsters/Dumpsters_v1/Dumpsters_v1_garbadge" },
            { "dumpster_empty", "Dumpsters/Dumpsters_v1/Dumpsters_v1_empty" },
            { "generator", "Other_props/Generators/Generator_v1/Generator_v1" },
            { "ebox3", "Other_props/Electric_box/Electric_box_v3/Electric_box_v3" },
            { "ebox1", "Other_props/Electric_box/Electric_box_v1/Electric_box_v1" },
            { "ebox2", "Other_props/Electric_box/Electric_box_v2/Electric_box_v2" },
            { "conditioner", "Other_props/Conditioners/Conditioner_v1/Conditioner_v1" },
            { "silo", "Oil_tanks/Oil_tank_v2/Oil_tank_v2" },
            { "pipe_v1", "Other_props/Pipes/Pipe_sets/Pipes_set_v1/Pipes_set_v1_V_set_v1" },
            { "pipe_v2", "Other_props/Pipes/Pipe_sets/Pipes_set_v1/Pipes_set_v1_V_set_v2" },
            { "pipe_h", "Other_props/Pipes/Pipe_sets/Pipes_set_v1/Pipes_set_v1_H_set_v4" },
            { "jersey", "Fences/Road_blocks/Road_block_v1/Road_block_v1" },
            { "fence_s", "Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_S" },
            { "fence_half", "Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_S_half" },
            { "dust", "Particles/Dust/Dust_v1/Dust_v1" },
        };

        static GameObject LoadPrefab(string key)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + Prefabs[key] + ".prefab");
            if (go == null) throw new InvalidOperationException("Missing prefab for key '" + key + "'");
            return go;
        }

        /// <summary>
        /// Places a pack prefab so its renderer bounds are centred on (x, z) and its bottom sits at y.
        /// </summary>
        static GameObject P(string key, Transform parent, float x, float z, float yaw = 0f, float y = 0f,
                            Vector3? scale = null, Material swapMat = null, bool addColliderIfMissing = true)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(key), parent);
            go.transform.localScale = scale ?? Vector3.one;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = new Vector3(x, 0f, z);
            if (swapMat != null)
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = swapMat;
            Bounds b = WorldBounds(go);
            go.transform.position += new Vector3(x - b.center.x, y - b.min.y, z - b.center.z);
            if (addColliderIfMissing && go.GetComponentInChildren<Collider>() == null) FitBoxCollider(go);
            return go;
        }

        static float Top(GameObject go) { return WorldBounds(go).max.y; }

        static void MarkStatic(Transform root)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic
                                          | StaticEditorFlags.OccluderStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>() != null) continue;
                if (t.GetComponent<ParticleSystem>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        static float R(float min, float max) { return min + (float)_rng.NextDouble() * (max - min); }
    }
}
