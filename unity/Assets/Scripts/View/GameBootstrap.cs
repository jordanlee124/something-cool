using EmberVale.Sim;
using UnityEngine;

namespace EmberVale.View
{
    /// <summary>Starts a watch-only game: runs the sim and keeps the camera framed on the action (or lets the player steer it).</summary>
    [RequireComponent(typeof(ModelSet))]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("0 picks a random seed")] public int seed = 0;
        [Range(1, 4)] public int startBands = 3;
        [Tooltip("Game seconds per real second (the HTML game runs 1x at about 2.1)")] public float gameSpeed = 6f;
        public Camera cam;
        [Tooltip("Frame the settlers and villages instead of the whole map")] public bool followAction = true;

        Game game;
        WorldView view;

        void Start()
        {
            Application.targetFrameRate = 60;
            if (seed == 0) seed = Random.Range(1, int.MaxValue);
            game = new Game(seed, startBands);
            var go = new GameObject("World");
            view = go.AddComponent<WorldView>();
            view.Init(game, GetComponent<ModelSet>());
            if (cam == null) cam = Camera.main;
            Debug.Log("Ember Vale seed " + seed);
        }

        void Update()
        {
            game.Step(Time.deltaTime * gameSpeed);
            Frame();
        }

        readonly CameraRig rig = new CameraRig();

        /// <summary>
        /// Portrait phone: look down at an angle. While settlers are walking or villages exist, the follow camera frames
        /// them (bands, then villages) with some margin; with nothing to follow it shows the whole map. The CameraRig
        /// glides there, and lets the player pan and zoom by hand (see CameraRig).
        /// </summary>
        void Frame()
        {
            if (cam == null) return;
            var w = game.World;
            float fov = cam.fieldOfView * Mathf.Deg2Rad;
            float tanHalf = Mathf.Tan(fov / 2);
            float Fit(float x0, float x1, float z0, float z1) =>
                Mathf.Max((z1 - z0) * 0.75f / tanHalf, (x1 - x0) * 0.75f / (tanHalf * cam.aspect));

            float fullDist = Fit(w.X0, w.X1, w.Y0, w.Y1);
            float minX = w.X0, maxX = w.X1, minZ = w.Y0, maxZ = w.Y1;
            if (followAction)
            {
                bool any = false;
                float x0 = 1e9f, x1 = -1e9f, z0 = 1e9f, z1 = -1e9f;
                void Add(double x, double y) { any = true; x0 = Mathf.Min(x0, (float)x); x1 = Mathf.Max(x1, (float)x); z0 = Mathf.Min(z0, (float)y); z1 = Mathf.Max(z1, (float)y); }
                foreach (var b in game.Bands) Add(b.X, b.Y);
                foreach (var s in game.Settlements) Add(s.CentreX, s.CentreY);
                if (any)
                {
                    const float margin = 9f, minSpan = 18f;
                    float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
                    float hx = Mathf.Max((x1 - x0) / 2 + margin, minSpan / 2), hz = Mathf.Max((z1 - z0) / 2 + margin, minSpan / 2);
                    minX = Mathf.Max(w.X0, cx - hx); maxX = Mathf.Min(w.X1, cx + hx);
                    minZ = Mathf.Max(w.Y0, cz - hz); maxZ = Mathf.Min(w.Y1, cz + hz);
                }
            }
            var centre = new Vector3((minX + maxX) / 2 + 0.5f, 0, (minZ + maxZ) / 2 + 0.5f);
            rig.Update(cam, w, centre, Fit(minX, maxX, minZ, maxZ), fullDist);
        }
    }
}
