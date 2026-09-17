# Inclusive eHMI for Pedestrian–Automated Vehicle Interaction

A desktop Unity/C# experimental prototype for studying how an automated vehicle's external Human-Machine Interface (eHMI) affects pedestrian crossing decisions. This is a **research prototype and forced-choice decision-task simulator**, not a completed VR/XR study and not a full pedestrian-walking simulation: the "pedestrian" experience is a fixed first-person camera viewpoint and a keypress response, not embodied movement.

## Research question

**How does eHMI modality affect pedestrian crossing decisions during interaction with a yielding automated vehicle?**

## Experimental design

A participant repeatedly views an approaching vehicle from a fixed roadside viewpoint and, once the vehicle begins braking, presses a key to decide whether to cross or wait. Two factors are manipulated:

**eHMI modality (3 levels):**
| Condition | Presentation |
|---|---|
| None | No eHMI — the vehicle brakes with no external signal |
| Visual | A `STOPPING` message displayed on the vehicle's front panel |
| Multimodal | The same visual message plus a spoken audio cue, `"Vehicle stopping"` |

**Initial vehicle speed (2 levels):** 30 km/h and 50 km/h

Every participant completes:
- **3 practice trials** (fixed order: None → Visual → Multimodal, all at 30 km/h, not logged to CSV, no ratings collected)
- **12 main trials** — all 6 condition × speed combinations, presented in two 6-trial blocks. Each block is randomized (Fisher–Yates) under the constraint that no two adjacent trials share the same eHMI condition, including across the block boundary.

### Response and ratings

- **Decision:** the participant presses **`C`** (Cross) or **`W`** (Wait) at any point after the vehicle begins braking. Only the first keypress per trial is recorded.
- **Subjective ratings (main trials only):** immediately after a decision, the participant answers two questions on a 1–5 scale, in order — **Clarity** ("How clear was the vehicle's intention?") then **Safety** ("How safe did you feel making your decision?"). Practice trials skip ratings entirely.

### Vehicle motion model

The vehicle approaches at a constant speed for a fixed 3-second observation window, then brakes at a constant deceleration (default 3 m/s²) to a stop a fixed offset (default 2 m) before the crossing line. Braking onset is the trigger for eHMI presentation and the start of the decision timer.

## Data collected

One CSV row is written per completed **main** trial (practice and ad-hoc test trials are never logged):

| Column | Description |
|---|---|
| `ParticipantID` | Participant identifier |
| `TrialNumber` | 1–12 (Block 1 = 1–6, Block 2 = 7–12) |
| `EHMICondition` | `None` / `Visual` / `Multimodal` |
| `InitialSpeed_kmh` | 30 or 50 |
| `Decision` | `Cross` or `Wait` |
| `DecisionLatency_s` | Time from braking onset to keypress |
| `CurrentSpeedAtDecision_mps` | Vehicle speed at the moment of decision |
| `DistanceAtDecision_m` | Distance from the vehicle's front bumper to the crossing line at decision |
| `TimeToStop_s` | Remaining time for the vehicle to reach a full stop, from its constant-deceleration model, evaluated at the decision moment |
| `ClarityRating` | 1–5 |
| `SafetyRating` | 1–5 |
| `Timestamp` | Wall-clock time the trial's data was finalized |

> **Note on derived variables:** `DecisionLatency_s`, `CurrentSpeedAtDecision_mps`, `DistanceAtDecision_m`, and `TimeToStop_s` are all mathematically derived from the same underlying quantity — elapsed time since braking onset — under the deterministic constant-deceleration motion model. `DecisionLatency_s` should be treated as the primary behavioral outcome; the other three are descriptive/derived and should not be analyzed as independent outcome variables.

## Architecture

Plain C# classes handle data and logic; MonoBehaviours handle the scene/runtime.

| Component | Responsibility |
|---|---|
| `TrialData` | Plain data holder for a single trial (condition, speed, decision, timings, ratings) |
| `TrialGenerator` | Builds the practice and main trial sequences, including the randomization and anti-repeat constraint |
| `VehicleController` | Constant-speed approach + constant-deceleration braking to a fixed stop point; fires a braking-onset event |
| `eHMIController` | Shows/hides the visual message and plays/stops the audio cue, independent of trial logic |
| `ExperimentManager` | Orchestrates the trial sequence, listens for the C/W keypress, coordinates ratings, and triggers CSV logging |
| `RatingUI` | Generic 1–5 rating panel, asks one question at a time and reports the answer via callback |
| `DataLogger` | Writes one CSV row per completed main trial to disk |

Editor tooling under `Assets/Editor/` programmatically builds the scene and prefab (vehicle, eHMI visuals, rating UI, experiment manager) and includes a randomization/anti-repeat verification tool (`Tools > Inclusive eHMI > Test Trial Generator`).

## Experiment workflow

1. Open `Assets/Scenes/ExperimentScene.unity`.
2. Select the `ExperimentManager` GameObject in the Hierarchy.
3. Enter Play Mode.
4. In the Inspector, click **Start Practice + Main Sequence**.
5. For each trial: watch the vehicle approach and brake, then press `C` or `W`.
6. On main trials, answer the Clarity and Safety rating prompts (1–5) as they appear.
7. Click **Next Trial** to continue. After all 12 main trials, the session's CSV file is finalized automatically.

An **ad-hoc single-trial test** (**Start Trial**, with condition/speed fields directly on `ExperimentManager`) is also available for quick manual checks outside the full sequence.

## How to open and run

1. Install **Unity 6 (6000.6.1f1)** or later via Unity Hub.
2. Open this folder as a project in Unity Hub.
3. Open `Assets/Scenes/ExperimentScene.unity`.
4. Press Play, select `ExperimentManager` in the Hierarchy, and click **Start Practice + Main Sequence** as described above.

## CSV output location

Each session writes a new file (existing files are never overwritten) to Unity's `Application.persistentDataPath`, named `<ParticipantID>_<yyyyMMdd_HHmmss>.csv`. On Windows this resolves to:

```
%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProjectName>\<ParticipantID>_<yyyyMMdd_HHmmss>.csv
```

A de-identified example is included in [`SampleData/`](SampleData/).

## Technologies

- Unity 6 (URP)
- C#
- TextMeshPro
- Unity Input System (new input backend)

## Methodological limitations

- This is a prototype/demo built to validate the experimental logic and data pipeline. It has **not** yet been evaluated with a participant sample.
- No claims of validation with pedestrians with disabilities are made or implied.
- The current implementation is desktop-based (fixed monitor viewpoint and keyboard input); XR/OpenXR embodiment is a possible future extension, not part of this build.
- Long-distance legibility of the visual eHMI message has been checked informally during development, not formally validated.
- The spoken semantic cue (`"Vehicle stopping"`) may introduce an attention/orienting effect in addition to a pure modality effect, since it engages an additional sensory channel at the same moment as the visual cue.
- Under the deterministic constant-deceleration motion model, `DecisionLatency_s`, `CurrentSpeedAtDecision_mps`, `DistanceAtDecision_m`, and `TimeToStop_s` are mathematically related to one another. `DecisionLatency_s` should be treated as the primary behavioral outcome; the others are descriptive/derived and should not be treated as independent measures.
- The fixed camera viewpoint is a simplified stand-in for a pedestrian's natural, freely-moving viewpoint.

## Project structure

```
Assets/
  Scripts/        Runtime logic (TrialData, TrialGenerator, VehicleController, eHMIController, ExperimentManager, RatingUI, DataLogger)
  Editor/         Scene/prefab build tooling and validation tools
  Scenes/         ExperimentScene.unity
  Prefabs/        AutomatedVehicle.prefab
  Materials/      Vehicle body and eHMI panel materials
  Audio/          Spoken "Vehicle stopping" clip
  Settings/       URP render pipeline assets
Packages/         Unity package manifest and lockfile
ProjectSettings/  Unity project configuration
Screenshots/      Portfolio screenshots
SampleData/       De-identified sample CSV output
```
