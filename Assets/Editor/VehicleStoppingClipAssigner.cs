using UnityEditor;
using UnityEngine;

namespace InclusiveEHMI.EditorTools
{
    // Step 7 (Multimodal audio) setup tool: imports the placeholder
    // VehicleStopping.wav clip (Assets/Audio/VehicleStopping.wav) and
    // assigns it to eHMIController's "Vehicle Stopping Clip" field on the
    // AutomatedVehicle prefab. Idempotent -- safe to re-run. Touches only
    // that one serialized field; does not change any other eHMI, vehicle,
    // camera, physics, or Step 7 logic.
    public static class VehicleStoppingClipAssigner
    {
        private const string PrefabPath = "Assets/Prefabs/AutomatedVehicle.prefab";
        private const string ClipPath = "Assets/Audio/VehicleStopping.wav";

        [MenuItem("Tools/Inclusive eHMI/Step 7 - Assign Vehicle Stopping Clip")]
        public static void AssignClip()
        {
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceUpdate);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null)
            {
                EditorUtility.DisplayDialog(
                    "Clip Not Found",
                    $"Could not find/import {ClipPath}. Make sure the WAV file exists at that path, then try again.",
                    "OK");
                return;
            }

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
                eHMIController controller = root.GetComponent<eHMIController>();
                if (controller == null)
                {
                    Debug.LogError("[VehicleStoppingClipAssigner] eHMIController not found on prefab root. " +
                                   "Run Step 6 - Build eHMI (Visual + Audio) first.");
                    return;
                }

                var serialized = new SerializedObject(controller);
                SerializedProperty clipProperty = serialized.FindProperty("vehicleStoppingClip");
                clipProperty.objectReferenceValue = clip;
                serialized.ApplyModifiedProperties();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[VehicleStoppingClipAssigner] Assigned '{ClipPath}' to eHMIController's " +
                      $"Vehicle Stopping Clip field on {PrefabPath}.");
        }
    }
}
