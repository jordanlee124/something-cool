using System;
using System.Collections.Generic;

namespace EmberVale.Sim
{
    public sealed class Tile
    {
        public Terrain T;
        public int Amt;          // what is left of the forest / rock
        public double H;         // per-tile random, used for visual variety
        public int Building;     // id of the building standing here, 0 if none
        public int Owner;        // settlement id owning this tile, 0 if none
        public bool Water => T == Terrain.Water;
    }

    /// <summary>
    /// A rectangle of tiles generated from a seed. Port of the World class in ember-vale.html
    /// (the chunk growth, borders and road logic come in later slices).
    /// </summary>
    public sealed class World
    {
        public readonly int Seed;
        public int X0, Y0, W, H;
        Tile[] tiles = new Tile[0];

        public World(int seed) { Seed = seed; }
        public int X1 => X0 + W;
        public int Y1 => Y0 + H;

        public bool Inside(int x, int y) { return x >= X0 && y >= Y0 && x < X1 && y < Y1; }
        public Tile At(int x, int y) { return Inside(x, y) ? tiles[(y - Y0) * W + (x - X0)] : null; }

        Tile Generate(int x, int y)
        {
            int sd = Seed;
            double h = Noise.Hash2(x, y, sd + 9);
            if (Noise.Noise2(x, y, 7, sd + 11) + (Noise.Hash2(x, y, sd + 5) - 0.5) * 0.06 > 0.82) return new Tile { T = Terrain.Water, H = h };
            if (Noise.Noise2(x, y, 4.5, sd + 1) + Noise.Hash2(x, y, sd + 2) * 0.14 > 0.64) return new Tile { T = Terrain.Forest, Amt = 30, H = h };
            if (Noise.Noise2(x, y, 4.8, sd + 3) + Noise.Hash2(x, y, sd + 4) * 0.12 > 0.72) return new Tile { T = Terrain.Rock, Amt = 60, H = h };
            return new Tile { T = Terrain.Meadow, H = h };
        }

        /// <summary>Cover chunk range [cx0..cx1] x [cy0..cy1], keeping tiles that already exist.</summary>
        public void GrowTo(int cx0, int cy0, int cx1, int cy1)
        {
            int nx0 = cx0, ny0 = cy0, nx1 = cx1, ny1 = cy1;
            if (W > 0)
            {
                int ox0 = (int)Math.Floor((double)X0 / K.CS), oy0 = (int)Math.Floor((double)Y0 / K.CS);
                int ox1 = ox0 + W / K.CS - 1, oy1 = oy0 + H / K.CS - 1;
                nx0 = Math.Min(ox0, cx0); ny0 = Math.Min(oy0, cy0); nx1 = Math.Max(ox1, cx1); ny1 = Math.Max(oy1, cy1);
            }
            int x0 = nx0 * K.CS, y0 = ny0 * K.CS, w2 = (nx1 - nx0 + 1) * K.CS, h2 = (ny1 - ny0 + 1) * K.CS;
            var next = new Tile[w2 * h2];
            for (int y = y0; y < y0 + h2; y++)
                for (int x = x0; x < x0 + w2; x++)
                    next[(y - y0) * w2 + (x - x0)] = At(x, y) ?? Generate(x, y);
            X0 = x0; Y0 = y0; W = w2; H = h2; tiles = next;
        }

        /// <summary>
        /// Cheapest 4-way walk from (sx,sy) to (tx,ty) inside a box around the trip (A*). Water blocks except the goal.
        /// Returns the tiles to step onto, excluding the start; null if there is no route.
        /// </summary>
        public List<Pt> FindPath(int sx, int sy, int tx, int ty, int margin)
        {
            int bx0 = Math.Max(X0, Math.Min(sx, tx) - margin), by0 = Math.Max(Y0, Math.Min(sy, ty) - margin);
            int bx1 = Math.Min(X1 - 1, Math.Max(sx, tx) + margin), by1 = Math.Min(Y1 - 1, Math.Max(sy, ty) + margin);
            if (tx < bx0 || tx > bx1 || ty < by0 || ty > by1 || sx < bx0 || sx > bx1 || sy < by0 || sy > by1) return null;
            int bw = bx1 - bx0 + 1, bh = by1 - by0 + 1, n = bw * bh;
            int start = (sy - by0) * bw + (sx - bx0), goal = (ty - by0) * bw + (tx - bx0);
            var g = new double[n]; var prev = new int[n]; var closed = new bool[n];
            for (int i = 0; i < n; i++) g[i] = double.PositiveInfinity;
            var heap = new MinHeap();
            g[start] = 0; prev[start] = start;
            heap.Push(start, Heur(start, goal, bw));
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (heap.Count > 0)
            {
                int cur = heap.Pop();
                if (closed[cur]) continue;
                closed[cur] = true;
                if (cur == goal) break;
                int x = cur % bw, y = cur / bw;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= bw || ny >= bh) continue;
                    int nn = ny * bw + nx;
                    if (closed[nn]) continue;
                    if (nn != goal && At(nx + bx0, ny + by0).Water) continue;
                    double ng = g[cur] + 1;
                    if (ng >= g[nn]) continue;
                    g[nn] = ng; prev[nn] = cur;
                    heap.Push(nn, ng + Heur(nn, goal, bw));
                }
            }
            if (double.IsPositiveInfinity(g[goal])) return null;
            var path = new List<Pt>();
            for (int c = goal; c != start; c = prev[c]) path.Add(new Pt(c % bw + bx0, c / bw + by0));
            path.Reverse();
            return path;
        }

        static double Heur(int a, int goal, int bw) { return Math.Abs(a % bw - goal % bw) + Math.Abs(a / bw - goal / bw); }

        /// <summary>Terrain as one digit per tile, row by row. Used by the JS parity check.</summary>
        public string TerrainString()
        {
            var sb = new System.Text.StringBuilder(tiles.Length);
            foreach (var t in tiles) sb.Append((int)t.T);
            return sb.ToString();
        }

        sealed class MinHeap
        {
            readonly List<int> items = new List<int>(); readonly List<double> keys = new List<double>();
            public int Count => items.Count;
            public void Push(int v, double k)
            {
                items.Add(v); keys.Add(k);
                int i = items.Count - 1;
                while (i > 0) { int p = (i - 1) >> 1; if (keys[p] <= keys[i]) break; Swap(i, p); i = p; }
            }
            public int Pop()
            {
                int top = items[0], last = items.Count - 1;
                items[0] = items[last]; keys[0] = keys[last];
                items.RemoveAt(last); keys.RemoveAt(last);
                int i = 0, n = items.Count;
                for (;;)
                {
                    int c = 2 * i + 1;
                    if (c >= n) break;
                    if (c + 1 < n && keys[c + 1] < keys[c]) c++;
                    if (keys[c] >= keys[i]) break;
                    Swap(i, c); i = c;
                }
                return top;
            }
            void Swap(int a, int b)
            {
                int tv = items[a]; items[a] = items[b]; items[b] = tv;
                double tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
            }
        }
    }

    public struct Pt
    {
        public int X, Y;
        public Pt(int x, int y) { X = x; Y = y; }
    }
}
