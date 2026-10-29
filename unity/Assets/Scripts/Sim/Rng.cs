using System;

namespace EmberVale.Sim
{
    /// <summary>
    /// Seeded random numbers (mulberry32). The HTML game uses Math.random, which can't be seeded;
    /// the port needs reproducible runs so sim tests and replays are possible.
    /// </summary>
    public sealed class Rng
    {
        uint s;
        public Rng(int seed) { s = (uint)seed; }

        /// <summary>0 &lt;= value &lt; 1, like Math.random().</summary>
        public double Next()
        {
            unchecked
            {
                s += 0x6D2B79F5u;
                uint t = s;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>Inclusive integer range, like the HTML game's ri(a, b).</summary>
        public int Int(int a, int b) { return a + (int)Math.Floor(Next() * (b - a + 1)); }
        public T Pick<T>(System.Collections.Generic.IList<T> list) { return list[(int)Math.Floor(Next() * list.Count)]; }
    }
}
