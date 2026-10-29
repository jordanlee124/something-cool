using EmberVale.Sim;
using UnityEngine;

namespace EmberVale.View
{
    /// <summary>Starts a watch-only game: runs the sim and keeps the camera framed on the action.</summary>
    [RequireComponent(typeof(ModelSet))]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("0 picks a random seed")] public int seed = 0;
        [Range(1, 4)] public int startBands = 3;
        [Tooltip("Game seconds per real second (the HTML game runs 1x at about 2.1)")] public float gameSpeed = 6f;
        public Camera cam;

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

        /// <summary>Portrait phone: look down at an angle and fit the whole map width on screen.</summary>
        void Frame()
        {
            if (cam == null) return;
            var centre = view.WorldCentre();
            float h = game.World.H, w = game.World.W;
            float fov = cam.fieldOfView * Mathf.Deg2Rad;
            float needH = h * 0.62f / Mathf.Tan(fov / 2);
            float needW = w * 0.62f / (Mathf.Tan(fov / 2) * cam.aspect);
            float dist = Mathf.Max(needH, needW);
            var rot = Quaternion.Euler(62, 0, 0);
            cam.transform.rotation = rot;
            cam.transform.position = centre - rot * Vector3.forward * dist;
        }
    }
}
