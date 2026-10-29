using System;
using System.Collections.Generic;

namespace EmberVale.Sim
{
    /// <summary>
    /// Life in a founded village (port of Colonist, the opening of Steward and Settlement.daily for the Settlement age):
    /// farmers work the fields, woodcutters fell trees, builders raise huts and farms, everyone carries loads to the hall,
    /// and each day the village eats, freezes or grows. Stone, roads, storehouses and the later ages come in later slices.
    /// </summary>
    public sealed partial class Game
    {
        public event Action<Settlement, Building> BuildingPlaced, BuildingFinished;
        public event Action<Settlement, Colonist> ColonistBorn, ColonistDied;
        public event Action<int, int> TileChanged;   // a tree was felled or a site was cleared

        // name, wood cost, build time (s), people housed, work places  (BUILD in the JS)
        static readonly Dictionary<string, (int wood, double time, int house, int slots)> Kinds = new Dictionary<string, (int, double, int, int)>
        {
            { "tc", (0, 1, 5, 0) }, { "hut", (12, 8, 3, 0) }, { "farm", (8, 6, 0, 2) },
        };

        int lastDay = 1;
        const int WantStone = 30;   // Settlement.want('stone') at age 0: 30 + age * 30 + upkeep (no stone upkeep yet)

        /// <summary>0 spring, 1 summer, 2 autumn, 3 winter.</summary>
        public int Season => ((Day - 1) % K.YEAR) / K.SEASON_DAYS;

        public static double BuildTime(string type) { return Kinds[type].time; }
        public static int HousingOf(string type) { return Kinds.TryGetValue(type, out var k) ? k.house : 0; }

        void StepVillages(double dt)
        {
            foreach (var s in Settlements.ToArray())
            {
                s.AiTimer += dt;
                if (s.AiTimer >= 1.5) { s.AiTimer = 0; AssignJobs(s); Build(s); }
                foreach (var c in s.People.ToArray()) UpdateColonist(s, c, dt);
            }
            while (lastDay < Day) { lastDay++; foreach (var s in Settlements.ToArray()) DailyVillage(s); }
        }

        // ---------- the steward ----------

        int Slots(Settlement s, string type)
        {
            int n = 0; foreach (var b in s.Buildings) if (b.Built && b.Type == type) n += Kinds[type].slots; return n;
        }

        int Sites(Settlement s) { int n = 0; foreach (var b in s.Buildings) if (!b.Built) n++; return n; }

        public int Housing(Settlement s) { int n = 0; foreach (var b in s.Buildings) if (b.Built) n += Kinds[b.Type].house; return n; }
        int FutureHousing(Settlement s) { int n = 0; foreach (var b in s.Buildings) n += Kinds[b.Type].house; return n; }

        /// <summary>Keeps the right number of hands on food, building and wood (Steward.assignJobs, Settlement age).</summary>
        void AssignJobs(Settlement s)
        {
            int pop = s.Pop, se = Season;
            if (pop == 0) return;
            int slots = Slots(s, "farm"), sites = Sites(s);
            double cap = K.CAP;
            double share = se == 3 ? 0.42 : se == 2 ? 0.5 : 0.36;
            if (s.Food < pop * 3) share += 0.1;
            // after the harvest the granary should carry the town through winter and spring
            double stock = pop * 12;
            if (s.Food > Math.Min(cap * 0.9, stock)) share = Math.Min(share, se == 3 ? 0.3 : 0.18);
            var want = new Dictionary<string, int>();
            want["farmer"] = Math.Min(slots, Math.Max(1, (int)Math.Ceiling(pop * share)));
            want["builder"] = sites > 0 ? Math.Min((int)Math.Ceiling(sites * 1.6), Math.Max(1, (int)Math.Ceiling(pop * 0.18))) : 0;
            // workers go where the town is short: miners until the stone store reaches what the village wants
            bool stoneHungry = s.Stone < Math.Min(cap * 0.85, WantStone * 2);
            want["miner"] = stoneHungry ? Math.Max(1, (int)Math.Ceiling(pop * 0.15)) : 0;
            int rest = pop - want["farmer"] - want["builder"] - want["miner"];
            if (rest < 0) { int cut = Math.Min(want["miner"], -rest); want["miner"] -= cut; rest += cut; }   // short of hands: miners wait first
            if (rest < 0) { int cut = Math.Min(Math.Max(0, want["builder"] - (sites > 0 ? 1 : 0)), -rest); want["builder"] -= cut; rest += cut; }
            want["woodcutter"] = Math.Max(0, rest);
            // more wood than the village can hold is no use: spare hands tend the fields instead
            if (s.Wood >= cap * 0.95 && want["woodcutter"] > 1)
            {
                int m = (int)Math.Floor(want["woodcutter"] * 0.7);
                want["woodcutter"] -= m;
                int room = Math.Max(0, slots - want["farmer"]);
                int toFarm = Math.Min(m, room);
                if (stoneHungry) want["miner"] += m;
                else { want["farmer"] += toFarm; want["builder"] += m - toFarm; }
            }
            var have = new Dictionary<string, int> { { "farmer", 0 }, { "builder", 0 }, { "miner", 0 }, { "woodcutter", 0 } };
            foreach (var c in s.People) have[c.Job]++;
            foreach (var k in new[] { "farmer", "builder", "miner", "woodcutter" })
            {
                while (have[k] > want[k])
                {
                    Colonist o = null;
                    foreach (var c in s.People) if (c.Job == k && c.Carry == null && c.State != Act.Work) { o = c; break; }
                    if (o == null) foreach (var c in s.People) if (c.Job == k && c.Carry == null) { o = c; break; }
                    if (o == null) foreach (var c in s.People) if (c.Job == k) { o = c; break; }
                    string to = null;
                    foreach (var j in new[] { "farmer", "builder", "miner", "woodcutter" }) if (have[j] < want[j]) { to = j; break; }
                    if (o == null || to == null) break;
                    o.Job = to; have[k]--; have[to]++;
                    if (o.Carry == null) { o.State = Act.Idle; o.Path.Clear(); o.Wait = 0; o.TargetBuilding = 0; }
                }
            }
        }

        /// <summary>Starts a new home or field when the village needs one (Steward.build, Settlement age).</summary>
        void Build(Settlement s)
        {
            int pop = s.Pop;
            if (Sites(s) >= 1 + pop / 10) return;
            if (Slots(s, "farm") < (int)Math.Ceiling(pop * 0.42) + 1 && StartSite(s, "farm")) return;
            if (pop >= FutureHousing(s) - 1 && pop < K.POPCAP0 + 5) StartSite(s, "hut");
        }

        bool StartSite(Settlement s, string type)
        {
            int cost = Kinds[type].wood;
            if (s.Wood < cost) return false;
            var spot = PlaceSpot(s, type);
            if (!spot.HasValue) return false;
            var T = World.At(spot.Value.X, spot.Value.Y);
            if (T.T == Ground.Forest) { s.Wood = Math.Min(K.CAP, s.Wood + T.Amt); T.T = Ground.Meadow; T.Amt = 0; TileChanged?.Invoke(spot.Value.X, spot.Value.Y); }
            s.Wood -= cost;
            AddBuilding(s, type, spot.Value.X, spot.Value.Y, false);
            return true;
        }

        int Near(int x, int y, int r, Func<Tile, bool> pred)
        {
            int n = 0;
            for (int yy = y - r; yy <= y + r; yy++)
                for (int xx = x - r; xx <= x + r; xx++)
                {
                    if (xx == x && yy == y) continue;
                    var T = World.At(xx, yy); if (T != null && pred(T)) n++;
                }
            return n;
        }

        Func<Tile, bool> IsType(Settlement s, params string[] types)
        {
            return T =>
            {
                if (T.Building == 0) return false;
                foreach (var b in s.Buildings) if (b.Id == T.Building) return Array.IndexOf(types, b.Type) >= 0;
                return false;
            };
        }

        Pt? PlaceSpot(Settlement s, string type)
        {
            int reach = 5 + 4;   // RADIUS[age] + 4
            Pt? best = null; double bs = 1e9;
            var homes = IsType(s, "tc", "hut"); var farms = IsType(s, "farm");
            for (int y = s.CentreY - reach; y <= s.CentreY + reach; y++)
                for (int x = s.CentreX - reach; x <= s.CentreX + reach; x++)
                {
                    var T = World.At(x, y);
                    if (T == null || T.Building != 0 || (T.T != Ground.Meadow && T.T != Ground.Forest)) continue;
                    // a building needs a neighbour it can be reached from
                    bool access = false;
                    foreach (var d in new[] { new Pt(1, 0), new Pt(-1, 0), new Pt(0, 1), new Pt(0, -1) })
                    { var N = World.At(x + d.X, y + d.Y); if (N != null && !N.Water && N.Building == 0) { access = true; break; } }
                    if (!access) continue;
                    double dist = Dist(x, y, s.CentreX, s.CentreY), j = Rng.Next();
                    if (dist < 1.5) continue;
                    double sc = T.T == Ground.Forest ? 1.8 : 0;
                    if (type == "hut") sc += dist * 1.3 - Near(x, y, 1, homes) * 0.9 + Near(x, y, 1, farms) * 1.5 + j * 0.6;
                    else sc += Math.Abs(dist - (3 + Math.Sqrt(s.Buildings.Count) * 0.75)) * 0.8 - Near(x, y, 1, farms) * 1.3 + Near(x, y, 1, homes) * 1.2 + j * 0.8;
                    if (sc < bs) { bs = sc; best = new Pt(x, y); }
                }
            return best;
        }

        // ---------- colonists ----------

        Building Drop(Settlement s, double x, double y)
        {
            Building best = null; double bd = 1e9;
            foreach (var b in s.Buildings)
                if (b.Built && b.Type == "tc") { double d = Math.Abs(b.X - x) + Math.Abs(b.Y - y); if (d < bd) { bd = d; best = b; } }
            return best;
        }

        int ClaimsOn(Settlement s, Colonist me, Func<Colonist, bool> pred)
        {
            int n = 0; foreach (var o in s.People) if (o != me && pred(o)) n++; return n;
        }

        Building Find(Settlement s, int id) { foreach (var b in s.Buildings) if (b.Id == id) return b; return null; }

        /// <summary>Where this colonist should work next, or null: a free field, an unfinished building, or the nearest trees.</summary>
        bool FindTarget(Settlement s, Colonist c, out int tid, out int tx, out int ty)
        {
            tid = 0; tx = 0; ty = 0;
            if (c.Job == "farmer" || c.Job == "builder")
            {
                bool farmer = c.Job == "farmer"; Building best = null; double bd = 1e9;
                foreach (var b in s.Buildings)
                {
                    if (farmer ? !(b.Built && b.Type == "farm") : b.Built) continue;
                    int slots = farmer ? Kinds[b.Type].slots : 3;
                    if (b.Id != c.TargetBuilding && ClaimsOn(s, c, o => o.Job == c.Job && o.TargetBuilding == b.Id) >= slots) continue;
                    double d = Dist(c.X, c.Y, b.X, b.Y) - (b.Id == c.TargetBuilding ? 100 : 0);
                    if (d < bd) { bd = d; best = b; }
                }
                if (best == null) return false;
                tid = best.Id; tx = best.X; ty = best.Y; return true;
            }
            // woodcutter / miner: nearest tree or rock not already taken, preferring ones near the hall
            // shortcut: miners take loose rock only; quarries come with the later ages
            var kind = c.Job == "miner" ? Ground.Rock : Ground.Forest;
            var drop = Drop(s, c.X, c.Y);
            foreach (int r in new[] { 10, 26 })
            {
                int cx = (int)Math.Round(c.X), cy = (int)Math.Round(c.Y); double bestD = 1e9; bool found = false;
                for (int y = Math.Max(World.Y0, cy - r); y <= Math.Min(World.Y1 - 1, cy + r); y++)
                    for (int x = Math.Max(World.X0, cx - r); x <= Math.Min(World.X1 - 1, cx + r); x++)
                    {
                        var T = World.At(x, y);
                        if (T.T != kind || T.Amt <= 0 || T.Building != 0) continue;
                        int taken = ClaimsOn(s, c, o => o.Job == c.Job && o.TargetBuilding == 0 && o.TX == x && o.TY == y && o.State != Act.Idle);
                        if (taken >= 2) continue;
                        double d = Dist(c.X, c.Y, x, y) * 0.5 + taken * 3;
                        if (d >= bestD) continue;
                        double hd = drop != null ? Math.Abs(drop.X - x) + Math.Abs(drop.Y - y) : 0;
                        d += hd;
                        if (d < bestD) { bestD = d; tx = x; ty = y; found = true; }
                    }
                if (found) return true;
            }
            return false;
        }

        void Plan(Settlement s, Colonist c)
        {
            if (c.Carry != null) { GoStore(s, c); return; }
            if (!FindTarget(s, c, out int tid, out int tx, out int ty))
            {
                // with nothing to do, people stroll round the town square
                var hall = Drop(s, c.X, c.Y);
                int hx = hall != null ? hall.X : (int)Math.Round(c.X), hy = hall != null ? hall.Y : (int)Math.Round(c.Y);
                int x = hx + Rng.Int(-3, 3), y = hy + Rng.Int(-3, 3);
                var T = World.At(x, y);
                c.TargetBuilding = 0; c.TX = x; c.TY = y;
                List<Pt> p = T != null && !T.Water ? World.FindPath((int)Math.Round(c.X), (int)Math.Round(c.Y), x, y, 14) : null;
                if (p != null && p.Count > 0) { c.State = Act.Wander; c.Path = p; } else { c.State = Act.Idle; c.Wait = 1 + Rng.Next() * 2; }
                return;
            }
            c.TargetBuilding = tid; c.TX = tx; c.TY = ty;
            var path = World.FindPath((int)Math.Round(c.X), (int)Math.Round(c.Y), tx, ty, 14);
            if (path == null) { c.State = Act.Idle; c.Wait = 2; return; }
            c.State = Act.GoWork; c.Path = path;
            if (path.Count == 0) Arrive(s, c);
        }

        void GoStore(Settlement s, Colonist c)
        {
            var d = Drop(s, c.X, c.Y);
            var p = d != null ? World.FindPath((int)Math.Round(c.X), (int)Math.Round(c.Y), d.X, d.Y, 14) : null;
            if (p == null) { c.Carry = null; c.CarryN = 0; c.State = Act.Idle; c.Wait = 1; return; }
            c.State = Act.GoStore; c.Path = p;
            if (p.Count == 0) Arrive(s, c);
        }

        void Arrive(Settlement s, Colonist c)
        {
            if (c.State == Act.GoWork) { c.State = Act.Work; c.Work = 0; }
            else if (c.State == Act.GoStore)
            {
                if (c.Carry == "food") s.Food = Math.Min(K.CAP, s.Food + c.CarryN);
                else if (c.Carry == "wood") s.Wood = Math.Min(K.CAP, s.Wood + c.CarryN);
                else if (c.Carry == "stone") s.Stone = Math.Min(K.CAP, s.Stone + c.CarryN);
                c.Carry = null; c.CarryN = 0; c.State = Act.Idle; c.Wait = 0.2;
            }
            else { c.State = Act.Idle; c.Wait = 1 + Rng.Next() * 2; }
        }

        int FarmYield(Settlement s)
        {
            int se = Season;
            if (se == 3) return 1;
            if (se == 2) return 3;   // the autumn harvest, in an average year
            return 1 + (Rng.Next() < (se == 0 ? 0.3 : 0.45) ? 1 : 0);   // off-season fields only just keep pace
        }

        void Quit(Colonist c) { c.State = Act.Idle; c.Wait = 0.4; }

        void DoWork(Settlement s, Colonist c, double dt)
        {
            if (c.Job == "farmer")
            {
                var b = Find(s, c.TargetBuilding);
                if (b == null || !b.Built) { Quit(c); return; }
                c.Work += dt;
                if (c.Work >= 5) { c.Carry = "food"; c.CarryN = FarmYield(s); GoStore(s, c); }
            }
            else if (c.Job == "woodcutter" || c.Job == "miner")
            {
                bool cut = c.Job == "woodcutter";
                var T = World.At(c.TX, c.TY);
                if (T == null || T.T != (cut ? Ground.Forest : Ground.Rock) || T.Amt <= 0) { Quit(c); return; }
                c.Work += dt;
                if (c.Work >= (cut ? 5 : 6))
                {
                    int n = Math.Min(cut ? 3 : 2, T.Amt);
                    T.Amt -= n;
                    if (T.Amt <= 0) { T.T = Ground.Meadow; T.Amt = 0; TileChanged?.Invoke(c.TX, c.TY); }
                    c.Carry = cut ? "wood" : "stone"; c.CarryN = n; GoStore(s, c);
                }
            }
            else
            {
                var b = Find(s, c.TargetBuilding);
                if (b == null || b.Built) { Quit(c); return; }
                b.Prog += dt;
                if (b.Prog >= Kinds[b.Type].time)
                {
                    b.Built = true;
                    BuildingFinished?.Invoke(s, b);
                    Quit(c);
                }
            }
        }

        void UpdateColonist(Settlement s, Colonist c, double dt)
        {
            if (c.Path.Count > 0)
            {
                double left = dt;
                while (left > 0 && c.Path.Count > 0)
                {
                    var n = c.Path[0]; double dx = n.X - c.X, dy = n.Y - c.Y, d = Math.Sqrt(dx * dx + dy * dy), v = K.SPEED;
                    if (d <= v * left) { c.X = n.X; c.Y = n.Y; c.Path.RemoveAt(0); left -= d / v; }
                    else { c.X += dx / d * v * left; c.Y += dy / d * v * left; left = 0; }
                }
                if (c.Path.Count == 0) Arrive(s, c);
                return;
            }
            if (c.State == Act.Work) DoWork(s, c, dt);
            else if (c.State == Act.Idle) { c.Wait -= dt; if (c.Wait <= 0) Plan(s, c); }
            else Plan(s, c);
        }

        // ---------- each day (Settlement.daily, Settlement age) ----------

        void DailyVillage(Settlement s)
        {
            if (s.Pop == 0) return;
            int se = Season, hungry = 0, starved = 0;
            var order = new List<Colonist>(s.People);
            for (int i = order.Count - 1; i > 0; i--) { int j = Rng.Int(0, i); var t = order[i]; order[i] = order[j]; order[j] = t; }
            foreach (var c in order)
            {
                if (s.Food >= 1) { s.Food--; c.Hunger = 0; }
                else { c.Hunger++; hungry++; if (c.Hunger >= 3) { Remove(s, c); starved++; } }
            }
            if (starved > 0) Note(starved + (starved == 1 ? " person starved in " : " people starved in ") + s.Name + ".", LogKind.Info);
            else if (hungry > 0) Note(hungry + " went to bed hungry in " + s.Name + ".", LogKind.Info);
            if (se == 3 && s.Pop > 0)
            {
                int need = (int)Math.Ceiling(s.Pop / 4.0);
                if (s.Wood >= need) s.Wood -= need;
                else
                {
                    s.Wood = 0;
                    if (Rng.Next() < 0.35) { Remove(s, s.People[Rng.Int(0, s.Pop - 1)]); Note("Someone froze in the night in " + s.Name + ".", LogKind.Info); }
                }
            }
            int pop = s.Pop, spare = Housing(s) - pop;
            if (pop >= 2 && pop < K.POPCAP0 && spare > 0 && s.Food >= pop * 1.5 && se != 3)
            {
                double moodK = 62.0 / 60;   // mood is not ported yet: the HTML starts every village at 62
                int n = Math.Min(spare, (int)Math.Floor(pop * 0.012 * moodK + Rng.Next()));
                for (int i = 0; i < n; i++) Spawn(s);
            }
        }

        void Remove(Settlement s, Colonist c)
        {
            s.People.Remove(c);
            ColonistDied?.Invoke(s, c);
        }

        void Spawn(Settlement s)
        {
            // a newborn joins whichever trade the village is shortest of hands in; the steward moves people around later
            var hall = Drop(s, s.CentreX, s.CentreY);
            var c = new Colonist { Id = nextId++, Settlement = s.Id, Job = "woodcutter", X = hall != null ? hall.X : s.CentreX, Y = hall != null ? hall.Y : s.CentreY, Wait = 0.5 };
            s.People.Add(c);
            ColonistBorn?.Invoke(s, c);
        }
    }
}
