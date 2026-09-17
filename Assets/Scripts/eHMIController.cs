using UnityEngine;

namespace InclusiveEHMI
{
    // Step 6: eHMI visual + audio display only. No trial/condition logic,
    // and no reference to VehicleController or ExperimentManager -- this
    // class just exposes independent Activate/Deactivate methods. A later
    // step (ExperimentManager) will call these based on
    // TrialData.EHMICondition, roughly as follows (not wired up yet):
    //   None       -> never call anything (nothing displayed or played)
    //   Visual     -> ActivateVisual()
    //   Multimodal -> ActivateVisual() + ActivateAudio()
    public class eHMIController : MonoBehaviour
    {
        [Tooltip("Root GameObject for the complete eHMI visual (background panel + \"STOPPING\" text), toggled as one unit. Auto-resolved from child path \"EHMI/VisualRoot\" if left empty.")]
        [SerializeField] private GameObject visualRoot;

        [Tooltip("AudioSource that plays the spoken \"Vehicle stopping\" clip. Auto-resolved from child \"EHMI\" if left empty.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Spoken \"Vehicle stopping\" clip played by ActivateAudio(). Must be assigned manually in the Inspector -- no audio asset is created by tooling.")]
        [SerializeField] private AudioClip vehicleStoppingClip;

        private void Awake()
        {
            if (visualRoot == null)
            {
                Transform found = transform.Find("EHMI/VisualRoot");
                if (found != null)
                {
                    visualRoot = found.gameObject;
                }
            }

            if (audioSource == null)
            {
                Transform ehmiRoot = transform.Find("EHMI");
                if (ehmiRoot != null)
                {
                    audioSource = ehmiRoot.GetComponent<AudioSource>();
                }
            }

            // Defensive default: eHMI starts fully off regardless of whatever
            // state the prefab/scene happened to save it in.
            Deactivate();
        }

        // Visual condition: shows the complete visual (background panel +
        // "STOPPING" text) as one unit. Does not touch audio.
        public void ActivateVisual()
        {
            if (visualRoot == null)
            {
                Debug.LogWarning("[eHMIController] ActivateVisual: visualRoot not assigned/found.");
                return;
            }

            visualRoot.SetActive(true);
            Debug.Log("[eHMIController] Visual activated (panel + \"STOPPING\" text shown).");
        }

        // Multimodal condition (called alongside ActivateVisual()): plays
        // the spoken "Vehicle stopping" clip. Does not touch the visual.
        public void ActivateAudio()
        {
            if (audioSource == null)
            {
                Debug.LogWarning("[eHMIController] ActivateAudio: audioSource not assigned/found.");
                return;
            }

            if (vehicleStoppingClip == null)
            {
                Debug.LogWarning("[eHMIController] ActivateAudio: vehicleStoppingClip is not assigned in the Inspector. No sound will play.");
                return;
            }

            audioSource.clip = vehicleStoppingClip;
            audioSource.Play();
            Debug.Log("[eHMIController] Audio activated (\"Vehicle stopping\" clip played).");
        }

        // No eHMI condition: hides the visual and stops any audio. Also
        // serves as the safe reset state between trials.
        public void Deactivate()
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            Debug.Log("[eHMIController] Deactivated (panel + text hidden, audio stopped).");
        }
    }
}
