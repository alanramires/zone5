using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace Zone5
{
    [MovedFrom(true, "Zone5", "Assembly-CSharp", "ManeuverTrainController")]
    public class DebugManeuverController : MonoBehaviour
    {
        [Header("UI")]
        public TMP_InputField blueInput;
        public TMP_InputField redInput;
        public TMP_InputField blueMissileInput;
        public TMP_InputField redMissileInput;
        public Button sendButton;
        public Button fireButton;

        [Header("Refs")]
        public TrailManager trailManager;
        public MissileManager missileManager;
        public ManeuverManager maneuverManager;
        public MatchControllerMvp matchController;

        [Header("Debug")]
        public bool logAlsoCards = true;

        [Header("Collision (MVP)")]
        [Range(0.10f, 1.00f)] public float aircraftHitRadiusFU = 0.10f;

        private readonly Dictionary<AircraftUnit, Vector3> lastEndByUnit = new();
        private readonly Dictionary<AircraftUnit, List<Segment>> turnSegmentsByUnit = new();

        private void Awake()
        {
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSend);

            if (fireButton != null)
                fireButton.onClick.AddListener(OnFire);

            if (blueInput != null) blueInput.onSubmit.AddListener(_ => OnSend());
            if (redInput != null)  redInput.onSubmit.AddListener(_ => OnSend());
        }

        private void Start()
        {
            if (trailManager == null)
                trailManager = FindFirstObjectByType<TrailManager>();
            if (maneuverManager == null)
                maneuverManager = FindFirstObjectByType<ManeuverManager>();
            if (matchController == null)
                matchController = FindFirstObjectByType<MatchControllerMvp>();

            CacheLastEnds();

            if (blueMissileInput != null && string.IsNullOrWhiteSpace(blueMissileInput.text))
                blueMissileInput.text = "M10F";

            if (redMissileInput != null && string.IsNullOrWhiteSpace(redMissileInput.text))
                redMissileInput.text = "M10F";
        }

        private void CacheLastEnds()
        {
            lastEndByUnit.Clear();
            foreach (var u in FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None))
            {
                if (u == null) continue;
                lastEndByUnit[u] = (u.ExhaustAnchor != null) ? u.ExhaustAnchor.position : u.transform.position;
            }
        }

        private void OnSend()
        {
            if (missileManager == null)
                missileManager = FindFirstObjectByType<MissileManager>();
            missileManager?.ClearMissiles();

            ClearTurnSegments();
            var killedPairs = new HashSet<string>();

            var blueUnit = FindTeamUnit(0);
            var redUnit  = FindTeamUnit(1);

            string blueName = blueUnit != null ? blueUnit.unitId : "BLUE(sem caca)";
            string redName  = redUnit  != null ? redUnit.unitId  : "RED(sem caca)";

            string blueRaw = blueInput ? blueInput.text : "";
            string redRaw  = redInput  ? redInput.text  : "";

            blueRaw = MvpRules.SanitizeManeuver(blueRaw);
            redRaw = MvpRules.SanitizeManeuver(redRaw);

            TurnDir blueDir = ParseDirFromRaw(blueRaw);
            TurnDir redDir  = ParseDirFromRaw(redRaw);

            Debug.Log($"manobras enviadas pelo {blueName} e {redName}");
            if (logAlsoCards)
                Debug.Log($"Blue='{blueRaw}'  Red='{redRaw}'");

            ManeuverProfile blueProfile = ResolveManeuverProfile(blueRaw);
            ManeuverProfile redProfile  = ResolveManeuverProfile(redRaw);

            ManeuverDef blueFallback = null;
            ManeuverDef redFallback = null;
            if (blueProfile == null || redProfile == null)
            {
                WarnProfileFallbackOnce();
                if (blueProfile == null) blueFallback = ManeuverCatalog.Resolve(blueRaw);
                if (redProfile == null) redFallback = ManeuverCatalog.Resolve(redRaw);
            }

            if (blueUnit != null)
            {
                string fallbackId = blueFallback != null ? blueFallback.id : "";
                string validRaw = string.IsNullOrWhiteSpace(blueRaw)
                    ? (blueProfile != null ? blueProfile.maneuverId : fallbackId)
                    : blueRaw;
                int g = blueProfile != null ? Mathf.RoundToInt(blueProfile.gForce) : 1;
                if (g <= 0 && validRaw.Length > 0 && char.IsDigit(validRaw[0])) int.TryParse(validRaw[0].ToString(), out g);
                blueUnit.maneuverHistory.Add(new AircraftUnit.FlightLog { TurnIndex = 99, ManeuverCode = validRaw, RawInput = validRaw, GForce = g, Speed = 1f });
                if (blueProfile != null)
                    ExecuteManeuver(blueUnit, blueProfile, blueDir, GameEnum.GameColors.TeamBlue);
                else if (blueFallback != null)
                    ExecuteManeuverLegacy(blueUnit, blueFallback, blueDir, GameEnum.GameColors.TeamBlue);
            }

            if (redUnit != null)
            {
                string fallbackId = redFallback != null ? redFallback.id : "";
                string validRaw = string.IsNullOrWhiteSpace(redRaw)
                    ? (redProfile != null ? redProfile.maneuverId : fallbackId)
                    : redRaw;
                int g = redProfile != null ? Mathf.RoundToInt(redProfile.gForce) : 1;
                if (g <= 0 && validRaw.Length > 0 && char.IsDigit(validRaw[0])) int.TryParse(validRaw[0].ToString(), out g);
                redUnit.maneuverHistory.Add(new AircraftUnit.FlightLog { TurnIndex = 99, ManeuverCode = validRaw, RawInput = validRaw, GForce = g, Speed = 1f });
                if (redProfile != null)
                    ExecuteManeuver(redUnit, redProfile, redDir, GameEnum.GameColors.TeamRed);
                else if (redFallback != null)
                    ExecuteManeuverLegacy(redUnit, redFallback, redDir, GameEnum.GameColors.TeamRed);
            }

            // Aircraft should not collide with aircraft trails (no TRON behavior).
            CheckBoundsCollisions(killedPairs);
        }

        private void OnFire()
        {
            if (missileManager == null)
                missileManager = FindFirstObjectByType<MissileManager>();

            string blueCode = blueMissileInput ? blueMissileInput.text : "M10F";
            string redCode  = redMissileInput  ? redMissileInput.text  : "M10F";

            missileManager?.FireTestBoth(blueCode, redCode);
        }

        private AircraftUnit FindTeamUnit(int teamId)
        {
            string key = $"_T{teamId}";
            return FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None)
                .FirstOrDefault(u => u != null && !string.IsNullOrEmpty(u.unitId) && u.unitId.Contains(key));
        }

        private ManeuverProfile ResolveManeuverProfile(string raw)
        {
            if (maneuverManager != null)
                return maneuverManager.Resolve(raw);
            return ManeuverProfileCatalog.Resolve(raw);
        }

        private void ExecuteManeuver(AircraftUnit unit, ManeuverProfile profile, TurnDir dir, Color teamColor)
        {
            if (trailManager == null || unit == null || profile == null) return;

            Vector3 start = GetStartForUnit(unit);

            float fuWorld = MovementCore.GetFUWorld(unit);
            Vector3 forward = MovementCore.GetForward(unit);
            Color strongColor = GetStrongTeamColor(teamColor);

            var path = new List<Vector3>();
            profile.BuildWorldPoints(start, forward, fuWorld, dir, path);
            if (path.Count < 2) return;

            for (int i = 0; i < path.Count - 1; i++)
            {
                trailManager.CreateSegment(unit, path[i], path[i + 1], strongColor);
                AddTurnSegment(unit, path[i], path[i + 1]);
            }

            Vector3 end = path[path.Count - 1];
            Vector3 endHeading = profile.ResolveEndHeading(forward, dir, path);

            MovementCore.AlignAndTeleportToEnd(unit, start, end, forward, endHeading);
            SetLastEnd(unit, end);
        }

        private void ExecuteManeuverLegacy(AircraftUnit unit, ManeuverDef m, TurnDir dir, Color teamColor)
        {
            if (trailManager == null || unit == null || m == null) return;

            Vector3 start = GetStartForUnit(unit);

            float fuWorld = MovementCore.GetFUWorld(unit);
            Vector3 forward = MovementCore.GetForward(unit);
            Color strongColor = GetStrongTeamColor(teamColor);

            if (m.pathMode == PathMode.Straight)
            {
                float distFU = Mathf.Max(0f, m.distanceFU);
                Vector3 end = start + forward * (distFU * fuWorld);

                trailManager.CreateSegment(unit, start, end, strongColor);
                AddTurnSegment(unit, start, end);

                MovementCore.AlignAndTeleportToEnd(unit, start, end, forward);
                SetLastEnd(unit, end);
                return;
            }

            if (m.pathMode == PathMode.BezierQuad)
            {
                float sign = (dir == TurnDir.D) ? -1f : 1f;
                Vector3 p0 = start;
                Vector3 forward0 = forward.normalized;

                float totalDist = m.distanceFU * fuWorld;
                float theta = m.turnAngleDeg * sign;
                float thetaRad = theta * Mathf.Deg2Rad;

                if (Mathf.Abs(thetaRad) < 0.0001f)
                {
                    Vector3 endStraight = p0 + forward0 * totalDist;
                    trailManager.CreateSegment(unit, p0, endStraight, strongColor);
                    AddTurnSegment(unit, p0, endStraight);
                    MovementCore.AlignAndTeleportToEnd(unit, p0, endStraight, forward0);
                    SetLastEnd(unit, endStraight);
                    return;
                }

                float radius = totalDist / Mathf.Abs(thetaRad);
                Vector3 right0 = MovementCore.Rotate2D(forward0, -90f).normalized;
                float thetaSign = Mathf.Sign(thetaRad);
                Vector3 center = p0 - right0 * (radius * thetaSign);
                Vector3 p2 = center + MovementCore.Rotate2D(p0 - center, theta);

                int steps = Mathf.Max(16, Mathf.CeilToInt(Mathf.Abs(theta) / 5f));
                Vector3 prev = p0;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float angDeg = theta * t;
                    Vector3 pt = center + MovementCore.Rotate2D(p0 - center, angDeg);
                    trailManager.CreateSegment(unit, prev, pt, strongColor);
                    AddTurnSegment(unit, prev, pt);
                    prev = pt;
                }
                Vector3 endDir = MovementCore.Rotate2D(forward0, theta).normalized;

                float width = 0.5f * fuWorld;
                if (unit.ExhaustL != null && unit.ExhaustR != null)
                    width = Vector3.Distance(unit.ExhaustL.position, unit.ExhaustR.position);

                Vector3 airRight = (unit.ExhaustR.position - unit.ExhaustL.position);
                airRight.z = 0f;
                if (airRight.sqrMagnitude < 0.000001f)
                    airRight = MovementCore.Rotate2D(endDir, -90f);
                airRight.Normalize();

                Vector3 endRight = MovementCore.Rotate2D(endDir, -90f).normalized;
                if (Vector3.Dot(endRight, airRight) < 0f) endRight = -endRight;

                Vector3 targetL = p2 - endRight * (width * 0.5f);
                Vector3 targetR = p2 + endRight * (width * 0.5f);

                MovementCore.MagnetAlignByTwoAnchors(unit, targetL, targetR);

                Vector3 exhaustPinned = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : unit.transform.position;

                Vector3 fwdNow = MovementCore.GetForward(unit);
                fwdNow.z = 0f;
                endDir.z = 0f;

                if (fwdNow.sqrMagnitude > 0.000001f && endDir.sqrMagnitude > 0.000001f)
                {
                    Quaternion rotToEnd = Quaternion.FromToRotation(fwdNow.normalized, endDir.normalized);
                    unit.transform.rotation = rotToEnd * unit.transform.rotation;

                    Vector3 exhaustAfter = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : unit.transform.position;
                    unit.transform.position += (exhaustPinned - exhaustAfter);
                }

                Vector3 finalEnd = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : p2;
                SetLastEnd(unit, finalEnd);
            }
        }

        private static Color GetStrongTeamColor(Color baseColor)
        {
            return new Color(
                Mathf.Clamp01(baseColor.r * 1.25f),
                Mathf.Clamp01(baseColor.g * 1.25f),
                Mathf.Clamp01(baseColor.b * 1.25f),
                baseColor.a
            );
        }

        private static TurnDir ParseDirFromRaw(string raw)
        {
            string s = (raw ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return TurnDir.F;

            char last = s[s.Length - 1];
            return last switch
            {
                'D' => TurnDir.D,
                'E' => TurnDir.E,
                'F' => TurnDir.F,
                _ => TurnDir.F
            };
        }

        private static bool _warnedProfileFallback;
        private static void WarnProfileFallbackOnce()
        {
            if (_warnedProfileFallback) return;
            _warnedProfileFallback = true;
            Debug.LogWarning("[DebugManeuverController] ManeuverProfile not found. Falling back to ManeuverCatalog.");
        }

        private Vector3 GetStartForUnit(AircraftUnit unit)
        {
            if (unit == null) return Vector3.zero;
            if (matchController != null && matchController.TryGetLastEnd(unit, out var sharedStart))
                return sharedStart;
            if (lastEndByUnit.TryGetValue(unit, out var localStart))
                return localStart;
            return unit.ExhaustAnchor != null ? unit.ExhaustAnchor.position : unit.transform.position;
        }

        private void SetLastEnd(AircraftUnit unit, Vector3 end)
        {
            if (unit == null) return;
            lastEndByUnit[unit] = end;
            if (matchController != null)
                matchController.SetLastEnd(unit, end);
        }

        private void CheckAircraftCollision(AircraftUnit a, AircraftUnit b, HashSet<string> killedPairs)
        {
            if (a == null || b == null) return;
            if (a.currentHp <= 0 || b.currentHp <= 0) return;
            var srA = a.GetComponentInChildren<SpriteRenderer>();
            var srB = b.GetComponentInChildren<SpriteRenderer>();
            if (srA == null || srB == null) return;

            string nameA = string.IsNullOrEmpty(a.unitId) ? a.name : a.unitId;
            string nameB = string.IsNullOrEmpty(b.unitId) ? b.name : b.unitId;
            string key = string.CompareOrdinal(nameA, nameB) <= 0 ? $"{nameA}|{nameB}" : $"{nameB}|{nameA}";
            if (killedPairs != null && killedPairs.Contains(key)) return;

            Bounds boundsA = srA.bounds;
            Bounds boundsB = srB.bounds;
            if (boundsA.Intersects(boundsB))
            {
                Debug.Log($"[Collision] Aircraft collision: {nameA} vs {nameB}");
                Debug.Log($"[Collision] Aeronaves abatidas por colisao: {nameA} e {nameB}");
                if (killedPairs != null) killedPairs.Add(key);
                a.Die();
                b.Die();
            }
        }

        private void ClearTurnSegments()
        {
            turnSegmentsByUnit.Clear();
        }

        private void AddTurnSegment(AircraftUnit unit, Vector3 start, Vector3 end)
        {
            if (unit == null) return;
            if (!turnSegmentsByUnit.TryGetValue(unit, out var list))
            {
                list = new List<Segment>();
                turnSegmentsByUnit[unit] = list;
            }
            list.Add(new Segment(start, end));
        }

        private void CheckPathCollisions(HashSet<string> killedPairs)
        {
            var aircrafts = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < aircrafts.Length; i++)
            {
                var a = aircrafts[i];
                if (a == null || a.currentHp <= 0) continue;
                for (int j = i + 1; j < aircrafts.Length; j++)
                {
                    var b = aircrafts[j];
                    if (b == null || b.currentHp <= 0) continue;
                    if (HasPathCollision(a, b))
                    {
                        string nameA = string.IsNullOrEmpty(a.unitId) ? a.name : a.unitId;
                        string nameB = string.IsNullOrEmpty(b.unitId) ? b.name : b.unitId;
                        string key = string.CompareOrdinal(nameA, nameB) <= 0 ? $"{nameA}|{nameB}" : $"{nameB}|{nameA}";
                        if (killedPairs != null && killedPairs.Contains(key)) continue;
                        Debug.Log($"[Collision][MVP] Path collision: {nameA} vs {nameB}");
                        if (killedPairs != null) killedPairs.Add(key);
                        a.currentHp = 0;
                        b.currentHp = 0;
                        a.Die();
                        b.Die();
                    }
                }
            }
        }

        private void CheckBoundsCollisions(HashSet<string> killedPairs)
        {
            var aircrafts = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < aircrafts.Length; i++)
            {
                var a = aircrafts[i];
                if (a == null) continue;
                for (int j = i + 1; j < aircrafts.Length; j++)
                {
                    var b = aircrafts[j];
                    if (b == null) continue;
                    CheckAircraftCollision(a, b, killedPairs);
                }
            }
        }

        private bool HasPathCollision(AircraftUnit a, AircraftUnit b)
        {
            if (a == null || b == null) return false;
            if (!turnSegmentsByUnit.TryGetValue(a, out var segA) || segA.Count == 0) return false;
            if (!turnSegmentsByUnit.TryGetValue(b, out var segB) || segB.Count == 0) return false;

            float radiusA = aircraftHitRadiusFU * MovementCore.GetFUWorld(a);
            float radiusB = aircraftHitRadiusFU * MovementCore.GetFUWorld(b);
            float combined = radiusA + radiusB;

            for (int i = 0; i < segA.Count; i++)
            {
                for (int j = 0; j < segB.Count; j++)
                {
                    float d = CollisionSystem.MinDistanceSegmentToSegment2D(segA[i].a, segA[i].b, segB[j].a, segB[j].b);
                    if (d <= combined) return true;
                }
            }
            return false;
        }
    }
}
