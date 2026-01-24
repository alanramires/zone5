using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public class AircraftView : MonoBehaviour
    {
        private Vector3[] _path;
        private float _duration = 2.0f;
        private Coroutine _animRoutine;

        private Material _trailMat;
        private float _trailWidth = 0.35f;
        private Color _trailColor = Color.white;
        private Transform _trailRoot;
        private AircraftUnit _unitSync;
        private LineRenderer _liveTrail;
        private LineRenderer _liveTrailL;
        private LineRenderer _liveTrailR;
        private Transform _trailPivotL;
        private Transform _trailPivotR;
        private bool _trailDeformEnabled;
        private float _trailDeformMinScale = 0.35f;
        private float _trailDeformMaxRollDeg = 90f;
        private float _trailDeformMaxScale = 3f;
        private float _globalTrailDeformMinScale = -1f;
        private List<float> _trailWidthByIndex;
        private List<float> _trailWidthHistory;
        private List<Vector3> _trailPointsL;
        private List<Vector3> _trailPointsR;
        private bool _useDualTrail;
        private float _lastDualSign;
        private bool _dualSwap;
        private bool _hasFinalHeading;
        private Vector3 _finalHeading;
        private float[] _segmentLengths;
        private float[] _cumulativeLengths;
        private float _totalLength;
        private ManeuverProfile _vfxProfile;
        private List<float> _vfxKeyCursors;
        private int _vfxKeyIndex;
        private ManeuverProfile.VfxPose _currentVfxPose;
        private Color _vfxNormalTint = Color.white;
        private Color _vfxCurrentTint = Color.white;
        private TurnDir _vfxDir = TurnDir.F;
        private Vector3 _vfxStart;
        private Vector3 _vfxForward;
        private float _vfxFuWorld = 1f;
        private int _lastLiveCount;
        private bool _lastAffectTrailDeform;
        private int _widthBlendRemaining;
        private float _widthBlendStart;
        private float _widthBlendTarget;
        private const int WidthBlendSteps = 4;
        private float _lastTrailWidth = -1f;
        private bool _hasLastTrailWidth;
        [SerializeField] private bool _trailDebug;
        private float _trailDebugNextTime;

        public void ConfigureTrail(TrailManager trailManager, Color color, AircraftUnit unit)
        {
            if (trailManager != null)
            {
                _trailMat = trailManager.GetTrailMaterial();
                _trailWidth = trailManager.lineWidth;
                _trailRoot = trailManager.trailsRoot != null ? trailManager.trailsRoot : trailManager.transform;
                _globalTrailDeformMinScale = Mathf.Max(0.0001f, trailManager.minDeformWidth);
            }

            _trailColor = color;
            _unitSync = unit;
        }

        public void ConfigureVfx(ManeuverProfile profile, TurnDir dir, Vector3 start, Vector3 forward, float fuWorld)
        {
            _vfxProfile = profile;
            _vfxDir = dir;
            _vfxStart = start;
            _vfxForward = forward;
            _vfxFuWorld = fuWorld;
            _vfxKeyIndex = 0;
            _vfxKeyCursors = null;
            _trailWidthByIndex = null;
            _trailWidthHistory = null;
            _trailDeformEnabled = profile != null && profile.trailDeformEnabled;
            _trailDeformMinScale = profile != null ? profile.trailDeformMinScale : 0.35f;
            _trailDeformMaxRollDeg = profile != null ? profile.trailDeformMaxRollDeg : 90f;
            _trailDeformMaxScale = profile != null ? profile.trailDeformMaxScale : 3f;
            if (_globalTrailDeformMinScale > 0f)
                _trailDeformMinScale = _globalTrailDeformMinScale;
            _useDualTrail = false;
            _trailPointsL = null;
            _trailPointsR = null;
            _dualSwap = false;
            _lastDualSign = 0f;
            _trailPivotL = null;
            _trailPivotR = null;
            _lastLiveCount = 0;
            _lastAffectTrailDeform = false;
            _widthBlendRemaining = 0;
            _widthBlendStart = 0f;
            _widthBlendTarget = 0f;
            _currentVfxPose = new ManeuverProfile.VfxPose
            {
                rollXDeg = 0f,
                rollYDeg = 0f,
                scale = Vector2.one,
                affectTrailDeform = false
            };

            if (_unitSync != null && _unitSync.visualSprite != null)
                _vfxNormalTint = _unitSync.visualSprite.color;
            else
                _vfxNormalTint = Color.white;
            _vfxCurrentTint = _vfxNormalTint;
        }

        public void SetPath(Vector3[] pts)
        {
            _path = pts;
            _hasFinalHeading = false;
            _finalHeading = Vector3.zero;
            _lastLiveCount = 0;
            _lastAffectTrailDeform = false;
            _widthBlendRemaining = 0;
            ResampleStraightPathIfNeeded();
            BuildDistances();
            BuildVfxKeyCursors();
            BuildTrailWidthCache();
            BuildTrailWidthHistory();
        }

        public void SetFinalHeading(Vector3 heading)
        {
            heading.z = 0f;
            if (heading.sqrMagnitude < 0.000001f)
            {
                _hasFinalHeading = false;
                _finalHeading = Vector3.zero;
                return;
            }

            _finalHeading = heading.normalized;
            _hasFinalHeading = true;
        }

        public void AnimatePath(float duration)
        {
            _duration = duration;
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _liveTrail = null; // start a fresh trail so older ones remain visible
            _liveTrailL = null;
            _liveTrailR = null;
            _lastLiveCount = 0;
            _lastAffectTrailDeform = false;
            _widthBlendRemaining = 0;
            _animRoutine = StartCoroutine(AnimateRoutine());
        }

        private IEnumerator AnimateRoutine()
        {
            if (_path == null || _path.Length < 2) yield break;

            EnsureLiveTrail();
            UpdateTransform(_path[0], _path[1] - _path[0], 0f);
            PrepareInitialWidthBlend();
            UpdateLiveTrail(_path[0], 0);
            float elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);

                Vector3 pos = SamplePath(t, out int currentIndex);
                float lookAhead = GetLookAheadStep();
                Vector3 prevPos = SamplePath(Mathf.Clamp01(t - lookAhead), out int _);
                Vector3 nextPos = SamplePath(Mathf.Clamp01(t + lookAhead), out int _);
                Vector3 dir = nextPos - prevPos;
                UpdateTransform(pos, dir, t);

                UpdateLiveTrail(pos, currentIndex);

                yield return null;
            }

            Vector3 finalDir = _path[_path.Length - 1] - _path[_path.Length - 2];
            UpdateTransform(_path[_path.Length - 1], finalDir, 1f);
            UpdateLiveTrail(_path[_path.Length - 1], _path.Length - 2);
            BakeTrailHistoryIfNeeded();
            CaptureLastTrailWidth();
            ApplyFinalHeadingIfNeeded();
            ResetVfxIfNeeded();
            _liveTrail = null;
            _liveTrailL = null;
            _liveTrailR = null;
        }

        private void UpdateTransform(Vector3 pos, Vector3 dir, float t)
        {
            if (_unitSync == null) return;

            dir.z = 0f;
            if (_hasFinalHeading && dir.sqrMagnitude > 0.0001f)
            {
                float blend = Mathf.InverseLerp(0.85f, 1f, Mathf.Clamp01(t));
                if (blend > 0f)
                    dir = Vector3.Slerp(dir.normalized, _finalHeading, blend);
            }
            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _unitSync.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
            }

            Vector3 exhaustPos = _unitSync.ExhaustAnchor != null ? _unitSync.ExhaustAnchor.position : _unitSync.transform.position;
            Vector3 delta = pos - exhaustPos;
            _unitSync.transform.position += delta;

            ApplyVfx(t);
        }

        private void ApplyVfx(float t)
        {
            if (_unitSync == null || _vfxProfile == null || !_vfxProfile.useVfx)
            {
                ResetVfxIfNeeded();
                _currentVfxPose = new ManeuverProfile.VfxPose
                {
                    rollXDeg = 0f,
                    rollYDeg = 0f,
                    scale = Vector2.one,
                    affectTrailDeform = false
                };
                return;
            }

            float cursor = _totalLength * Mathf.Clamp01(t);
            ManeuverProfile.VfxPose pose;
            if (_vfxProfile.vfxMode == VfxMode.ByPathXY && _vfxKeyCursors != null && _vfxKeyCursors.Count == _vfxProfile.vfxXY.Count)
                pose = _vfxProfile.EvaluateVfxByXY(_vfxKeyIndex, cursor, _vfxKeyCursors, out _vfxKeyIndex);
            else
                pose = _vfxProfile.EvaluateVfxByProgress(t);

            _currentVfxPose = pose;
            Color tint = ResolveBackfaceTint(pose.rollXDeg, pose.rollYDeg);
            _unitSync.ApplyVisualPose(pose.rollXDeg, pose.rollYDeg, pose.scale, tint);
        }

        private Color ResolveBackfaceTint(float rollXDeg, float rollYDeg)
        {
            if (_vfxProfile == null || !_vfxProfile.backfaceEnabled)
                return _vfxNormalTint;

            float roll = rollYDeg;
            if (Mathf.Abs(rollXDeg) > Mathf.Abs(rollYDeg))
                roll = rollXDeg;
            float angle = Mathf.Repeat(roll, 360f);
            bool back = angle > _vfxProfile.backfaceThresholdDeg && angle <= 270f;
            if (!back)
            {
                _vfxCurrentTint = _vfxNormalTint;
                return _vfxCurrentTint;
            }

            Color target = back ? _vfxProfile.backfaceColor : _vfxNormalTint;
            float lerp = Mathf.Clamp01(_vfxProfile.backfaceLerp);
            if (lerp <= 0f)
                _vfxCurrentTint = target;
            else
                _vfxCurrentTint = Color.Lerp(_vfxCurrentTint, target, lerp);
            return _vfxCurrentTint;
        }

        private void ResetVfxIfNeeded()
        {
            if (_unitSync == null) return;
            _vfxCurrentTint = _vfxNormalTint;
            _unitSync.ResetVisualPose(_vfxNormalTint);
        }

        private void ApplyFinalHeadingIfNeeded()
        {
            if (!_hasFinalHeading || _unitSync == null) return;

            Vector3 desired = _finalHeading;
            Vector3 current = MovementCore.GetForward(_unitSync);
            current.z = 0f;
            if (current.sqrMagnitude < 0.000001f) return;

            Vector3 exhaustPinned = _unitSync.ExhaustAnchor != null ? _unitSync.ExhaustAnchor.position : _unitSync.transform.position;
            Quaternion rotDelta = Quaternion.FromToRotation(current.normalized, desired.normalized);
            _unitSync.transform.rotation = rotDelta * _unitSync.transform.rotation;

            Vector3 exhaustAfter = _unitSync.ExhaustAnchor != null ? _unitSync.ExhaustAnchor.position : _unitSync.transform.position;
            _unitSync.transform.position += (exhaustPinned - exhaustAfter);
        }

        private void DrawSegment(int idxA, int idxB)
        {
            if (_path == null || idxA >= _path.Length || idxB >= _path.Length) return;
            if (_unitSync == null) return;

            Vector3 A = _path[idxA];
            Vector3 B = _path[idxB];

            string unitId = string.IsNullOrEmpty(_unitSync.unitId) ? _unitSync.name : _unitSync.unitId;
            var go = new GameObject($"TrailSegment_{unitId}_{idxA}");
            if (_trailRoot != null) go.transform.SetParent(_trailRoot, false);
            else go.transform.SetParent(_unitSync.transform.parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = _trailWidth;
            lr.endWidth = _trailWidth;
            lr.sortingLayerName = "Background";
            lr.sortingOrder = 1;

            if (_trailMat != null)
                lr.material = _trailMat;

            lr.colorGradient = BuildTrailGradient(_trailColor);
            if (lr.material != null) lr.material.color = Color.white;

            lr.SetPosition(0, A);
            lr.SetPosition(1, B);

            _unitSync.AddTrail(lr);
        }

        private void EnsureLiveTrail()
        {
            if (UseDualTrail())
            {
                EnsureLiveDualTrails();
                return;
            }
            if (_liveTrail != null || _unitSync == null) return;

            string unitId = string.IsNullOrEmpty(_unitSync.unitId) ? _unitSync.name : _unitSync.unitId;
            var go = new GameObject($"TrailSegment_{unitId}_live");
            if (_trailRoot != null) go.transform.SetParent(_trailRoot, false);
            else go.transform.SetParent(_unitSync.transform.parent, false);

            _liveTrail = go.AddComponent<LineRenderer>();
            SetupLiveLineRenderer(_liveTrail);
            _unitSync.AddTrail(_liveTrail);
        }

        private void EnsureLiveDualTrails()
        {
            if (_unitSync == null) return;
            if (_liveTrailL != null && _liveTrailR != null) return;

            EnsureTrailPivots();
            string unitId = string.IsNullOrEmpty(_unitSync.unitId) ? _unitSync.name : _unitSync.unitId;
            _liveTrailL = CreateLiveTrail($"TrailSegment_{unitId}_live_L");
            _liveTrailR = CreateLiveTrail($"TrailSegment_{unitId}_live_R");

            _unitSync.AddTrail(_liveTrailL);
            _unitSync.AddTrail(_liveTrailR);
        }

        private LineRenderer CreateLiveTrail(string name)
        {
            var go = new GameObject(name);
            if (_trailRoot != null) go.transform.SetParent(_trailRoot, false);
            else go.transform.SetParent(_unitSync.transform.parent, false);

            var lr = go.AddComponent<LineRenderer>();
            SetupLiveLineRenderer(lr);
            return lr;
        }

        // Sets up common properties for live line renderers - defines the start and ending width, material, and color gradient
        private void SetupLiveLineRenderer(LineRenderer lr)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = _trailWidth;
            lr.endWidth = _trailWidth;
            lr.sortingLayerName = "Background";
            lr.sortingOrder = 1;

            if (_trailMat == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _trailMat = new Material(shader);
            }
            if (_trailMat != null) lr.material = _trailMat;

            lr.colorGradient = BuildTrailGradient(_trailColor);
            if (lr.material != null) lr.material.color = Color.white;
        }

        private bool UseDualTrail()
        {
            if (!_useDualTrail) return false;
            return _unitSync != null && _unitSync.ExhaustL != null && _unitSync.ExhaustR != null;
        }

        private void EnsureTrailPivots()
        {
            if (_unitSync == null || _unitSync.visualRoot == null) return;
            if (_trailPivotL != null && _trailPivotR != null) return;
            if (_unitSync.ExhaustL == null || _unitSync.ExhaustR == null) return;

            _trailPivotL = CreateTrailPivot("PivotL", _unitSync.ExhaustL.position);
            _trailPivotR = CreateTrailPivot("PivotR", _unitSync.ExhaustR.position);
        }

        private Transform CreateTrailPivot(string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_unitSync.visualRoot, true);
            go.transform.position = worldPos;
            return go.transform;
        }

        // use at the end of the animation to bake the live trail into a static trail with width curve
        private void BakeTrailHistoryIfNeeded()
        {
            if (!_trailDeformEnabled || _unitSync == null || _path == null || _path.Length < 2)
                return;
            string unitId = string.IsNullOrEmpty(_unitSync.unitId) ? _unitSync.name : _unitSync.unitId;
            if (UseDualTrail())
            {
                if (_liveTrailL == null || _liveTrailR == null)
                    return;

                string turnSuffix = GetTurnSuffix();

                BakeTrailFromLive(_liveTrailL, $"TrailSegment_{unitId}_{turnSuffix}_L");
                BakeTrailFromLive(_liveTrailR, $"TrailSegment_{unitId}_{turnSuffix}_R");

                Destroy(_liveTrailL.gameObject);
                Destroy(_liveTrailR.gameObject);
                _liveTrailL = null;
                _liveTrailR = null;
                return;
            }
            if (_liveTrail == null)
                return;

            var go = new GameObject($"TrailSegment_{unitId}_baked");
            if (_trailRoot != null) go.transform.SetParent(_trailRoot, false);
            else go.transform.SetParent(_unitSync.transform.parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = _path.Length;
            lr.sortingLayerName = "Background";
            lr.sortingOrder = 1;

            if (_trailMat != null)
                lr.material = _trailMat;

            lr.colorGradient = BuildTrailGradient(_trailColor);
            if (lr.material != null) lr.material.color = Color.white;

            for (int i = 0; i < _path.Length; i++)
                lr.SetPosition(i, _path[i]);

            float denom = Mathf.Max(_totalLength, 0.0001f);
            var keys = new Keyframe[_path.Length];
            float prevTime = 0f;
            for (int i = 0; i < _path.Length; i++)
            {
                float dist = i == 0 ? 0f : (i == _path.Length - 1 ? _totalLength : _cumulativeLengths[i - 1]);
                float time = Mathf.Clamp01(dist / denom);
                time = Mathf.Max(time, prevTime);
                prevTime = time;

                float width = _trailWidth;
                if (_trailWidthHistory != null && i < _trailWidthHistory.Count)
                    width = _trailWidthHistory[i];
                else if (_trailWidthByIndex != null && i < _trailWidthByIndex.Count)
                    width = _trailWidth * _trailWidthByIndex[i];
                keys[i] = new Keyframe(time, width, 0f, 0f);
            }

            lr.widthMultiplier = 1f;
            lr.widthCurve = new AnimationCurve(keys);
            _unitSync.AddTrail(lr);

            Destroy(_liveTrail.gameObject);
        }

        private void BakeTrailFromLive(LineRenderer live, string name)
        {
            if (live == null || _unitSync == null) return;

            var go = new GameObject(name);
            if (_trailRoot != null) go.transform.SetParent(_trailRoot, false);
            else go.transform.SetParent(_unitSync.transform.parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = live.positionCount;
            lr.sortingLayerName = "Background";
            lr.sortingOrder = 1;

            if (_trailMat != null)
                lr.material = _trailMat;

            lr.colorGradient = BuildTrailGradient(_trailColor);
            if (lr.material != null) lr.material.color = Color.white;

            for (int i = 0; i < live.positionCount; i++)
                lr.SetPosition(i, live.GetPosition(i));

            ApplyBakedWidthCurve(lr, live.positionCount);
            _unitSync.AddTrail(lr);
        }

        // applies the width curve to the given line renderer based on the current trail width history or width by index
        private void ApplyBakedWidthCurve(LineRenderer lr, int count)
        {
            if (lr == null || count <= 0) return;
            //Debug.Log ("Applying baked width curve with count: " + count);

            float denom = Mathf.Max(_totalLength, 0.0001f);
            var keys = new Keyframe[count];
            float prevTime = 0f;
            for (int i = 0; i < count; i++)
            {
                float dist;
                if (i == 0)
                    dist = 0f;
                else if (_cumulativeLengths != null && i - 1 < _cumulativeLengths.Length)
                    dist = _cumulativeLengths[i - 1];
                else
                    dist = _totalLength;

                float time = Mathf.Clamp01(dist / denom);
                time = Mathf.Max(time, prevTime);
                prevTime = time;

                float width = _trailWidth;
                if (_trailWidthHistory != null && i < _trailWidthHistory.Count)
                    width = _trailWidthHistory[i];
                else if (_trailWidthByIndex != null && i < _trailWidthByIndex.Count)
                    width = _trailWidth * _trailWidthByIndex[i];
                keys[i] = new Keyframe(time, width, 0f, 0f);
            }

            lr.widthMultiplier = 1f;
            lr.widthCurve = new AnimationCurve(keys);
        }

        private string GetTurnSuffix()
        {
            if (_unitSync == null || _unitSync.maneuverHistory == null || _unitSync.maneuverHistory.Count == 0)
                return "T?";
            int last = _unitSync.maneuverHistory.Count - 1;
            return $"T{_unitSync.maneuverHistory[last].TurnIndex}";
        }

        private void UpdateLiveTrail(Vector3 currentPos, int currentIndex)
        {
            if (UseDualTrail())
            {
                UpdateDualLiveTrail(currentPos, currentIndex);
                return;
            }
            if (_liveTrail == null || _path == null || _path.Length < 2) return;

            int clampedIndex = Mathf.Clamp(currentIndex, 0, _path.Length - 2);
            int liveCount = clampedIndex + 2;
            if (_liveTrail.positionCount != liveCount)
                _liveTrail.positionCount = liveCount;

            for (int i = 0; i <= clampedIndex; i++)
                _liveTrail.SetPosition(i, _path[i]);

            _liveTrail.SetPosition(clampedIndex + 1, currentPos);
            float currentDist = GetDistanceAlongPath(clampedIndex, currentPos);
            UpdateTrailWidthHistory(liveCount, currentDist);
            ApplyTrailWidthCurve(_liveTrail, clampedIndex, liveCount, currentDist);
            LogTrailDebug(liveCount, currentDist);
        }

        private void UpdateDualLiveTrail(Vector3 currentPos, int currentIndex)
        {
            if (_liveTrailL == null || _liveTrailR == null || _path == null || _path.Length < 2) return;
            if (_unitSync == null) return;
            EnsureTrailPivots();

            int clampedIndex = Mathf.Clamp(currentIndex, 0, _path.Length - 2);
            int liveCount = clampedIndex + 2;

            if (_liveTrailL.positionCount != liveCount)
                _liveTrailL.positionCount = liveCount;
            if (_liveTrailR.positionCount != liveCount)
                _liveTrailR.positionCount = liveCount;

            GetDualEmitPositions(currentPos, out Vector3 emitL, out Vector3 emitR);

            if (_trailPointsL == null) _trailPointsL = new List<Vector3>();
            if (_trailPointsR == null) _trailPointsR = new List<Vector3>();

            while (_trailPointsL.Count < liveCount)
                _trailPointsL.Add(emitL);
            while (_trailPointsR.Count < liveCount)
                _trailPointsR.Add(emitR);

            _trailPointsL[liveCount - 1] = emitL;
            _trailPointsR[liveCount - 1] = emitR;

            for (int i = 0; i < liveCount; i++)
            {
                _liveTrailL.SetPosition(i, _trailPointsL[i]);
                _liveTrailR.SetPosition(i, _trailPointsR[i]);
            }

            float currentDist = GetDistanceAlongPath(clampedIndex, currentPos);
            UpdateTrailWidthHistory(liveCount, currentDist);
            ApplyTrailWidthCurve(_liveTrailL, clampedIndex, liveCount, currentDist);
            ApplyTrailWidthCurve(_liveTrailR, clampedIndex, liveCount, currentDist);
            LogTrailDebug(liveCount, currentDist);
        }

        private void GetDualEmitPositions(Vector3 centerPoint, out Vector3 emitL, out Vector3 emitR)
        {
            Vector3 forward2D = MovementCore.GetForward(_unitSync);
            forward2D.z = 0f;
            if (forward2D.sqrMagnitude < 0.000001f) forward2D = Vector3.up;
            forward2D.Normalize();
            Vector3 right2D = new Vector3(-forward2D.y, forward2D.x, 0f);

            float exhaustHalfSep = 0.25f;
            if (_unitSync.ExhaustL != null && _unitSync.ExhaustR != null)
                exhaustHalfSep = Vector3.Distance(_unitSync.ExhaustL.position, _unitSync.ExhaustR.position) * 0.5f;

            float converge01 = 0f;
            if (_currentVfxPose.affectTrailDeform)
                converge01 = Mathf.Clamp01(Mathf.Abs(_currentVfxPose.rollYDeg) / 90f);
            float offsetMag = exhaustHalfSep * (1f - converge01);

            emitL = centerPoint - right2D * offsetMag;
            emitR = centerPoint + right2D * offsetMag;

            float signSource = Mathf.Abs(_currentVfxPose.rollYDeg) >= Mathf.Abs(_currentVfxPose.rollXDeg)
                ? _currentVfxPose.rollYDeg
                : _currentVfxPose.rollXDeg;
            float sign = Mathf.Sign(signSource);
            if (Mathf.Abs(sign) > 0f && sign != _lastDualSign)
            {
                _dualSwap = !_dualSwap;
                _lastDualSign = sign;
            }

            if (_dualSwap)
            {
                var tmp = emitL;
                emitL = emitR;
                emitR = tmp;
            }
        }

        private static Gradient BuildTrailGradient(Color teamColor)
        {
            Color light = Color.Lerp(Color.white, teamColor, 0.5f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(light, 0f),
                    new GradientColorKey(teamColor, 0.5f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(teamColor.a, 0f),
                    new GradientAlphaKey(teamColor.a, 1f)
                }
            );
            return gradient;
        }

        private Vector3 SamplePath(float t, out int currentIndex)
        {
            if (_path.Length == 0) { currentIndex = 0; return transform.position; }
            if (_path.Length == 1) { currentIndex = 0; return _path[0]; }

            if (_totalLength <= 0.000001f)
            {
                currentIndex = 0;
                return _path[0];
            }

            float clampedT = Mathf.Clamp01(t);
            float targetDist = clampedT * _totalLength;
            int segIndex = FindSegmentIndex(targetDist);
            currentIndex = Mathf.Clamp(segIndex, 0, _path.Length - 2);

            float prevCum = currentIndex == 0 ? 0f : _cumulativeLengths[currentIndex - 1];
            float segLen = _segmentLengths[currentIndex];
            float subT = segLen > 0.000001f ? (targetDist - prevCum) / segLen : 0f;
            return Vector3.Lerp(_path[currentIndex], _path[currentIndex + 1], subT);
        }

        private float GetLookAheadStep()
        {
            if (_path == null || _path.Length < 2 || _totalLength <= 0.000001f) return 0.01f;
            float lookAheadDist = Mathf.Clamp(_totalLength * 0.02f, 0.02f, 0.2f);
            return Mathf.Clamp(lookAheadDist / _totalLength, 0.002f, 0.05f);
        }

        private void BuildDistances()
        {
            if (_path == null || _path.Length < 2)
            {
                _segmentLengths = null;
                _cumulativeLengths = null;
                _totalLength = 0f;
                return;
            }

            int segments = _path.Length - 1;
            if (_segmentLengths == null || _segmentLengths.Length != segments)
                _segmentLengths = new float[segments];
            if (_cumulativeLengths == null || _cumulativeLengths.Length != segments)
                _cumulativeLengths = new float[segments];

            float total = 0f;
            for (int i = 0; i < segments; i++)
            {
                float len = Vector3.Distance(_path[i], _path[i + 1]);
                _segmentLengths[i] = len;
                total += len;
                _cumulativeLengths[i] = total;
            }
            _totalLength = total;
        }

        private void ResampleStraightPathIfNeeded()
        {
            if (!_trailDeformEnabled || _path == null || _path.Length != 2)
                return;

            int samples = _vfxProfile != null ? Mathf.Max(2, _vfxProfile.previewSamples) : 24;
            if (samples <= 2)
                return;

            var resampled = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = samples == 1 ? 0f : i / (float)(samples - 1);
                resampled[i] = Vector3.Lerp(_path[0], _path[1], t);
            }
            _path = resampled;
        }

        private int FindSegmentIndex(float targetDist)
        {
            if (_cumulativeLengths == null || _cumulativeLengths.Length == 0) return 0;
            for (int i = 0; i < _cumulativeLengths.Length; i++)
            {
                if (targetDist <= _cumulativeLengths[i])
                    return i;
            }
            return _cumulativeLengths.Length - 1;
        }

        private void BuildTrailWidthCache()
        {
            if (!_trailDeformEnabled || _path == null || _path.Length < 2 || _totalLength <= 0.000001f)
            {
                _trailWidthByIndex = null;
                _trailWidthHistory = null;
                return;
            }

            _trailWidthByIndex = new List<float>(_path.Length);
            for (int i = 0; i < _path.Length; i++)
            {
                float dist = i == 0 ? 0f : _cumulativeLengths[i - 1];
                float t = Mathf.Clamp01(dist / _totalLength);
                _trailWidthByIndex.Add(EvaluateTrailWidthScale(t));
            }
        }

        private void BuildTrailWidthHistory()
        {
            if (!_trailDeformEnabled)
            {
                _trailWidthHistory = null;
                return;
            }
            _trailWidthHistory = new List<float>();
            if (_hasLastTrailWidth)
                _trailWidthHistory.Add(_lastTrailWidth);
        }

        // called during live trail update to extend the width history as needed
        private void UpdateTrailWidthHistory(int count, float currentDist)
        {
            if (!_trailDeformEnabled) return;
            if (_trailWidthHistory == null)
                _trailWidthHistory = new List<float>();

            if (count <= _lastLiveCount)
                return;

            bool isFirstUpdate = _lastLiveCount == 0;
            if (_trailWidthHistory.Count == 0 && _hasLastTrailWidth)
                _trailWidthHistory.Add(_lastTrailWidth);

            while (_trailWidthHistory.Count < count)
                _trailWidthHistory.Add(GetWidthForNewPoint());

            if (isFirstUpdate && _trailDebug)
            {
                float firstWidth = _trailWidthHistory.Count > 0 ? _trailWidthHistory[0] : -1f;
                Debug.Log($"[TrailDebug] seed hasLast={_hasLastTrailWidth} last={_lastTrailWidth:F3} base={_trailWidth:F3} firstHist={firstWidth:F3} evalStart={EvaluateCurrentTrailWidth():F3}");
            }
            _lastLiveCount = count;
        }

        // applies the width curve to the given line renderer based on the current trail width history or width by index
        private void ApplyTrailWidthCurve(LineRenderer lr, int clampedIndex, int count, float currentDist)
        {
            if (lr == null || _totalLength <= 0.000001f)
                return;

           // Debug.Log($"Applying trail width curve: count={count}, clampedIndex={clampedIndex}, currentDist={currentDist:F3}");
            if (!_trailDeformEnabled)
            {
                lr.widthMultiplier = 1f;
                lr.startWidth = _trailWidth;
                lr.endWidth = _trailWidth;
                lr.widthCurve = AnimationCurve.Constant(0f, 1f, _trailWidth);
                return;
            }

            EnsureTrailWidthHistory(count, currentDist);
            var keys = new Keyframe[count];
            float prevTime = 0f;
            for (int i = 0; i <= clampedIndex; i++)
            {
                float time = count <= 1 ? 0f : (float)i / (count - 1);
                time = Mathf.Max(time, prevTime);
                prevTime = time;
                float scale = 1f;
                float width = _trailWidth;
                if (_trailWidthHistory != null && i < _trailWidthHistory.Count)
                    width = _trailWidthHistory[i];
                else if (_trailWidthByIndex != null && i < _trailWidthByIndex.Count)
                    width = _trailWidth * _trailWidthByIndex[i];
                keys[i] = new Keyframe(time, width, 0f, 0f);
            }

            float lastTime = 1f;
            lastTime = Mathf.Max(lastTime, prevTime);
            float lastWidth = _trailWidth;
            if (_trailWidthHistory != null && count - 1 < _trailWidthHistory.Count)
                lastWidth = _trailWidthHistory[count - 1];
            keys[count - 1] = new Keyframe(lastTime, lastWidth, 0f, 0f);

            lr.widthMultiplier = 1f;
            lr.startWidth = 1f;
            lr.endWidth = 1f;
            lr.widthCurve = new AnimationCurve(keys);
        }

        private void EnsureTrailWidthHistory(int count, float currentDist)
        {
            if (_trailWidthHistory == null)
                _trailWidthHistory = new List<float>();

            if (_trailWidthHistory.Count >= count)
                return;

            if (_trailWidthHistory.Count == 0 && _hasLastTrailWidth)
                _trailWidthHistory.Add(_lastTrailWidth);

            while (_trailWidthHistory.Count < count)
                _trailWidthHistory.Add(GetWidthForNewPoint());
        }

        private float EvaluateCurrentTrailWidth()
        {
            float currentScale = EvaluateTrailWidthScaleFromPose(_currentVfxPose);
            return _trailWidth * currentScale;
        }

        private float GetWidthForNewPoint()
        {
            bool affect = _currentVfxPose.affectTrailDeform;
            if (_trailWidthHistory != null && _trailWidthHistory.Count == 0)
                _lastAffectTrailDeform = affect;

            if (affect != _lastAffectTrailDeform)
            {
                _widthBlendStart = _trailWidthHistory != null && _trailWidthHistory.Count > 0
                    ? _trailWidthHistory[_trailWidthHistory.Count - 1]
                    : _trailWidth;
                _widthBlendTarget = EvaluateCurrentTrailWidth();
                _widthBlendRemaining = WidthBlendSteps;
                _lastAffectTrailDeform = affect;
            }

            if (_widthBlendRemaining > 0)
            {
                float t = 1f - (_widthBlendRemaining / (float)WidthBlendSteps);
                _widthBlendRemaining--;
                return Mathf.Lerp(_widthBlendStart, _widthBlendTarget, t);
            }

            return EvaluateCurrentTrailWidth();
        }

        private void CaptureLastTrailWidth()
        {
            if (_trailWidthHistory != null && _trailWidthHistory.Count > 0)
            {
                _lastTrailWidth = _trailWidthHistory[_trailWidthHistory.Count - 1];
                _hasLastTrailWidth = true;
                return;
            }

            _lastTrailWidth = _trailWidth;
            _hasLastTrailWidth = true;
        }

        private void PrepareInitialWidthBlend()
        {
            if (!_trailDeformEnabled || !_hasLastTrailWidth)
                return;

            float currentWidth = EvaluateCurrentTrailWidth();
            if (Mathf.Abs(currentWidth - _lastTrailWidth) <= 0.0001f)
                return;

            _widthBlendStart = _lastTrailWidth;
            _widthBlendTarget = currentWidth;
            _widthBlendRemaining = WidthBlendSteps;
        }

        private void LogTrailDebug(int liveCount, float currentDist)
        {
            if (!_trailDebug) return;
            if (Time.time < _trailDebugNextTime) return;

            float rollDeg = _currentVfxPose.rollYDeg;
            float ang = Mathf.Repeat(Mathf.Abs(rollDeg), 360f);
            float open01 = Mathf.Abs(Mathf.Cos(ang * Mathf.Deg2Rad));
            const float sharpness = 2f;
            open01 = Mathf.Pow(open01, sharpness);
            float currentWidth = EvaluateCurrentTrailWidth();
            int historyCount = _trailWidthHistory != null ? _trailWidthHistory.Count : 0;
            Debug.Log($"[TrailDebug] rollY={rollDeg:F1} open01={open01:F2} width={currentWidth:F3} liveCount={liveCount} history={historyCount}");
            _trailDebugNextTime = Time.time + 0.2f;
        }
        private float GetDistanceAlongPath(int clampedIndex, Vector3 currentPos)
        {
            if (_path == null || _path.Length < 2 || _cumulativeLengths == null || _segmentLengths == null)
                return 0f;

            int idx = Mathf.Clamp(clampedIndex, 0, _path.Length - 2);
            float prevCum = idx == 0 ? 0f : _cumulativeLengths[idx - 1];
            float segLen = _segmentLengths[idx];
            if (segLen <= 0.000001f) return prevCum;

            float distIntoSeg = Vector3.Distance(_path[idx], currentPos);
            distIntoSeg = Mathf.Clamp(distIntoSeg, 0f, segLen);
            return prevCum + distIntoSeg;
        }

        private float EvaluateTrailWidthScale(float t)
        {
            if (_vfxProfile == null) return 1f;

            ManeuverProfile.VfxPose pose;
            if (_vfxProfile.vfxMode == VfxMode.ByPathXY && _vfxKeyCursors != null && _vfxKeyCursors.Count == _vfxProfile.vfxXY.Count)
            {
                float cursor = _totalLength * Mathf.Clamp01(t);
                pose = EvaluateVfxByXYAtCursor(cursor);
            }
            else
            {
                pose = _vfxProfile.EvaluateVfxByProgress(t);
            }

            return EvaluateTrailWidthScaleFromPose(pose);
        }

        private float EvaluateTrailWidthScaleFromPose(ManeuverProfile.VfxPose pose)
        {
            if (!pose.affectTrailDeform)
                return 1f;

            float rollDeg = pose.rollYDeg;
            float ang = Mathf.Repeat(Mathf.Abs(rollDeg), 360f);
            float open01 = Mathf.Abs(Mathf.Cos(ang * Mathf.Deg2Rad));
            const float sharpness = 2f;
            open01 = Mathf.Pow(open01, sharpness);
            float rollFactor = Mathf.Lerp(_trailDeformMinScale, 1f, open01);
            float scaleFactor = (pose.scale.x + pose.scale.y) * 0.5f;
            scaleFactor = Mathf.Clamp(scaleFactor, 0.1f, 3f);

            float finalScale = rollFactor * scaleFactor;
            float minAbs = Mathf.Max(0.0001f, _trailDeformMinScale);
            float maxAbs = Mathf.Max(minAbs, _trailDeformMaxScale);
            return Mathf.Clamp(finalScale, minAbs, maxAbs);
        }

        private ManeuverProfile.VfxPose EvaluateVfxByXYAtCursor(float cursor)
        {
            if (_vfxProfile == null || _vfxKeyCursors == null || _vfxKeyCursors.Count == 0)
                return new ManeuverProfile.VfxPose { rollXDeg = 0f, rollYDeg = 0f, scale = Vector2.one, affectTrailDeform = false };

            int lastIndex = _vfxKeyCursors.Count - 1;
            if (cursor <= _vfxKeyCursors[0])
            {
                var k = _vfxProfile.vfxXY[0];
                return new ManeuverProfile.VfxPose { rollXDeg = k.rollXDeg, rollYDeg = k.rollYDeg, scale = k.scale, affectTrailDeform = k.affectTrailDeform };
            }
            if (cursor >= _vfxKeyCursors[lastIndex])
            {
                var k = _vfxProfile.vfxXY[lastIndex];
                return new ManeuverProfile.VfxPose { rollXDeg = k.rollXDeg, rollYDeg = k.rollYDeg, scale = k.scale, affectTrailDeform = k.affectTrailDeform };
            }

            int i1 = 1;
            for (; i1 < _vfxKeyCursors.Count; i1++)
            {
                if (cursor <= _vfxKeyCursors[i1]) break;
            }
            int i0 = Mathf.Max(0, i1 - 1);

            float a = _vfxKeyCursors[i0];
            float b = _vfxKeyCursors[i1];
            float t = Mathf.InverseLerp(a, b, cursor);
            if (_vfxProfile.useSmooth) t = SmoothStep01(t);

            var k0 = _vfxProfile.vfxXY[i0];
            var k1 = _vfxProfile.vfxXY[i1];

            return new ManeuverProfile.VfxPose
            {
                rollXDeg = Mathf.Lerp(k0.rollXDeg, k1.rollXDeg, t),
                rollYDeg = Mathf.Lerp(k0.rollYDeg, k1.rollYDeg, t),
                scale = Vector2.Lerp(k0.scale, k1.scale, t),
                affectTrailDeform = k0.affectTrailDeform
            };
        }

        private static float SmoothStep01(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return clamped * clamped * (3f - 2f * clamped);
        }

        private void BuildVfxKeyCursors()
        {
            if (_vfxProfile == null || !_vfxProfile.useVfx || _vfxProfile.vfxMode != VfxMode.ByPathXY)
            {
                _vfxKeyCursors = null;
                return;
            }
            if (_path == null || _path.Length < 2 || _segmentLengths == null || _segmentLengths.Length == 0)
            {
                _vfxKeyCursors = null;
                return;
            }

            var keys = _vfxProfile.vfxXY;
            if (keys == null || keys.Count == 0)
            {
                _vfxKeyCursors = null;
                return;
            }

            _vfxKeyCursors = new List<float>(keys.Count);
            Vector3 forward = _vfxForward;
            forward.z = 0f;
            if (forward.sqrMagnitude < 0.000001f) forward = Vector3.up;
            forward.Normalize();
            Vector3 right = new Vector3(-forward.y, forward.x, 0f).normalized;
            float sign = _vfxDir == TurnDir.D ? -1f : 1f;
            float distanceWorld = Mathf.Max(0f, _vfxProfile.distanceFU) * Mathf.Max(0.01f, _vfxFuWorld);

            float minCursor = 0f;
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                Vector3 world = _vfxStart
                    + forward * (k.x * distanceWorld)
                    + right * (k.y * distanceWorld * sign);
                float cursor = FindClosestCursor(world, minCursor);
                _vfxKeyCursors.Add(cursor);
                minCursor = cursor;
            }
        }

        private float FindClosestCursor(Vector3 target, float minCursor)
        {
            float bestSqr = float.MaxValue;
            float bestCursor = minCursor;
            int startSeg = FindSegmentIndex(minCursor);

            for (int i = startSeg; i < _path.Length - 1; i++)
            {
                Vector3 a = _path[i];
                Vector3 b = _path[i + 1];
                Vector3 ab = b - a;
                ab.z = 0f;
                float abLenSq = ab.sqrMagnitude;
                if (abLenSq < 0.000001f) continue;

                float t = Mathf.Clamp01(Vector3.Dot(target - a, ab) / abLenSq);
                Vector3 proj = a + ab * t;
                float sqr = (target - proj).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    float prevCum = i == 0 ? 0f : _cumulativeLengths[i - 1];
                    float segLen = Mathf.Sqrt(abLenSq);
                    bestSqr = sqr;
                    bestCursor = prevCum + segLen * t;
                }
            }

            return bestCursor;
        }
    }
}
