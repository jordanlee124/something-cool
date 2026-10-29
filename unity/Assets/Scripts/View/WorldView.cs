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
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        sealed class BandView { public GameObject root, tent, banner; public List<Transform> walkers = new List<Transform>(); }

        public void Init(Game g, ModelSet m)
        {
            game = g; models = m;
            BuildGround();
            ScatterNature();
            foreach (var b in game.Bands) AddBand(b);
            game.SettlementFounded += OnFounded;
        }

        void OnDestroy() { if (game != null) game.SettlementFounded -= OnFounded; }

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
                    else if (t.T == Ground.Rock) prefab = models.rock;
                    if (prefab == null) continue;
                    var o = Instantiate(prefab, new Vector3(x + 0.5f, 0, y + 0.5f), Quaternion.Euler(0, (float)t.H * 360f, 0), parent);
                    o.transform.localScale *= 0.85f + (float)((t.H * 7) % 1) * 0.4f;
                }
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
            foreach (var b in s.Buildings)
            {
                var prefab = b.Type == "tc" ? models.hall : models.hut;   // farm uses the hut until it has its own model
                var o = Instantiate(prefab, new Vector3(b.X + 0.5f, 0, b.Y + 0.5f), Quaternion.identity, root.transform);
                if (b.Type == "tc") Tint(Instantiate(models.banner, o.transform.position + new Vector3(0.9f, 0, -0.5f), Quaternion.identity, root.transform), s.Color);
            }
            for (int i = 0; i < s.People.Count; i++)
            {
                var p = s.People[i];
                Instantiate(models.settler, new Vector3((float)p.X + 0.5f + (i - 2) * 0.25f, 0, (float)p.Y - 0.7f), Quaternion.identity, root.transform);
            }
            settlementObjects.Add(root);
        }

        static void Tint(GameObject o, string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, c);
            foreach (var r in o.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
        }

        void LateUpdate() { UpdateBands(); }
    }
}
