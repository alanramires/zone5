using UnityEngine;

namespace Zone5
{
    public class TrailManager : MonoBehaviour
    {
        public Transform trailsRoot;
        public Material lineMaterial;
        public bool forceGradientMaterial = true;
        public Material gradientMaterial;
        public float lineWidth = 0.35f;
        public float minDeformWidth = 0.01f;
        private Material _runtimeGradientMaterial;
        private Material _runtimeLineMaterial;

        public LineRenderer CreateSegment(AircraftUnit owner, Vector3 start, Vector3 end, Color color)
        {
            if (owner == null) return null;

            Transform parent = trailsRoot != null ? trailsRoot : transform;
            int index = owner.trailSegments != null ? owner.trailSegments.Count : 0;
            string unitId = string.IsNullOrEmpty(owner.unitId) ? owner.name : owner.unitId;

            var go = new GameObject($"TrailSegment_{unitId}_{index}");
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.sortingLayerName = "Background";
            lr.sortingOrder = 1;

            lr.material = GetTrailMaterial();

            lr.colorGradient = BuildTrailGradient(color);
            if (lr.material != null)
                lr.material.color = Color.white;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            owner.AddTrail(lr);
            return lr;
        }

        public Material GetTrailMaterial()
        {
            if (forceGradientMaterial)
                return GetGradientMaterial();
            return GetLineMaterial();
        }

        private Material GetGradientMaterial()
        {
            if (gradientMaterial != null) return gradientMaterial;
            if (_runtimeGradientMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    _runtimeGradientMaterial = new Material(shader);
            }
            return _runtimeGradientMaterial;
        }

        private Material GetLineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            if (_runtimeLineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    _runtimeLineMaterial = new Material(shader);
            }
            return _runtimeLineMaterial;
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
    }
}
