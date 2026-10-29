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

        Console.WriteLine("Determinism");
        Check(Fingerprint(555, 3) == Fingerprint(555, 3), "same seed gives the same history");
        Check(Fingerprint(555, 3) != Fingerprint(556, 3), "different seed gives a different history");

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
            good &= t.T == Terrain.Meadow && s.Buildings.Exists(b => b.Type == "tc") && s.People.Count == 5;
        }
        Check(good, "each village has a hall on meadow and five settlers");
        double minD = 1e9;
        for (int i = 0; i < g.Settlements.Count; i++) for (int j = i + 1; j < g.Settlements.Count; j++)
            minD = Math.Min(minD, Math.Sqrt(Math.Pow(g.Settlements[i].CentreX - g.Settlements[j].CentreX, 2) + Math.Pow(g.Settlements[i].CentreY - g.Settlements[j].CentreY, 2)));
        if (g.Settlements.Count > 1) Check(minD >= 6, "villages are apart (closest " + minD.ToString("0.0") + " tiles)");
    }

    static string Fingerprint(int seed, int bands)
    {
        var g = new Game(seed, bands);
        for (int i = 0; i < 20000 && g.Bands.Count > 0; i++) g.Step(0.5);
        var parts = new List<string>();
        foreach (var s in g.Settlements) parts.Add(s.Name + "@" + s.CentreX + "," + s.CentreY);
        return string.Join(";", parts) + "|" + g.Day;
    }

}
