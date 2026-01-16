using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zone5
{
    [Flags]
    public enum AllowedDirs
    {
        None = 0,
        Forward = 1 << 0,
        Left = 1 << 1,
        Right = 1 << 2,
        Acrobatics = 1 << 3
    }

    public enum TurnDir { F, D, E }

    public enum ManeuverMainDir { Undetermined, F, D, E }

    public enum ManeuverUsage { Unlimited, OncePerMatch }

    public enum MachTier
    {
        M06 = 6,
        M09 = 9,
        M12 = 12,
        M18 = 18
    }

    public enum EvasionPenaltyTier
    {
        Zero = 0,
        One = 1,
        Two = 2,
        Three = 3
    }

    [CreateAssetMenu(
        menuName = "Zone5/Maneuver Profile",
        fileName = "Maneuver_"
    )]
    public class ManeuverProfile : ScriptableObject
    {
        [Header("Identity")]
        public string maneuverId;
        public string displayName;
        public string[] aliases;
        public AllowedDirs allowedDirs = AllowedDirs.Forward | AllowedDirs.Left | AllowedDirs.Right;

        [Header("Classification")]
        public ManeuverKind kind = ManeuverKind.Move;
        public ManeuverMainDir mainDir = ManeuverMainDir.Undetermined;
        public ManeuverUsage usage = ManeuverUsage.Unlimited;
        public bool usedInAfterBurner = false;

        [Header("Stats")]
        public float gForce;
        [FormerlySerializedAs("mach")]
        public MachTier machTier = MachTier.M09;
        [FormerlySerializedAs("evasionPenalty")]
        public EvasionPenaltyTier evasionPenalty = EvasionPenaltyTier.Zero;

        [Header("Movement")]
        public PathMode pathMode = PathMode.Straight;
        public float distanceFU = 1f;
        public float turnAngleDeg;
        public float curveBias = 1f;
        public bool useLegacyArc = false;
        [FormerlySerializedAs("leadInFrac")]
        public float straightLeadInFrac;
        [FormerlySerializedAs("handleFrac")]
        [InspectorName("Beizer (y)")]
        public float bezierForwardHandleFrac = 0.35f;
        [FormerlySerializedAs("lateralFrac")]
        [InspectorName("Beizer (X)")]
        public float bezierLateralFrac = 0.65f;
        public List<Vector2> pointsNorm = new();
        public bool enforceStraightStartEnd = true;
        public bool endHeadingSameAsStart = true;
        public bool useEndHeadingOverride = false;
        public float endHeadingOverrideDeg = 0f;

        [Header("Preview")]
        public Color previewColor = new Color(1f, 0.5f, 0f, 0.6f);
        public int previewSamples = 24;

        private static readonly HashSet<string> Warned = new();

        public int Fuel => GetFuelForMach(machTier);

        public IEnumerable<string> GetAllKeys()
        {
            yield return maneuverId;
            if (aliases == null) yield break;
            foreach (var a in aliases)
            {
                if (!string.IsNullOrWhiteSpace(a))
                    yield return a.Trim().ToUpperInvariant();
            }
        }

        public void BuildWorldPoints(
            Vector3 startExhaustWorld,
            Vector3 forwardWorld,
            float fuWorld,
            TurnDir dir,
            List<Vector3> outPointsWorld)
        {
            if (outPointsWorld == null) return;
            outPointsWorld.Clear();

            Vector3 forward = forwardWorld;
            forward.z = 0f;
            if (forward.sqrMagnitude < 0.000001f)
                forward = Vector3.up;
            forward.Normalize();

            Vector3 right = new Vector3(-forward.y, forward.x, 0f).normalized;

            float sign = DirSign(dir);
            float distanceWorld = Mathf.Max(0f, distanceFU) * Mathf.Max(0.01f, fuWorld);

            if (pathMode == PathMode.Straight)
            {
                outPointsWorld.Add(startExhaustWorld);
                outPointsWorld.Add(startExhaustWorld + forward * distanceWorld);
                return;
            }

            if (pathMode == PathMode.BezierQuad)
            {
                if (useLegacyArc)
                {
                    float arcTheta = turnAngleDeg * sign;
                    float arcThetaRad = arcTheta * Mathf.Deg2Rad;

                    if (Mathf.Abs(arcThetaRad) < 0.0001f)
                    {
                        outPointsWorld.Add(startExhaustWorld);
                        outPointsWorld.Add(startExhaustWorld + forward * distanceWorld);
                        return;
                    }

                    float radius = distanceWorld / Mathf.Abs(arcThetaRad);
                    Vector3 right0 = Rotate2D(forward, -90f).normalized;
                    float thetaSign = Mathf.Sign(arcThetaRad);
                    Vector3 center = startExhaustWorld - right0 * (radius * thetaSign);

                    int arcSamples = Mathf.Max(2, previewSamples);
                    float arcBias = Mathf.Clamp01(curveBias);
                    for (int i = 0; i < arcSamples; i++)
                    {
                        float t = i / (float)(arcSamples - 1);
                        float tBiased = ApplyBias(t, arcBias);
                        float angDeg = arcTheta * tBiased;
                        Vector3 pt = center + Rotate2D(startExhaustWorld - center, angDeg);
                        outPointsWorld.Add(pt);
                    }
                    return;
                }

                float leadFrac = Mathf.Clamp(straightLeadInFrac, 0f, 0.9f);
                float leadDist = distanceWorld * leadFrac;
                Vector3 leadStart = startExhaustWorld + forward * leadDist;
                float remainingDist = distanceWorld - leadDist;

                float bezierTheta = turnAngleDeg * sign;
                float bezierThetaRad = bezierTheta * Mathf.Deg2Rad;
                Vector3 arcEnd = leadStart + forward * remainingDist;
                if (Mathf.Abs(bezierThetaRad) >= 0.0001f)
                {
                    float radius = remainingDist / Mathf.Abs(bezierThetaRad);
                    Vector3 right0 = Rotate2D(forward, -90f).normalized;
                    float thetaSign = Mathf.Sign(bezierThetaRad);
                    Vector3 center = leadStart - right0 * (radius * thetaSign);
                    arcEnd = center + Rotate2D(leadStart - center, bezierTheta);
                }

                float bezierBias = Mathf.Clamp01(curveBias);
                Vector3 control = leadStart
                    + forward * (remainingDist * bezierForwardHandleFrac)
                    + right * (remainingDist * bezierLateralFrac * sign * bezierBias);

                outPointsWorld.Add(startExhaustWorld);
                if (leadDist > 0.0001f)
                    outPointsWorld.Add(leadStart);

                int curveSamples = Mathf.Max(2, previewSamples);
                int startIndex = leadDist > 0.0001f ? 1 : 0;
                for (int i = startIndex; i < curveSamples; i++)
                {
                    float t = i / (float)(curveSamples - 1);
                    outPointsWorld.Add(BezierQuad(leadStart, control, arcEnd, t));
                }
                return;
            }

            if (pathMode == PathMode.PointList)
            {
                var pts = pointsNorm ?? new List<Vector2>();
                if (enforceStraightStartEnd)
                {
                    bool hasStart = pts.Count > 0 && pts[0] == Vector2.zero;
                    bool hasEnd = pts.Count > 0 && pts[pts.Count - 1] == new Vector2(1f, 0f);
                    if (!hasStart || !hasEnd)
                    {
                        string key = string.IsNullOrEmpty(maneuverId) ? name : maneuverId;
                        if (!Warned.Contains(key))
                        {
                            Debug.LogWarning($"[ManeuverProfile] {key} missing (0,0) or (1,0). Auto-fixing for runtime.");
                            Warned.Add(key);
                        }
                    }
                }

                List<Vector2> usePts = new();
                if (pts.Count == 0)
                {
                    usePts.Add(Vector2.zero);
                    usePts.Add(new Vector2(1f, 0f));
                }
                else
                {
                    if (enforceStraightStartEnd && pts[0] != Vector2.zero)
                        usePts.Add(Vector2.zero);
                    usePts.AddRange(pts);
                    if (enforceStraightStartEnd && usePts[usePts.Count - 1] != new Vector2(1f, 0f))
                        usePts.Add(new Vector2(1f, 0f));
                }

                for (int i = 0; i < usePts.Count; i++)
                {
                    var p = usePts[i];
                    float xWorld = p.x * distanceWorld;
                    float yWorld = p.y * distanceWorld * sign;
                    outPointsWorld.Add(startExhaustWorld + forward * xWorld + right * yWorld);
                }
            }
        }

        public Vector3 ResolveEndHeading(Vector3 forwardWorld, TurnDir dir, List<Vector3> pathWorld)
        {
            Vector3 forward = forwardWorld;
            forward.z = 0f;
            if (forward.sqrMagnitude < 0.000001f)
                forward = Vector3.up;
            forward.Normalize();

            if (useEndHeadingOverride)
            {
                Vector3 overrideDir = Rotate2D(forward, endHeadingOverrideDeg);
                overrideDir.z = 0f;
                if (overrideDir.sqrMagnitude > 0.000001f)
                    return overrideDir.normalized;
                return forward;
            }

            if (endHeadingSameAsStart)
                return forward;

            if (pathMode == PathMode.BezierQuad)
            {
                Vector3 endForward = Rotate2D(forward, DirSign(dir) * turnAngleDeg);
                endForward.z = 0f;
                if (endForward.sqrMagnitude > 0.000001f)
                    return endForward.normalized;
            }

            if (pathWorld != null && pathWorld.Count >= 2)
            {
                Vector3 endDir = pathWorld[pathWorld.Count - 1] - pathWorld[pathWorld.Count - 2];
                endDir.z = 0f;
                if (endDir.sqrMagnitude > 0.000001f)
                    return endDir.normalized;
            }

            return forward;
        }

        private static float DirSign(TurnDir dir)
        {
            return dir == TurnDir.D ? -1f : 1f;
        }

        private static Vector3 Rotate2D(Vector3 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector3(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos,
                v.z
            );
        }

        private static Vector3 BezierQuad(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return (u * u) * a + (2f * u * t) * b + (t * t) * c;
        }

        private static float ApplyBias(float t, float bias)
        {
            float clamped = Mathf.Clamp01(bias);
            float exponent = Mathf.Lerp(2f, 0.5f, clamped);
            return Mathf.Pow(Mathf.Clamp01(t), exponent);
        }

        private static int GetFuelForMach(MachTier tier)
        {
            return tier switch
            {
                MachTier.M06 => 1,
                MachTier.M09 => 2,
                MachTier.M12 => 3,
                MachTier.M18 => 4,
                _ => 1
            };
        }
    }
}
