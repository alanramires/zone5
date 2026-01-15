using UnityEngine;
using System.Collections;

namespace Zone5
{
    public class MissileView : MonoBehaviour
    {
        private Vector3[] _path;
        private float _duration = 2.0f;
        private Coroutine _animRoutine;

        // Trail Config
        private Material _trailMat;
        private float _width;
        private Color _color;
        private Transform _trailRoot;
        private MissileUnit _unitSync; // To register trails for cleanup

        public void ConfigureTrail(Material mat, float width, Color color, Transform root, MissileUnit unit)
        {
            _trailMat = mat;
            _width = width;
            _color = color;
            _trailRoot = root;
            _unitSync = unit;
        }

        public void SetPath(Vector3[] pts)
        {
            _path = pts;
        }

        public void AnimatePath(float duration)
        {
            _duration = duration;
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateRoutine());
        }

        private IEnumerator AnimateRoutine()
        {
            if (_path == null || _path.Length < 2) yield break;

            float elapsed = 0f;
            int lastDrawnIndex = 0;

            // Start at first point
            transform.position = _path[0];
            
            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);
                
                // 1. Move visuals
                Vector3 pos = SamplePath(t, out int currentIndex);
                transform.position = pos;
                
                Vector3 nextPos = SamplePath(t + 0.01f, out int _);
                Vector3 dir = (nextPos - pos);
                if (dir.sqrMagnitude > 0.0001f)
                {
                   float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                   transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
                }

                // 2. Draw segments we passed
                // We want to draw from [lastDrawn] up to [currentIndex].
                // But precisely, we only complete a segment when we reach index+1.
                // Loop to fill gaps if we skipped frames
                while (lastDrawnIndex < currentIndex && lastDrawnIndex < _path.Length - 1)
                {
                    DrawSegment(lastDrawnIndex, lastDrawnIndex + 1);
                    lastDrawnIndex++;
                }

                yield return null;
            }
            
            // Snap to end and finish trails
            if (_path.Length > 0)
            {
                transform.position = _path[_path.Length - 1];
                // Ensure all segments drawn
                while (lastDrawnIndex < _path.Length - 1)
                {
                    DrawSegment(lastDrawnIndex, lastDrawnIndex + 1);
                    lastDrawnIndex++;
                }
            }
        }

        private void DrawSegment(int idxA, int idxB)
        {
            if (_path == null || idxA >= _path.Length || idxB >= _path.Length) return;
            
            Vector3 A = _path[idxA];
            Vector3 B = _path[idxB];
            
            // Logic adapted from MissileManager.DrawMissileSegment
            // Ideally we'd call back, but let's replicate locally to keep logic inside View
            
            string id = (_unitSync != null) ? _unitSync.missileInstanceId : "null";
            var go = new GameObject($"MissileTrail_{id}_{idxA}");
            if (_trailRoot != null) go.transform.SetParent(_trailRoot, false);
            else go.transform.SetParent(transform.parent, false); // fallback

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = _width;
            lr.endWidth = _width;
            lr.sortingLayerName = "Background";
            lr.sortingOrder = 2;
            if (_trailMat != null) lr.material = _trailMat;
            
            // Apply Color
            lr.startColor = _color;
            lr.endColor = _color;
            if (lr.material != null) lr.material.color = _color;

            lr.SetPosition(0, A);
            lr.SetPosition(1, B);
            
            // Register for cleanup
            if (_unitSync != null) _unitSync.AddTrail(lr);
        }

        private Vector3 SamplePath(float t, out int currentIndex)
        {
            // t is 0..1
            if (_path.Length == 0) { currentIndex = 0; return transform.position; }
            if (_path.Length == 1) { currentIndex = 0; return _path[0]; }

            int totalSegments = _path.Length - 1;
            float floatIndex = t * totalSegments;
            currentIndex = Mathf.FloorToInt(floatIndex);
            
            if (currentIndex >= totalSegments) 
            {
                currentIndex = totalSegments - 1; 
                return _path[totalSegments];
            }

            float subT = floatIndex - currentIndex;
            return Vector3.Lerp(_path[currentIndex], _path[currentIndex+1], subT);
        }
    }
}
