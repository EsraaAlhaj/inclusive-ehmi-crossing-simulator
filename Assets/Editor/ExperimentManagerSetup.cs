using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InclusiveEHMI.EditorTools
{
    // Step 7 setup tool: creates a scene-level "ExperimentManager" GameObject
    // (idempotent -- reuses it if already present) in the currently open
    // ExperimentScene and attaches the ExperimentManager component.
    // ExperimentManager auto-resolves its VehicleController/eHMIController
    // references via FindAnyObjectByType in Awake, so no manual wiring is
    // required after running this.
    public static class ExperimentManagerSetup
    {
        private const string GameObjectName = "ExperimentManager";

        [MenuItem("Tools/Inclusive eHMI/Step 7 - Setup ExperimentManager")]
        public static void Setup()
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

            GameObject go = GameObject.Find(GameObjectName);
            if (go == null)
            {
                go = new GameObject(GameObjectName);
            }

            if (go.GetComponent<ExperimentManager>() == null)
            {
                go.AddComponent<ExperimentManager>();
            }

            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.Refresh();

            Debug.Log($"[ExperimentManagerSetup] '{GameObjectName}' present in the scene with ExperimentManager attached. " +
                      "vehicleController/ehmiController auto-resolve via FindAnyObjectByType in Play Mode.");

            Selection.activeGameObject = go;
        }
    }
}
