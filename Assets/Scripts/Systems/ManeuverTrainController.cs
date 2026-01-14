using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zone5
{
    public class ManeuverTrainController : MonoBehaviour
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

        [Header("Debug")]
        public bool logAlsoCards = true;

        [Header("Collision (MVP)")]
        [Range(0.10f, 1.00f)] public float aircraftHitRadiusFU = 0.10f;

        // Endpoint “persistente” por unidade (pra conectar no próximo turno)
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

            string blueName = blueUnit != null ? blueUnit.unitId : "BLUE(sem caça)";
            string redName  = redUnit  != null ? redUnit.unitId  : "RED(sem caça)";

            string blueRaw = blueInput ? blueInput.text : "";
            string redRaw  = redInput  ? redInput.text  : "";

            blueRaw = MvpRules.SanitizeManeuver(blueRaw);
            redRaw = MvpRules.SanitizeManeuver(redRaw);

            TurnDir blueDir = ParseDirFromRaw(blueRaw);
            TurnDir redDir  = ParseDirFromRaw(redRaw);

            Debug.Log($"manobras enviadas pelo {blueName} e {redName}");
            if (logAlsoCards)
                Debug.Log($"Blue='{blueRaw}'  Red='{redRaw}'");

            // ✅ AQUI: busca no catálogo (vazio/ruim -> fallback automático dentro do Resolve)
            ManeuverDef blueM = ManeuverCatalog.Resolve(blueRaw);
            ManeuverDef redM  = ManeuverCatalog.Resolve(redRaw);

            // Move e desenha
            if (blueUnit != null)
            {
                string validRaw = string.IsNullOrWhiteSpace(blueRaw) ? blueM.id : blueRaw;
                int g = 1; if (validRaw.Length > 0 && char.IsDigit(validRaw[0])) int.TryParse(validRaw[0].ToString(), out g);
                blueUnit.maneuverHistory.Add(new AircraftUnit.FlightLog { TurnIndex = 99, ManeuverCode = validRaw, RawInput = validRaw, GForce = g, Speed = 1f });
                ExecuteManeuver(blueUnit, blueM, blueDir, GameEnum.GameColors.TeamBlue);
            }
            if (redUnit != null)
            {
                string validRaw = string.IsNullOrWhiteSpace(redRaw) ? redM.id : redRaw;
                int g = 1; if (validRaw.Length > 0 && char.IsDigit(validRaw[0])) int.TryParse(validRaw[0].ToString(), out g);
                redUnit.maneuverHistory.Add(new AircraftUnit.FlightLog { TurnIndex = 99, ManeuverCode = validRaw, RawInput = validRaw, GForce = g, Speed = 1f });
                ExecuteManeuver(redUnit, redM, redDir, GameEnum.GameColors.TeamRed);
            }

            if (MvpRules.IsMvp)
                CheckPathCollisions(killedPairs);
            else
                CheckBoundsCollisions(killedPairs);

            // PÓS-MVP (2 cartas):
            // Em vez de Resolve() (que pega 1 só), use ManeuverCatalog.ParseCombo("1G18+1G18")
            // e execute em sequência encadeando endpoints (guardando o endpoint intermediário).
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
            // UnitSpawner cria unitId com sufixo "_T{teamId}"
            string key = $"_T{teamId}";
            return FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None)
                .FirstOrDefault(u => u != null && !string.IsNullOrEmpty(u.unitId) && u.unitId.Contains(key));
        }

        private void ExecuteManeuver(AircraftUnit unit, ManeuverDef m, TurnDir dir, Color teamColor)
        {
            if (trailManager == null || unit == null || m == null) return;

            if (!lastEndByUnit.TryGetValue(unit, out Vector3 start))
                start = unit.ExhaustAnchor != null ? unit.ExhaustAnchor.position : unit.transform.position;

            float fuWorld = MovementCore.GetFUWorld(unit);
            Vector3 forward = MovementCore.GetForward(unit);

            Color strongColor = GetStrongTeamColor(teamColor);

            // === STRAIGHT ===
            if (m.pathMode == PathMode.Straight)
            {
                float distFU = Mathf.Max(0f, m.distanceFU);
                Vector3 end = start + forward * (distFU * fuWorld);

                trailManager.CreateSegment(unit, start, end, strongColor);
                AddTurnSegment(unit, start, end);

                MovementCore.AlignAndTeleportToEnd(unit, start, end, forward);
                lastEndByUnit[unit] = end;
                return;
            }

            // === ARC (3G/7G) ===
            if (m.pathMode == PathMode.BezierQuad)
            {
                float sign = (dir == TurnDir.D) ? -1f : 1f; // D = clockwise
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
                    lastEndByUnit[unit] = endStraight;
                    return;
                }

                // Arc center and endpoint based on total arc length.
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

                lastEndByUnit[unit] = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : p2;
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

        private enum TurnDir { F, D, E }

        // HELPERS
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





