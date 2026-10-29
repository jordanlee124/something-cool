using System.Collections.Generic;
using EmberVale.Sim;
using UnityEngine;

namespace EmberVale.View
{
    /// <summary>
    /// Draws the sim: one tile = one Unity unit on the XZ plane (sim x -> Unity x, sim y -> Unity z).
    /// The ground is one quad textured with a pixel per tile; trees, rocks, tents, buildings and settlers are
    /// the Blender prefabs. The view only reads the sim, it never changes it.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        ModelSet models;
        Game game;
        Texture2D groundTex;
        GameObject groundQuad;
        readonly Dictionary<Band, BandView> bandViews = new Dictionary<Band, BandView>();
        readonly List<GameObject> settlementObjects = new List<GameObject>();
        readonly Dictionary<long, GameObject> trees = new Dictionary<long, GameObject>();
        readonly Dictionary<int, BuildingView> buildingViews = new Dictionary<int, BuildingView>();
        readonly Dictionary<Colonist, ColonistView> colonistViews = new Dictionary<Colonist, ColonistView>();
        readonly Dictionary<int, Transform> villageRoots = new Dictionary<int, Transform>();
        Material fieldMat, siteMat;
        bool groundDirty;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        sealed class BuildingView { public Building b; public GameObject go; public bool done; }
        sealed class ColonistView { public Transform t; public float phase; public Vector3 last; }
        sealed class BandView { public GameObject root, tent, banner; public List<Transform> walkers = new List<Transform>(); }

        public void Init(Game g, ModelSet m)
        {
            game = g; models = m;
            BuildGround();
            ScatterNature();
            foreach (var b in game.Bands) AddBand(b);
            fieldMat = FlatMaterial(new Color(0.55f, 0.42f, 0.22f));
            siteMat = FlatMaterial(new Color(0.40f, 0.33f, 0.22f));
            game.SettlementFounded += OnFounded;
            game.BuildingPlaced += OnPlaced;
            game.BuildingFinished += OnFinished;
            game.TileChanged += OnTileChanged;
            game.ColonistBorn += OnBorn;
            game.ColonistDied += OnDied;
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.SettlementFounded -= OnFounded;
            game.BuildingPlaced -= OnPlaced;
            game.BuildingFinished -= OnFinished;
            game.TileChanged -= OnTileChanged;
            game.ColonistBorn -= OnBorn;
            game.ColonistDied -= OnDied;
        }

        static Material FlatMaterial(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor(BaseColor, c);
            return m;
        }

        public Vector3 WorldCentre()
        {
            var w = game.World;
            return new Vector3(w.X0 + w.W * 0.5f, 0, w.Y0 + w.H * 0.5f);
        }

        // ---------- ground and nature ----------

        static Color32 GroundColour(Tile t)
        {
            float v = (float)t.H * 0.12f - 0.06f;
            switch (t.T)
            {
                case Ground.Water: return new Color(0.24f + v, 0.45f + v, 0.70f + v);
                case Ground.Forest: return new Color(0.28f + v, 0.52f + v, 0.27f + v);
                case Ground.Rock: return new Color(0.50f + v, 0.50f + v, 0.48f + v);
                default: return new Color(0.50f + v, 0.69f + v, 0.38f + v);
            }
        }

        void BuildGround()
        {
            var w = game.World;
            groundTex = new Texture2D(w.W, w.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < w.H; y++)
                for (int x = 0; x < w.W; x++)
                    groundTex.SetPixel(x, y, GroundColour(w.At(w.X0 + x, w.Y0 + y)));
            groundTex.Apply();

            groundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            groundQuad.name = "Ground";
            Destroy(groundQuad.GetComponent<Collider>());
            groundQuad.transform.SetParent(transform, false);
            groundQuad.transform.rotation = Quaternion.Euler(90, 0, 0);
            groundQuad.transform.localScale = new Vector3(w.W, w.H, 1);
            groundQuad.transform.position = new Vector3(w.X0 + w.W * 0.5f, 0, w.Y0 + w.H * 0.5f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.mainTexture = groundTex;
            mat.SetTexture("_BaseMap", groundTex);
            groundQuad.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void ScatterNature()
        {
            var parent = new GameObject("Nature").transform; parent.SetParent(transform, false);
            var w = game.World;
            for (int y = w.Y0; y < w.Y1; y++)
                for (int x = w.X0; x < w.X1; x++)
                {
                    var t = w.At(x, y);
                    GameObject prefab = null;
                    if (t.T == Ground.Forest) prefab = t.H > 0.55 ? models.oak : models.tree;
                    else if (t.T == Ground.Rock && (t.H * 13) % 1 < 0.4) prefab = models.rock;   // rock patches are thinned: a model on every rock tile buries the map
                    if (prefab == null) continue;
                    var o = Instantiate(prefab, new Vector3(x + 0.5f, 0, y + 0.5f), Quaternion.Euler(0, (float)t.H * 360f, 0), parent);
                    o.transform.localScale *= 0.85f + (float)((t.H * 7) % 1) * 0.4f;
                    if (t.T == Ground.Forest) trees[Key(x, y)] = o;
                }
        }

        static long Key(int x, int y) { return ((long)x << 32) ^ (uint)y; }

        /// <summary>A tree was felled or a site cleared: the tile turns to meadow and its model goes.</summary>
        void OnTileChanged(int x, int y)
        {
            var w = game.World;
            groundTex.SetPixel(x - w.X0, y - w.Y0, GroundColour(w.At(x, y)));
            groundDirty = true;
            if (trees.TryGetValue(Key(x, y), out var tree)) { Destroy(tree); trees.Remove(Key(x, y)); }
        }

        // ---------- settler bands ----------

        void AddBand(Band b)
        {
            var v = new BandView { root = new GameObject("Band " + b.Name) };
            v.root.transform.SetParent(transform, false);
            for (int i = 0; i < b.N; i++)
            {
                var s = Instantiate(models.settler, v.root.transform);
                v.walkers.Add(s.transform);
            }
            v.banner = Instantiate(models.banner, v.root.transform);
            Tint(v.banner, b.Color);
            v.tent = Instantiate(models.tent, v.root.transform);
            bandViews[b] = v;
        }

        void UpdateBands()
        {
            foreach (var kv in bandViews)
            {
                var b = kv.Key; var v = kv.Value;
                v.root.transform.position = new Vector3((float)b.X + 0.5f, 0, (float)b.Y + 0.5f);
                v.tent.SetActive(b.Camping);
                // a loose huddle that sways as it walks
                for (int i = 0; i < v.walkers.Count; i++)
                {
                    float a = i * 2.4f, r = 0.35f + 0.08f * (i % 3);
                    float bob = b.Camping ? 0 : Mathf.Abs(Mathf.Sin(Time.time * 6 + i)) * 0.04f;
                    v.walkers[i].localPosition = new Vector3(Mathf.Cos(a) * r, bob, Mathf.Sin(a) * r);
                }
                v.banner.transform.localPosition = new Vector3(0.0f, 0, 0.0f);
            }
        }

        // ---------- villages ----------

        void OnFounded(Settlement s)
        {
            Band gone = null;
            foreach (var kv in bandViews) if (kv.Key.Name == s.Name) { gone = kv.Key; break; }
            if (gone != null) { Destroy(bandViews[gone].root); bandViews.Remove(gone); }

            var root = new GameObject("Village " + s.Name);
            root.transform.SetParent(transform, false);
            villageRoots[s.Id] = root.transform;
            settlementObjects.Add(root);
            foreach (var b in s.Buildings) AddBuildingView(s, b);
            foreach (var p in s.People) AddColonist(s, p);
        }

        void OnPlaced(Settlement s, Building b) { AddBuildingView(s, b); }

        void OnFinished(Settlement s, Building b)
        {
            if (buildingViews.TryGetValue(b.Id, out var v)) { Destroy(v.go); buildingViews.Remove(b.Id); }
            AddBuildingView(s, b);
        }

        void AddBuildingView(Settlement s, Building b)
        {
            if (!villageRoots.TryGetValue(s.Id, out var root)) return;
            var pos = new Vector3(b.X + 0.5f, 0, b.Y + 0.5f);
            GameObject o;
            if (b.Type == "farm")
            {
                o = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(o.GetComponent<Collider>());
                o.transform.SetParent(root, false);
                o.transform.position = pos + new Vector3(0, 0.03f, 0);
                o.transform.localScale = new Vector3(0.92f, 0.06f, 0.92f);
                o.GetComponent<Renderer>().sharedMaterial = b.Built ? fieldMat : siteMat;
            }
            else
            {
                o = Instantiate(b.Type == "tc" ? models.hall : models.hut, pos, Quaternion.identity, root);   // house, hut: same model for now
                if (b.Type == "tc") PlantFlag(o, s.Color, root);
                if (!b.Built) o.transform.localScale = Vector3.one * 0.3f;
            }
            buildingViews[b.Id] = new BuildingView { b = b, go = o, done = b.Built };
        }

        void AddColonist(Settlement s, Colonist c)
        {
            if (!villageRoots.TryGetValue(s.Id, out var root)) return;
            var o = Instantiate(models.settler, new Vector3((float)c.X + 0.5f, 0, (float)c.Y + 0.5f), Quaternion.identity, root);
            colonistViews[c] = new ColonistView { t = o.transform, phase = c.Id * 1.7f, last = o.transform.position };
        }

        void OnBorn(Settlement s, Colonist c) { AddColonist(s, c); }

        void OnDied(Settlement s, Colonist c)
        {
            if (colonistViews.TryGetValue(c, out var v)) { Destroy(v.t.gameObject); colonistViews.Remove(c); }
        }

        /// <summary>Villagers glide to where the sim has them and face the way they walk; sites grow as builders work.</summary>
        void UpdateVillages()
        {
            foreach (var kv in colonistViews)
            {
                var c = kv.Key; var v = kv.Value;
                bool walking = c.Path.Count > 0;
                float bob = walking ? Mathf.Abs(Mathf.Sin(Time.time * 9 + v.phase)) * 0.05f : (c.State == Act.Work ? Mathf.Abs(Mathf.Sin(Time.time * 5 + v.phase)) * 0.04f : 0f);
                var target = new Vector3((float)c.X + 0.5f, bob, (float)c.Y + 0.5f);
                var flat = new Vector3(target.x - v.last.x, 0, target.z - v.last.z);
                if (flat.sqrMagnitude > 1e-6f) v.t.rotation = Quaternion.Slerp(v.t.rotation, Quaternion.LookRotation(flat), 0.25f);
                v.last = new Vector3(target.x, 0, target.z);
                v.t.position = target;
            }
            foreach (var kv in buildingViews)
            {
                var v = kv.Value;
                if (v.b.Built || v.b.Type == "farm") continue;
                v.go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, (float)(v.b.Prog / Game.BuildTime(v.b.Type)));
            }
        }

        /// <summary>
        /// Flies the faction banner from the top of the hall's turret. The hall model carries an empty child called flag_mount at the roof
        /// ridge (made in blender/generate_assets.py), so the spot follows the model whatever the import axes are.
        /// </summary>
        void PlantFlag(GameObject hall, string colour, Transform parent)
        {
            var mount = FindDeep(hall.transform, "flag_mount");
            var pos = mount != null ? mount.position : hall.transform.position + new Vector3(0.9f, 0, -0.5f);   // old model without a mount: beside the hall
            var flag = Instantiate(models.banner, pos, Quaternion.identity, parent);
            if (mount != null) flag.transform.localScale *= 0.6f;   // the pole is 1 tile tall: on a roof it should be a pennant
            Tint(flag, colour);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        static void Tint(GameObject o, string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, c);
            foreach (var r in o.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
        }

        void LateUpdate()
        {
            UpdateBands();
            UpdateVillages();
            if (groundDirty) { groundTex.Apply(); groundDirty = false; }
        }
    }
}
