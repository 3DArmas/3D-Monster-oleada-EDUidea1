using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace Outbreak.EditorTools
{
    /// <summary>
    /// Batch-mode verification of Warehouse.unity: NavMesh path checks, light counts and screenshots.
    /// Usage: -executeMethod Outbreak.EditorTools.WarehouseMapVerifier.Run -shotsDir "&lt;dir&gt;"
    /// (the directory defaults to "&lt;project&gt;/Temp/WarehouseShots"). Does not save the scene.
    /// </summary>
    public static class WarehouseMapVerifier
    {
        const int W = 1600, H = 900;

        public static void Run()
        {
            try { RunInternal(); }
            catch (Exception e)
            {
                Debug.LogError("[WarehouseVerify] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        static string ShotsDir()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-shotsDir") return args[i + 1];
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "WarehouseShots");
        }

        static void RunInternal()
        {
            string dir = ShotsDir();
            Directory.CreateDirectory(dir);
            var scene = EditorSceneManager.OpenScene(WarehouseMapBuilder.ScenePath, OpenSceneMode.Single);

            CheckLights();
            CheckZombies();
            CheckNavMesh(scene);
            RenderShots(dir);
            Debug.Log("[WarehouseVerify] DONE");
        }

        static void CheckLights()
        {
            int realtime = 0, shadows = 0, point = 0, dir = 0, spot = 0;
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l.enabled || !l.gameObject.activeInHierarchy) continue;
                realtime++;
                if (l.shadows != LightShadows.None) shadows++;
                if (l.type == LightType.Point) point++;
                else if (l.type == LightType.Directional) dir++;
                else spot++;
            }
            Debug.Log("[WarehouseVerify] LIGHTS realtime=" + realtime + " (point=" + point + ", directional=" + dir + ", spot=" + spot + ") shadow-casting=" + shadows);
        }

        static void CheckZombies()
        {
            int zombies = UnityEngine.Object.FindObjectsByType<ZombieController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Debug.Log("[WarehouseVerify] ZOMBIES_IN_SCENE=" + zombies);
        }

        static void CheckNavMesh(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject playerGo = null;
            foreach (GameObject r in scene.GetRootGameObjects())
                if (r.name == "Player(Body)") playerGo = r;
            Vector3 playerPos = playerGo != null ? playerGo.transform.position : Vector3.zero;
            NavMeshHit ph;
            if (!NavMesh.SamplePosition(playerPos, out ph, 3f, NavMesh.AllAreas))
            {
                Debug.LogError("[WarehouseVerify] PATH player start is not near the NavMesh: " + playerPos);
                return;
            }
            Debug.Log("[WarehouseVerify] Player start " + playerPos + " -> navmesh " + ph.position);

            Transform spawns = null;
            foreach (GameObject r in scene.GetRootGameObjects())
                if (r.name.StartsWith("Spawn(")) spawns = r.transform;
            if (spawns == null) { Debug.LogError("[WarehouseVerify] spawner not found"); return; }

            var spawner = spawns.GetComponent<ZombieSpawner>();
            var so = new SerializedObject(spawner);
            SerializedProperty arr = so.FindProperty("spawnPoints");
            int ok = 0;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var t = arr.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (t == null) { Debug.LogError("[WarehouseVerify] PATH spawn " + (i + 1) + " is null"); continue; }
                NavMeshHit sh;
                bool onMesh = NavMesh.SamplePosition(t.position, out sh, 0.5f, NavMesh.AllAreas);
                var path = new NavMeshPath();
                bool found = onMesh && NavMesh.CalculatePath(sh.position, ph.position, NavMesh.AllAreas, path);
                float len = 0f;
                if (found) for (int k = 1; k < path.corners.Length; k++) len += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                bool complete = found && path.status == NavMeshPathStatus.PathComplete;
                if (complete) ok++;
                Debug.Log("[WarehouseVerify] PATH spawn " + (i + 1) + " " + t.name + " pos=" + t.position + " onNavMesh=" + onMesh
                          + " (dist to mesh " + (onMesh ? Vector3.Distance(t.position, sh.position).ToString("F2") : "n/a") + ")"
                          + " status=" + (found ? path.status.ToString() : "NoPath") + " length=" + len.ToString("F1") + "m");
            }
            // Reachability of the elevated areas from the player start (stairs, platform, container roof).
            Vector3[] elevated =
            {
                new Vector3(0f, 3.1f, 14f), new Vector3(10f, 3.1f, 14f), new Vector3(-9.5f, 3.1f, 10.5f), new Vector3(-12f, 3.0f, 0f), new Vector3(-7f, 3.0f, -6f)
            };
            string[] labels = { "mezzanine centre", "mezzanine east", "container platform", "container1 roof", "container2 roof" };
            for (int i = 0; i < elevated.Length; i++)
            {
                NavMeshHit eh;
                bool near = NavMesh.SamplePosition(elevated[i], out eh, 1.0f, NavMesh.AllAreas);
                var p2 = new NavMeshPath();
                bool ok2 = near && NavMesh.CalculatePath(ph.position, eh.position, NavMesh.AllAreas, p2) && p2.status == NavMeshPathStatus.PathComplete;
                Debug.Log("[WarehouseVerify] REACH " + labels[i] + " target=" + elevated[i] + " onNavMesh=" + near
                          + (near ? " y=" + eh.position.y.ToString("F2") : "") + " pathComplete=" + ok2);
            }
            // Stair ramp collider profile (walkable slope for the CharacterController, limit 45 deg).
            Physics.SyncTransforms();
            float prevY = 0f, maxSlope = 0f;
            var sbp = new StringBuilder();
            for (float z = 7.2f; z <= 12.0f; z += 0.6f)
            {
                RaycastHit rh;
                if (Physics.Raycast(new Vector3(-3f, 6f, z), Vector3.down, out rh, 10f))
                {
                    if (z > 7.2f) maxSlope = Mathf.Max(maxSlope, Mathf.Atan2(rh.point.y - prevY, 0.6f) * Mathf.Rad2Deg);
                    prevY = rh.point.y;
                    sbp.Append("z=" + z.ToString("F1") + ":y=" + rh.point.y.ToString("F2") + " ");
                }
            }
            Debug.Log("[WarehouseVerify] STAIR_RAMP profile " + sbp + "maxSlope=" + maxSlope.ToString("F1") + "deg");
            Debug.Log("[WarehouseVerify] PATH SUMMARY " + ok + "/" + arr.arraySize + " complete");
        }

        // ------------------------------------------------------------------
        // Screenshots
        // ------------------------------------------------------------------
        static void RenderShots(string dir)
        {
            Camera main = Camera.main;
            Vector3 eye = main != null ? main.transform.position : new Vector3(0f, 1.76f, -2f);

            var camGo = new GameObject("VerifyCamera");
            var cam = camGo.AddComponent<Camera>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            cam.fieldOfView = main != null ? main.fieldOfView : 68f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            if (main != null) main.enabled = false;

            Transform roof = FindByName("Roof");
            bool fog = RenderSettings.fog;
            Color ambient = RenderSettings.ambientLight;

            // Overview shots: hide the roof (like the isometric concept art).
            if (roof != null) roof.gameObject.SetActive(false);
            RenderSettings.fog = false;
            cam.fieldOfView = 38f;
            Shot(cam, dir, "01_overview_iso", new Vector3(26f, 50f, -40f), new Vector3(0f, 0f, 0f));
            RenderSettings.ambientLight = new Color(0.32f, 0.36f, 0.48f);
            Shot(cam, dir, "02_overview_iso_lit", new Vector3(26f, 50f, -40f), new Vector3(0f, 0f, 0f));
            Shot(cam, dir, "03_overview_topdown", new Vector3(0f, 62f, -0.5f), new Vector3(0f, 0f, 0f));
            Shot(cam, dir, "04_overview_from_south_west", new Vector3(-32f, 42f, -34f), new Vector3(2f, 0f, 2f));
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = fog;
            if (roof != null) roof.gameObject.SetActive(true);

            // Eye-level shots.
            cam.fieldOfView = main != null ? main.fieldOfView : 68f;
            Shot(cam, dir, "10_eye_start_north", eye, eye + Vector3.forward * 10f);
            Shot(cam, dir, "11_eye_start_east", eye, eye + Vector3.right * 10f);
            Shot(cam, dir, "12_eye_start_west", eye, eye + Vector3.left * 10f);
            Shot(cam, dir, "13_eye_start_south", eye, eye + Vector3.back * 10f);
            Shot(cam, dir, "14_eye_start_looking_up", eye, eye + new Vector3(2f, 7f, 8f));
            Shot(cam, dir, "15_eye_start_roof_gap", eye + new Vector3(0f, 0f, -2f), new Vector3(-4f, 9f, -3f));
            Vector3 merchant = new Vector3(13.0f, 1.7f, -12.0f);
            Light moonLight = RenderSettings.sun;
            if (moonLight != null)
                Shot(cam, dir, "16_eye_moon_direction", eye + new Vector3(-6f, 0f, 0f), eye + new Vector3(-6f, 0f, 0f) - moonLight.transform.forward * 20f);
            Shot(cam, dir, "20_eye_merchant_room", merchant, new Vector3(16f, 1.0f, -15f));
            Shot(cam, dir, "21_eye_merchant_door_west", new Vector3(14.2f, 1.7f, -13.9f), new Vector3(6f, 1.4f, -13.9f));
            Shot(cam, dir, "30_eye_mezzanine_deck", new Vector3(0f, DeckEye(), 14f), new Vector3(0f, 1.5f, 0f));
            Shot(cam, dir, "31_eye_container_corridor", new Vector3(-15.2f, 1.7f, -7f), new Vector3(-15.2f, 1.7f, 10f));
            Shot(cam, dir, "32_eye_loading_door", new Vector3(8f, 1.7f, 5f), new Vector3(17f, 2.5f, 8.5f));
            Shot(cam, dir, "33_eye_east_corridor", new Vector3(16.1f, 1.7f, -9.8f), new Vector3(15.5f, 1.7f, 2f));
            Shot(cam, dir, "34_eye_north_breach", new Vector3(0f, 1.7f, 6f), new Vector3(0f, 1.8f, 16.5f));

            UnityEngine.Object.DestroyImmediate(camGo);
            if (main != null) main.enabled = true;
        }

        static float DeckEye() { return 3.1f + 1.7f; }

        static Transform FindByName(string name)
        {
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }

        static void Shot(Camera cam, string dir, string name, Vector3 pos, Vector3 lookAt)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(lookAt);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            string path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("[WarehouseVerify] SHOT " + path);
        }
    }
}
