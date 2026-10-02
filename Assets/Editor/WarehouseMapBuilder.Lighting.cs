using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Outbreak.EditorTools
{
    // Lighting/atmosphere (night mood), gameplay wiring and NavMesh bake.
    public static partial class WarehouseMapBuilder
    {
        // Light tuning (URP point-light intensities are unitless).
        const float SodiumIntensity = 8f, SodiumRange = 11f;
        const float RedIntensity = 6f, RedRange = 7f;
        const float TealIntensity = 8f, TealRange = 8f;
        static readonly Color SodiumColor = new Color(1f, 0.58f, 0.22f);
        static readonly Color RedColor = new Color(1f, 0.12f, 0.08f);
        static readonly Color TealColor = new Color(0.15f, 0.95f, 0.85f);
        static readonly Color MoonColor = new Color(0.55f, 0.66f, 1f);

        public const string VolumeProfilePath = "Assets/Settings/WarehouseProfile.asset";

        static Light AddPoint(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_lights != null ? _lights : _root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        /// <summary>Emissive wall lamp: housing on the surface and a point light pushed into the room.</summary>
        static void WallLamp(char side, float c, float y, float intensity = SodiumIntensity)
        {
            Vector3 a, o;
            SideVectors(side, out a, out o);
            Vector3 pos = o * (Half - 0.12f) + a * c + Vector3.up * y;
            LampFixture(pos, -o, intensity);
        }

        /// <summary>Lamp housing at pos looking along 'inward' (towards the room) plus its light.</summary>
        static void LampFixture(Vector3 pos, Vector3 inward, float intensity = SodiumIntensity)
        {
            Quaternion rot = Quaternion.LookRotation(inward);
            Transform fixtures = _lights;
            Box(fixtures, "LampBracket", pos - inward * 0.02f, new Vector3(0.12f, 0.12f, 0.22f), _steelDark, false, null, rot);
            Box(fixtures, "LampHousing", pos + inward * 0.12f, new Vector3(0.34f, 0.16f, 0.2f), _steelDark, false, null, rot);
            Box(fixtures, "LampGlow", pos + inward * 0.12f - Vector3.up * 0.075f, new Vector3(0.28f, 0.03f, 0.16f), _lampMat, false, null, rot);
            AddPoint("Sodium", pos + inward * 0.8f - Vector3.up * 0.35f, SodiumColor, intensity, SodiumRange);
        }

        static void BuildLightsAndAtmosphere()
        {
            // --- Sodium lamps along the walls ---
            foreach (float z in new[] { -14f, -8f, 4f, 10f, 15f }) if (!NearGap(z, GapsW, 0.8f)) WallLamp('W', z, 3.1f);
            foreach (float z in new[] { -8.5f, 0f, 3f, 14.5f }) if (!NearGap(z, GapsE, 0.8f)) WallLamp('E', z, 3.1f);
            foreach (float x in new[] { -10f, -5.5f, 5.5f, 10.5f }) WallLamp('N', x, 2.6f);
            foreach (float x in new[] { -9f, -5f, 3.5f, 8f, 10.5f }) if (!NearGap(x, GapsS, 0.8f)) WallLamp('S', x, 3.1f);

            // Lamps on the long container flanks (corridors) and under the mezzanine deck.
            foreach (float z in new[] { -6f, 1f, 8f })
            {
                LampFixture(new Vector3(-13.52f, 2.5f, z), Vector3.left, 4f);
                LampFixture(new Vector3(-5.48f, 2.5f, z + 1f), Vector3.right, 4f);
            }
            foreach (float x in new[] { -8f, -3f, 4f, 9.5f })
                AddPoint("MezzUnderLight", new Vector3(x, 2.7f, 14f), SodiumColor, 4f, 7f);

            // Dock lamp, merchant/SW room accents.
            LampFixture(new Vector3(16.9f, 4.2f, 4.2f), Vector3.left, 5f);
            LampFixture(new Vector3(-16.9f, 3.0f, -14f), Vector3.right, 4f);
            AddPoint("MerchantTeal", new Vector3(14.6f, 2.4f, -13.9f), TealColor, TealIntensity, TealRange);
            AddPoint("YardMoonGlow", new Vector3(19.5f, 6f, 8.5f), new Color(0.45f, 0.6f, 1f), 10f, 16f);
            AddPoint("MerchantTealSoft", new Vector3(13.2f, 1.4f, -15.4f), TealColor, 2.5f, 3.5f);

            // --- Red emergency lights at each breach ---
            for (int i = 0; i < SpawnPoints.Length; i++)
            {
                Vector3 s = SpawnPoints[i];
                Vector3 p = new Vector3(s.x, 3.0f, s.z);
                Box(_lights, "EmergencyBulb_" + (i + 1), p + Vector3.up * 0.25f, new Vector3(0.22f, 0.22f, 0.22f), _redMat);
                AddPoint("Emergency_" + (i + 1), p, RedColor, RedIntensity, RedRange);
            }

            ConfigureMoonAndSky();
            ConfigurePostProcessing();
        }

        static Light FindMoon()
        {
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            var go = new GameObject("Directional Light");
            var nl = go.AddComponent<Light>();
            nl.type = LightType.Directional;
            return nl;
        }

        static void ConfigureMoonAndSky()
        {
            Light moon = FindMoon();
            moon.name = "Directional Light";
            moon.color = MoonColor;
            moon.intensity = 2.6f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.95f;
            moon.lightmapBakeType = LightmapBakeType.Realtime;
            moon.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            Material sky = new Material(Shader.Find("Skybox/Procedural"));
            string skyPath = MatDir + "/WH_NightSky.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (existing == null) { AssetDatabase.CreateAsset(sky, skyPath); existing = sky; }
            existing.SetFloat("_SunDisk", 2f);
            existing.SetFloat("_SunSize", 0.09f);
            existing.SetFloat("_SunSizeConvergence", 6f);
            existing.SetFloat("_AtmosphereThickness", 0.35f);
            existing.SetColor("_SkyTint", new Color(0.12f, 0.17f, 0.4f));
            existing.SetColor("_GroundColor", new Color(0.02f, 0.025f, 0.04f));
            existing.SetFloat("_Exposure", 0.9f);
            EditorUtility.SetDirty(existing);

            RenderSettings.skybox = existing;
            RenderSettings.sun = moon;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.125f, 0.22f);
            RenderSettings.reflectionIntensity = 0.4f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.06f, 0.09f, 0.14f);
            RenderSettings.fogDensity = 0.012f;
        }

        static T GetOrAdd<T>(VolumeProfile p) where T : VolumeComponent
        {
            T c;
            if (p.TryGet(out c)) return c;
            c = p.Add<T>(true);
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static void ConfigurePostProcessing()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath) == null)
                AssetDatabase.CopyAsset("Assets/Settings/SampleSceneProfile.asset", VolumeProfilePath);
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);

            var tone = GetOrAdd<Tonemapping>(prof);
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = GetOrAdd<Bloom>(prof);
            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.85f, 0.7f));

            var vig = GetOrAdd<Vignette>(prof);
            vig.active = true;
            vig.intensity.Override(0.38f);
            vig.smoothness.Override(0.5f);

            var ca = GetOrAdd<ColorAdjustments>(prof);
            ca.active = true;
            ca.postExposure.Override(0.5f);
            ca.contrast.Override(14f);
            ca.saturation.Override(6f);

            var split = GetOrAdd<SplitToning>(prof);
            split.active = true;
            split.shadows.Override(new Color(0.22f, 0.55f, 0.6f));
            split.highlights.Override(new Color(1f, 0.68f, 0.38f));
            split.balance.Override(10f);

            EditorUtility.SetDirty(prof);

            foreach (Volume v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v.isGlobal) { v.sharedProfile = prof; EditorUtility.SetDirty(v); }
        }

        // ------------------------------------------------------------------
        // Gameplay wiring (T3)
        // ------------------------------------------------------------------
        static Transform FindAny(Scene scene, string name)
        {
            foreach (GameObject r in scene.GetRootGameObjects())
                foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        static void WireGameplay(Scene scene)
        {
            // Old arena: disable, do not delete.
            foreach (string n in new[] { "TERRENO", "Obstaculos" })
            {
                Transform t = FindAny(scene, n);
                if (t != null) t.gameObject.SetActive(false);
                else Debug.LogWarning("[WarehouseMapBuilder] Old arena object not found: " + n);
            }

            // Player start.
            Transform player = FindAny(scene, "Player(Body)");
            if (player != null)
            {
                player.position = PlayerStart;
                player.rotation = Quaternion.identity;
            }
            else Debug.LogWarning("[WarehouseMapBuilder] Player(Body) not found");

            // Shop cube (it re-places itself in front of the player when the shop opens; this is its resting place).
            Transform shop = FindAny(scene, "ShopCube");
            if (shop != null) shop.position = ShopPosition;

            // Spawn points: reuse the 4 existing ones, add 2, assign all 6 to the spawner.
            Transform spawnerT = FindAny(scene, "Spawn(Zpmbie)");
            if (spawnerT == null) throw new InvalidOperationException("Spawner object 'Spawn(Zpmbie)' not found");
            var spawner = spawnerT.GetComponent<ZombieSpawner>();
            var points = new Transform[SpawnPoints.Length];
            for (int i = 0; i < SpawnPoints.Length; i++)
            {
                string pn = "SpawnPoint_" + (i + 1);
                Transform sp = null;
                foreach (Transform ch in spawnerT) if (ch.name == pn) sp = ch;
                if (sp == null)
                {
                    var go = new GameObject(pn);
                    sp = go.transform;
                    sp.SetParent(spawnerT, false);
                }
                sp.position = SpawnPoints[i];
                points[i] = sp;
            }
            var so = new SerializedObject(spawner);
            SerializedProperty arr = so.FindProperty("spawnPoints");
            arr.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedProperties();
            Debug.Log("[WarehouseMapBuilder] Spawner now references " + points.Length + " spawn points");
        }

        static void BakeNavMesh()
        {
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null) AssetDatabase.DeleteAsset(NavMeshPath);

            var surface = _root.gameObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.defaultArea = 0;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("NavMesh bake produced no data");
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
            Debug.Log("[WarehouseMapBuilder] NavMesh baked: " + NavMeshPath);
        }
    }
}
