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
        }
#endif
    }
}
