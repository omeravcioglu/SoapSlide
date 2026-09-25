using System;
using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Follows the target with different offsets, look heights, and orbit angles per match phase
    /// (planning vs action, etc.) so the view shifts perspective instead of staying fixed.
    /// </summary>
    public sealed class SoapSlideCameraFollow : MonoBehaviour
    {
        [Serializable]
        public struct PhaseRig
        {
            [Tooltip("Camera position = anchor + RotateY(orbitYaw) * offset.")]
            public Vector3 Offset;
            [Tooltip("Look-at point relative to the follow target.")]
            public Vector3 LookAtOffset;
            [Tooltip("Orbit the rig around the player around world Y (degrees).")]
            [Range(-55f, 55f)]
            public float OrbitYawDegrees;
            [Tooltip("Optional: set Main Camera field of view for this phase; 0 = leave unchanged.")]
            [Range(0f, 120f)]
            public float FieldOfView;
        }

        [Header("Planning (aim line)")]
        [SerializeField] PhaseRig _planning = new PhaseRig
        {
            Offset = new Vector3(1.2f, 16.5f, -20f),
            LookAtOffset = new Vector3(0f, 0.32f, 0f),
            OrbitYawDegrees = 14f,
            FieldOfView = 58f
        };

        [Header("Action (slide)")]
        [SerializeField] PhaseRig _action = new PhaseRig
        {
            Offset = new Vector3(0f, 11f, -14.5f),
            LookAtOffset = new Vector3(0f, 0.5f, 0f),
            OrbitYawDegrees = 0f,
            FieldOfView = 52f
        };

        [Header("Between rounds")]
        [SerializeField] PhaseRig _betweenRounds = new PhaseRig
        {
            Offset = new Vector3(0.6f, 13.5f, -17f),
            LookAtOffset = new Vector3(0f, 0.4f, 0f),
            OrbitYawDegrees = 8f,
            FieldOfView = 55f
        };

        [Header("Game over")]
        [SerializeField] PhaseRig _gameOver = new PhaseRig
        {
            Offset = new Vector3(0f, 15f, -21f),
            LookAtOffset = new Vector3(0f, 0.45f, 0f),
            OrbitYawDegrees = 0f,
            FieldOfView = 56f
        };

        [Header("Motion")]
        [Tooltip("How fast the rig morphs when the match phase changes.")]
        [SerializeField] float _styleBlendTime = 0.38f;
        [Tooltip("Lower = snappier rig follow after style is blended.")]
        [SerializeField] float _positionSmoothTime = 0.095f;
        [Tooltip("Smoothing for the aim / look point.")]
        [SerializeField] float _lookPointSmoothTime = 0.075f;
        [Tooltip("Thin clients: kinematic replicas often have zero RB velocity — use at least this SmoothDamp time to hide net jitter.")]
        [SerializeField] float _thinClientMinPositionSmooth = 0.11f;
        [SerializeField] float _thinClientMinLookSmooth = 0.09f;
        [SerializeField] float _rotationSharpness = 18f;

        [Header("Slide feel")]
        [SerializeField] bool _leadWithVelocity = true;
        [SerializeField] float _positionLeadByVelocity = 0.1f;
        [SerializeField] float _lookLeadByVelocity = 0.055f;
        [SerializeField] float _maxLeadMeters = 1.35f;

        [Header("Startup")]
        [SerializeField] bool _snapOnSetTarget = true;

        SoapSlidePhase _phase = SoapSlidePhase.Planning;
        Camera _cam;

        Transform _target;
        Rigidbody _targetBody;
        Vector3 _posVelocity;
        Vector3 _lookPointVelocity;
        Vector3 _smoothedLookPoint;
        bool _pendingSnap;

        Vector3 _blendedOffset;
        Vector3 _blendedLookAtOffset;
        float _blendedOrbitYaw;
        Vector3 _stylePosVel;
        Vector3 _styleLookVel;
        float _styleYawVel;
        float _blendedFov;
        float _fovVel;

        Vector3 _lastTargetPosForVel;
        bool _hasLastTargetPos;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            var p = _planning;
            _blendedOffset = p.Offset;
            _blendedLookAtOffset = p.LookAtOffset;
            _blendedOrbitYaw = p.OrbitYawDegrees;
            _blendedFov = _cam != null && _cam.fieldOfView > 0.1f ? _cam.fieldOfView : p.FieldOfView;
        }

        public void SetMatchPhase(SoapSlidePhase phase) => _phase = phase;

        public void SetTarget(Transform target)
        {
            bool targetChanged = _target != target;
            _target = target;
            _targetBody = target != null ? target.GetComponent<Rigidbody>() : null;
            if (targetChanged)
            {
                _posVelocity = Vector3.zero;
                _lookPointVelocity = Vector3.zero;
                _stylePosVel = Vector3.zero;
                _styleLookVel = Vector3.zero;
                _styleYawVel = 0f;
                _fovVel = 0f;
            }

            if (_target != null)
            {
                var rig = CurrentRigTarget();
                _smoothedLookPoint = _target.position + rig.LookAtOffset;
                if (targetChanged && _snapOnSetTarget)
                    _pendingSnap = true;
                _lastTargetPosForVel = _target.position;
                _hasLastTargetPos = true;
            }
            else
            {
                _pendingSnap = false;
                _hasLastTargetPos = false;
            }
        }

        PhaseRig CurrentRigTarget()
        {
            switch (_phase)
            {
                case SoapSlidePhase.Planning: return _planning;
                case SoapSlidePhase.Action: return _action;
                case SoapSlidePhase.BetweenRounds: return _betweenRounds;
                case SoapSlidePhase.GameOver: return _gameOver;
                case SoapSlidePhase.WaitingToStart: return _planning;
                default: return _planning;
            }
        }

        void LateUpdate()
        {
            if (_target == null) return;

            var rigTarget = CurrentRigTarget();

            if (_pendingSnap)
            {
                ApplyFollowPoseImmediate(rigTarget);
                _pendingSnap = false;
                return;
            }

            _blendedOffset = Vector3.SmoothDamp(
                _blendedOffset,
                rigTarget.Offset,
                ref _stylePosVel,
                Mathf.Max(0.02f, _styleBlendTime),
                Mathf.Infinity,
                Time.deltaTime);
            _blendedLookAtOffset = Vector3.SmoothDamp(
                _blendedLookAtOffset,
                rigTarget.LookAtOffset,
                ref _styleLookVel,
                Mathf.Max(0.02f, _styleBlendTime),
                Mathf.Infinity,
                Time.deltaTime);
            _blendedOrbitYaw = Mathf.SmoothDamp(
                _blendedOrbitYaw,
                rigTarget.OrbitYawDegrees,
                ref _styleYawVel,
                Mathf.Max(0.02f, _styleBlendTime),
                Mathf.Infinity,
                Time.deltaTime);

            SmoothFieldOfView(rigTarget.FieldOfView);

            Quaternion orbit = Quaternion.Euler(0f, _blendedOrbitYaw, 0f);

            float posSmooth = _positionSmoothTime;
            float lookSmooth = _lookPointSmoothTime;
            if (SoapSlideNetUtils.IsNetcodeActive && !SoapSlideNetUtils.ShouldRunGameSimulation)
            {
                posSmooth = Mathf.Max(posSmooth, _thinClientMinPositionSmooth);
                lookSmooth = Mathf.Max(lookSmooth, _thinClientMinLookSmooth);
            }

            Vector3 horizontalVel = Vector3.zero;
            if (_leadWithVelocity && _targetBody != null)
            {
                var v = _targetBody.linearVelocity;
                horizontalVel = new Vector3(v.x, 0f, v.z);
                if (horizontalVel.sqrMagnitude < 0.0004f && SoapSlideNetUtils.IsNetcodeActive &&
                    !SoapSlideNetUtils.ShouldRunGameSimulation && _hasLastTargetPos)
                {
                    float dt = Time.deltaTime;
                    if (dt > 1e-5f)
                    {
                        var delta = _target.position - _lastTargetPosForVel;
                        delta.y = 0f;
                        horizontalVel = delta / dt;
                    }
                }
            }

            _lastTargetPosForVel = _target.position;

            float lead = horizontalVel.magnitude;
            Vector3 leadOffset = Vector3.zero;
            if (lead > 0.01f)
            {
                Vector3 dir = horizontalVel / lead;
                float clamped = Mathf.Min(lead * _positionLeadByVelocity, _maxLeadMeters);
                leadOffset = dir * clamped;
            }

            Vector3 anchor = _target.position + leadOffset;
            Vector3 worldOffset = orbit * _blendedOffset;
            Vector3 desiredPos = anchor + worldOffset;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPos,
                ref _posVelocity,
                posSmooth,
                Mathf.Infinity,
                Time.deltaTime);

            Vector3 lookHint = _target.position + _blendedLookAtOffset;
            if (_leadWithVelocity && lead > 0.01f)
            {
                Vector3 dir = horizontalVel / lead;
                float clamped = Mathf.Min(lead * _lookLeadByVelocity, _maxLeadMeters * 0.6f);
                lookHint += dir * clamped;
            }

            _smoothedLookPoint = Vector3.SmoothDamp(
                _smoothedLookPoint,
                lookHint,
                ref _lookPointVelocity,
                Mathf.Max(0.02f, lookSmooth),
                Mathf.Infinity,
                Time.deltaTime);

            Quaternion desiredRot = Quaternion.LookRotation(_smoothedLookPoint - transform.position, Vector3.up);
            float t = 1f - Mathf.Exp(-_rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
        }

        void SmoothFieldOfView(float targetFov)
        {
            if (_cam == null || targetFov <= 0.1f) return;
            _blendedFov = Mathf.SmoothDamp(_blendedFov, targetFov, ref _fovVel, Mathf.Max(0.02f, _styleBlendTime),
                Mathf.Infinity, Time.deltaTime);
            _cam.fieldOfView = _blendedFov;
        }

        void ApplyFollowPoseImmediate(PhaseRig rig)
        {
            Quaternion orbit = Quaternion.Euler(0f, rig.OrbitYawDegrees, 0f);
            _blendedOffset = rig.Offset;
            _blendedLookAtOffset = rig.LookAtOffset;
            _blendedOrbitYaw = rig.OrbitYawDegrees;
            _stylePosVel = Vector3.zero;
            _styleLookVel = Vector3.zero;
            _styleYawVel = 0f;

            Vector3 worldOffset = orbit * _blendedOffset;
            transform.position = _target.position + worldOffset;

            Vector3 lookHint = _target.position + _blendedLookAtOffset;
            _smoothedLookPoint = lookHint;
            Quaternion desiredRot = Quaternion.LookRotation(lookHint - transform.position, Vector3.up);
            transform.rotation = desiredRot;
            _posVelocity = Vector3.zero;
            _lookPointVelocity = Vector3.zero;

            if (_cam != null && rig.FieldOfView > 0.1f)
            {
                _blendedFov = rig.FieldOfView;
                _fovVel = 0f;
                _cam.fieldOfView = _blendedFov;
            }

            if (_target != null)
            {
                _lastTargetPosForVel = _target.position;
                _hasLastTargetPos = true;
            }
        }
    }
}
