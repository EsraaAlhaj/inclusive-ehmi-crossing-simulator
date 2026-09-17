using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InclusiveEHMI.EditorTools
{
    // Step 1 build tool. Constructs the static scene only (road, sidewalks,
    // zebra crossing, lighting, fixed participant camera) — no gameplay
    // scripts are attached to anything it creates.
    //
    // Re-runnable: each run rebuilds the layout from these constants and
    // overwrites Assets/Scenes/ExperimentScene.unity, so the scene can be
    // regenerated exactly if it's ever changed by hand and needs resetting.
    //
    // World scale: 1 Unity unit = 1 meter.
    // Layout convention: the road runs along world +Z. The vehicle (added in
    // a later step) will approach along -Z toward the crossing line at Z = 0.
    // X is the road's width axis, Y is up.
    public static class ExperimentSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ExperimentScene.unity";
        private const string MaterialsFolder = "Assets/Materials";

        private const float RoadWidth = 6f;
        private const float RoadNearZ = -5f;
        private const float RoadFarZ = 120f;

        private const float SidewalkWidth = 2f;
        private const float SidewalkHeight = 0.2f;

        private const int StripeCount = 5;
        private const float StripeWidth = 0.3f;
        private const float StripeSpacing = 0.6f;

        private const float CameraEyeHeight = 1.6f;
        private const float CameraZ = -2f;
        private const float CameraTiltDegrees = 5f;

        [MenuItem("Tools/Inclusive eHMI/Build Experiment Scene")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "Build Experiment Scene",
                    "This creates/overwrites Assets/Scenes/ExperimentScene.unity with the static road, sidewalks, crossing, lighting and camera. Continue?",
                    "Build", "Cancel"))
            {
                return;
            }

            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            float roadLength = RoadFarZ - RoadNearZ;
            float roadCenterZ = (RoadFarZ + RoadNearZ) * 0.5f;

            GameObject environment = new GameObject("Environment");

            BuildRoad(environment.transform, roadCenterZ, roadLength);
            BuildSidewalk(environment.transform, "Sidewalk_Left", -(RoadWidth * 0.5f + SidewalkWidth * 0.5f), roadCenterZ, roadLength);
            BuildSidewalk(environment.transform, "Sidewalk_Right", RoadWidth * 0.5f + SidewalkWidth * 0.5f, roadCenterZ, roadLength);
            BuildCrosswalk(environment.transform);
            BuildCrossingLineMarker(environment.transform);

            BuildLight();
            BuildCamera();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[ExperimentSceneBuilder] Scene built and saved to {ScenePath}. " +
                      $"Road spans Z = {RoadNearZ}..{RoadFarZ}, crossing line at Z = 0, camera at Z = {CameraZ}.");
        }

        private static void BuildRoad(Transform parent, float centerZ, float length)
        {
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
            road.name = "Road";
            road.transform.SetParent(parent);
            road.transform.position = new Vector3(0f, 0f, centerZ);
            // Default Plane primitive is 10x10 units at scale 1.
            road.transform.localScale = new Vector3(RoadWidth / 10f, 1f, length / 10f);
            ApplyMaterial(road, "M_Road", new Color(0.15f, 0.15f, 0.15f));
        }

        private static void BuildSidewalk(Transform parent, string name, float centerX, float centerZ, float length)
        {
            GameObject sidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sidewalk.name = name;
            sidewalk.transform.SetParent(parent);
            sidewalk.transform.position = new Vector3(centerX, SidewalkHeight * 0.5f, centerZ);
            sidewalk.transform.localScale = new Vector3(SidewalkWidth, SidewalkHeight, length);
            ApplyMaterial(sidewalk, "M_Sidewalk", new Color(0.6f, 0.6f, 0.6f));
        }

        private static void BuildCrosswalk(Transform parent)
        {
            GameObject crosswalk = new GameObject("CrosswalkMarkings");
            crosswalk.transform.SetParent(parent);
            crosswalk.transform.position = Vector3.zero;

            float totalSpan = (StripeCount - 1) * StripeSpacing;
            float startZ = -totalSpan * 0.5f;

            for (int i = 0; i < StripeCount; i++)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = $"Stripe_{i}";
                stripe.transform.SetParent(crosswalk.transform);
                float z = startZ + i * StripeSpacing;
                stripe.transform.position = new Vector3(0f, 0.011f, z);
                stripe.transform.localScale = new Vector3(RoadWidth, 0.02f, StripeWidth);
                ApplyMaterial(stripe, "M_Crosswalk", Color.white);
            }
        }

        private static void BuildCrossingLineMarker(Transform parent)
        {
            // Empty reference transform at the crossing line (world Z = 0), for
            // later scripts (VehicleController, ExperimentManager) to measure
            // distance against instead of hardcoding world coordinates.
            GameObject marker = new GameObject("CrossingLine");
            marker.transform.SetParent(parent);
            marker.transform.position = Vector3.zero;
        }

        private static void BuildLight()
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void BuildCamera()
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            camObj.AddComponent<AudioListener>();
            camObj.transform.position = new Vector3(0f, CameraEyeHeight, CameraZ);
            camObj.transform.rotation = Quaternion.Euler(CameraTiltDegrees, 0f, 0f);
        }

        private static void ApplyMaterial(GameObject go, string materialName, Color color)
        {
            string path = $"{MaterialsFolder}/{materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                mat = new Material(shader) { name = materialName };
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
                else if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", color);
                }
                AssetDatabase.CreateAsset(mat, path);
            }

            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
