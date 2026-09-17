using System;
using UnityEngine;

namespace InclusiveEHMI
{
    // Step 5: constant-speed approach + constant-deceleration braking to a
    // fixed stop point. Still no eHMI, no trial/decision logic — this fires
    // OnBrakingOnset so other systems (ExperimentManager, later) can react,
    // but VehicleController itself knows nothing about eHMI or trials.
    //
    // Two hard constraints, both exact (no buffer margin in the motion math):
    //   1) the constant-speed phase lasts exactly 3.000s before braking begins.
    //   2) FrontBumper comes to rest exactly `stopOffset` meters before the
    //      crossing line (world Z = 0).
    public class VehicleController : MonoBehaviour
    {
        private const float KmhToMps = 1f / 3.6f;
        private const float ObservationWindow_s = 3f;

        [Tooltip("Constant deceleration during braking, m/s^2.")]
        [SerializeField] private float deceleration = 3f;

        [Tooltip("Distance FrontBumper stops before the crossing line (world Z = 0), meters.")]
        [SerializeField] private float stopOffset = 2f;

        [Tooltip("Leading-edge reference point used for the stop-offset and distance-to-crossing math. Auto-resolved from a child named \"FrontBumper\" if left empty.")]
        [SerializeField] private Transform frontBumper;

        // Fires once, at the exact moment braking begins, with the vehicle's
        // speed (m/s) at that instant.
        public event Action<float> OnBrakingOnset;

        private enum Phase { Idle, ConstantSpeed, Braking, Stopped }

        private Phase _phase = Phase.Idle;
        private float _currentSpeed_mps;
        private float _elapsedSinceApproachStart_s;

        // Captured once in SetupTrial and re-applied on every position update
        // (see MoveAlongZ). Movement must never be derived from
        // transform.forward each frame — any incidental rotation drift on
        // this transform (from whatever source) would otherwise leak
        // straight into translation, which is what caused the ~5 degree
        // lateral (X) drift observed during Step 5 testing.
        private float _spawnX;
        private float _spawnY;

        private void Awake()
        {
            if (frontBumper == null)
            {
                frontBumper = transform.Find("FrontBumper");
            }
        }

        public float GetCurrentSpeed()
        {
            return _currentSpeed_mps;
        }

        // Exposes the configured constant-deceleration value so other
        // systems (ExperimentManager's TimeToStop_s calculation) can derive
        // physically-consistent quantities from it without duplicating the
        // constant. Read-only -- does not affect braking physics.
        public float GetDeceleration()
        {
            return deceleration;
        }

        // Distance from the vehicle's front bumper to the crossing line at
        // world Z = 0, measured along the road.
        public float GetDistanceToCrossing()
        {
            return frontBumper.position.z;
        }

        // Positions the vehicle at the exact spawn distance for this initial
        // speed and starts the constant-speed approach. Start distance is
        // derived purely from v0 and braking physics — no buffer margin:
        //   startDistance = v0*3.0 + d_braking,  d_braking = v0^2 / (2*a)
        // measured from FrontBumper's stop position (stopOffset before the
        // crossing line) back to FrontBumper's spawn position.
        public void SetupTrial(float initialSpeed_kmh)
        {
            _currentSpeed_mps = initialSpeed_kmh * KmhToMps;

            float dBraking = (_currentSpeed_mps * _currentSpeed_mps) / (2f * deceleration);
            float startDistance = _currentSpeed_mps * ObservationWindow_s + dBraking;

            float frontBumperLocalOffsetZ = frontBumper.localPosition.z;
            float frontBumperStopWorldZ = stopOffset;
            float frontBumperSpawnWorldZ = frontBumperStopWorldZ + startDistance;
            float rootSpawnWorldZ = frontBumperSpawnWorldZ + frontBumperLocalOffsetZ;

            transform.position = new Vector3(0f, 0f, rootSpawnWorldZ);
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            _spawnX = transform.position.x;
            _spawnY = transform.position.y;

            _elapsedSinceApproachStart_s = 0f;
            _phase = Phase.ConstantSpeed;

            Debug.Log($"[VehicleController] SetupTrial: {initialSpeed_kmh} km/h = {_currentSpeed_mps:F6} m/s, " +
                      $"d_braking = {dBraking:F4} m, startDistance = {startDistance:F4} m, " +
                      $"root spawn Z = {rootSpawnWorldZ:F4} m, front bumper spawn Z = {frontBumperSpawnWorldZ:F4} m.");
            Debug.Log($"[VehicleController] X at SetupTrial start: {transform.position.x:F6} m (expected 0).");
        }

        // Moves strictly along world -Z (the vehicle's fixed direction of
        // travel) by `distance` meters, pinning X and Y to their
        // SetupTrial-time values every frame. Deliberately does NOT use
        // transform.forward: deriving the movement vector from the
        // transform's current rotation each frame let any incidental
        // rotation drift leak directly into lateral (X) translation.
        private void MoveAlongZ(float distance)
        {
            Vector3 position = transform.position;
            position.x = _spawnX;
            position.y = _spawnY;
            position.z -= distance;
            transform.position = position;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.ConstantSpeed:
                    UpdateConstantSpeed();
                    break;
                case Phase.Braking:
                    ApplyBraking(Time.deltaTime);
                    break;
            }
        }

        private void UpdateConstantSpeed()
        {
            float remainingToOnset = ObservationWindow_s - _elapsedSinceApproachStart_s;

            if (Time.deltaTime < remainingToOnset)
            {
                MoveAlongZ(_currentSpeed_mps * Time.deltaTime);
                _elapsedSinceApproachStart_s += Time.deltaTime;
                return;
            }

            // Advance exactly to the 3.000s mark within this frame, then spend
            // any leftover fraction of the frame on braking, so braking onset
            // itself always lands on precisely t = 3.000s regardless of frame rate.
            MoveAlongZ(_currentSpeed_mps * remainingToOnset);
            float leftoverDeltaTime = Time.deltaTime - remainingToOnset;
            _elapsedSinceApproachStart_s = ObservationWindow_s;

            _phase = Phase.Braking;
            Debug.Log($"[VehicleController] Braking onset at t={_elapsedSinceApproachStart_s:F6}s " +
                      $"(target {ObservationWindow_s:F6}s), speed={_currentSpeed_mps:F4} m/s.");
            Debug.Log($"[VehicleController] X at braking onset: {transform.position.x:F6} m (expected {_spawnX:F6} m).");
            OnBrakingOnset?.Invoke(_currentSpeed_mps);

            if (leftoverDeltaTime > 0f)
            {
                ApplyBraking(leftoverDeltaTime);
            }
        }

        private void ApplyBraking(float dt)
        {
            float timeToStopFromCurrent = _currentSpeed_mps / deceleration;

            if (dt >= timeToStopFromCurrent)
            {
                // Speed reaches zero partway through this frame's dt.
                float distance = _currentSpeed_mps * timeToStopFromCurrent
                                  - 0.5f * deceleration * timeToStopFromCurrent * timeToStopFromCurrent;
                MoveAlongZ(distance);
                _currentSpeed_mps = 0f;
                SnapToStopPosition();
                _phase = Phase.Stopped;
                return;
            }

            float distanceThisStep = _currentSpeed_mps * dt - 0.5f * deceleration * dt * dt;
            MoveAlongZ(distanceThisStep);
            _currentSpeed_mps -= deceleration * dt;
        }

        // Snaps to the exact stop position (FrontBumper at stopOffset before
        // the crossing line), eliminating any residual floating-point drift
        // from the discrete per-frame braking integration above.
        private void SnapToStopPosition()
        {
            float frontBumperLocalOffsetZ = frontBumper.localPosition.z;
            float rootStopWorldZ = stopOffset + frontBumperLocalOffsetZ;
            Vector3 position = transform.position;
            position.x = _spawnX;
            position.y = _spawnY;
            position.z = rootStopWorldZ;
            transform.position = position;

            Debug.Log($"[VehicleController] Stopped. Front bumper Z={frontBumper.position.z:F4}m " +
                      $"(target stopOffset={stopOffset:F4}m), root Z={transform.position.z:F4}m.");
            Debug.Log($"[VehicleController] X at final stop: {transform.position.x:F6} m (expected {_spawnX:F6} m).");
        }
    }
}
