using System;

namespace EmberVale.Sim
{
    /// <summary>
    /// Terrain noise. These two functions are a line-for-line port of hash2/noise2 in ember-vale.html,
    /// so the same seed produces the same land in both versions (checked by Tools~/parity).
    /// </summary>
    public static class Noise
    {
        public static double Hash2(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                h ^= (int)((uint)h >> 16);
                return (uint)h / 4294967296.0;
            }
        }

        public static double Noise2(double x, double y, double scale, int seed)
        {
            double fx = x / scale, fy = y / scale;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            double a = Hash2(x0, y0, seed), b = Hash2(x0 + 1, y0, seed), c = Hash2(x0, y0 + 1, seed), d = Hash2(x0 + 1, y0 + 1, seed);
            return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
        }
    }
}
