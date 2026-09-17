using UnityEditor;
using UnityEngine;

namespace InclusiveEHMI.EditorTools
{
    // Step 4 setup tool: attaches VehicleController to the AutomatedVehicle
    // prefab created in Step 2 (idempotent — safe to re-run).
    public static class VehicleControllerSetup
    {
        private const string PrefabPath = "Assets/Prefabs/AutomatedVehicle.prefab";

        [MenuItem("Tools/Inclusive eHMI/Step 4 - Attach VehicleController")]
        public static void AttachVehicleController()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Prefab Not Found",
                    $"Could not find {PrefabPath}. Run Step 2 (Tools > Inclusive eHMI > Place Vehicle Body) first.",
                    "OK");
                return;
            }

            using (var editScope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
            {
                GameObject root = editScope.prefabContentsRoot;
                if (root.GetComponent<VehicleController>() == null)
                {
                    root.AddComponent<VehicleController>();
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[VehicleControllerSetup] VehicleController attached to AutomatedVehicle prefab.");
        }
    }
}
