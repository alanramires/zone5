using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zone5
{
    [ExecuteAlways]
    public class ManeuverPreviewDebugView : MonoBehaviour
    {
        public ManeuverProfile profile;
        public Transform origin;
        public Transform exhaust;
        public TurnDir previewDir = TurnDir.F;
        public bool drawInScene = true;
        public bool drawInGame = false;
        public LineRenderer lineRenderer;
        public string sortingLayerName = "Trails";
        public int sortingOrder = 1;

        private readonly List<Vector3> _points = new();

        private void OnValidate()
        {
            AutoBindAnchors();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!drawInGame || lineRenderer == null) return;
            if (!TryBuild(out var pts)) return;

            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = pts.Count;
            lineRenderer.SetPositions(pts.ToArray());
            lineRenderer.sortingLayerName = sortingLayerName;
            lineRenderer.sortingOrder = sortingOrder;

            if (profile != null && profile.useVfx)
                DrawRuntimeVfxMarkers(pts);
        }

        private void AutoBindAnchors()
        {
            if (origin == null)
            {
                var t = transform.Find("NoseAnchor");
                if (t != null) origin = t;
            }
            if (exhaust == null)
            {
                var t = transform.Find("ExhaustAnchor");
                if (t != null) exhaust = t;
            }
        }

        private bool TryBuild(out List<Vector3> pts)
        {
            pts = _points;
            pts.Clear();
            if (profile == null) return false;

            Vector3 start = exhaust != null ? exhaust.position : (origin != null ? origin.position : transform.position);
            Vector3 forward = Vector3.up;
            if (origin != null && exhaust != null)
            {
                forward = origin.position - exhaust.position;
            }
            forward.z = 0f;
            if (forward.sqrMagnitude < 0.000001f) forward = transform.up;

            float fuWorld = 1f;
            if (origin != null && exhaust != null)
                fuWorld = Vector3.Distance(origin.position, exhaust.position);

            profile.BuildWorldPoints(start, forward, fuWorld, previewDir, pts);
            return pts.Count >= 2;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawInScene) return;
            if (!TryBuild(out var pts)) return;

            Handles.color = profile != null ? profile.previewColor : Color.yellow;
            Handles.DrawAAPolyLine(3f, pts.ToArray());

            if (profile != null && profile.useVfx)
                DrawEditorVfxMarkers(pts);
        }
#endif

        private void DrawRuntimeVfxMarkers(List<Vector3> pts)
        {
            if (profile == null) return;
            if (profile.vfxMode == VfxMode.ByProgress)
            {
                foreach (var key in profile.vfxProgress)
                {
                    Vector3 pos = SamplePolylineByProgress(pts, Mathf.Clamp01(key.p));
                    Debug.DrawLine(pos + Vector3.left * 0.1f, pos + Vector3.right * 0.1f, Color.cyan);
                    Debug.DrawLine(pos + Vector3.up * 0.1f, pos + Vector3.down * 0.1f, Color.cyan);
                }
            }
            else if (profile.vfxMode == VfxMode.ByPathXY)
            {
                BuildMarkerBasis(out var start, out var forward, out var right, out var fuWorld, out var sign);
                foreach (var key in profile.vfxXY)
                {
                    Vector3 pos = start
                        + forward * (key.x * fuWorld)
                        + right * (key.y * fuWorld * sign);
                    Debug.DrawLine(pos + Vector3.left * 0.1f, pos + Vector3.right * 0.1f, Color.cyan);
                    Debug.DrawLine(pos + Vector3.up * 0.1f, pos + Vector3.down * 0.1f, Color.cyan);
                }
            }
        }

#if UNITY_EDITOR
        private void DrawEditorVfxMarkers(List<Vector3> pts)
        {
            if (profile == null) return;
            Handles.color = new Color(0.2f, 1f, 1f, 0.9f);

            if (profile.vfxMode == VfxMode.ByProgress)
            {
                foreach (var key in profile.vfxProgress)
                {
                    Vector3 pos = SamplePolylineByProgress(pts, Mathf.Clamp01(key.p));
                    Handles.DrawSolidDisc(pos, Vector3.forward, 0.06f);
                }
                return;
            }

            if (profile.vfxMode == VfxMode.ByPathXY)
            {
                BuildMarkerBasis(out var start, out var forward, out var right, out var fuWorld, out var sign);
                foreach (var key in profile.vfxXY)
                {
                    Vector3 pos = start
                        + forward * (key.x * fuWorld)
                        + right * (key.y * fuWorld * sign);
                    Handles.DrawSolidDisc(pos, Vector3.forward, 0.06f);
                }
            }
        }
#endif

        private Vector3 SamplePolylineByProgress(List<Vector3> pts, float p01)
        {
            if (pts == null || pts.Count == 0) return transform.position;
            if (pts.Count == 1) return pts[0];

            float total = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
                total += Vector3.Distance(pts[i], pts[i + 1]);
            if (total <= 0.000001f) return pts[0];

            float target = Mathf.Clamp01(p01) * total;
            float accum = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float seg = Vector3.Distance(pts[i], pts[i + 1]);
                if (accum + seg >= target)
                {
                    float t = seg > 0.000001f ? (target - accum) / seg : 0f;
                    return Vector3.Lerp(pts[i], pts[i + 1], t);
                }
                accum += seg;
            }
            return pts[pts.Count - 1];
        }

        private void BuildMarkerBasis(out Vector3 start, out Vector3 forward, out Vector3 right, out float fuWorld, out float sign)
        {
            start = exhaust != null ? exhaust.position : (origin != null ? origin.position : transform.position);
            forward = Vector3.up;
            if (origin != null && exhaust != null)
                forward = origin.position - exhaust.position;
            forward.z = 0f;
            if (forward.sqrMagnitude < 0.000001f) forward = transform.up;
            forward.Normalize();
            right = new Vector3(-forward.y, forward.x, 0f).normalized;

            fuWorld = 1f;
            if (origin != null && exhaust != null)
                fuWorld = Vector3.Distance(origin.position, exhaust.position);

            sign = previewDir == TurnDir.D ? -1f : 1f;
            fuWorld = Mathf.Max(0.01f, fuWorld) * Mathf.Max(0f, profile != null ? profile.distanceFU : 1f);
        }
    }
}
