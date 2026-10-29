using System.IO;
using EmberVale.View;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EmberVale.EditorTools
{
    /// <summary>
    /// One-click project setup. Menu: Ember Vale > Build Demo Scene.
    /// Also callable headless: Unity -batchmode -quit -projectPath unity -executeMethod EmberVale.EditorTools.ProjectSetup.BuildBatch
    /// </summary>
    public static class ProjectSetup
    {
        const string ModelDir = "Assets/Art/Models/";
        const string SceneDir = "Assets/Scenes";
        const string ScenePath = SceneDir + "/Demo.unity";
        const string RenderDir = "Assets/Settings";

        [MenuItem("Ember Vale/Build Demo Scene")]
        public static void Build()
        {
            ConfigurePlayer();
            ConfigureUrp();
            BuildScene();
            Debug.Log("Ember Vale: demo scene ready at " + ScenePath + ". Press Play.");
        }

        public static void BuildBatch() { Build(); }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Jordan Lee";
            PlayerSettings.productName = "Ember Vale";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.jordanlee.embervale");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.iOS.hideHomeButton = true;
        }

        static void ConfigureUrp()
        {
            Directory.CreateDirectory(RenderDir);
            string path = RenderDir + "/EmberVale_URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, RenderDir + "/EmberVale_Renderer.asset");
                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, path);
            }
            if (asset == null)
            {
                // Not verified against a real editor: if this API differs in your Unity version, make the asset by hand
                // (Assets > Create > Rendering > URP Asset (with Universal Renderer)) and assign it in Project Settings > Graphics.
                Debug.LogError("Ember Vale: could not create a URP asset; create one by hand and assign it in Project Settings > Graphics.");
                return;
            }
            // phone-friendly: no HDR, no post, cheap shadows, 1 additional light is enough for a top-down sim
            asset.supportsHDR = false;
            asset.msaaSampleCount = 2;
            asset.shadowDistance = 60f;
            EditorUtility.SetDirty(asset);
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            QualitySettings.vSyncCount = 0;
            AssetDatabase.SaveAssets();
        }

        static GameObject Model(string name)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + name + ".fbx");
            if (g == null) Debug.LogError("Missing model " + ModelDir + name + ".fbx (run unity/blender/generate_assets.py)");
            return g;
        }

        static void BuildScene()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.16f, 0.12f);
            cam.fieldOfView = 35;
            cam.nearClipPlane = 0.5f; cam.farClipPlane = 400;
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);

            var boot = new GameObject("Game");
            var models = boot.AddComponent<ModelSet>();
            models.hall = Model("hall"); models.hut = Model("hut"); models.house = Model("house");
            models.barracks = Model("barracks"); models.tree = Model("tree"); models.oak = Model("oak");
            models.rock = Model("rock"); models.soldier = Model("soldier"); models.settler = Model("settler");
            models.banner = Model("banner"); models.tent = Model("tent");
            var gb = boot.AddComponent<GameBootstrap>();
            gb.cam = cam;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
