using System;
using System.Collections.Generic;

namespace InclusiveEHMI
{
    // Builds the trial sequence: 3 fixed-order practice trials, then two
    // randomized 6-trial main blocks (Fisher-Yates), with a check to avoid
    // an immediate repeat of the same condition across the block boundary.
    // Plain C# class — no MonoBehaviour, no scene/3D logic.
    public class TrialGenerator
    {
        private static readonly float[] Speeds_kmh = { 30f, 50f };
        private static readonly EHMICondition[] Conditions =
        {
            EHMICondition.None, EHMICondition.Visual, EHMICondition.Multimodal
        };

        private readonly Random _random;

        public TrialGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        // Fixed order per the design: No eHMI -> Visual -> Multimodal, all at
        // 30 km/h. Practice trials are never logged/rated.
        public List<TrialData> GeneratePracticeTrials(string participantId)
        {
            var practiceOrder = new[]
            {
                EHMICondition.None, EHMICondition.Visual, EHMICondition.Multimodal
            };

            var trials = new List<TrialData>();
            for (int i = 0; i < practiceOrder.Length; i++)
            {
                trials.Add(new TrialData
                {
                    ParticipantID = participantId,
                    TrialNumber = i + 1,
                    EHMICondition = practiceOrder[i],
                    InitialSpeed_kmh = 30f
                });
            }
            return trials;
        }

        // Block 1 = all 6 eHMI x speed combinations, Fisher-Yates shuffled
        // such that no two adjacent trials within the block share the same
        // EHMICondition (regardless of speed). Block 2 = the same 6
        // combinations, shuffled under the same no-adjacent-repeat rule, then
        // reshuffled (in full) if its first trial's EHMICondition would repeat
        // Block 1's last trial's EHMICondition. Returns a single 12-trial
        // list, numbered 1-12 (Block 1 = 1-6, Block 2 = 7-12).
        public List<TrialData> GenerateMainTrials(string participantId)
        {
            List<(EHMICondition condition, float speed)> block1 = ShuffleWithoutAdjacentConditionRepeat();
            List<(EHMICondition condition, float speed)> block2;
            do
            {
                block2 = ShuffleWithoutAdjacentConditionRepeat();
            }
            while (block2[0].condition == block1[block1.Count - 1].condition);

            var combined = new List<(EHMICondition condition, float speed)>(block1);
            combined.AddRange(block2);

            var trials = new List<TrialData>();
            for (int i = 0; i < combined.Count; i++)
            {
                trials.Add(new TrialData
                {
                    ParticipantID = participantId,
                    TrialNumber = i + 1,
                    EHMICondition = combined[i].condition,
                    InitialSpeed_kmh = combined[i].speed
                });
            }
            return trials;
        }

        // Shuffles all 6 combinations (Fisher-Yates), rejecting and reshuffling
        // whenever two adjacent trials in the result share the same
        // EHMICondition. A valid arrangement always exists (each of the 3
        // conditions appears only twice among 6 slots), so this terminates
        // quickly in practice.
        private List<(EHMICondition condition, float speed)> ShuffleWithoutAdjacentConditionRepeat()
        {
            List<(EHMICondition condition, float speed)> combinations;
            do
            {
                combinations = AllCombinations();
                FisherYatesShuffle(combinations);
            }
            while (HasAdjacentConditionRepeat(combinations));

            return combinations;
        }

        private static List<(EHMICondition condition, float speed)> AllCombinations()
        {
            var combinations = new List<(EHMICondition, float)>();
            foreach (EHMICondition condition in Conditions)
            {
                foreach (float speed in Speeds_kmh)
                {
                    combinations.Add((condition, speed));
                }
            }
            return combinations;
        }

        private static bool HasAdjacentConditionRepeat(IList<(EHMICondition condition, float speed)> combinations)
        {
            for (int i = 0; i < combinations.Count - 1; i++)
            {
                if (combinations[i].condition == combinations[i + 1].condition)
                {
                    return true;
                }
            }
            return false;
        }

        private void FisherYatesShuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
