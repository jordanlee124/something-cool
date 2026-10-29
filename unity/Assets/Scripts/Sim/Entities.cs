using System.Collections.Generic;

namespace EmberVale.Sim
{
    public sealed class Building
    {
        public int Id, Settlement, X, Y;
        public string Type;      // "tc", "farm", ... same keys as BUILD in the JS
        public bool Built;
    }

    public sealed class Colonist
    {
        public int Id, Settlement;
        public string Job;       // "farmer", "woodcutter", "builder", ...
        public double X, Y;
    }

    public sealed class Settlement
    {
        public int Id;
        public string Name, Color;
        public int CentreX, CentreY;
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<Colonist> People = new List<Colonist>();
    }

    /// <summary>A party of settlers walking out to find a place for a village (the game's opening).</summary>
    public sealed class Band
    {
        public int Id, N = 5;
        public string Name, Color;
        public double X, Y;
        public Pt Start;
        public Site Site;
        public bool HasSite;   // PlanBand has chosen a site (the JS tests `o.site`)
        public List<Pt> Waypoints = new List<Pt>();
        public List<Pt> Path = new List<Pt>();
        public double Wait, Age;
        public bool Camping;   // standing at a stop: the view draws tents and a fire
    }

    public struct Site { public int X, Y, Forest, Rock, Water; }

    public enum LogKind { Info, Good }

    public struct LogEntry
    {
        public int Day; public string Text; public LogKind Kind;
        public LogEntry(int day, string text, LogKind kind) { Day = day; Text = text; Kind = kind; }
    }
}
