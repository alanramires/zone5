using System.Collections;
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
        private bool _hasFinalHeading;
        private Vector3 _finalHeading;
        private float[] _segmentLengths;
        private float[] _cumulativeLengths;
        private float _totalLength;

        public void ConfigureTrail(TrailManager trailManager, Color color, AircraftUnit unit)
        {
            if (trailManager != null)
            {
                _trailMat = trailManager.lineMaterial;
                _trailWidth = trailManager.lineWidth;
                _trailRoot = trailManager.trailsRoot != null ? trailManager.trailsRoot : trailManager.transform;
            }

            _trailColor = color;
            _unitSync = unit;
        }

        public void SetPath(Vector3[] pts)
        {
            _path = pts;
            _hasFinalHeading = false;
            _finalHeading = Vector3.zero;
            BuildDistances();
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
            _animRoutine = StartCoroutine(AnimateRoutine());
        }

        private IEnumerator AnimateRoutine()
        {
            if (_path == null || _path.Length < 2) yield break;

            EnsureLiveTrail();
            UpdateTransform(_path[0], _path[1] - _path[0], 0f);
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
            ApplyFinalHeadingIfNeeded();
            _liveTrail = null;
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

            lr.startColor = _trailColor;
            lr.endColor = _trailColor;
            if (lr.material != null) lr.material.color = _trailColor;

            lr.SetPosition(0, A);
            lr.SetPosition(1, B);

            _unitSync.AddTrail(lr);
        }

        private void EnsureLiveTrail()
        {
            if (_liveTrail != null || _unitSync == null) return;

            string unitId = string.IsNullOrEmpty(_unitSync.unitId) ? _unitSync.name : _unitSync.unitId;
            var go = new GameObject($"TrailSegment_{unitId}_live");
            if (_trailRoot != null) go.transform.SetParent(_trailRoot, false);
            else go.transform.SetParent(_unitSync.transform.parent, false);

            _liveTrail = go.AddComponent<LineRenderer>();
            _liveTrail.useWorldSpace = true;
            _liveTrail.positionCount = 2;
            _liveTrail.startWidth = _trailWidth;
            _liveTrail.endWidth = _trailWidth;
            _liveTrail.sortingLayerName = "Background";
            _liveTrail.sortingOrder = 1;

            if (_trailMat == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _trailMat = new Material(shader);
            }
            if (_trailMat != null) _liveTrail.material = _trailMat;

            _liveTrail.startColor = _trailColor;
            _liveTrail.endColor = _trailColor;
            if (_liveTrail.material != null) _liveTrail.material.color = _trailColor;

            _unitSync.AddTrail(_liveTrail);
        }

        private void UpdateLiveTrail(Vector3 currentPos, int currentIndex)
        {
            if (_liveTrail == null || _path == null || _path.Length < 2) return;

            int clampedIndex = Mathf.Clamp(currentIndex, 0, _path.Length - 2);
            int count = clampedIndex + 2;
            if (_liveTrail.positionCount != count)
                _liveTrail.positionCount = count;

            for (int i = 0; i <= clampedIndex; i++)
                _liveTrail.SetPosition(i, _path[i]);

            _liveTrail.SetPosition(clampedIndex + 1, currentPos);
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
    }
}
