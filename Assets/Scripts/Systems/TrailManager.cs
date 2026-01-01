using UnityEngine;

namespace Zone5
{
    public class TrailManager : MonoBehaviour
    {
        public Transform trailsRoot;
        public Material lineMaterial;
        public float lineWidth = 0.35f;

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

            if (lineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    lineMaterial = new Material(shader);
                }
            }

            if (lineMaterial != null)
            {
                lr.material = lineMaterial;
            }

            lr.startColor = color;
            lr.endColor = color;
            if (lr.material != null)
            {
                lr.material.color = color;
            }
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            owner.AddTrail(lr);
            return lr;
        }
    }
}
