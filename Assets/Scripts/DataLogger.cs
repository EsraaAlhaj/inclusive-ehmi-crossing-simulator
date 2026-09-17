using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace InclusiveEHMI
{
    // Step 10: writes one CSV row per completed MAIN trial to a file under
    // Application.persistentDataPath. Responsible only for file/stream
    // management and CSV formatting -- knows nothing about trial
    // sequencing, ratings, or decisions. ExperimentManager decides when a
    // trial is "complete" (decision + both ratings recorded) and calls
    // WriteTrial() exactly once per trial; practice trials are never passed
    // to this class at all. Plain C# class, not a MonoBehaviour -- same
    // convention as TrialGenerator.
    public class DataLogger
    {
        private const string HeaderRow =
            "ParticipantID,TrialNumber,EHMICondition,InitialSpeed_kmh,Decision," +
            "DecisionLatency_s,CurrentSpeedAtDecision_mps,DistanceAtDecision_m," +
            "TimeToStop_s,ClarityRating,SafetyRating,Timestamp";

        private StreamWriter _writer;
        private string _filePath;

        // Creates a new CSV file (one per experiment session) named with the
        // participant ID and a session timestamp, so repeated sessions never
        // overwrite each other, and writes the header row exactly once.
        // Closes any previously open session first, so re-starting the
        // sequence within the same Play session can't leak file handles.
        public void BeginSession(string participantId)
        {
            EndSession();

            string safeParticipantId = string.IsNullOrEmpty(participantId) ? "UnknownParticipant" : participantId;
            string sessionTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string fileName = $"{safeParticipantId}_{sessionTimestamp}.csv";
            _filePath = Path.Combine(Application.persistentDataPath, fileName);

            _writer = new StreamWriter(_filePath, append: false);
            _writer.WriteLine(HeaderRow);
            _writer.Flush();

            Debug.Log($"[DataLogger] CSV session file created: {_filePath}");
        }

        // Appends exactly one row for a completed MAIN trial and flushes
        // immediately so the row survives an unexpected close. Caller is
        // responsible for calling this exactly once per trial, only once
        // decision + both ratings are recorded -- this method does not
        // deduplicate.
        public void WriteTrial(TrialData trial)
        {
            if (_writer == null)
            {
                Debug.LogWarning("[DataLogger] WriteTrial: no active session -- call BeginSession() first. Row not written.");
                return;
            }

            string row = string.Join(",",
                EscapeCsvField(trial.ParticipantID),
                trial.TrialNumber.ToString(CultureInfo.InvariantCulture),
                trial.EHMICondition.ToString(),
                trial.InitialSpeed_kmh.ToString("F4", CultureInfo.InvariantCulture),
                trial.Decision.ToString(),
                trial.DecisionLatency_s.ToString("F4", CultureInfo.InvariantCulture),
                trial.CurrentSpeedAtDecision_mps.ToString("F4", CultureInfo.InvariantCulture),
                trial.DistanceAtDecision_m.ToString("F4", CultureInfo.InvariantCulture),
                trial.TimeToStop_s.ToString("F4", CultureInfo.InvariantCulture),
                trial.ClarityRating.ToString(CultureInfo.InvariantCulture),
                trial.SafetyRating.ToString(CultureInfo.InvariantCulture),
                trial.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            _writer.WriteLine(row);
            _writer.Flush();
        }

        // Closes the current session's writer, if any, and logs the final
        // file path. Safe to call multiple times / when no session is open
        // (no-ops the second time), so it can double as a defensive
        // shutdown-time close.
        public void EndSession()
        {
            if (_writer == null)
            {
                return;
            }

            _writer.Flush();
            _writer.Close();
            _writer.Dispose();
            _writer = null;

            Debug.Log($"[DataLogger] Experiment data saved to: {_filePath}");
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }
    }
}
