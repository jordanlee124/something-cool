using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EmberVale.Sim;

static class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) failures++;
    }

    static int Main(string[] args)
    {
        Console.WriteLine("Terrain parity with ember-vale.html");
        foreach (int seed in new[] { 12345, 987654321, 42, 777000111 }) TerrainParity(seed);

        Console.WriteLine("Settlers found villages");
        foreach (int bands in new[] { 1, 3 }) Founding(2026, bands);

        Console.WriteLine("Village life");
        VillageLife(2026, 1);
        VillageLife(31, 3);

        Console.WriteLine("Determinism");
        Check(Fingerprint(555, 3) == Fingerprint(555, 3), "same seed gives the same history");
        Check(Fingerprint(555, 3) != Fingerprint(556, 3), "different seed gives a different history");
        Check(Fingerprint(555, 3, 40) == Fingerprint(555, 3, 40), "villages repeat the same years");

        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    static void TerrainParity(int seed)
    {
        string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../parity"));
        string js = Run("node", Path.Combine(dir, "terrain.js") + " " + seed);
        if (js == null) { Console.WriteLine("  skip (node not available) seed " + seed); return; }
        var lines = js.Split('\n');
        var world = new World(seed);
        world.GrowTo(0, 0, 2, 3);
        string head = world.W + " " + world.H + " " + world.X0 + " " + world.Y0;
        Check(lines[0].Trim() == head, "seed " + seed + ": map size " + head);
        string a = lines[1].Trim(), b = world.TerrainString();
        int diff = 0;
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++) if (a[i] != b[i]) diff++;
        Check(a.Length == b.Length && diff == 0, "seed " + seed + ": " + b.Length + " tiles identical (" + diff + " differ)");
    }

    static string Run(string exe, string args)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo(exe, args) { RedirectStandardOutput = true, UseShellExecute = false });
            string o = p.StandardOutput.ReadToEnd(); p.WaitForExit();
            return p.ExitCode == 0 ? o : null;
        }
        catch (Exception) { return null; }
    }

    static void Founding(int seed, int bands)
    {
        var g = new Game(seed, bands);
        int founded = 0; g.SettlementFounded += s => founded++;
        Check(g.Bands.Count == bands, bands + " band(s) land");
        for (int i = 0; i < 20000 && g.Bands.Count > 0; i++) g.Step(0.5);
        Check(g.Bands.Count == 0 && founded == bands, "every band founded a village by day " + g.Day + " (" + founded + "/" + bands + ")");
        bool good = true;
        foreach (var s in g.Settlements)
        {
            var t = g.World.At(s.CentreX, s.CentreY);
            good &= t.T == Ground.Meadow && s.Buildings.Exists(b => b.Type == "tc") && s.People.Count == 5;
        }
        Check(good, "each village has a hall on meadow and five settlers");
        double minD = 1e9;
        for (int i = 0; i < g.Settlements.Count; i++) for (int j = i + 1; j < g.Settlements.Count; j++)
            minD = Math.Min(minD, Math.Sqrt(Math.Pow(g.Settlements[i].CentreX - g.Settlements[j].CentreX, 2) + Math.Pow(g.Settlements[i].CentreY - g.Settlements[j].CentreY, 2)));
        if (g.Settlements.Count > 1) Check(minD >= 6, "villages are apart (closest " + minD.ToString("0.0") + " tiles)");
    }

    // run the village for some years and check it does what the HTML village does: grow, fell trees, build, eat
    static void VillageLife(int seed, int bands)
    {
        var g = new Game(seed, bands);
        int placed = 0, finished = 0, felled = 0, born = 0, died = 0;
        g.BuildingPlaced += (s, b) => placed++;
        g.BuildingFinished += (s, b) => finished++;
        g.TileChanged += (x, y) => felled++;
        g.ColonistBorn += (s, c) => born++;
        g.ColonistDied += (s, c) => died++;
        double maxFood = 0; bool badStock = false, offMap = false;
        int steps = 0;
        while (g.Day <= K.YEAR * 4 + 1)
        {
            g.Step(0.5); steps++;
            if (steps % 20 != 0) continue;
            foreach (var s in g.Settlements)
            {
                maxFood = Math.Max(maxFood, s.Food);
                badStock |= s.Food < 0 || s.Wood < 0 || s.Food > K.CAP || s.Wood > K.CAP;
                foreach (var c in s.People) offMap |= !g.World.Inside((int)Math.Round(c.X), (int)Math.Round(c.Y));
            }
        }
        string tag = "seed " + seed + " (" + bands + " band(s)), 4 years: ";
        Check(g.Settlements.Count == bands, tag + "villages founded");
        Check(placed > 0 && finished > 0, tag + "huts and fields were placed (" + placed + ") and finished (" + finished + ")");
        Check(felled > 0, tag + "trees were felled (" + felled + " tiles cleared)");
        bool built = true, fed = true; int total = 0;
        foreach (var s in g.Settlements)
        {
            int huts = s.Buildings.FindAll(b => b.Type == "hut" && b.Built).Count;
            built &= huts >= 1;
            total += s.Pop;
            fed &= s.Pop >= 1;
            Console.WriteLine("       " + s.Name + ": pop " + s.Pop + ", huts " + huts + ", farms " + s.Buildings.FindAll(b => b.Type == "farm" && b.Built).Count + ", food " + (int)s.Food + ", wood " + (int)s.Wood + ", born " + born + ", died " + died);
        }
        Check(built, tag + "every village raised a hut");
        Check(fed, tag + "no village died out");
        Check(born > 0, tag + "children were born (" + born + ")");
        Check(!badStock, tag + "stores stay between 0 and the cap");
        Check(!offMap, tag + "nobody walked off the map");
    }

    static string Fingerprint(int seed, int bands, int days = 0)
    {
        var g = new Game(seed, bands);
        for (int i = 0; i < 20000 && g.Bands.Count > 0; i++) g.Step(0.5);
        if (days > 0) while (g.Day <= days) g.Step(0.5);
        if (days > 0) { var q = new List<string>(); foreach (var s in g.Settlements) q.Add(s.Pop + "/" + (int)s.Food + "/" + (int)s.Wood + "/" + s.Buildings.Count); return string.Join(";", q); }
        var parts = new List<string>();
        foreach (var s in g.Settlements) parts.Add(s.Name + "@" + s.CentreX + "," + s.CentreY);
        return string.Join(";", parts) + "|" + g.Day;
    }

}
