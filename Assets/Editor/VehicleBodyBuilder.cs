using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InclusiveEHMI.EditorTools
{
    // Step 2 build tool: static vehicle body placement only. No movement, no
    // eHMI display, no audio — those are added in later steps by scripts
    // (VehicleController, eHMIController, AudioSource) that will attach to
    // the "AutomatedVehicle" root this creates.
    //
    // Root/child split: the root's transform is the vehicle's logical
    // position (ground level, i.e. Y=0 sits on the road surface) — this is
    // what later movement/distance code will read and move. "Body" is a
    // purely visual child mesh offset upward to sit on the ground.
    public static class VehicleBodyBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/AutomatedVehicle.prefab";
        private const string MaterialsFolder = "Assets/Materials";

        private const string RootName = "AutomatedVehicle";

        private const float BodyWidth = 1.9f;   // X, meters
        private const float BodyLength = 4.5f;  // Z, meters
        private const float BodyHeight = 1.5f;  // Y, meters

        // Static placement for proportion-checking against the Step 1 road
        // and crossing line only. Not the per-trial start distance — that
        // gets computed from v0/braking physics in Step 4/5.
        private const float PlacementX = 0f;
        private const float PlacementZ = 30f;

        [MenuItem("Tools/Inclusive eHMI/Place Vehicle Body (Step 2)")]
        public static void PlaceVehicleBody()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "ExperimentScene")
            {
                EditorUtility.DisplayDialog(
                    "Wrong Scene",
                    "Open Assets/Scenes/ExperimentScene.unity first (Tools > Inclusive eHMI > Build Experiment Scene, or double-click the scene asset), then run this again.",
                    "OK");
                return;
            }

            // Remove any previous instance so re-running this tool is idempotent.
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject(RootName);
            root.transform.position = new Vector3(PlacementX, 0f, PlacementZ);
            // Yaw 180° so transform.forward points toward world -Z, matching
            // the vehicle's approach direction (+Z -> 0). Later VehicleController
            // movement and front-mounted eHMI placement can then rely on
            // transform.forward directly instead of treating -Z as a special case.
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, BodyHeight * 0.5f, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(BodyWidth, BodyHeight, BodyLength);
            ApplyMaterial(body, "M_VehicleBody", new Color(0.2f, 0.35f, 0.65f));

            // Reference point at the leading edge of the body, along the
            // root's local forward axis (+Z locally == world -Z, the
            // direction of travel, per the Step 2 yaw-180 fix). Step 5's
            // braking/stop-offset math measures against this, not the root.
            GameObject frontBumper = new GameObject("FrontBumper");
            frontBumper.transform.SetParent(root.transform);
            frontBumper.transform.localPosition = new Vector3(0f, 0f, BodyLength * 0.5f);
            frontBumper.transform.localRotation = Quaternion.identity;

            Directory.CreateDirectory("Assets/Prefabs");
            AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);

            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.Refresh();

            Debug.Log($"[VehicleBodyBuilder] Placed '{RootName}' at {root.transform.position}, " +
                      $"body {BodyWidth}m (W) x {BodyHeight}m (H) x {BodyLength}m (L). " +
                      $"Saved prefab to {PrefabPath}.");

            Selection.activeGameObject = root;
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
                Directory.CreateDirectory(MaterialsFolder);
                AssetDatabase.CreateAsset(mat, path);
            }

            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
