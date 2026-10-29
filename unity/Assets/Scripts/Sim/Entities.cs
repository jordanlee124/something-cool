using System.Collections.Generic;

namespace EmberVale.Sim
{
    public sealed class Building
    {
        public int Id, Settlement, X, Y;
        public string Type;      // "tc", "farm", ... same keys as BUILD in the JS
        public bool Built;
        public double Prog;      // seconds of building work put in so far
    }

    public enum Act { Idle, GoWork, Work, GoStore, Wander }

    public sealed class Colonist
    {
        public int Id, Settlement;
        public string Job;       // "farmer", "woodcutter", "builder", ...
        public double X, Y;
        public Act State;
        public List<Pt> Path = new List<Pt>();
        public double Wait, Work;   // Wait: seconds idle before planning again; Work: seconds spent on the current task
        public int TargetBuilding, TX, TY;   // what the colonist is working at: a building, or a tree tile
        public string Carry; public int CarryN;   // a load on the way to the store
        public int Hunger;       // days without a meal
    }

    public sealed class Settlement
    {
        public int Id;
        public string Name, Color;
        public int CentreX, CentreY;
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<Colonist> People = new List<Colonist>();
        public double Food = 40, Wood = 30, Stone = 10;   // the starting stores of Settlement.found
        public double AiTimer;   // the steward thinks every 1.5 s
        public int Age;          // 0 Settlement, 1 Village, 2 Town (the later ages come with their own slices)
        public int LowDays;      // days spent below the age's size or without its landmark
        public int Pop => People.Count;
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
