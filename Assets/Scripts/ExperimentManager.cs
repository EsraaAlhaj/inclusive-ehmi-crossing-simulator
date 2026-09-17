using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InclusiveEHMI
{
    // Step 7/8: runs eHMI trials. Two independent entry points:
    //   - StartTrial()             : Step 7's original ad hoc single-trial
    //                                 test (uses testEHMICondition/
    //                                 testInitialSpeed_kmh below). Verified
    //                                 working -- left untouched.
    //   - StartExperimentSequence(): Step 8's full sequence -- 3 fixed-order
    //                                 practice trials (not part of final
    //                                 data) followed by TrialGenerator's
    //                                 12-trial main sequence (2 blocks of
    //                                 all 6 EHMICondition x speed
    //                                 combinations, no adjacent same-
    //                                 condition repeats even across the
    //                                 block boundary). AdvanceToNextTrial()
    //                                 is the explicit, temporary "ready
    //                                 state" gate between trials -- nothing
    //                                 auto-launches after a decision.
    // Starting an ad hoc trial forcibly exits any in-progress sequence (and
    // vice versa is not possible -- AdvanceToNextTrial() only acts while a
    // sequence is active), so the two entry points can't corrupt each
    // other's state.
    //
    // Uses the new Input System (UnityEngine.InputSystem.Keyboard) rather
    // than the legacy UnityEngine.Input class -- this project's Active Input
    // Handling (Project Settings > Player) is set to "Input System Package
    // (New)" only, under which the legacy Input class throws at runtime.
    //
    // Step 9: after a MAIN trial's C/W decision (practice and ad hoc trials
    // are unaffected), the ready-for-next-trial gate is withheld until two
    // sequential 1-5 ratings are collected via RatingUI -- Clarity, then
    // Safety. See BeginRatings()/OnClarityRated()/OnSafetyRated() below.
    //
    // Step 10: once a MAIN trial's Safety rating lands (the single point at
    // which decision + both ratings are all guaranteed present -- see
    // OnSafetyRated()), the completed TrialData is written to CSV via
    // DataLogger. Practice and ad hoc trials never reach that point, so
    // they're never logged. DataLogger itself knows nothing about trial
    // sequencing -- it only writes what it's given, when it's given it.
    public class ExperimentManager : MonoBehaviour
    {
        [Tooltip("Auto-resolved via FindAnyObjectByType<VehicleController>() in Awake if left empty.")]
        [SerializeField] private VehicleController vehicleController;

        [Tooltip("Auto-resolved via FindAnyObjectByType<eHMIController>() in Awake if left empty.")]
        [SerializeField] private eHMIController ehmiController;

        [Tooltip("Auto-resolved via FindAnyObjectByType<RatingUI>() in Awake if left empty.")]
        [SerializeField] private RatingUI ratingUI;

        [Header("Step 7 ad hoc single-trial test (temporary -- independent of the Step 8 sequence below)")]
        [SerializeField] private EHMICondition testEHMICondition = EHMICondition.Visual;
        [SerializeField] private float testInitialSpeed_kmh = 30f;

        [Header("Step 8 full experiment sequence (temporary controls)")]
        [Tooltip("Stamped onto each generated TrialData.ParticipantID. Placeholder until a later step handles real participant ID entry.")]
        [SerializeField] private string participantId = "P001";

        private enum SequencePhase { Idle, Practice, Main, Complete }

        private readonly TrialGenerator _trialGenerator = new TrialGenerator();
        private readonly DataLogger _dataLogger = new DataLogger();
        private List<TrialData> _practiceTrials;
        private List<TrialData> _mainTrials;
        private SequencePhase _sequencePhase = SequencePhase.Idle;
        private int _sequenceIndex;
        private bool _sequenceReadyForNextTrial;

        private TrialData _currentTrial;
        private string _currentTrialLabel;
        private bool _waitingForDecision;
        private float _decisionTimerStart_s;
        private bool _subscribedToBrakingOnset;

        private void Awake()
        {
            if (vehicleController == null)
            {
                vehicleController = FindAnyObjectByType<VehicleController>();
            }

            if (ehmiController == null)
            {
                ehmiController = FindAnyObjectByType<eHMIController>();
            }

            if (ratingUI == null)
            {
                ratingUI = FindAnyObjectByType<RatingUI>();
            }
        }

        private void OnDestroy()
        {
            if (_subscribedToBrakingOnset && vehicleController != null)
            {
                vehicleController.OnBrakingOnset -= HandleBrakingOnset;
            }

            // Defensive: ensures the CSV writer is flushed/closed even if
            // the scene/Play Mode stops before the 12th main trial
            // finishes. No-ops if EndSession() already ran normally below.
            _dataLogger.EndSession();
        }

        // ------------------------------------------------------------------
        // Step 7: ad hoc single-trial test (unchanged from prior verification).
        // ------------------------------------------------------------------
        public void StartTrial()
        {
            if (vehicleController == null || ehmiController == null)
            {
                Debug.LogWarning("[ExperimentManager] StartTrial: vehicleController or ehmiController not assigned/found.");
                return;
            }

            // Forcibly exit any in-progress Step 8 sequence -- ad hoc testing
            // and the sequence must never advance each other's state.
            _sequencePhase = SequencePhase.Idle;
            _sequenceReadyForNextTrial = false;

            var trial = new TrialData
            {
                ParticipantID = participantId,
                EHMICondition = testEHMICondition,
                InitialSpeed_kmh = testInitialSpeed_kmh
            };

            BeginTrial(trial, "AD-HOC trial");
        }

        // ------------------------------------------------------------------
        // Step 8: full practice + main experiment sequence.
        // ------------------------------------------------------------------

        // Entry point for the temporary "Start Practice + Main Sequence"
        // Inspector button. Generates a fresh practice + main trial list
        // (via the existing TrialGenerator) and starts the first practice trial.
        public void StartExperimentSequence()
        {
            if (vehicleController == null || ehmiController == null)
            {
                Debug.LogWarning("[ExperimentManager] StartExperimentSequence: vehicleController or ehmiController not assigned/found.");
                return;
            }

            _practiceTrials = _trialGenerator.GeneratePracticeTrials(participantId);
            _mainTrials = _trialGenerator.GenerateMainTrials(participantId);

            _sequencePhase = SequencePhase.Practice;
            _sequenceIndex = 0;
            _sequenceReadyForNextTrial = false;

            Debug.Log($"[ExperimentManager] Experiment sequence started for participant '{participantId}': " +
                      $"{_practiceTrials.Count} practice trials (Console-logged only, not part of final data), " +
                      $"then {_mainTrials.Count} main trials (2 blocks x 6 condition/speed combinations).");

            _dataLogger.BeginSession(participantId);

            RunSequenceTrial();
        }

        // Entry point for the temporary "Next Trial" Inspector button -- the
        // explicit ready-state gate. Does nothing (with a warning) until a
        // decision has been recorded for the current sequence trial.
        public void AdvanceToNextTrial()
        {
            if (!_sequenceReadyForNextTrial)
            {
                Debug.LogWarning("[ExperimentManager] AdvanceToNextTrial: no completed trial waiting to advance (record a C/W decision first).");
                return;
            }

            _sequenceReadyForNextTrial = false;
            _sequenceIndex++;

            if (_sequencePhase == SequencePhase.Practice)
            {
                if (_sequenceIndex < _practiceTrials.Count)
                {
                    RunSequenceTrial();
                }
                else
                {
                    _sequencePhase = SequencePhase.Main;
                    _sequenceIndex = 0;
                    Debug.Log("[ExperimentManager] Practice trials complete (not part of final data). Starting main experiment (12 trials).");
                    RunSequenceTrial();
                }
            }
            else if (_sequencePhase == SequencePhase.Main)
            {
                if (_sequenceIndex < _mainTrials.Count)
                {
                    RunSequenceTrial();
                }
                else
                {
                    _sequencePhase = SequencePhase.Complete;
                    Debug.Log("[ExperimentManager] Main experiment complete: all 12 trials finished.");
                    _dataLogger.EndSession();
                }
            }
        }

        private void RunSequenceTrial()
        {
            TrialData trial;
            string label;

            if (_sequencePhase == SequencePhase.Practice)
            {
                trial = _practiceTrials[_sequenceIndex];
                label = $"PRACTICE trial {_sequenceIndex + 1}/{_practiceTrials.Count}";
            }
            else
            {
                trial = _mainTrials[_sequenceIndex];
                label = $"MAIN trial {_sequenceIndex + 1}/{_mainTrials.Count}";
            }

            BeginTrial(trial, label);
        }

        // ------------------------------------------------------------------
        // Shared trial machinery (used by both entry points above).
        // ------------------------------------------------------------------

        private void BeginTrial(TrialData trial, string label)
        {
            _currentTrial = trial;
            _currentTrialLabel = label;
            _waitingForDecision = false;

            ehmiController.Deactivate(); // reset to the "no eHMI" baseline before the trial begins
            ratingUI?.HideImmediate(); // reset to the "no rating active" baseline before the trial begins

            if (!_subscribedToBrakingOnset)
            {
                vehicleController.OnBrakingOnset += HandleBrakingOnset;
                _subscribedToBrakingOnset = true;
            }

            vehicleController.SetupTrial(trial.InitialSpeed_kmh);

            Debug.Log($"[ExperimentManager] {label} started: EHMICondition={trial.EHMICondition}, InitialSpeed={trial.InitialSpeed_kmh} km/h.");
        }

        // Fires once, at the exact instant VehicleController begins braking.
        private void HandleBrakingOnset(float speedAtOnset_mps)
        {
            if (_currentTrial == null)
            {
                Debug.LogWarning("[ExperimentManager] HandleBrakingOnset: no active trial (braking onset fired without a preceding StartTrial()/StartExperimentSequence()).");
                return;
            }

            switch (_currentTrial.EHMICondition)
            {
                case EHMICondition.Visual:
                    ehmiController.ActivateVisual();
                    break;
                case EHMICondition.Multimodal:
                    ehmiController.ActivateVisual();
                    ehmiController.ActivateAudio();
                    break;
                case EHMICondition.None:
                    // Nothing displayed or played, per the "No eHMI" condition.
                    break;
            }

            _decisionTimerStart_s = Time.time;
            _waitingForDecision = true;

            Debug.Log($"[ExperimentManager] {_currentTrialLabel}: braking onset, eHMI condition '{_currentTrial.EHMICondition}' presented, decision timer started.");
        }

        private void Update()
        {
            if (!_waitingForDecision)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[Key.C].wasPressedThisFrame)
            {
                RecordDecision(Decision.Cross);
            }
            else if (keyboard[Key.W].wasPressedThisFrame)
            {
                RecordDecision(Decision.Wait);
            }
        }

        // Accepts only the first C/W keypress per trial -- _waitingForDecision
        // is cleared immediately, so any further keypresses this trial are ignored.
        private void RecordDecision(Decision decision)
        {
            _waitingForDecision = false;

            _currentTrial.Decision = decision;
            _currentTrial.DecisionLatency_s = Time.time - _decisionTimerStart_s;
            _currentTrial.CurrentSpeedAtDecision_mps = vehicleController.GetCurrentSpeed();
            _currentTrial.DistanceAtDecision_m = vehicleController.GetDistanceToCrossing();

            // Physically meaningful remaining stopping time under the
            // vehicle's constant-deceleration model, evaluated at the
            // moment of decision -- deliberately not TTA (time to arrival).
            _currentTrial.TimeToStop_s = _currentTrial.CurrentSpeedAtDecision_mps / vehicleController.GetDeceleration();

            Debug.Log(
                $"[ExperimentManager] {_currentTrialLabel} -- Decision recorded:\n" +
                $"  EHMICondition: {_currentTrial.EHMICondition}\n" +
                $"  InitialSpeed_kmh: {_currentTrial.InitialSpeed_kmh}\n" +
                $"  Decision: {_currentTrial.Decision}\n" +
                $"  DecisionLatency_s: {_currentTrial.DecisionLatency_s:F4}\n" +
                $"  CurrentSpeedAtDecision_mps: {_currentTrial.CurrentSpeedAtDecision_mps:F4}\n" +
                $"  DistanceAtDecision_m: {_currentTrial.DistanceAtDecision_m:F4}\n" +
                $"  TimeToStop_s: {_currentTrial.TimeToStop_s:F4}");

            if (_sequencePhase == SequencePhase.Practice)
            {
                // Practice trials collect no ratings -- go straight to the
                // ready-for-next-trial gate.
                _sequenceReadyForNextTrial = true;
                Debug.Log("[ExperimentManager] Ready for next trial -- click 'Next Trial' to continue.");
            }
            else if (_sequencePhase == SequencePhase.Main)
            {
                BeginRatings();
            }
        }

        // ------------------------------------------------------------------
        // Step 9: main-trial subjective ratings (Clarity, then Safety).
        // Practice trials and the Step 7 ad hoc entry point never call this.
        // ------------------------------------------------------------------

        private void BeginRatings()
        {
            if (ratingUI == null)
            {
                Debug.LogWarning("[ExperimentManager] BeginRatings: ratingUI not assigned/found -- skipping ratings, trial ready for Next Trial.");
                _sequenceReadyForNextTrial = true;
                return;
            }

            ratingUI.AskRating("How clear was the vehicle's intention?", OnClarityRated);
        }

        private void OnClarityRated(int rating)
        {
            _currentTrial.ClarityRating = rating;
            Debug.Log($"[ExperimentManager] ClarityRating = {rating}");

            ratingUI.AskRating("How safe did you feel making your decision?", OnSafetyRated);
        }

        private void OnSafetyRated(int rating)
        {
            _currentTrial.SafetyRating = rating;
            Debug.Log($"[ExperimentManager] SafetyRating = {rating}");

            // This is the single point where decision + both ratings are
            // all guaranteed present -- write exactly one CSV row here.
            // RatingUI's own single-answer-per-question guard (verified in
            // Step 9) means OnSafetyRated can't fire twice for the same
            // trial, so this can't produce a duplicate row even if rating
            // buttons or "Next Trial" are clicked repeatedly.
            _currentTrial.Timestamp = DateTime.Now;
            _dataLogger.WriteTrial(_currentTrial);
            Debug.Log($"[ExperimentManager] Saved MAIN trial {_currentTrial.TrialNumber}/{_mainTrials.Count} to CSV");

            _sequenceReadyForNextTrial = true;
            Debug.Log($"[ExperimentManager] {_currentTrialLabel} ratings complete -- ready for Next Trial.");
        }
    }
}
