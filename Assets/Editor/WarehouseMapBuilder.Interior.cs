using System;
using UnityEngine;

namespace Outbreak.EditorTools
{
    // Interior: containers + platform, mezzanine with stairs, east partition, merchant room,
    // south-west room, central cover, forklift, lamp post and wall props.
    public static partial class WarehouseMapBuilder
    {
        static void BuildInterior()
        {
            Transform g = Group("Interior");
            BuildContainersAndPlatform(Group("WestContainers", g));
            BuildMezzanine(Group("Mezzanine", g));
            BuildEastWing(Group("EastWing", g));
            BuildMerchantRoom(Group("MerchantRoom", g));
            BuildSouthWestRoom(Group("SouthWestRoom", g));
            BuildCenter(Group("Center", g));
            BuildWallProps(Group("WallProps", g));
            BuildAtmospherePieces(Group("Atmosphere", g));
        }

        // ------------------------------------------------------------------
        // Reusable structures
        // ------------------------------------------------------------------
        static Material GratingFor(float w, float d)
        {
            return M("WH_Grating_" + Key(w) + "x" + Key(d), Color.white, 0.6f, 0.3f, null,
                     _grating.GetTexture("_BaseMap"), new Vector2(Mathf.Max(1f, w / 0.8f), Mathf.Max(1f, d / 0.8f)));
        }

        /// <summary>Safety railing between two points at the same height (posts, two rails, toe board, invisible collider).</summary>
        static void Rail(Transform parent, Vector3 a, Vector3 b, float height = 1.1f, bool collider = true)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.1f) return;
            Quaternion rot = Quaternion.LookRotation(d.normalized);
            Vector3 mid = (a + b) * 0.5f;
            Transform g = Group("Rail", parent);
            int posts = Mathf.CeilToInt(len / 1.8f) + 1;
            for (int i = 0; i < posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, posts == 1 ? 0f : (float)i / (posts - 1));
                Box(g, "Post", p + Vector3.up * (height * 0.5f), new Vector3(0.06f, height, 0.06f), _yellow);
            }
            Box(g, "TopRail", mid + Vector3.up * height, new Vector3(0.06f, 0.06f, len), _yellow, false, null, rot);
            Box(g, "MidRail", mid + Vector3.up * (height * 0.5f), new Vector3(0.04f, 0.04f, len), _yellow, false, null, rot);
            Box(g, "ToeBoard", mid + Vector3.up * 0.06f, new Vector3(0.02f, 0.12f, len), _yellow, false, null, rot);
            if (collider)
            {
                var c = new GameObject("RailCollider");
                c.transform.SetParent(g, false);
                c.transform.position = mid + Vector3.up * 0.65f;
                c.transform.rotation = rot;
                var bc = c.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.12f, 1.3f, len);
            }
        }

        /// <summary>Open steel staircase descending towards -Z from the deck edge. A ramp collider makes it walkable.</summary>
        static void Stairs(Transform parent, float xc, float topZ, float run, float width)
        {
            Transform g = Group("Stairs_" + Key(xc), parent);
            float rise = DeckY;
            const int steps = 16;
            Vector3 T = new Vector3(xc, rise, topZ), B = new Vector3(xc, 0f, topZ - run);
            float L = Mathf.Sqrt(run * run + rise * rise);
            Vector3 d = new Vector3(0f, rise, run) / L;
            Vector3 n = new Vector3(0f, run, -rise) / L;
            Quaternion rot = Quaternion.LookRotation(d, n);
            Vector3 mid = (T + B) * 0.5f;

            // Walkable ramp collider (the visible steps have no collider so the CharacterController glides).
            var ramp = new GameObject("StairRamp");
            ramp.transform.SetParent(g, false);
            ramp.transform.position = mid - n * 0.12f;
            ramp.transform.rotation = rot;
            var rc = ramp.AddComponent<BoxCollider>();
            rc.size = new Vector3(width, 0.24f, L + 0.1f);

            // Vertical side guards (collider only) so nobody walks off the side of the stairs.
            for (int s = -1; s <= 1; s += 2)
            {
                var w = new GameObject("StairGuard");
                w.transform.SetParent(g, false);
                w.transform.position = new Vector3(xc + s * (width * 0.5f + 0.05f), (rise + 1.1f) * 0.5f, topZ - run * 0.5f);
                var wc = w.AddComponent<BoxCollider>();
                wc.size = new Vector3(0.1f, rise + 1.1f, run);
            }

            float treadDepth = run / steps;
            for (int i = 1; i <= steps; i++)
            {
                float z = B.z + (i - 0.5f) * treadDepth;
                float y = i * rise / steps - 0.03f;
                Box(g, "Tread", new Vector3(xc, y, z), new Vector3(width - 0.1f, 0.06f, treadDepth + 0.02f), _grating, false);
                Box(g, "Riser", new Vector3(xc, y - rise / steps * 0.5f, z - treadDepth * 0.5f), new Vector3(width - 0.1f, rise / steps, 0.02f), _steelDark, false);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float x = xc + s * (width * 0.5f - 0.02f);
                Box(g, "Stringer", new Vector3(x, mid.y, mid.z) - n * 0.12f, new Vector3(0.08f, 0.34f, L), _steelDark, false, null, rot);
                Box(g, "HandRail", new Vector3(x, mid.y, mid.z) + n * 0.95f, new Vector3(0.05f, 0.05f, L), _yellow, false, null, rot);
                Box(g, "HandRailMid", new Vector3(x, mid.y, mid.z) + n * 0.5f, new Vector3(0.035f, 0.035f, L), _yellow, false, null, rot);
                for (int i = 0; i <= steps; i += 4)
                {
                    float z = B.z + i * treadDepth;
                    float y = i * rise / steps;
                    Box(g, "StairPost", new Vector3(x, y + 0.48f, z), new Vector3(0.05f, 0.95f, 0.05f), _yellow);
                }
            }
        }

        static void Ladder(Transform parent, float x, float z, float height)
        {
            Transform g = Group("Ladder", parent);
            Box(g, "RailL", new Vector3(x - 0.25f, height * 0.5f, z), new Vector3(0.05f, height, 0.05f), _yellow);
            Box(g, "RailR", new Vector3(x + 0.25f, height * 0.5f, z), new Vector3(0.05f, height, 0.05f), _yellow);
            for (float y = 0.3f; y < height - 0.1f; y += 0.3f)
                Box(g, "Rung", new Vector3(x, y, z), new Vector3(0.5f, 0.035f, 0.035f), _yellow);
        }

        // ------------------------------------------------------------------
        // West containers and top platform
        // ------------------------------------------------------------------
        static void BuildContainersAndPlatform(Transform g)
        {
            Material red = AssetDatabase_Load("Containers/Cargo_container_v1/Source/Cargo_container_v2.mat");
            float len = ContainerZ1 - ContainerZ0;          // 20 m: stretch the 8 m container 2.5x along its length
            float zc = (ContainerZ0 + ContainerZ1) * 0.5f;
            Vector3 stretch = new Vector3(len / 8f, 1f, 1f);
            P("cont", g, -12f, zc, 90f, 0f, stretch, red);
            P("cont", g, -7f, zc, 90f, 0f, stretch, red);

            // Platform on top of the north end of both containers, flush with the mezzanine deck.
            const float pz0 = 8.8f, pz1 = MezzZ0;
            float px0 = MezzX0, px1 = -5.4f;
            Box(g, "PlatformDeck", new Vector3((px0 + px1) * 0.5f, DeckY - 0.125f, (pz0 + pz1) * 0.5f),
                new Vector3(px1 - px0, 0.25f, pz1 - pz0), GratingFor(px1 - px0, pz1 - pz0), true);
            Box(g, "PlatformBeam_A", new Vector3((px0 + px1) * 0.5f, DeckY - 0.43f, pz1 - 0.3f), new Vector3(px1 - px0, 0.35f, 0.2f), _steelDark);
            Box(g, "PlatformBeam_B", new Vector3((px0 + px1) * 0.5f, DeckY - 0.43f, pz0 + 0.3f), new Vector3(px1 - px0, 0.35f, 0.2f), _steelDark);
            foreach (float lx in new[] { -13.4f, -9.5f, -5.6f })
                Box(g, "PlatformLeg", new Vector3(lx, (DeckY - 0.25f) * 0.5f, pz1 - 0.25f), new Vector3(0.3f, DeckY - 0.25f, 0.3f), _steelDark, true);

            // Rails only on the edges that are not above a container roof.
            Rail(g, new Vector3(px0, DeckY, 10.9f), new Vector3(px0, DeckY, pz1), 1.1f);
            Rail(g, new Vector3(px1, DeckY, 10.9f), new Vector3(px1, DeckY, pz1), 1.1f);
            Rail(g, new Vector3(-10.4f, DeckY, pz0), new Vector3(-8.6f, DeckY, pz0), 1.1f);

            // Crates on the platform and container roofs.
            GameObject b1 = P("box", g, -12.4f, 10.0f, 15f, DeckY);
            P("box2", g, -12.4f, 10.0f, 40f, Top(b1));
            P("box", g, -11.2f, 11.2f, 0f, DeckY);
            P("barrel_rust", g, -6.4f, 11.2f, 0f, DeckY);
            P("barrel_rust", g, -6.9f, 11.7f, 0f, DeckY);
            P("box_long", g, -12.85f, 4.0f, 0f, 3.0f);   // against the roof edge so the roof stays walkable
            P("box", g, -6.1f, -2.0f, 20f, 3.0f);

            // West wall dead-end: stacked crates and a barrel near the container doors.
            P("box_long", g, -15.9f, -9.0f, 80f);
            P("barrel_rust", g, -15.6f, -7.6f);
            P("box", g, -9.4f, -9.6f, 25f);
        }

        // Loads a pack material by pack-relative path (used to swap container paint).
        static Material AssetDatabase_Load(string packRelative)
        {
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(PackRoot + packRelative);
            if (m == null) Debug.LogWarning("[WarehouseMapBuilder] Missing material " + packRelative);
            return m;
        }

        // ------------------------------------------------------------------
        // Mezzanine
        // ------------------------------------------------------------------
        static void BuildMezzanine(Transform g)
        {
            float w = MezzX1 - MezzX0, d = MezzZ1 - MezzZ0;
            Box(g, "MezzDeck", new Vector3((MezzX0 + MezzX1) * 0.5f, DeckY - 0.125f, (MezzZ0 + MezzZ1) * 0.5f),
                new Vector3(w, 0.25f, d), GratingFor(w, d), true);
            foreach (float z in new[] { MezzZ0 + 0.2f, (MezzZ0 + MezzZ1) * 0.5f, MezzZ1 - 0.2f })
                Box(g, "MezzBeam", new Vector3((MezzX0 + MezzX1) * 0.5f, DeckY - 0.43f, z), new Vector3(w, 0.35f, 0.2f), _steelDark);
            foreach (float x in new[] { MezzX0 + 0.2f, -8f, -2f, 4f, 10f, MezzX1 - 0.2f })
                Box(g, "MezzCrossBeam", new Vector3(x, DeckY - 0.45f, (MezzZ0 + MezzZ1) * 0.5f), new Vector3(0.2f, 0.3f, d), _steelDark);
            float[] cols = { -12.8f, -8.5f, -4.5f, -0.5f, 3.5f, 9.5f, 12.2f };
            foreach (float x in cols)
            {
                Box(g, "Column_F", new Vector3(x, (DeckY - 0.25f) * 0.5f, MezzZ0 + 0.3f), new Vector3(0.3f, DeckY - 0.25f, 0.3f), _steelDark, true);
                Box(g, "Column_B", new Vector3(x, (DeckY - 0.25f) * 0.5f, MezzZ1 - 0.3f), new Vector3(0.3f, DeckY - 0.25f, 0.3f), _steelDark, true);
            }

            // Stairs and ladder (decorative).
            const float stair1 = -3f, stair2 = 7.5f, sw = 2.2f;
            Stairs(g, stair1, MezzZ0, 4.9f, sw);
            Stairs(g, stair2, MezzZ0, 4.9f, sw);
            Ladder(g, 2.5f, MezzZ0 - 0.15f, DeckY + 1.1f);

            // Railings: north, west (above ground), east, and the south edge with openings for the stairs.
            Rail(g, new Vector3(MezzX0, DeckY, MezzZ1), new Vector3(MezzX1, DeckY, MezzZ1));
            Rail(g, new Vector3(MezzX0, DeckY, MezzZ0), new Vector3(MezzX0, DeckY, MezzZ1));
            Rail(g, new Vector3(MezzX1, DeckY, MezzZ0), new Vector3(MezzX1, DeckY, MezzZ1));
            Rail(g, new Vector3(-5.4f, DeckY, MezzZ0), new Vector3(stair1 - sw * 0.5f, DeckY, MezzZ0));
            Rail(g, new Vector3(stair1 + sw * 0.5f, DeckY, MezzZ0), new Vector3(stair2 - sw * 0.5f, DeckY, MezzZ0));
            Rail(g, new Vector3(stair2 + sw * 0.5f, DeckY, MezzZ0), new Vector3(MezzX1, DeckY, MezzZ0));

            // Crates and gear on the deck.
            GameObject a = P("pallet", g, -9.5f, 14.4f, 0f, DeckY);
            P("box", g, -9.9f, 14.4f, 10f, Top(a));
            P("box", g, -9.1f, 14.5f, -8f, Top(a));
            P("box_long", g, -7.0f, 14.9f, 0f, DeckY);
            GameObject c = P("box", g, 4.5f, 14.6f, 0f, DeckY);
            P("box2", g, 4.5f, 14.6f, 30f, Top(c));
            P("barrel_blue1", g, 9.4f, 14.8f, 0f, DeckY);
            P("barrel_blue1", g, 10.1f, 14.4f, 0f, DeckY);
            P("bags2", g, 11.0f, 13.3f, 90f, DeckY);
            P("box_long", g, 0.8f, 15.3f, 0f, DeckY);

            // Under the mezzanine (ground level, keep x in [-1.8, 1.8] clear for the north breach).
            P("generator", g, -9f, 15.4f, 0f);
            P("ebox3", g, 8f, 15.8f, 0f);
            P("barrel_rust", g, -5.8f, 14.1f);
            P("barrel_rust", g, -5.2f, 14.6f);
            P("barrel_rust", g, -6.4f, 14.7f);
            GameObject pl = P("pallet", g, 4.2f, 13.6f, 5f);
            P("box", g, 4.2f, 13.6f, 20f, Top(pl));
            P("bags", g, 12.0f, 14.2f, 0f);
            // Green tarp leaning against a column.
            Box(g, "TarpHang", new Vector3(-0.2f, 1.7f, 12.6f), new Vector3(2.6f, 3.0f, 0.05f), _tarp, false, new Vector3(-6f, 0f, 0f));
        }

        // ------------------------------------------------------------------
        // East partition + corridor
        // ------------------------------------------------------------------
        static void BuildEastWing(Transform g)
        {
            // Partition wall from the south wall up to z = 1.5, with a door into the merchant room.
            WallRun(g, "Partition", false, PartitionX, -Half, 1.5f, 0.5f, 0f, PartitionH, true, new Gap(-14f, 3f, 2.7f));
            Box(g, "PartitionCap", new Vector3(PartitionX, PartitionH + 0.06f, (-Half + 1.5f) * 0.5f), new Vector3(0.7f, 0.12f, 1.5f + Half), _steelDark);
            // Merchant room north wall.
            WallRun(g, "MerchantNorth", true, MerchantNorthZ, PartitionX, Half, 0.5f, 0f, PartitionH, true);
            Box(g, "MerchantNorthCap", new Vector3((PartitionX + Half) * 0.5f, PartitionH + 0.06f, MerchantNorthZ), new Vector3(Half - PartitionX, 0.12f, 0.7f), _steelDark);

            // East corridor dressing (spawn at z = -3.5 stays clear).
            P("box_long", g, 15.8f, -7.4f, 10f);
            GameObject pl = P("pallet_set", g, 14.0f, -8.9f, 0f);
            P("box", g, 14.0f, -8.9f, 15f, Top(pl));
            P("barrel_v3_4", g, 16.0f, 0.6f, 20f);
            P("box", g, 13.0f, -2.0f, 35f);
            GameObject st = P("pallet", g, 13.6f, 3.2f, 0f);
            P("box", g, 13.6f, 3.2f, 25f, Top(st));
            P("barrel_blue1", g, 16.3f, 2.4f);
            // Short wall stub near the loading dock, as in the concept art.
            WallRun(g, "DockStub", false, 9.7f, 2.0f, 4.4f, 0.5f, 0f, PartitionH, true);
            P("jersey", g, 12.8f, 3.3f, 90f);
        }

        static void BuildMerchantRoom(Transform g)
        {
            // Counter along the east wall, shelves on the north and south walls, tarp canopy, terminal.
            Box(g, "Counter", new Vector3(16.1f, 0.5f, -13.9f), new Vector3(0.7f, 1.0f, 2.6f), _wood, true);
            Box(g, "CounterTop", new Vector3(16.05f, 1.03f, -13.9f), new Vector3(0.9f, 0.06f, 2.8f), _steelDark);
            Shelf(g, 13.2f, -11.15f, 0f, 1.8f);
            Shelf(g, 15.4f, -11.15f, 0f, 1.8f);
            Shelf(g, 13.8f, -16.75f, 180f, 2.2f);

            // Tarp canopy over the counter.
            Box(g, "Tarp", new Vector3(15.6f, 3.0f, -13.9f), new Vector3(3.0f, 0.05f, 3.4f), _tarp, false, new Vector3(0f, 0f, 7f));
            foreach (Vector3 p in new[] { new Vector3(14.2f, 0f, -15.6f), new Vector3(14.2f, 0f, -12.2f), new Vector3(16.8f, 0f, -15.6f), new Vector3(16.8f, 0f, -12.2f) })
                Cyl(g, "TarpPole", new Vector3(p.x, 1.5f, p.z), 0.04f, 3.0f, _steelDark);

            // Table with a teal terminal.
            Box(g, "Table", new Vector3(13.2f, 0.45f, -15.6f), new Vector3(1.4f, 0.9f, 0.8f), _wood, true);
            Box(g, "Terminal", new Vector3(13.2f, 1.15f, -15.75f), new Vector3(0.7f, 0.4f, 0.05f), _tealMat, false, new Vector3(-15f, 0f, 0f));
            Box(g, "TerminalBase", new Vector3(13.2f, 0.95f, -15.6f), new Vector3(0.5f, 0.06f, 0.4f), _steelDark);

            P("barrel_blue1", g, 16.5f, -11.7f);
            P("barrel_blue1", g, 16.5f, -16.4f);
            P("box", g, 12.7f, -16.4f, 15f);
            P("box2", g, 12.7f, -16.4f, 35f, 0.81f);
            P("ebox1", g, 12.45f, -12.0f, 90f, 1.0f);
        }

        static void Shelf(Transform parent, float x, float z, float yaw, float width)
        {
            Transform g = Group("Shelf", parent);
            g.localPosition = new Vector3(x, 0f, z);
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            const float h = 2.0f, dpt = 0.5f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Box(g, "Upright", new Vector3(sx * (width * 0.5f - 0.03f), h * 0.5f, sz * (dpt * 0.5f - 0.03f)), new Vector3(0.06f, h, 0.06f), _steelDark);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.15f + i * 0.6f;
                Box(g, "Board", new Vector3(0f, y, 0f), new Vector3(width, 0.04f, dpt), _wood);
                if (i < 3)
                    for (int k = 0; k < 3; k++)
                        Box(g, "Item", new Vector3(-width * 0.33f + k * width * 0.33f, y + 0.15f, 0f),
                            new Vector3(R(0.25f, 0.4f), R(0.2f, 0.3f), 0.3f), (k + i) % 2 == 0 ? _wood : _tarp);
            }
            var col = new GameObject("ShelfCollider");
            col.transform.SetParent(g, false);
            col.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            col.AddComponent<BoxCollider>().size = new Vector3(width, h, dpt);
        }

        static void BuildSouthWestRoom(Transform g)
        {
            WallRun(g, "SwNorth", true, SwRoomNorthZ, -Half, SwRoomEastX, 0.5f, 0f, 3.8f, true);
            WallRun(g, "SwEast", false, SwRoomEastX, -Half, SwRoomNorthZ, 0.5f, 0f, 3.8f, true, new Gap(-14f, 2.6f, 2.6f));
            Box(g, "SwNorthCap", new Vector3((-Half + SwRoomEastX) * 0.5f, 3.86f, SwRoomNorthZ), new Vector3(SwRoomEastX + Half, 0.12f, 0.7f), _steelDark);
            Box(g, "SwEastCap", new Vector3(SwRoomEastX, 3.86f, (-Half + SwRoomNorthZ) * 0.5f), new Vector3(0.7f, 0.12f, SwRoomNorthZ + Half), _steelDark);
            P("barrel_rust", g, -16.2f, -11.9f);
            P("barrel_rust", g, -15.5f, -11.9f);
            P("barrel_blue4", g, -10.7f, -16.0f, 30f);
            GameObject pl = P("pallet_set", g, -10.8f, -12.2f, 10f);
            P("box", g, -10.8f, -12.2f, 30f, Top(pl));
            P("box_long", g, -16.0f, -16.4f, 5f);
        }

        // ------------------------------------------------------------------
        // Central combat zone
        // ------------------------------------------------------------------
        static void BuildCenter(Transform g)
        {
            // Crate/pallet stacks (cover).
            GameObject a = P("pallet", g, -3.3f, 5.2f, 10f);
            GameObject a1 = P("box", g, -3.7f, 5.2f, 5f, Top(a));
            P("box", g, -2.9f, 5.3f, 15f, Top(a));
            P("box2", g, -3.4f, 5.2f, 0f, Top(a1));
            P("barrel_rust", g, -4.9f, 3.9f);
            P("barrel_rust", g, -4.3f, 4.2f);
            P("barrel_rust", g, -4.7f, 3.3f);

            GameObject c = P("pallet_set", g, -3.6f, 0.8f, 0f);
            P("box", g, -4.0f, 0.8f, 20f, Top(c));
            P("box2", g, -3.2f, 0.9f, 0f, Top(c));
            GameObject d = P("pallet", g, 3.0f, 0.4f, 15f);
            P("box_long", g, 3.0f, 0.4f, 15f, Top(d));

            GameObject f = P("pallet_set", g, 5.8f, 2.6f, 30f);
            GameObject f1 = P("box", g, 5.5f, 2.4f, 10f, Top(f));
            P("box2", g, 6.3f, 2.8f, 40f, Top(f));
            P("box", g, 5.5f, 2.4f, 0f, Top(f1));
            P("box", g, 7.2f, -4.2f, 20f);
            P("box2", g, 7.2f, -4.2f, 10f, 0.81f);
            P("barrel_blue1", g, 8.0f, -3.5f);
            GameObject h = P("pallet_set", g, -2.4f, -5.3f, 5f);
            P("box", g, -2.4f, -5.3f, 25f, Top(h));
            P("bags", g, 3.2f, -11.5f, 20f);
            P("bags2", g, 8.6f, 0.2f, 90f);

            // Jersey barriers.
            P("jersey", g, 5.5f, 5.8f, 70f);
            P("jersey", g, 1.5f, 3.3f, 8f);
            P("jersey", g, 5.2f, -6.8f, 100f);
            P("jersey", g, 3.2f, -7.4f, 80f);
            P("jersey", g, -1.5f, -9.0f, 15f);
            P("jersey", g, 7.0f, -13.5f, 0f);
            P("jersey", g, 8.3f, -12.0f, 60f);

            // South wall clutter (the breach at x = -1 stays clear).
            P("box", g, 6.5f, -15.8f, 10f);
            P("box_long", g, 8.8f, -15.9f, 0f);
            P("barrel_rust", g, 4.2f, -15.9f);
            P("dumpster_empty", g, -6.5f, -15.9f, 0f);

            Forklift(g, 8.3f, -1.4f, 200f);
            LampPost(g, 0.4f, 2.5f);
        }

        static void Forklift(Transform parent, float x, float z, float yaw)
        {
            var root = new GameObject("Forklift");
            root.transform.SetParent(parent, false);
            Transform g = root.transform;
            Box(g, "Body", new Vector3(0f, 0.65f, -0.2f), new Vector3(1.1f, 0.8f, 1.7f), _yellow);
            Box(g, "Counterweight", new Vector3(0f, 0.75f, -1.15f), new Vector3(1.1f, 0.9f, 0.55f), _steelDark);
            Box(g, "Seat", new Vector3(0f, 1.15f, -0.4f), new Vector3(0.6f, 0.1f, 0.5f), _black);
            foreach (float sx in new[] { -0.5f, 0.5f })
                foreach (float sz in new[] { -0.8f, 0.4f })
                    Box(g, "CabPost", new Vector3(sx, 1.75f, sz), new Vector3(0.06f, 1.2f, 0.06f), _steelDark);
            Box(g, "CabRoof", new Vector3(0f, 2.35f, -0.2f), new Vector3(1.2f, 0.06f, 1.5f), _steelDark);
            foreach (float sx in new[] { -0.62f, 0.62f })
            {
                Cyl(g, "WheelF", new Vector3(sx, 0.36f, 0.45f), 0.36f, 0.26f, _black, false, new Vector3(0f, 0f, 90f));
                Cyl(g, "WheelR", new Vector3(sx, 0.3f, -0.95f), 0.3f, 0.22f, _black, false, new Vector3(0f, 0f, 90f));
            }
            foreach (float sx in new[] { -0.32f, 0.32f })
            {
                Box(g, "MastUpright", new Vector3(sx, 1.1f, 0.95f), new Vector3(0.09f, 2.2f, 0.1f), _steelDark);
                Box(g, "Fork", new Vector3(sx, 0.08f, 1.6f), new Vector3(0.11f, 0.05f, 1.2f), _steelDark);
            }
            Box(g, "MastCross", new Vector3(0f, 2.15f, 0.95f), new Vector3(0.8f, 0.08f, 0.08f), _steelDark);
            Box(g, "Carriage", new Vector3(0f, 0.55f, 1.0f), new Vector3(0.95f, 0.45f, 0.06f), _steel);
            FitBoxCollider(root);
            g.localPosition = new Vector3(x, 0f, z);
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        static void LampPost(Transform parent, float x, float z)
        {
            Transform g = Group("LampPost", parent);
            Cyl(g, "Base", new Vector3(x, 0.15f, z), 0.22f, 0.3f, _steelDark, true);
            Cyl(g, "Pole", new Vector3(x, 2.1f, z), 0.06f, 4.2f, _steelDark, true);
            Box(g, "Arm", new Vector3(x + 0.25f, 4.15f, z), new Vector3(0.6f, 0.05f, 0.05f), _steelDark);
            Box(g, "Housing", new Vector3(x + 0.55f, 4.1f, z), new Vector3(0.5f, 0.1f, 0.3f), _steelDark);
            Box(g, "Lamp", new Vector3(x + 0.55f, 4.03f, z), new Vector3(0.42f, 0.04f, 0.24f), _lampMat);
            AddPoint("LampPostLight", new Vector3(x + 0.55f, 3.7f, z), new Color(1f, 0.6f, 0.25f), 9f, 11f);
        }

        // ------------------------------------------------------------------
        // Props against the perimeter walls and in the corners
        // ------------------------------------------------------------------
        static void BuildWallProps(Transform g)
        {
            P("dumpster", g, -15.9f, 5.0f, 90f);
            P("pipe_v2", g, -16.45f, -6.5f, 90f);
            P("pipe_v1", g, -16.45f, 2.6f, 90f);
            P("pipe_v2", g, 16.45f, -7.0f, 90f);
            P("pipe_v1", g, 5.0f, -16.45f, 0f);
            P("ebox1", g, -16.7f, -3.8f, 90f, 1.0f);
            P("ebox2", g, -16.7f, 0.8f, 90f, 0.6f);
            P("conditioner", g, 16.75f, 3.0f, 90f, 2.0f);
            P("silo", g, 15.3f, 14.6f);
            P("barrel_blue4", g, 13.2f, 15.6f, 20f);
            P("barrel_v3_4", g, 15.4f, 12.9f, 0f);
            P("silo", g, -15.8f, 14.9f);
        }

        static void BuildAtmospherePieces(Transform g)
        {
            foreach (Vector3 p in new[] { new Vector3(-3f, 1.5f, 2f), new Vector3(4f, 1.5f, -6f), new Vector3(-8f, 1.5f, -4f) })
            {
                GameObject d = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(LoadPrefab("dust"), g);
                d.transform.position = p;
            }
        }
    }
}
