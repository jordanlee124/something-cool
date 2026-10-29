using System;
using System.Collections.Generic;

namespace EmberVale.Sim
{
    /// <summary>
    /// The running world. This slice covers the opening of the HTML game: a map, bands of settlers that
    /// pick a site, walk there and found a village (hall, farm, first five settlers). Everything is driven by
    /// Step(dt) with dt in game seconds, exactly like the JS frame loop, and by one seeded Rng.
    /// </summary>
    public sealed class Game
    {
        public readonly World World;
        public readonly Rng Rng;
        public readonly List<Band> Bands = new List<Band>();
        public readonly List<Settlement> Settlements = new List<Settlement>();
        public readonly List<LogEntry> Log = new List<LogEntry>();
        public double Time;   // game seconds since the start
        public int Day => (int)(Time / K.DAY) + 1;
        public int Year => (Day - 1) / K.YEAR + 1;
        public event Action<Settlement> SettlementFounded;

        int nextId = 1;

        public Game(int seed, int startBands)
        {
            Rng = new Rng(seed);
            World = new World(seed);
            World.GrowTo(0, 0, startBands == 1 ? 1 : 2, startBands == 1 ? 2 : 3);
            Setup(startBands);
        }

        void Setup(int n)
        {
            var sites = PickStarts(n);
            foreach (var p in sites)
            {
                // clear a landing meadow, then make sure forest and stone are within reach
                for (int y = p.Y - 4; y <= p.Y + 4; y++)
                    for (int x = p.X - 4; x <= p.X + 4; x++)
                    {
                        var t = World.At(x, y); double d = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));
                        if (t != null && (d < 3.2 || (t.Water && d < 4.5))) { t.T = Ground.Meadow; t.Amt = 0; }
                    }
                foreach (var spec in new[] { (Ground.Forest, 30, 7), (Ground.Rock, 60, 4) })
                {
                    int put = 0, guard = 0;
                    while (put < spec.Item3 && guard++ < 400)
                    {
                        int x = p.X + Rng.Int(-6, 6), y = p.Y + Rng.Int(-6, 6);
                        var t = World.At(x, y);
                        if (t != null && t.T == Ground.Meadow && Dist(x, y, p.X, p.Y) > 3.4) { t.T = spec.Item1; t.Amt = spec.Item2; put++; }
                    }
                }
            }
            var used = new HashSet<string>();
            for (int i = 0; i < sites.Count; i++)
            {
                string name = i == 0 ? "Ember Vale" : PickName(used);
                used.Add(name);
                var b = new Band
                {
                    Id = nextId++, Name = name, Color = K.Colors[i % K.Colors.Length],
                    X = sites[i].X, Y = sites[i].Y, Start = sites[i], N = 5,
                };
                PlanBand(b);
                Bands.Add(b);
            }
            Note(n == 1 ? "Five settlers land in the vale and set out to find a place to live."
                        : n + " bands of settlers land in the vale and set out to find a place to live, each unaware of what the others intend.");
        }

        string PickName(HashSet<string> used)
        {
            var pool = new List<string>();
            foreach (var s in K.FactionNames) if (!used.Contains(s)) pool.Add(s);
            return Rng.Pick(pool);
        }

        static double Dist(double ax, double ay, double bx, double by) { return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)); }

        List<Pt> PickStarts(int n)
        {
            var W = World; var ok = new List<Pt>();
            for (int y = W.Y0 + 7; y < W.Y1 - 7; y++)
                for (int x = W.X0 + 7; x < W.X1 - 7; x++)
                {
                    bool wet = false;
                    for (int yy = y - 4; yy <= y + 4 && !wet; yy++)
                        for (int xx = x - 4; xx <= x + 4; xx++) if (W.At(xx, yy).Water) { wet = true; break; }
                    if (!wet) ok.Add(new Pt(x, y));
                }
            var mid = new Pt(W.X0 + (W.W >> 1), W.Y0 + (W.H >> 1));
            if (ok.Count == 0) ok.Add(mid);
            if (n == 1)
            {
                Pt best = ok[0];
                foreach (var p in ok) if (Dist(p.X, p.Y, mid.X, mid.Y) < Dist(best.X, best.Y, mid.X, mid.Y)) best = p;
                return new List<Pt> { best };
            }
            List<Pt> bestPts = null; double bs = -1;
            for (int tries = 0; tries < 300; tries++)
            {
                var pts = new List<Pt>();
                for (int i = 0; i < n; i++) pts.Add(Rng.Pick(ok));
                double md = 1e9;
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) md = Math.Min(md, Dist(pts[i].X, pts[i].Y, pts[j].X, pts[j].Y));
                if (md > bs) { bs = md; bestPts = pts; }
            }
            return bestPts;
        }

        // ---------- bands of settlers (port of class Band) ----------

        /// <summary>How good a place to settle is, or null if it can't be a village at all.</summary>
        public (double score, Site site)? ScoreSite(int x, int y, double d, List<Band> others)
        {
            var W = World; var T = W.At(x, y);
            if (T == null || T.T != Ground.Meadow || T.Building != 0 || x < W.X0 + 4 || y < W.Y0 + 4 || x >= W.X1 - 4 || y >= W.Y1 - 4) return null;
            foreach (var o in others) if (o.HasSite && Dist(o.Site.X, o.Site.Y, x, y) < 11) return null;
            double open = 0; int forest = 0, rock = 0, water = 0;
            for (int yy = y - 6; yy <= y + 6; yy++)
                for (int xx = x - 6; xx <= x + 6; xx++)
                {
                    var U = W.At(xx, yy); if (U == null) continue;
                    bool near = Math.Abs(xx - x) <= 2 && Math.Abs(yy - y) <= 2;
                    if (U.T == Ground.Meadow) { if (near) open++; }
                    else if (U.T == Ground.Forest) { forest++; if (near) open += 0.6; }
                    else if (U.T == Ground.Rock) rock++;
                    else if (U.T == Ground.Water && Math.Abs(xx - x) <= 5 && Math.Abs(yy - y) <= 5) water++;
                }
            if (open < 15) return null;
            double sc = open * 0.6 + Math.Min(forest, 10) * 0.7 + (rock > 0 ? 4 + Math.Min(rock, 3) : -10) + (forest < 2 ? -8 : 0)
                      + (water > 0 ? 3 + Math.Min(water, 6) * 0.4 : 0) - d * 0.25 + Rng.Next() * 4;
            return (sc, new Site { X = x, Y = y, Forest = forest, Rock = rock, Water = water });
        }

        /// <summary>Pick where this band is heading: the best place within a day or two's walk, maybe after a look elsewhere.</summary>
        void PlanBand(Band b)
        {
            var cands = new List<(double score, Site site)>();
            for (int y = b.Start.Y - 16; y <= b.Start.Y + 16; y += 2)
                for (int x = b.Start.X - 16; x <= b.Start.X + 16; x += 2)
                {
                    double d = Dist(x, y, b.Start.X, b.Start.Y);
                    if (d > 16 || d < 6) continue;   // the landing place is only a clearing: settlers look further afield
                    var r = ScoreSite(x, y, d, Bands); if (r.HasValue) cands.Add(r.Value);
                }
            cands.Sort((p, q) => q.score.CompareTo(p.score));
            Site B = cands.Count > 0 ? cands[0].site : new Site { X = b.Start.X, Y = b.Start.Y, Forest = 3, Rock = 1 };
            double bScore = cands.Count > 0 ? cands[0].score : 0;
            Site? A = null;
            foreach (var c in cands) if (Dist(c.site.X, c.site.Y, B.X, B.Y) >= 7 && c.score > bScore - 12) { A = c.site; break; }
            b.Site = B; b.HasSite = true;
            b.Waypoints = A.HasValue ? new List<Pt> { new Pt(A.Value.X, A.Value.Y), new Pt(B.X, B.Y) } : new List<Pt> { new Pt(B.X, B.Y) };
            b.Path = new List<Pt>();
            b.Wait = K.DAY * (0.3 + Rng.Next() * 0.5);
            b.Camping = true;
        }

        public void Step(double dt)
        {
            Time += dt;
            foreach (var b in Bands.ToArray()) StepBand(b, dt);
        }

        void StepBand(Band b, double dt)
        {
            b.Age += dt;
            if (b.Wait > 0) { b.Wait -= dt; if (b.Wait <= 0) b.Camping = false; return; }
            if (b.Path.Count == 0)
            {
                if (b.Waypoints.Count == 0) { Found(b); return; }
                var t = b.Waypoints[0];
                b.Path = World.FindPath((int)Math.Round(b.X), (int)Math.Round(b.Y), t.X, t.Y, 14) ?? new List<Pt> { t };
            }
            double left = K.BAND_SPEED * dt;
            while (left > 0 && b.Path.Count > 0)
            {
                var n = b.Path[0]; double dx = n.X - b.X, dy = n.Y - b.Y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d <= left) { b.X = n.X; b.Y = n.Y; b.Path.RemoveAt(0); left -= d; }
                else { b.X += dx / d * left; b.Y += dy / d * left; left = 0; }
            }
            if (b.Path.Count == 0)
            {
                b.Waypoints.RemoveAt(0); b.Camping = true;
                if (b.Waypoints.Count > 0)
                {
                    b.Wait = K.DAY * 0.9;
                    if (Rng.Next() < 0.7) Note("The settlers of " + b.Name + " camped to weigh up the country" + (b.Site.Water > 0 ? " by the water" : "") + ", then moved on.");
                }
                else b.Wait = K.DAY * 0.6;
            }
        }

        void Found(Band b)
        {
            int x = (int)Math.Round(b.X), y = (int)Math.Round(b.Y);
            for (int yy = y - 2; yy <= y + 2; yy++)
                for (int xx = x - 2; xx <= x + 2; xx++)
                {
                    var T = World.At(xx, yy);
                    if (T != null && (T.T == Ground.Forest || T.T == Ground.Rock) && Dist(xx, yy, x, y) < 1.7) { T.T = Ground.Meadow; T.Amt = 0; }
                }
            Bands.Remove(b);
            var s = new Settlement { Id = nextId++, Name = b.Name, Color = b.Color, CentreX = x, CentreY = y };
            Settlements.Add(s);
            var hall = World.At(x, y); hall.T = Ground.Meadow; hall.Amt = 0;
            AddBuilding(s, "tc", x, y, true);
            var fieldTile = World.At(x - 2, y + 1);
            if (fieldTile != null && fieldTile.Building == 0 && !fieldTile.Water) { fieldTile.T = Ground.Meadow; fieldTile.Amt = 0; AddBuilding(s, "farm", x - 2, y + 1, true); }
            foreach (var job in new[] { "farmer", "farmer", "woodcutter", "woodcutter", "builder" })
                s.People.Add(new Colonist { Id = nextId++, Settlement = s.Id, Job = job, X = x, Y = y });
            string why = b.Site.Water > 0 ? "beside the water" : b.Site.Rock >= 3 ? "under the rocks" : b.Site.Forest >= 8 ? "at the edge of the forest" : "on open ground";
            Note("The settlers of " + s.Name + " raised their first hall " + why + ".", LogKind.Good);
            SettlementFounded?.Invoke(s);
        }

        Building AddBuilding(Settlement s, string type, int x, int y, bool built)
        {
            var b = new Building { Id = nextId++, Settlement = s.Id, Type = type, X = x, Y = y, Built = built };
            s.Buildings.Add(b); World.At(x, y).Building = b.Id;
            return b;
        }

        void Note(string text, LogKind kind = LogKind.Info) { Log.Insert(0, new LogEntry(Day, text, kind)); if (Log.Count > 160) Log.RemoveAt(Log.Count - 1); }
    }
}
