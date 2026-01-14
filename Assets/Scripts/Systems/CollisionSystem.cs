using UnityEngine;

namespace Zone5
{
    public struct Segment
    {
        public Vector3 a;
        public Vector3 b;

        public Segment(Vector3 a, Vector3 b)
        {
            this.a = a;
            this.b = b;
        }
    }

    public static class CollisionSystem
    {
        public static float MinDistanceSegmentToSegment2D(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if ((b - a).sqrMagnitude < 1e-8f) return DistancePointToSegment2D(a, c, d);
            if ((d - c).sqrMagnitude < 1e-8f) return DistancePointToSegment2D(c, a, b);

            if (SegmentsIntersect2D(a, b, c, d)) return 0f;

            float d1 = DistancePointToSegment2D(a, c, d);
            float d2 = DistancePointToSegment2D(b, c, d);
            float d3 = DistancePointToSegment2D(c, a, b);
            float d4 = DistancePointToSegment2D(d, a, b);
            return Mathf.Min(Mathf.Min(d1, d2), Mathf.Min(d3, d4));
        }

        public static float DistancePointToSegment2D(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; ab.z = 0f;
            Vector3 ap = point - a; ap.z = 0f;
            float abLen2 = ab.sqrMagnitude;
            if (abLen2 < 1e-8f) return (point - a).magnitude;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / abLen2);
            Vector3 proj = a + t * ab;
            return (point - proj).magnitude;
        }

        public static bool SegmentsIntersect2D(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float o1 = Cross2D(b - a, c - a);
            float o2 = Cross2D(b - a, d - a);
            float o3 = Cross2D(d - c, a - c);
            float o4 = Cross2D(d - c, b - c);

            if ((o1 > 0f && o2 < 0f || o1 < 0f && o2 > 0f) &&
                (o3 > 0f && o4 < 0f || o3 < 0f && o4 > 0f))
                return true;

            const float eps = 1e-6f;
            if (Mathf.Abs(o1) < eps && OnSegment2D(a, b, c)) return true;
            if (Mathf.Abs(o2) < eps && OnSegment2D(a, b, d)) return true;
            if (Mathf.Abs(o3) < eps && OnSegment2D(c, d, a)) return true;
            if (Mathf.Abs(o4) < eps && OnSegment2D(c, d, b)) return true;

            return false;
        }

        public static float Cross2D(Vector3 u, Vector3 v) => u.x * v.y - u.y * v.x;

        public static bool OnSegment2D(Vector3 a, Vector3 b, Vector3 p)
        {
            return p.x >= Mathf.Min(a.x, b.x) - 1e-6f &&
                   p.x <= Mathf.Max(a.x, b.x) + 1e-6f &&
                   p.y >= Mathf.Min(a.y, b.y) - 1e-6f &&
                   p.y <= Mathf.Max(a.y, b.y) + 1e-6f;
        }
    }
}
