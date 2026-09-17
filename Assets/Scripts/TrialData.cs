using System;

namespace InclusiveEHMI
{
    public enum EHMICondition
    {
        None,
        Visual,
        Multimodal
    }

    public enum Decision
    {
        NotRecorded,
        Cross,
        Wait
    }

    // Plain data holder for one trial (not a MonoBehaviour). TrialGenerator
    // creates these with the planned condition/speed already set; the rest of
    // the fields are filled in progressively by ExperimentManager as the
    // trial runs (decision-time values, ratings, timestamp).
    //
    // Note for analysis: DecisionLatency_s, CurrentSpeedAtDecision_mps,
    // DistanceAtDecision_m, and TimeToStop_s are all mathematically derived
    // from the same single quantity — elapsed time since braking onset (t) —
    // under the deterministic constant-deceleration motion model. DecisionLatency
    // is the primary statistical outcome; the other three are descriptive/
    // derived and should be presented as such during analysis, not treated as
    // independent outcome variables.
    public class TrialData
    {
        public string ParticipantID;
        public int TrialNumber;
        public EHMICondition EHMICondition;
        public float InitialSpeed_kmh;

        public float CurrentSpeedAtDecision_mps;
        public Decision Decision = Decision.NotRecorded;
        public float DecisionLatency_s;
        public float DistanceAtDecision_m;
        public float TimeToStop_s;

        public int ClarityRating;
        public int SafetyRating;

        public DateTime Timestamp;
    }
}
