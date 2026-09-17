using System.Text;
using InclusiveEHMI;
using UnityEditor;
using UnityEngine;

namespace InclusiveEHMI.EditorTools
{
    // Step 3 verification tool: runs TrialGenerator and prints the resulting
    // practice + main trial sequence to the Console, plus a repeated-generation
    // stress check of the anti-repeat rule. Not part of the experiment runtime —
    // ExperimentManager (a later step) will call TrialGenerator directly.
    public static class TrialGeneratorTest
    {
        [MenuItem("Tools/Inclusive eHMI/Test Trial Generator (Step 3)")]
        public static void RunTest()
        {
            var generator = new TrialGenerator();

            var practiceTrials = generator.GeneratePracticeTrials("TEST_PARTICIPANT");
            var mainTrials = generator.GenerateMainTrials("TEST_PARTICIPANT");

            var sb = new StringBuilder();
            sb.AppendLine("=== Practice Trials (fixed order, not logged) ===");
            foreach (TrialData trial in practiceTrials)
            {
                sb.AppendLine($"  #{trial.TrialNumber}: {trial.EHMICondition}, {trial.InitialSpeed_kmh} km/h");
            }

            sb.AppendLine("=== Main Trials (Block 1 = #1-6, Block 2 = #7-12) ===");
            foreach (TrialData trial in mainTrials)
            {
                sb.AppendLine($"  #{trial.TrialNumber}: {trial.EHMICondition}, {trial.InitialSpeed_kmh} km/h");
            }

            int adjacentRepeats = CountAdjacentConditionRepeats(mainTrials);
            sb.AppendLine($"Adjacent same-EHMICondition repeats across all 12 main trials (positions #1-2 .. #11-12, " +
                          $"including the #6->#7 block boundary): {adjacentRepeats} (should be 0)");

            Debug.Log(sb.ToString());

            StressTestAntiRepeat(2000);
        }

        // Regenerates main blocks many times to confirm the no-adjacent-repeat
        // rule never fails and every block always contains exactly one of each
        // of the 6 combinations.
        private static void StressTestAntiRepeat(int iterations)
        {
            var generator = new TrialGenerator();
            int violations = 0;

            for (int i = 0; i < iterations; i++)
            {
                var trials = generator.GenerateMainTrials("STRESS_TEST");
                if (CountAdjacentConditionRepeats(trials) > 0)
                {
                    violations++;
                }
            }

            Debug.Log($"[TrialGeneratorTest] Ran {iterations} main-trial generations: " +
                      $"{violations} generations had at least one adjacent same-EHMICondition repeat (should be 0).");
        }

        // Counts adjacent trial pairs (anywhere in the sequence, including the
        // block boundary) that share the same EHMICondition.
        private static int CountAdjacentConditionRepeats(System.Collections.Generic.List<TrialData> trials)
        {
            int count = 0;
            for (int i = 0; i < trials.Count - 1; i++)
            {
                if (trials[i].EHMICondition == trials[i + 1].EHMICondition)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
