# Inclusive eHMI for Pedestrian–Automated Vehicle Interaction

A desktop Unity/C# prototype exploring how an automated vehicle's external Human-Machine Interface (eHMI) affects pedestrian crossing decisions. It's a forced-choice decision task, not a full walking simulation — the participant watches from a fixed viewpoint and responds with a keypress.

## Research question

**How does eHMI modality affect pedestrian crossing decisions during interaction with a yielding automated vehicle?**

## Experimental design

A participant watches an approaching vehicle from a fixed roadside viewpoint. Once the vehicle starts braking, they press a key to decide whether to cross or wait. Two factors are manipulated:

**eHMI modality:**

| Condition  | Presentation                                                          |
| ---------- | ---------------------------------------------------------------------- |
| None       | No eHMI — the vehicle just brakes                                     |
| Visual     | A `STOPPING` message on the vehicle's front panel                     |
| Multimodal | Same message, plus a spoken audio cue, `"Vehicle stopping"`           |

**Initial speed:** 30 km/h or 50 km/h

Each session runs 3 practice trials (fixed order: None → Visual → Multimodal, 30 km/h, not logged), then 12 main trials covering all 6 condition × speed combinations across two randomized 6-trial blocks — no two adjacent trials share the same eHMI condition, including across the block boundary.

### Response and ratings

The participant presses **`C`** (Cross) or **`W`** (Wait) after braking begins; only the first keypress counts. On main trials, they then rate Clarity ("How clear was the vehicle's intention?") and Safety ("How safe did you feel making your decision?") on a 1–5 scale. Practice trials skip ratings.

### Vehicle motion

The vehicle holds a constant speed for a fixed 3-second observation window, then brakes at a constant deceleration (default 3 m/s²) to a stop 2 m before the crossing line. Braking onset triggers the eHMI and starts the decision timer.

## Data collected

One CSV row per completed main trial:

| Column | Description |
| --- | --- |
| `ParticipantID` | Participant identifier |
| `TrialNumber` | 1–12 |
| `EHMICondition` | `None` / `Visual` / `Multimodal` |
| `InitialSpeed_kmh` | 30 or 50 |
| `Decision` | `Cross` or `Wait` |
| `DecisionLatency_s` | Time from braking onset to keypress |
| `CurrentSpeedAtDecision_mps` | Vehicle speed at the moment of decision |
| `DistanceAtDecision_m` | Front-bumper distance to the crossing line at decision |
| `TimeToStop_s` | Time remaining to a full stop at the decision moment |
| `ClarityRating` / `SafetyRating` | 1–5 |
| `Timestamp` | When the trial's data was finalized |

*(Note: under the constant-deceleration motion model, `DecisionLatency_s`, `CurrentSpeedAtDecision_mps`, `DistanceAtDecision_m`, and `TimeToStop_s` are all derived from the same underlying quantity — elapsed time since braking onset. `DecisionLatency_s` is the primary outcome; treat the rest as descriptive, not independent measures.)*

## Architecture

Plain C# classes handle data and logic; MonoBehaviours handle the scene and runtime.

- **TrialData** – plain data holder for a single trial
- **TrialGenerator** – builds the practice/main trial sequences, handles randomization and the anti-repeat constraint
- **VehicleController** – constant-speed approach + braking physics, fires a braking-onset event
- **eHMIController** – shows/hides the visual message and plays the audio cue
- **ExperimentManager** – runs the trial sequence, listens for input, coordinates ratings and logging
- **RatingUI** – generic 1–5 rating panel
- **DataLogger** – writes one CSV row per completed main trial

Editor tooling under `Assets/Editor/` builds the scene and prefabs, and includes a randomization/anti-repeat check (`Tools > Inclusive eHMI > Test Trial Generator`).

## Running it

1. Install Unity 6 (6000.6.1f1+) via Unity Hub.
2. Open this folder as a project.
3. Open `Assets/Scenes/ExperimentScene.unity`.
4. Press Play, select `ExperimentManager` in the Hierarchy, click **Start Practice + Main Sequence**.
5. For each trial: watch the vehicle, press `C` or `W`, rate Clarity/Safety on main trials, click **Next Trial**.

A single-trial test mode (**Start Trial**, with condition/speed set directly on `ExperimentManager`) is also available for quick manual checks.

## CSV output

Each session writes a new file to Unity's `Application.persistentDataPath` (existing files are never overwritten), named `<ParticipantID>_<yyyyMMdd_HHmmss>.csv`. On Windows:
