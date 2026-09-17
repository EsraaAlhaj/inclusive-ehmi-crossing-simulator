using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InclusiveEHMI
{
    // Step 9: simple on-screen 1-5 rating UI. Deliberately independent of
    // ExperimentManager and TrialData -- it knows nothing about trials,
    // EHMICondition, or sequencing. A caller (ExperimentManager) asks a
    // single question via AskRating(question, callback); RatingUI shows
    // itself, accepts exactly one button click, hides itself, then reports
    // the chosen 1-5 value back through the callback.
    public class RatingUI : MonoBehaviour
    {
        [Tooltip("Root GameObject for the whole rating panel, active only while a rating is being collected. Auto-resolved from child \"Panel\" if left empty.")]
        [SerializeField] private GameObject panelRoot;

        [SerializeField] private TextMeshProUGUI questionText;

        [Tooltip("Exactly 5 buttons, labeled 1-5 in order (index 0 = rating 1, ... index 4 = rating 5).")]
        [SerializeField] private Button[] ratingButtons;

        private Action<int> _onAnswered;
        private bool _awaitingAnswer;

        private void Awake()
        {
            if (panelRoot == null)
            {
                Transform found = transform.Find("Panel");
                if (found != null)
                {
                    panelRoot = found.gameObject;
                }
            }

            if (ratingButtons != null)
            {
                for (int i = 0; i < ratingButtons.Length; i++)
                {
                    Button button = ratingButtons[i];
                    if (button == null)
                    {
                        continue;
                    }

                    int rating = i + 1; // capture per-button value, not the shared loop variable
                    button.onClick.AddListener(() => HandleButtonClicked(rating));
                }
            }

            // Defensive default: the rating panel starts hidden regardless of
            // whatever state the scene happened to save it in.
            HideImmediate();
        }

        // Shows the panel with the given question and arms all 5 buttons for
        // input. onAnswered fires exactly once, with the 1-5 value of
        // whichever button is clicked first; the panel is hidden immediately
        // before the callback fires.
        public void AskRating(string question, Action<int> onAnswered)
        {
            _onAnswered = onAnswered;
            _awaitingAnswer = true;

            if (questionText != null)
            {
                questionText.text = question;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }

        // Hides the panel and discards any pending callback. Safe to call
        // whether or not a rating is currently in progress.
        public void HideImmediate()
        {
            _awaitingAnswer = false;
            _onAnswered = null;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        // Ignores every click after the first per AskRating() call, so a
        // double-click (or a second button) can't answer the same question twice.
        private void HandleButtonClicked(int rating)
        {
            if (!_awaitingAnswer)
            {
                return;
            }

            _awaitingAnswer = false;
            Action<int> callback = _onAnswered;
            _onAnswered = null;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            callback?.Invoke(rating);
        }
    }
}
