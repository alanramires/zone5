using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zone5
{
    [ExecuteAlways]
    public class MissileAimRectDebugView : MonoBehaviour
    {
        [Header("Refs")]
        public Transform origin;
        public Transform exhaust;

        [Header("Sizing")]
        public MissileProfile missileProfile;
        public float rangeFU = 10f;
        public float fuWorld = 1f;
        public Transform refNose;
        public Transform refExhaust;
        public float debugAbsYNorm = 0.15f;

        [Header("Visual")]
        public Color fillColor = new Color(1f, 0f, 0f, 0.6f);
        public string sortingLayerName = "Trails";
        public int sortingOrder = 1;
        public float zOffset = 0f;
        public Material fillMaterial;

        private bool _visible;
        private Transform _runtimeRoot;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private bool _runtimeCreated;
        private Material _runtimeFillMat;

        private void OnEnable()
        {
            AutoBindAnchors();
            if (Application.isPlaying)
                EnsureRuntimeObjects();
        }

        private void OnDisable()
        {
            CleanupRuntimeObjects();
        }

        private void OnDestroy()
        {
            CleanupRuntimeObjects();
        }

        private void OnValidate()
        {
            AutoBindAnchors();
        }

        public void SetVisible(bool value)
        {
            _visible = value;
            ApplyRuntimeVisibility();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!_visible) { ApplyRuntimeVisibility(); return; }

            EnsureRuntimeObjects();
            if (!TryGetRectPoints(out var p0, out var p1, out var p2, out var p3)) return;
            UpdateMesh(p0, p1, p2, p3);
        }

        private void AutoBindAnchors()
        {
            if (origin == null)
            {
                var t = transform.Find("NoseAnchor");
                if (t != null) origin = t;
                else
                {
                    var unit = GetComponentInParent<AircraftUnit>();
                    if (unit != null) origin = unit.NoseAnchor;
                }
            }

            if (exhaust == null)
            {
                var t = transform.Find("ExhaustAnchor");
                if (t != null) exhaust = t;
                else
                {
                    var unit = GetComponentInParent<AircraftUnit>();
                    if (unit != null) exhaust = unit.ExhaustAnchor;
                }
            }
        }

        private void EnsureRuntimeObjects()
        {
            if (_runtimeRoot == null)
            {
                var existing = transform.Find("AimRectRuntime");
                if (existing != null)
                {
                    _runtimeRoot = existing;
                }
                else
                {
                    var go = new GameObject("AimRectRuntime");
                    go.transform.SetParent(transform, false);
                    _runtimeRoot = go.transform;
                    _runtimeCreated = true;
                }
            }

            if (_meshFilter == null) _meshFilter = _runtimeRoot.GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = _runtimeRoot.gameObject.AddComponent<MeshFilter>();

            if (_meshRenderer == null) _meshRenderer = _runtimeRoot.GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = _runtimeRoot.gameObject.AddComponent<MeshRenderer>();

            ConfigureRuntimeRenderer();
        }

        private void ConfigureRuntimeRenderer()
        {
            if (_meshRenderer == null) return;

            if (_meshRenderer.sharedMaterial == null)
                _meshRenderer.sharedMaterial = GetFillMaterial();
            _meshRenderer.sortingLayerName = sortingLayerName;
            _meshRenderer.sortingOrder = sortingOrder;
            _meshRenderer.enabled = _visible;
        }

        private void ApplyRuntimeVisibility()
        {
            if (_meshRenderer != null) _meshRenderer.enabled = _visible;
        }

        private void CleanupRuntimeObjects()
        {
            if (!Application.isPlaying) return;
            if (_runtimeCreated && _runtimeRoot != null)
                Destroy(_runtimeRoot.gameObject);
            _runtimeRoot = null;
            _meshFilter = null;
            _meshRenderer = null;
            _mesh = null;
            _runtimeFillMat = null;
            _runtimeCreated = false;
        }

        private Material GetFillMaterial()
        {
            if (fillMaterial != null) return fillMaterial;
            if (_runtimeFillMat != null) return _runtimeFillMat;
            var shader = Shader.Find("Sprites/Default");
            _runtimeFillMat = shader != null ? new Material(shader) : null;
            return _runtimeFillMat;
        }

        private void UpdateMesh(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            if (_mesh == null) _mesh = new Mesh { name = "AimRectMesh" };
            if (_runtimeRoot == null) return;

            Vector3 l0 = _runtimeRoot.InverseTransformPoint(p0);
            Vector3 l1 = _runtimeRoot.InverseTransformPoint(p1);
            Vector3 l2 = _runtimeRoot.InverseTransformPoint(p2);
            Vector3 l3 = _runtimeRoot.InverseTransformPoint(p3);

            var vertices = new System.Collections.Generic.List<Vector3>
            {
                l0, l1, l2, l3
            };

            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            _mesh.RecalculateBounds();

            _meshFilter.sharedMesh = _mesh;
            if (_meshRenderer.sharedMaterial == null)
                _meshRenderer.sharedMaterial = GetFillMaterial();
            if (_meshRenderer.sharedMaterial != null)
                _meshRenderer.sharedMaterial.color = fillColor;
        }

        private void OnDrawGizmos()
        {
            DrawRectGizmos(force: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawRectGizmos(force: true);
        }

        private void DrawRectGizmos(bool force)
        {
            if (!force && !_visible) return;
            if (!TryGetRectPoints(out var p0, out var p1, out var p2, out var p3)) return;

            Gizmos.color = fillColor;
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);

#if UNITY_EDITOR
            Handles.color = fillColor;
            Handles.DrawAAConvexPolygon(p0, p1, p2, p3);
#endif
        }

        private bool TryGetRectPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            p0 = Vector3.zero;
            p1 = Vector3.zero;
            p2 = Vector3.zero;
            p3 = Vector3.zero;

            if (origin == null) return false;
            Vector3 basePos = exhaust != null ? exhaust.position : origin.position;
            basePos.z = zOffset;

            Vector3 forward = Vector3.zero;
            if (exhaust != null)
            {
                forward = origin.position - exhaust.position;
                forward.z = 0f;
            }
            if (forward.sqrMagnitude < 0.000001f)
            {
                forward = transform.up;
                forward.z = 0f;
            }
            forward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.up;

            Vector3 right = new Vector3(-forward.y, forward.x, 0f).normalized;

            float effectiveFuWorld = fuWorld;
            if (refNose != null && refExhaust != null)
                effectiveFuWorld = Vector3.Distance(refNose.position, refExhaust.position);
            else if (origin != null && exhaust != null)
                effectiveFuWorld = Vector3.Distance(origin.position, exhaust.position);
            if (effectiveFuWorld <= 0.00001f) effectiveFuWorld = 1f;

            float effectiveRangeFU = missileProfile != null ? missileProfile.rangeFU : rangeFU;
            float rangeWorld = effectiveRangeFU * effectiveFuWorld;
            float oneFuWorld = 1f * effectiveFuWorld;
            float usableRange = Mathf.Max(0f, rangeWorld - oneFuWorld);

            Vector3 startPos = basePos + forward * oneFuWorld;
            Vector3 endPos = startPos + forward * usableRange;

            float halfWidthWorld = debugAbsYNorm * rangeWorld;

            p0 = startPos + right * halfWidthWorld;
            p1 = startPos - right * halfWidthWorld;
            p2 = endPos - right * halfWidthWorld;
            p3 = endPos + right * halfWidthWorld;
            return true;
        }
    }
}
