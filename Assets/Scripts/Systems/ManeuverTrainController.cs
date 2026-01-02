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
        public Button sendButton;

        [Header("Refs")]
        public TrailManager trailManager;

        [Header("Debug")]
        public bool logAlsoCards = true;

        // Endpoint “persistente” por unidade (pra conectar no próximo turno)
        private readonly Dictionary<AircraftUnit, Vector3> lastEndByUnit = new();

        private void Awake()
        {
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSend);

            if (blueInput != null) blueInput.onSubmit.AddListener(_ => OnSend());
            if (redInput != null)  redInput.onSubmit.AddListener(_ => OnSend());
        }

        private void Start()
        {
            if (trailManager == null)
                trailManager = FindFirstObjectByType<TrailManager>();

            CacheLastEnds();
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
            var blueUnit = FindTeamUnit(0);
            var redUnit  = FindTeamUnit(1);

            string blueName = blueUnit != null ? blueUnit.unitId : "BLUE(sem caça)";
            string redName  = redUnit  != null ? redUnit.unitId  : "RED(sem caça)";

            string blueRaw = blueInput ? blueInput.text : "";
            string redRaw  = redInput  ? redInput.text  : "";

            TurnDir blueDir = ParseDirFromRaw(blueRaw);
            TurnDir redDir  = ParseDirFromRaw(redRaw);

            Debug.Log($"manobras enviadas pelo {blueName} e {redName}");
            if (logAlsoCards)
                Debug.Log($"Blue='{blueRaw}'  Red='{redRaw}'");

            // ✅ AQUI: busca no catálogo (vazio/ruim -> fallback automático dentro do Resolve)
            ManeuverDef blueM = ManeuverCatalog.Resolve(blueRaw);
            ManeuverDef redM  = ManeuverCatalog.Resolve(redRaw);

            // Move e desenha (por enquanto só reta via distanceFU)
            if (blueUnit != null) ExecuteManeuver(blueUnit, blueM, blueDir, GameEnum.GameColors.TeamBlue);
            if (redUnit  != null) ExecuteManeuver(redUnit,  redM,  redDir,  GameEnum.GameColors.TeamRed);

            // PÓS-MVP (2 cartas):
            // Em vez de Resolve() (que pega 1 só), use ManeuverCatalog.ParseCombo("1G18+1G18")
            // e execute em sequência encadeando endpoints (guardando o endpoint intermediário).
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

            float fuWorld = GetFUWorld(unit);
            Vector3 forward = GetForward(unit);

            Color strongColor = GetStrongTeamColor(teamColor);

            // === STRAIGHT ===
            if (m.pathMode == PathMode.Straight)
            {
                float distFU = Mathf.Max(0f, m.distanceFU);
                Vector3 end = start + forward * (distFU * fuWorld);

                trailManager.CreateSegment(unit, start, end, strongColor);

                AlignAndTeleportToEnd(unit, start, end, forward);
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
                    AlignAndTeleportToEnd(unit, p0, endStraight, forward0);
                    lastEndByUnit[unit] = endStraight;
                    return;
                }

                // Arc center and endpoint based on total arc length.
                float radius = totalDist / Mathf.Abs(thetaRad);
                Vector3 right0 = Rotate2D(forward0, -90f).normalized;
                float thetaSign = Mathf.Sign(thetaRad);
                Vector3 center = p0 - right0 * (radius * thetaSign);
                Vector3 p2 = center + Rotate2D(p0 - center, theta);

                int steps = Mathf.Max(16, Mathf.CeilToInt(Mathf.Abs(theta) / 5f));
                Vector3 prev = p0;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float angDeg = theta * t;
                    Vector3 pt = center + Rotate2D(p0 - center, angDeg);
                    trailManager.CreateSegment(unit, prev, pt, strongColor);
                    prev = pt;
                }

                // Tangent direction at the end of the arc.
                Vector3 endDir = Rotate2D(forward0, theta).normalized;

                // Width of the exhaust bar (ExhaustL -> ExhaustR).
                float width = 0.5f * fuWorld; // fallback
                if (unit.ExhaustL != null && unit.ExhaustR != null)
                    width = Vector3.Distance(unit.ExhaustL.position, unit.ExhaustR.position);

                // Right direction based on the two exhausts.
                Vector3 airRight = (unit.ExhaustR.position - unit.ExhaustL.position);
                airRight.z = 0f;

                if (airRight.sqrMagnitude < 0.000001f)
                    airRight = Rotate2D(endDir, -90f); // fallback

                airRight.Normalize();

                // End right based on the final heading.
                Vector3 endRight = Rotate2D(endDir, -90f).normalized; // -90 = right em Unity 2D
                // Keep endRight on the same side as airRight.
                if (Vector3.Dot(endRight, airRight) < 0f) endRight = -endRight;

                Vector3 targetL = p2 - endRight * (width * 0.5f);
                Vector3 targetR = p2 + endRight * (width * 0.5f);


                // aplica alinhamento por dois anchors (ExhaustL/ExhaustR)
                MagnetAlignByTwoAnchors(unit, targetL, targetR);

                // 6) Pos-ima: garantir que o NARIZ aponta pro tangente final (endDir)
                //    sem perder o encaixe do ExhaustAnchor no endpoint.
                Vector3 exhaustPinned = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : unit.transform.position;

                // forward real do aviao (nariz -> bunda)
                Vector3 fwdNow = GetForward(unit);
                fwdNow.z = 0f;
                endDir.z = 0f;

                if (fwdNow.sqrMagnitude > 0.000001f && endDir.sqrMagnitude > 0.000001f)
                {
                    Quaternion rotToEnd = Quaternion.FromToRotation(fwdNow.normalized, endDir.normalized);
                    unit.transform.rotation = rotToEnd * unit.transform.rotation;

                    // recoloca pra manter o exhaust preso no mesmo ponto
                    Vector3 exhaustAfter = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : unit.transform.position;
                    unit.transform.position += (exhaustPinned - exhaustAfter);
                }

                // Persist endpoint after magnet align.
                lastEndByUnit[unit] = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : p2;

                return;
        
            }


            // === POINT LIST (pós-MVP) ===
            // (deixa quieto por enquanto)
        }


        private static float GetFUWorld(AircraftUnit unit)
        {
            if (unit.NoseAnchor == null || unit.ExhaustAnchor == null) return 1f;
            return Mathf.Max(0.01f, Vector3.Distance(unit.NoseAnchor.position, unit.ExhaustAnchor.position));
        }

        private static Vector3 GetForward(AircraftUnit unit)
        {
            if (unit.NoseAnchor != null && unit.ExhaustAnchor != null)
            {
                Vector3 v = unit.NoseAnchor.position - unit.ExhaustAnchor.position;
                if (v.sqrMagnitude > 0.000001f) return v.normalized;
            }
            return unit.transform.up.normalized;
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

        private static Vector3 BezierQuad(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float u = 1f - t;
            return (u * u) * p0 + (2f * u * t) * p1 + (t * t) * p2;
        }

        private void AlignAndTeleportToEnd(AircraftUnit unit, Vector3 start, Vector3 end, Vector3 currentForward, Vector3? desiredForward = null)
        {
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
        
        // Alinha o avião usando os dois pontos de exaustão (se existirem)
        private void MagnetAlignByTwoAnchors(AircraftUnit unit, Vector3 targetL, Vector3 targetR)
        {
            if (unit.ExhaustL == null || unit.ExhaustR == null) return;

            Vector3 aL = unit.ExhaustL.position;
            Vector3 aR = unit.ExhaustR.position;

            Vector3 vA = (aR - aL);
            Vector3 vB = (targetR - targetL);

            vA.z = 0f; vB.z = 0f;
            if (vA.sqrMagnitude < 0.000001f || vB.sqrMagnitude < 0.000001f) return;

            // ✅ anti-inversão: se o alvo veio invertido, troca targetL/targetR
            if (Vector3.Dot(vA, vB) < 0f)
            {
                (targetL, targetR) = (targetR, targetL);
                vB = (targetR - targetL);
                vB.z = 0f;
            }

            Quaternion rotDelta = Quaternion.FromToRotation(vA, vB);
            unit.transform.rotation = rotDelta * unit.transform.rotation;

            // cola o L certinho
            Vector3 newAL = unit.ExhaustL.position;
            Vector3 delta = targetL - newAL;
            unit.transform.position += delta;
        }


    }
}




