using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zone5
{
    [Flags]
    public enum Stance
    {
        None = 0,
        [InspectorName("Flight Mode")]
        FlightMode = 1 << 0,
        [InspectorName("After Burner")]
        AfterBurner = 1 << 1,
        [InspectorName("Low Acrobatics")]
        LowAcrobatics = 1 << 2,
        [InspectorName("High Acrobatics")]
        HighAcrobatics = 1 << 3
    }

    public enum AllowedDirs
    {
        [InspectorName("Break Right")]
        BreakRight,
        Right,
        Forward,
        Left,
        [InspectorName("Break Left")]
        BreakLeft
    }

    public enum TurnDir { F, D, E }

    public enum ManeuverMainDir { Undetermined, F, D, E }

    public enum ManeuverUsage { Unlimited, OncePerMatch }

    public enum VfxMode
    {
        ByProgress,
        ByPathXY
    }

    public enum Mach
    {
        [InspectorName("0.6")]
        M06 = 6,
        [InspectorName("0.9")]
        M09 = 9,
        [InspectorName("1.2")]
        M12 = 12,
        [InspectorName("1.8")]
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
        [Serializable]
        public struct VfxKeyProgress
        {
            public float p;
            [FormerlySerializedAs("pitchDeg")]
            public float rollXDeg;
            [FormerlySerializedAs("rollDeg")]
            public float rollYDeg;
            public Vector2 scale;
            public bool affectTrailDeform;
        }

        [Serializable]
        public struct VfxKeyXY
        {
            public float x;
            public float y;
            [FormerlySerializedAs("pitchDeg")]
            public float rollXDeg;
            [FormerlySerializedAs("rollDeg")]
            public float rollYDeg;
            public Vector2 scale;
            public bool affectTrailDeform;
        }

        public struct VfxPose
        {
            public float rollXDeg;
            public float rollYDeg;
            public Vector2 scale;
            public bool affectTrailDeform;
        }

        [Header("Identity")]
        public string maneuverId;
        public string displayName;
        public string[] aliases;
        public AllowedDirs allowedDirs = AllowedDirs.Forward;
        public Sprite defaultSprite;
        public Sprite afterburnerSprite;

        [Header("Classification")]
        public Stance stance = Stance.FlightMode;
        public ManeuverKind kind = ManeuverKind.Move;
        public ManeuverMainDir mainDir = ManeuverMainDir.Undetermined;
        public ManeuverUsage usage = ManeuverUsage.Unlimited;
        public bool usedInAfterBurner = false;

        [Header("Stats")]
        public float gForce;
        [FormerlySerializedAs("mach")]
        [FormerlySerializedAs("machTier")]
        public Mach mach = Mach.M12;
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

        [Header("VFX")]
        public bool useVfx = false;
        public VfxMode vfxMode = VfxMode.ByProgress;
        public List<VfxKeyProgress> vfxProgress = new();
        public List<VfxKeyXY> vfxXY = new();
        public bool backfaceEnabled = true;
        public float backfaceThresholdDeg = 90f;
        public float backfaceLerp = 0.2f;
        public Color backfaceColor = Color.black;
        public bool useSmooth = true;
        public bool trailDeformEnabled = false;
        public float trailDeformMinScale = 0.35f;
        public float trailDeformMaxRollDeg = 90f;
        public float trailDeformMaxScale = 3f;

        private static readonly HashSet<string> Warned = new();

        public int Fuel => GetFuelForMach(mach);

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
                bool hasNonMonotonicX = false;
                for (int i = 1; i < pts.Count; i++)
                {
                    if (pts[i].x < pts[i - 1].x)
                    {
                        hasNonMonotonicX = true;
                        break;
                    }
                }
                bool enforceStraight = enforceStraightStartEnd && !hasNonMonotonicX;
                if (enforceStraight)
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
                    if (enforceStraight && pts[0] != Vector2.zero)
                        usePts.Add(Vector2.zero);
                    usePts.AddRange(pts);
                    if (enforceStraight && usePts[usePts.Count - 1] != new Vector2(1f, 0f))
                        usePts.Add(new Vector2(1f, 0f));
                }

                var rawWorld = new List<Vector3>(usePts.Count);
                for (int i = 0; i < usePts.Count; i++)
                {
                    var p = usePts[i];
                    float xWorld = p.x * distanceWorld;
                    float yWorld = p.y * distanceWorld * sign;
                    rawWorld.Add(startExhaustWorld + forward * xWorld + right * yWorld);
                }

                if (rawWorld.Count < 3)
                {
                    outPointsWorld.AddRange(rawWorld);
                    return;
                }

                int samplesPerSegment = Mathf.Max(1, Mathf.RoundToInt(previewSamples / Mathf.Max(1, rawWorld.Count - 1)));
                var smooth = SmoothCatmullRom(rawWorld, samplesPerSegment);
                outPointsWorld.AddRange(smooth);
            }
        }

        private void OnValidate()
        {
            if (pathMode == PathMode.PointList)
            {
                if (vfxMode == VfxMode.ByProgress && vfxXY.Count > 0 && vfxProgress.Count == 0)
                    vfxMode = VfxMode.ByPathXY;
            }
            else
            {
                if (vfxMode == VfxMode.ByPathXY && vfxProgress.Count > 0 && vfxXY.Count == 0)
                    vfxMode = VfxMode.ByProgress;
            }
            if (vfxProgress.Count == 0 && vfxXY.Count == 0)
                vfxMode = pathMode == PathMode.PointList ? VfxMode.ByPathXY : VfxMode.ByProgress;
        }

        public VfxPose EvaluateVfxByProgress(float p01)
        {
            if (vfxProgress == null || vfxProgress.Count == 0)
                return new VfxPose { rollXDeg = 0f, rollYDeg = 0f, scale = Vector2.one, affectTrailDeform = false };

            List<VfxKeyProgress> keys = vfxProgress;
            bool sorted = true;
            for (int i = 1; i < keys.Count; i++)
            {
                if (keys[i].p < keys[i - 1].p)
                {
                    sorted = false;
                    break;
                }
            }

            if (!sorted)
            {
                keys = new List<VfxKeyProgress>(vfxProgress);
                keys.Sort((a, b) => a.p.CompareTo(b.p));
            }

            float p = Mathf.Clamp01(p01);
            if (p <= keys[0].p)
                return MakePose(keys[0].rollXDeg, keys[0].rollYDeg, keys[0].scale, keys[0].affectTrailDeform);
            if (p >= keys[keys.Count - 1].p)
            {
                var last = keys[keys.Count - 1];
                return MakePose(last.rollXDeg, last.rollYDeg, last.scale, last.affectTrailDeform);
            }

            VfxKeyProgress k0 = keys[0];
            VfxKeyProgress k1 = keys[1];
            for (int i = 1; i < keys.Count; i++)
            {
                if (p <= keys[i].p)
                {
                    k0 = keys[i - 1];
                    k1 = keys[i];
                    break;
                }
            }

            float t = Mathf.InverseLerp(k0.p, k1.p, p);
            if (useSmooth) t = SmoothStep01(t);

            return new VfxPose
            {
                rollXDeg = Mathf.Lerp(k0.rollXDeg, k1.rollXDeg, t),
                rollYDeg = Mathf.Lerp(k0.rollYDeg, k1.rollYDeg, t),
                scale = Vector2.Lerp(NormalizeScale(k0.scale), NormalizeScale(k1.scale), t),
                affectTrailDeform = k0.affectTrailDeform
            };
        }

        public VfxPose EvaluateVfxByXY(int currentKeyIndex, float cursor, List<float> keyCursors, out int advancedKeyIndex)
        {
            advancedKeyIndex = currentKeyIndex;
            if (vfxXY == null || vfxXY.Count == 0 || keyCursors == null || keyCursors.Count != vfxXY.Count)
                return new VfxPose { rollXDeg = 0f, rollYDeg = 0f, scale = Vector2.one, affectTrailDeform = false };

            int lastIndex = vfxXY.Count - 1;
            if (currentKeyIndex < 0) currentKeyIndex = 0;
            if (currentKeyIndex > lastIndex) currentKeyIndex = lastIndex;

            while (advancedKeyIndex < lastIndex && cursor >= keyCursors[advancedKeyIndex + 1])
                advancedKeyIndex++;

            int nextIndex = Mathf.Min(advancedKeyIndex + 1, lastIndex);
            if (advancedKeyIndex == nextIndex)
            {
                var key = vfxXY[advancedKeyIndex];
                return MakePose(key.rollXDeg, key.rollYDeg, key.scale, key.affectTrailDeform);
            }

            float a = keyCursors[advancedKeyIndex];
            float b = keyCursors[nextIndex];
            float t = Mathf.InverseLerp(a, b, cursor);
            if (useSmooth) t = SmoothStep01(t);

            var k0 = vfxXY[advancedKeyIndex];
            var k1 = vfxXY[nextIndex];

            return new VfxPose
            {
                rollXDeg = Mathf.Lerp(k0.rollXDeg, k1.rollXDeg, t),
                rollYDeg = Mathf.Lerp(k0.rollYDeg, k1.rollYDeg, t),
                scale = Vector2.Lerp(NormalizeScale(k0.scale), NormalizeScale(k1.scale), t),
                affectTrailDeform = k0.affectTrailDeform
            };
        }
        public bool HasTrailDeformKeys()
        {
            if (vfxProgress != null)
            {
                for (int i = 0; i < vfxProgress.Count; i++)
                {
                    if (vfxProgress[i].affectTrailDeform)
                        return true;
                }
            }
            if (vfxXY != null)
            {
                for (int i = 0; i < vfxXY.Count; i++)
                {
                    if (vfxXY[i].affectTrailDeform)
                        return true;
                }
            }
            return false;
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

        private static List<Vector3> SmoothCatmullRom(List<Vector3> controlPoints, int samplesPerSegment)
        {
            if (controlPoints == null || controlPoints.Count < 2)
                return controlPoints ?? new List<Vector3>();

            if (samplesPerSegment < 1) samplesPerSegment = 1;

            int n = controlPoints.Count;
            var result = new List<Vector3>(n * (samplesPerSegment + 1));
            result.Add(controlPoints[0]);

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];
                Vector3 p3 = controlPoints[Mathf.Min(i + 2, n - 1)];

                for (int s = 1; s <= samplesPerSegment; s++)
                {
                    float t = s / (float)samplesPerSegment;
                    result.Add(CentripetalCatmullRom(p0, p1, p2, p3, t));
                }
            }

            return result;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            const float alpha = 0.5f;
            float t0 = 0f;
            float t1 = t0 + Mathf.Pow(Vector3.Distance(p0, p1), alpha);
            float t2 = t1 + Mathf.Pow(Vector3.Distance(p1, p2), alpha);
            float t3 = t2 + Mathf.Pow(Vector3.Distance(p2, p3), alpha);

            float tScaled = Mathf.Lerp(t1, t2, Mathf.Clamp01(t));

            Vector3 A1 = Vector3.Lerp(p0, p1, (t1 - t0) <= 0.0001f ? 0f : (tScaled - t0) / (t1 - t0));
            Vector3 A2 = Vector3.Lerp(p1, p2, (t2 - t1) <= 0.0001f ? 0f : (tScaled - t1) / (t2 - t1));
            Vector3 A3 = Vector3.Lerp(p2, p3, (t3 - t2) <= 0.0001f ? 0f : (tScaled - t2) / (t3 - t2));

            Vector3 B1 = Vector3.Lerp(A1, A2, (t2 - t0) <= 0.0001f ? 0f : (tScaled - t0) / (t2 - t0));
            Vector3 B2 = Vector3.Lerp(A2, A3, (t3 - t1) <= 0.0001f ? 0f : (tScaled - t1) / (t3 - t1));

            return Vector3.Lerp(B1, B2, (t2 - t1) <= 0.0001f ? 0f : (tScaled - t1) / (t2 - t1));
        }

        private static float ApplyBias(float t, float bias)
        {
            float clamped = Mathf.Clamp01(bias);
            float exponent = Mathf.Lerp(2f, 0.5f, clamped);
            return Mathf.Pow(Mathf.Clamp01(t), exponent);
        }

        private static float SmoothStep01(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return clamped * clamped * (3f - 2f * clamped);
        }

        private static Vector2 NormalizeScale(Vector2 scale)
        {
            return scale == Vector2.zero ? Vector2.one : scale;
        }

        private static VfxPose MakePose(float rollXDeg, float rollYDeg, Vector2 scale, bool affectTrailDeform)
        {
            return new VfxPose
            {
                rollXDeg = rollXDeg,
                rollYDeg = rollYDeg,
                scale = NormalizeScale(scale),
                affectTrailDeform = affectTrailDeform
            };
        }

        private static int GetFuelForMach(Mach tier)
        {
            return tier switch
            {
                Mach.M06 => 1,
                Mach.M09 => 2,
                Mach.M12 => 3,
                Mach.M18 => 4,
                _ => 1
            };
        }
    }
}
