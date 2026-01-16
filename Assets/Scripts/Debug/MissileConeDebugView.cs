using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zone5
{
    [ExecuteAlways]
    public class MissileConeDebugView : MonoBehaviour
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
        public bool useProfileMaxAbsY = true;
        public MissilePathProfile pathProfile;
        public float debugAbsYNorm = 0.14f;

        [Header("Visual")]
        public Color fillColor = new Color(1f, 0.5f, 0f, 0.35f);
        public Color hatchColor = new Color(1f, 0.5f, 0f, 0.75f);
        [Range(4, 64)] public int hatchSteps = 12;
        public float hatchWidth = 0.08f;
        public string sortingLayerName = "Trails";
        public int sortingOrder = 1;
        public float zOffset = 0f;
        public Material fillMaterial;
        public Material hatchMaterial;

        private bool _visible;
        private Transform _runtimeRoot;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private bool _runtimeCreated;
        private Material _runtimeFillMat;
        private Material _runtimeHatchMat;

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
            if (!TryGetConePoints(out var originPos, out var tipLeft, out var tipRight)) return;

            UpdateMesh(originPos, tipLeft, tipRight);
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
                var existing = transform.Find("AimConeRuntime");
                if (existing != null)
                {
                    _runtimeRoot = existing;
                }
                else
                {
                    var go = new GameObject("AimConeRuntime");
                    go.transform.SetParent(transform, false);
                    _runtimeRoot = go.transform;
                    _runtimeCreated = true;
                }
            }

            if (_meshFilter == null) _meshFilter = _runtimeRoot.GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = _runtimeRoot.gameObject.AddComponent<MeshFilter>();

            if (_meshRenderer == null) _meshRenderer = _runtimeRoot.GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = _runtimeRoot.gameObject.AddComponent<MeshRenderer>();

            ConfigureRuntimeRenderers();
        }

        private void ConfigureRuntimeRenderers()
        {
            if (_meshRenderer != null)
            {
                var materials = _meshRenderer.sharedMaterials;
                if (materials == null || materials.Length != 2)
                    materials = new Material[2];
                materials[0] = GetFillMaterial();
                materials[1] = GetHatchMaterial();
                _meshRenderer.sharedMaterials = materials;
                _meshRenderer.sortingLayerName = sortingLayerName;
                _meshRenderer.sortingOrder = sortingOrder;
                _meshRenderer.enabled = _visible;
            }
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
            _runtimeHatchMat = null;
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

        private Material GetHatchMaterial()
        {
            if (hatchMaterial != null) return hatchMaterial;
            if (_runtimeHatchMat != null) return _runtimeHatchMat;
            var shader = Shader.Find("Sprites/Default");
            _runtimeHatchMat = shader != null ? new Material(shader) : null;
            return _runtimeHatchMat;
        }

        private void UpdateMesh(Vector3 originPos, Vector3 tipLeft, Vector3 tipRight)
        {
            if (_mesh == null) _mesh = new Mesh { name = "AimConeMesh" };
            if (_runtimeRoot == null) return;
            Vector3 o = _runtimeRoot.InverseTransformPoint(originPos);
            Vector3 l = _runtimeRoot.InverseTransformPoint(tipLeft);
            Vector3 r = _runtimeRoot.InverseTransformPoint(tipRight);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var trisFill = new System.Collections.Generic.List<int>();
            var trisHatch = new System.Collections.Generic.List<int>();

            vertices.Add(o);
            vertices.Add(l);
            vertices.Add(r);
            trisFill.Add(0);
            trisFill.Add(1);
            trisFill.Add(2);

            if (hatchSteps > 0 && hatchWidth > 0f)
            {
                for (int i = 1; i <= hatchSteps; i++)
                {
                    float t = i / (float)(hatchSteps + 1);
                    Vector3 a = Vector3.Lerp(o, l, t);
                    Vector3 b = Vector3.Lerp(o, r, t);

                    Vector3 dir = (b - a);
                    dir.z = 0f;
                    if (dir.sqrMagnitude < 0.000001f) continue;
                    dir.Normalize();
                    Vector3 n = new Vector3(-dir.y, dir.x, 0f) * (hatchWidth * 0.5f);

                    int baseIdx = vertices.Count;
                    vertices.Add(a - n);
                    vertices.Add(a + n);
                    vertices.Add(b + n);
                    vertices.Add(b - n);

                    trisHatch.Add(baseIdx + 0);
                    trisHatch.Add(baseIdx + 1);
                    trisHatch.Add(baseIdx + 2);
                    trisHatch.Add(baseIdx + 0);
                    trisHatch.Add(baseIdx + 2);
                    trisHatch.Add(baseIdx + 3);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(trisFill, 0);
            _mesh.SetTriangles(trisHatch, 1);
            _mesh.RecalculateBounds();

            _meshFilter.sharedMesh = _mesh;
            var materials = _meshRenderer.sharedMaterials;
            if (materials == null || materials.Length != 2)
                materials = new Material[2];
            materials[0] = GetFillMaterial();
            materials[1] = GetHatchMaterial();
            _meshRenderer.sharedMaterials = materials;
            if (materials[0] != null) materials[0].color = fillColor;
            if (materials[1] != null) materials[1].color = hatchColor;
        }

        private void OnDrawGizmos()
        {
            DrawConeGizmos(force: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawConeGizmos(force: true);
        }

        private void DrawConeGizmos(bool force)
        {
            if (!force && !_visible) return;
            if (!TryGetConePoints(out var originPos, out var tipLeft, out var tipRight)) return;

            Gizmos.color = hatchColor;
            Gizmos.DrawLine(originPos, tipLeft);
            Gizmos.DrawLine(originPos, tipRight);
            Gizmos.DrawLine(tipLeft, tipRight);

#if UNITY_EDITOR
            Handles.color = fillColor;
            Handles.DrawAAConvexPolygon(originPos, tipLeft, tipRight);

            if (hatchSteps > 0)
            {
                Handles.color = hatchColor;
                for (int i = 1; i <= hatchSteps; i++)
                {
                    float t = i / (float)(hatchSteps + 1);
                    Vector3 a = Vector3.Lerp(originPos, tipLeft, t);
                    Vector3 b = Vector3.Lerp(originPos, tipRight, t);
                    Handles.DrawAAPolyLine(2f, a, b);
                }
            }
#endif
        }

        private bool TryGetConePoints(out Vector3 originPos, out Vector3 tipLeft, out Vector3 tipRight)
        {
            originPos = Vector3.zero;
            tipLeft = Vector3.zero;
            tipRight = Vector3.zero;

            if (origin == null) return false;
            originPos = exhaust != null ? exhaust.position : origin.position;
            originPos.z = zOffset;

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

            float maxAbsY = debugAbsYNorm;
            if (useProfileMaxAbsY && pathProfile != null && pathProfile.pointsNorm != null)
            {
                maxAbsY = 0f;
                foreach (var p in pathProfile.pointsNorm)
                {
                    float abs = Mathf.Abs(p.y);
                    if (abs > maxAbsY) maxAbsY = abs;
                }
                if (maxAbsY <= 0f) maxAbsY = debugAbsYNorm;
            }

            float effectiveFuWorld = fuWorld;
            if (refNose != null && refExhaust != null)
                effectiveFuWorld = Vector3.Distance(refNose.position, refExhaust.position);
            else if (origin != null && exhaust != null)
                effectiveFuWorld = Vector3.Distance(origin.position, exhaust.position);
            if (effectiveFuWorld <= 0.00001f) effectiveFuWorld = 1f;

            float effectiveRangeFU = missileProfile != null ? missileProfile.rangeFU : rangeFU;
            float rangeWorld = effectiveRangeFU * effectiveFuWorld;
            float halfWidthWorld = maxAbsY * rangeWorld;

            tipLeft = originPos + forward * rangeWorld + right * halfWidthWorld;
            tipRight = originPos + forward * rangeWorld - right * halfWidthWorld;
            return true;
        }
    }
}
