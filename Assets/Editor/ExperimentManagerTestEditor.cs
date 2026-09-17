using UnityEditor;
using UnityEngine;

namespace InclusiveEHMI.EditorTools
{
    // Step 7/8 test-only controls, kept entirely separate from
    // ExperimentManager's own trial-flow logic. Only meaningful in Play Mode.
    [CustomEditor(typeof(ExperimentManager))]
    public class ExperimentManagerTestEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to run trials.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.LabelField("Step 7 Ad Hoc Single-Trial Test (temporary)", EditorStyles.boldLabel);
                if (GUILayout.Button("Start Trial (ad hoc)", GUILayout.Height(28)))
                {
                    var manager = (ExperimentManager)target;
                    manager.StartTrial();
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Step 8 Full Experiment Sequence (temporary)", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Start Practice + Main Sequence", GUILayout.Height(28)))
                {
                    var manager = (ExperimentManager)target;
                    manager.StartExperimentSequence();
                }
                if (GUILayout.Button("Next Trial", GUILayout.Height(28)))
                {
                    var manager = (ExperimentManager)target;
                    manager.AdvanceToNextTrial();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "During a trial: after the vehicle begins braking, press C to Cross or W to Wait (only the first press counts). " +
                "For the Step 8 sequence, the trial does NOT auto-advance after a decision -- click 'Next Trial' to continue " +
                "(practice trial 1 -> 2 -> 3, then main trial 1 -> ... -> 12).",
                MessageType.Info);
        }
    }
}
