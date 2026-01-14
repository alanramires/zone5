using UnityEngine;

namespace Zone5
{
    /// <summary>
    /// Centralized math and alignment logic for Flight Units (FU) and aircraft movement.
    /// Extracts common logic from MatchControllerMvp and ManeuverTrainController.
    /// </summary>
    public static class MovementCore
    {
        public static float GetFUWorld(AircraftUnit unit)
        {
            if (unit == null || unit.NoseAnchor == null || unit.ExhaustAnchor == null) return 1f;
            return Mathf.Max(0.01f, Vector3.Distance(unit.NoseAnchor.position, unit.ExhaustAnchor.position));
        }

        public static Vector3 GetForward(AircraftUnit unit)
        {
            if (unit != null && unit.NoseAnchor != null && unit.ExhaustAnchor != null)
            {
                Vector3 v = unit.NoseAnchor.position - unit.ExhaustAnchor.position;
                if (v.sqrMagnitude > 0.000001f) return v.normalized;
            }
            return unit != null ? unit.transform.up.normalized : Vector3.up;
        }

        public static Vector3 Rotate2D(Vector3 v, float degrees)
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

        public static void AlignAndTeleportToEnd(AircraftUnit unit, Vector3 start, Vector3 end, Vector3 currentForward, Vector3? desiredForward = null)
        {
            if (unit == null) return;
            
            Vector3 dir = desiredForward ?? (end - start).normalized;
            if (dir.sqrMagnitude > 0.000001f)
            {
                Quaternion rotDelta = Quaternion.FromToRotation(currentForward, dir);
                unit.transform.rotation = rotDelta * unit.transform.rotation;
            }

            Vector3 exhaustNow = unit.ExhaustAnchor != null ? unit.ExhaustAnchor.position : unit.transform.position;
            Vector3 deltaPos = end - exhaustNow;
            unit.transform.position += deltaPos;
        }

        public static void MagnetAlignByTwoAnchors(AircraftUnit unit, Vector3 targetL, Vector3 targetR)
        {
            if (unit == null || unit.ExhaustL == null || unit.ExhaustR == null) return;

            Vector3 aL = unit.ExhaustL.position;
            Vector3 aR = unit.ExhaustR.position;

            Vector3 vA = (aR - aL);
            Vector3 vB = (targetR - targetL);

            vA.z = 0f; vB.z = 0f;
            if (vA.sqrMagnitude < 0.000001f || vB.sqrMagnitude < 0.000001f) return;

            // Anti-inversion check
            if (Vector3.Dot(vA, vB) < 0f)
            {
                (targetL, targetR) = (targetR, targetL);
                vB = (targetR - targetL);
                vB.z = 0f;
            }

            Quaternion rotDelta = Quaternion.FromToRotation(vA, vB);
            unit.transform.rotation = rotDelta * unit.transform.rotation;

            Vector3 newAL = unit.ExhaustL.position;
            Vector3 delta = targetL - newAL;
            unit.transform.position += delta;
        }
    }
}
