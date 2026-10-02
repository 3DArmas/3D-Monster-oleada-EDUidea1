using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outbreak.EditorTools
{
    // Shell: floor, perimeter walls, breaches, windows, loading door, roof and crane.
    public static partial class WarehouseMapBuilder
    {
        struct Gap
        {
            public float c, w, h;
            public Gap(float c, float w, float h) { this.c = c; this.w = w; this.h = h; }
        }

        static Transform _lights;

        static readonly Gap[] GapsN = { new Gap(-14f, BreachW, BreachH), new Gap(0f, BreachW, BreachH) };
        static readonly Gap[] GapsS = { new Gap(-13.2f, BreachW, BreachH), new Gap(-1f, BreachW, BreachH) };
        static readonly Gap[] GapsW = { new Gap(-1.5f, BreachW, BreachH) };
        static readonly Gap[] GapsE = { new Gap(-3.5f, BreachW, BreachH), new Gap(8.5f, 6f, 5f) };

        static Material WallMat(float len, float h, bool concrete = false)
        {
            if (concrete)
                return Tiled("WH_Concrete", _concTex, new Color(0.5f, 0.5f, 0.52f), len / 4f, h / 4f, 0.1f);
            return Tiled("WH_Brick", _brickTex, new Color(0.62f, 0.55f, 0.52f), len / 4f, h / 4f, 0.1f);
        }

        static void SideVectors(char side, out Vector3 a, out Vector3 o)
        {
            switch (side)
            {
                case 'N': a = Vector3.right; o = Vector3.forward; break;
                case 'S': a = Vector3.right; o = Vector3.back; break;
                case 'W': a = Vector3.forward; o = Vector3.left; break;
                default: a = Vector3.forward; o = Vector3.right; break;
            }
        }

        /// <summary>
        /// Straight wall made of box segments with openings. Under a gap there is nothing; above it a lintel
        /// closes the wall up to its full height.
        /// </summary>
        static void WallRun(Transform parent, string name, bool alongX, float fixedC, float a, float b,
                            float thick, float baseY, float height, bool concrete, params Gap[] gaps)
        {
            var sorted = new List<Gap>(gaps);
            sorted.Sort((p, q) => p.c.CompareTo(q.c));
            float cur = a;
            int n = 0;
            Action<float, float, float, float> seg = (s0, s1, y0, hgt) =>
            {
                float len = s1 - s0;
                if (len < 0.05f || hgt < 0.05f) return;
                float mid = (s0 + s1) * 0.5f;
                Vector3 pos = alongX ? new Vector3(mid, y0 + hgt * 0.5f, fixedC) : new Vector3(fixedC, y0 + hgt * 0.5f, mid);
                Vector3 size = alongX ? new Vector3(len, hgt, thick) : new Vector3(thick, hgt, len);
                Box(parent, name + "_" + (n++), pos, size, WallMat(len, hgt, concrete), true);
            };
            foreach (Gap g in sorted)
            {
                float g0 = g.c - g.w * 0.5f, g1 = g.c + g.w * 0.5f;
                if (g0 > cur) seg(cur, g0, baseY, height);
                if (g.h < height) seg(g0, g1, baseY + g.h, height - g.h);
                cur = g1;
            }
            if (cur < b) seg(cur, b, baseY, height);
        }

        static bool NearGap(float c, Gap[] gaps, float margin)
        {
            foreach (Gap g in gaps) if (Mathf.Abs(c - g.c) < g.w * 0.5f + margin) return true;
            return false;
        }

        static void BuildFloorAndShell()
        {
            _lights = Group("Lights");
            Transform g = Group("Shell");

            Box(g, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(FloorHalf * 2f, 0.2f, FloorHalf * 2f), _floorMat, true);
            Material outer = M("WH_OuterGround", new Color(0.05f, 0.06f, 0.08f), 0f, 0.2f);
            Box(g, "OuterGround", new Vector3(0f, -0.3f, 0f), new Vector3(400f, 0.2f, 400f), outer, false);

            // Invisible bounds around the exterior yard so the player cannot leave through the loading door.
            float ob = FloorHalf + 0.5f;
            Box(g, "Bounds_N", new Vector3(0f, 4f, ob), new Vector3(FloorHalf * 2f + 2f, 8f, 1f), _black, true).GetComponent<Renderer>().enabled = false;
            Box(g, "Bounds_S", new Vector3(0f, 4f, -ob), new Vector3(FloorHalf * 2f + 2f, 8f, 1f), _black, true).GetComponent<Renderer>().enabled = false;
            Box(g, "Bounds_E", new Vector3(ob, 4f, 0f), new Vector3(1f, 8f, FloorHalf * 2f + 2f), _black, true).GetComponent<Renderer>().enabled = false;
            Box(g, "Bounds_W", new Vector3(-ob, 4f, 0f), new Vector3(1f, 8f, FloorHalf * 2f + 2f), _black, true).GetComponent<Renderer>().enabled = false;

            float c = Half + WallT * 0.5f;
            float e = Half + WallT;
            WallRun(g, "Wall_N", true, c, -e, e, WallT, 0f, WallH, false, GapsN);
            WallRun(g, "Wall_S", true, -c, -e, e, WallT, 0f, WallH, false, GapsS);
            WallRun(g, "Wall_W", false, -c, -Half, Half, WallT, 0f, WallH, false, GapsW);
            WallRun(g, "Wall_E", false, c, -Half, Half, WallT, 0f, WallH, false, GapsE);

            // Cornice (dark steel cap along the inner top edge).
            Box(g, "Cornice_N", new Vector3(0f, WallH - 0.15f, Half - 0.12f), new Vector3(34f, 0.3f, 0.25f), _steelDark);
            Box(g, "Cornice_S", new Vector3(0f, WallH - 0.15f, -Half + 0.12f), new Vector3(34f, 0.3f, 0.25f), _steelDark);
            Box(g, "Cornice_W", new Vector3(-Half + 0.12f, WallH - 0.15f, 0f), new Vector3(0.25f, 0.3f, 34f), _steelDark);
            Box(g, "Cornice_E", new Vector3(Half - 0.12f, WallH - 0.15f, 0f), new Vector3(0.25f, 0.3f, 34f), _steelDark);

            Transform dec = Group("WallDetail", g);
            float[] pil = { -16.4f, -10.5f, -5f, 5f, 10.5f, 16.4f };
            foreach (float p in pil)
            {
                if (!NearGap(p, GapsN, 0.45f)) Pilaster(dec, 'N', p);
                if (!NearGap(p, GapsS, 0.45f)) Pilaster(dec, 'S', p);
                if (!NearGap(p, GapsW, 0.45f)) Pilaster(dec, 'W', p);
                if (!NearGap(p, GapsE, 0.45f)) Pilaster(dec, 'E', p);
            }

            Window(dec, 'N', -8f, 3.2f, 3.2f, 1.9f);
            Window(dec, 'N', 8.5f, 3.2f, 3.2f, 1.9f);
            Window(dec, 'S', -7f, 3.2f, 3.2f, 1.9f);
            Window(dec, 'S', 6f, 3.2f, 3.2f, 1.9f);
            Window(dec, 'S', 12.5f, 3.2f, 3.2f, 1.9f);
            Window(dec, 'W', 7f, 3.2f, 3.2f, 1.9f);
            Window(dec, 'W', -8f, 3.2f, 3.2f, 1.9f);
            Window(dec, 'E', -9f, 3.2f, 3.2f, 1.9f);
            Window(dec, 'E', 14.5f, 3.2f, 3.2f, 1.9f);

            foreach (Gap gp in GapsN) Breach(g, 'N', gp.c);
            foreach (Gap gp in GapsS) Breach(g, 'S', gp.c);
            foreach (Gap gp in GapsW) Breach(g, 'W', gp.c);
            Breach(g, 'E', -3.5f);

            BuildLoadingDoor(g);
        }

        static void Pilaster(Transform parent, char side, float c)
        {
            Vector3 a, o;
            SideVectors(side, out a, out o);
            Vector3 pos = o * (Half - 0.175f) + a * c + Vector3.up * (WallH * 0.5f);
            Box(parent, "Pilaster_" + side + c, pos, new Vector3(0.5f, WallH, 0.35f), _steelDark, true, null, Quaternion.LookRotation(o));
        }

        static void Window(Transform parent, char side, float c, float y0, float w, float h)
        {
            Vector3 a, o;
            SideVectors(side, out a, out o);
            Quaternion rot = Quaternion.LookRotation(o);
            Vector3 face = o * Half + a * c;
            Vector3 mid = face - o * 0.03f + Vector3.up * (y0 + h * 0.5f);
            Box(parent, "WindowGlass", mid, new Vector3(w, h, 0.04f), _glass, false, null, rot);
            Vector3 fp = face - o * 0.07f;
            Box(parent, "WinFrameTop", fp + Vector3.up * (y0 + h + 0.06f), new Vector3(w + 0.24f, 0.12f, 0.12f), _steelDark, false, null, rot);
            Box(parent, "WinFrameBottom", fp + Vector3.up * (y0 - 0.06f), new Vector3(w + 0.24f, 0.12f, 0.12f), _steelDark, false, null, rot);
            Box(parent, "WinFrameL", fp + a * (-w * 0.5f - 0.06f) + Vector3.up * (y0 + h * 0.5f), new Vector3(0.12f, h + 0.24f, 0.12f), _steelDark, false, null, rot);
            Box(parent, "WinFrameR", fp + a * (w * 0.5f + 0.06f) + Vector3.up * (y0 + h * 0.5f), new Vector3(0.12f, h + 0.24f, 0.12f), _steelDark, false, null, rot);
            Box(parent, "WinMullionV", fp + Vector3.up * (y0 + h * 0.5f), new Vector3(0.08f, h, 0.08f), _steelDark, false, null, rot);
            Box(parent, "WinMullionH", fp + Vector3.up * (y0 + h * 0.5f), new Vector3(w, 0.08f, 0.08f), _steelDark, false, null, rot);
        }

        /// <summary>
        /// Broken wall opening: pocket walls outside, ragged lintel, rubble beside the opening and a red light.
        /// </summary>
        static void Breach(Transform parent, char side, float c)
        {
            Vector3 a, o;
            SideVectors(side, out a, out o);
            Quaternion rot = Quaternion.LookRotation(o);
            Vector3 inner = o * Half + a * c;
            Transform g = Group("Breach_" + side + "_" + c, parent);

            // Pocket outside the opening (3 walls) so the spawn point reads as a collapsed room.
            const float ph = 4.4f, depth = 2.4f;
            float sideOff = BreachW * 0.5f + 0.3f;
            Vector3 sideMid = inner + o * (WallT + depth * 0.5f);
            Box(g, "PocketWall_A", sideMid + a * sideOff + Vector3.up * (ph * 0.5f), new Vector3(0.6f, ph, depth), WallMat(depth, ph), true, null, rot);
            Box(g, "PocketWall_B", sideMid - a * sideOff + Vector3.up * (ph * 0.5f), new Vector3(0.6f, ph, depth), WallMat(depth, ph), true, null, rot);
            Box(g, "PocketBack", inner + o * (WallT + depth + 0.3f) + Vector3.up * (ph * 0.5f), new Vector3(BreachW + 1.2f, ph, 0.6f), WallMat(BreachW + 1.2f, ph), true, null, rot);

            Material rubbleMat = WallMat(1.5f, 1.5f, true);
            for (int i = 0; i < 8; i++)
            {
                float t = (i % 2 == 0 ? 1f : -1f) * R(1.35f, 1.75f);
                float s = -R(0.4f, 1.7f);
                Vector3 size = new Vector3(R(0.35f, 0.9f), R(0.25f, 0.65f), R(0.35f, 0.9f));
                Vector3 pos = inner + a * t + o * s + Vector3.up * (size.y * 0.5f);
                Box(g, "Rubble", pos, size, rubbleMat, true, new Vector3(R(-8f, 8f), R(0f, 180f), R(-8f, 8f)));
            }
            for (int i = 0; i < 4; i++)
            {
                float t = (i % 2 == 0 ? 1f : -1f) * R(0.3f, 1.2f);
                Vector3 size = new Vector3(R(0.5f, 0.9f), R(0.3f, 0.6f), R(0.5f, 0.9f));
                Vector3 pos = inner + a * t + o * (WallT + R(0.4f, 1.8f)) + Vector3.up * (size.y * 0.5f);
                Box(g, "PocketRubble", pos, size, rubbleMat, true, new Vector3(R(-8f, 8f), R(0f, 180f), R(-8f, 8f)));
            }
            // Jagged lintel: broken pieces hanging into the opening.
            for (int i = 0; i < 5; i++)
            {
                float t = (i % 2 == 0 ? 1f : -1f) * R(0.7f, 1.75f);
                Vector3 size = new Vector3(R(0.25f, 0.55f), R(0.35f, 0.9f), R(0.25f, 0.45f));
                Vector3 pos = inner + a * t + o * R(0.1f, 0.5f) + Vector3.up * (BreachH - size.y * 0.5f + 0.05f);
                Box(g, "Teeth", pos, size, rubbleMat, false, new Vector3(R(-12f, 12f), R(0f, 180f), R(-12f, 12f)));
            }
        }

        static void BuildLoadingDoor(Transform parent)
        {
            Transform g = Group("LoadingDoor", parent);
            const float zc = 8.5f, w = 6f, h = 5f;
            float xi = Half - 0.3f;
            // Frame
            Box(g, "FrameL", new Vector3(xi, h * 0.5f, zc - w * 0.5f - 0.1f), new Vector3(0.6f, h + 0.3f, 0.5f), _steelDark, true);
            Box(g, "FrameR", new Vector3(xi, h * 0.5f, zc + w * 0.5f + 0.1f), new Vector3(0.6f, h + 0.3f, 0.5f), _steelDark, true);
            Box(g, "FrameTop", new Vector3(xi, h + 0.15f, zc), new Vector3(0.6f, 0.4f, w + 0.9f), _steelDark);
            // Raised roll-up door: roll drum + short slatted skirt.
            Cyl(g, "RollDrum", new Vector3(xi - 0.15f, h - 0.45f, zc), 0.42f, w - 0.1f, _steel, false, new Vector3(90f, 0f, 0f));
            Box(g, "Slats", new Vector3(Half - 0.1f, h - 1.15f, zc), new Vector3(0.1f, 0.9f, w - 0.2f), _slats);
            // Hazard stripes on the threshold.
            Material hz = M("WH_HazardStrip", Color.white, 0f, 0.3f, null, _hazard.GetTexture("_BaseMap"), new Vector2(1f, 5f));
            Box(g, "Hazard_Threshold", new Vector3(Half - 0.8f, 0.012f, zc), new Vector3(1.3f, 0.02f, w), hz);
            Box(g, "Hazard_Threshold_Out", new Vector3(Half + 1.0f, 0.012f, zc), new Vector3(0.8f, 0.02f, w), hz);
            // Bollards (yellow).
            foreach (float bz in new[] { 5.0f, 12.0f })
            {
                Cyl(g, "Bollard_In", new Vector3(Half - 1.0f, 0.55f, bz), 0.17f, 1.1f, _yellow, true);
                Cyl(g, "Bollard_Out", new Vector3(Half + 1.6f, 0.55f, bz), 0.17f, 1.1f, _yellow, true);
            }
            // Parked trailer outside (stand-in for the truck of the concept art).
            P("cont", g, 21.4f, 8.5f, 90f, 0f, null, null, true);
            P("jersey", g, 19.3f, 2.6f, 90f);
            P("jersey", g, 19.3f, 14.4f, 80f);
        }

        // ------------------------------------------------------------------
        // Roof and crane
        // ------------------------------------------------------------------
        static void BuildRoofAndCrane()
        {
            Transform g = Group("Roof");
            float[] xs = { -12.75f, -4.25f, 4.25f, 12.75f };
            float[] zs = { -14.17f, -8.5f, -2.83f, 2.83f, 8.5f, 14.17f };
            var open = new HashSet<int> { 2, 5, 7, 9, 11, 12 }; // keys: row * 10 + col (see below)
            open.Clear();
            int[,] cells = { { 0, 2 }, { 1, 1 }, { 1, 3 }, { 2, 1 }, { 2, 2 }, { 3, 1 }, { 3, 2 }, { 4, 2 }, { 5, 0 } };
            for (int i = 0; i < cells.GetLength(0); i++) open.Add(cells[i, 0] * 10 + cells[i, 1]);

            Material roofMat = M("WH_RoofPanel", new Color(0.26f, 0.15f, 0.1f), 0.5f, 0.25f);
            for (int r = 0; r < zs.Length; r++)
                for (int c = 0; c < xs.Length; c++)
                {
                    if (open.Contains(r * 10 + c)) continue;
                    Box(g, "RoofPanel_" + r + "_" + c, new Vector3(xs[c], WallH + 0.07f, zs[r]), new Vector3(8.4f, 0.12f, 5.6f), roofMat);
                }

            // Hanging broken panels at the edge of the central hole.
            Box(g, "BrokenPanel_1", new Vector3(-8.2f, WallH - 0.9f, 0.5f), new Vector3(4f, 0.1f, 2.2f), roofMat, false, new Vector3(0f, 12f, 28f));
            Box(g, "BrokenPanel_2", new Vector3(0.2f, WallH - 0.8f, 2.5f), new Vector3(3.2f, 0.1f, 2.4f), roofMat, false, new Vector3(-24f, 0f, 6f));
            Box(g, "BrokenPanel_3", new Vector3(-4.3f, WallH - 0.7f, 5.5f), new Vector3(2.6f, 0.1f, 2.0f), roofMat, false, new Vector3(35f, 20f, 0f));
            Box(g, "BrokenPanel_4", new Vector3(8.4f, WallH - 1.0f, -9.2f), new Vector3(3.6f, 0.1f, 2.2f), roofMat, false, new Vector3(-18f, 8f, -22f));

            // Trusses (W-E) and purlins (N-S).
            for (int i = 1; i < 6; i++)
            {
                float z = -17f + i * (34f / 6f);
                Box(g, "Truss_" + i, new Vector3(0f, WallH - 0.3f, z), new Vector3(34f, 0.5f, 0.22f), _steelDark);
                Box(g, "TrussFlange_" + i, new Vector3(0f, WallH - 0.04f, z), new Vector3(34f, 0.06f, 0.5f), _steelDark);
            }
            for (int i = 1; i < 4; i++)
            {
                float x = -17f + i * 8.5f;
                Box(g, "Purlin_" + i, new Vector3(x, WallH - 0.1f, 0f), new Vector3(0.18f, 0.22f, 34f), _steelDark);
            }

            // Overhead crane: girder W-E with end trucks, trolley and chains with a hook.
            Transform cr = Group("Crane", g);
            const float cz = 3.2f, cy = 5.15f;
            Box(cr, "Girder", new Vector3(0f, cy, cz), new Vector3(33.4f, 0.8f, 0.55f), _yellow);
            Box(cr, "GirderFlange", new Vector3(0f, cy + 0.45f, cz), new Vector3(33.4f, 0.08f, 0.9f), _steelDark);
            Box(cr, "EndTruck_W", new Vector3(-16.4f, cy, cz), new Vector3(1.2f, 0.9f, 1.4f), _steelDark);
            Box(cr, "EndTruck_E", new Vector3(16.4f, cy, cz), new Vector3(1.2f, 0.9f, 1.4f), _steelDark);
            Box(cr, "Trolley", new Vector3(-3f, cy - 0.7f, cz), new Vector3(1.7f, 0.5f, 1.3f), _steelDark);
            Cyl(cr, "HoistDrum", new Vector3(-3f, cy - 1.05f, cz), 0.22f, 1.0f, _yellow, false, new Vector3(0f, 0f, 90f));
            Chain(cr, -3.25f, cz, cy - 1.0f, 2.3f);
            Chain(cr, -2.75f, cz, cy - 1.0f, 2.3f);
            Box(cr, "HookBlock", new Vector3(-3f, cy - 1.0f - 2.45f, cz), new Vector3(0.5f, 0.35f, 0.3f), _yellow);
            Cyl(cr, "Hook", new Vector3(-3f, cy - 1.0f - 2.85f, cz), 0.05f, 0.5f, _steelDark);
            Box(cr, "Trolley2", new Vector3(7f, cy - 0.7f, cz), new Vector3(1.5f, 0.45f, 1.2f), _steelDark);
            Chain(cr, 7f, cz, cy - 0.95f, 1.5f);
            Box(cr, "HookBlock2", new Vector3(7f, cy - 0.95f - 1.65f, cz), new Vector3(0.4f, 0.3f, 0.25f), _yellow);
        }

        static void Chain(Transform parent, float x, float z, float yTop, float length)
        {
            Transform g = Group("Chain", parent);
            int links = Mathf.RoundToInt(length / 0.13f);
            for (int i = 0; i < links; i++)
                Box(g, "Link", new Vector3(x, yTop - i * 0.13f, z), new Vector3(0.1f, 0.17f, 0.035f), _steelDark, false,
                    new Vector3(0f, i % 2 == 0 ? 0f : 90f, 0f));
        }
    }
}
