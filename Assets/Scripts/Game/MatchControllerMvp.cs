using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zone5
{
    public class MatchControllerMvp : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private TrailManager trailManager;
        [SerializeField] private MissileManager missileManager;

        [Header("Debug")]
        [SerializeField] private bool logFlow = true;

        [Header("Collision (MVP)")]
        [Range(0.10f, 1.00f)] public float aircraftHitRadiusFU = 0.10f;

        private readonly Dictionary<AircraftUnit, Vector3> lastEndByUnit = new();
        private readonly Dictionary<AircraftUnit, List<Segment>> turnSegmentsByUnit = new();

        private void Awake()
        {
            if (turnManager == null)
                turnManager = FindFirstObjectByType<TurnManager>();
            if (trailManager == null)
                trailManager = FindFirstObjectByType<TrailManager>();
            if (missileManager == null)
                missileManager = FindFirstObjectByType<MissileManager>();
        }

        private void OnEnable()
        {
            if (turnManager != null)
                turnManager.OnStateChanged += HandleTurnStateChanged;
        }

        private void OnDisable()
        {
            if (turnManager != null)
                turnManager.OnStateChanged -= HandleTurnStateChanged;
        }

        private void Start()
        {
            CacheLastEnds();
        }

        public void SubmitManeuver(int playerId, string raw)
        {
            if (turnManager == null) return;
            var row = turnManager.GetOrCreatePlayerRow(playerId);
            if (row == null) return;

            row.maneuverRaw = MvpRules.SanitizeManeuver(raw);
            row.maneuverReady = !string.IsNullOrWhiteSpace(row.maneuverRaw);
            turnManager.NotifySheetChanged();
            turnManager.TryAdvance();
        }

        public void SubmitWeapon(int playerId, string raw)
        {
            if (turnManager == null) return;
            var row = turnManager.GetOrCreatePlayerRow(playerId);
            if (row == null) return;

            row.weaponCode = MvpRules.SanitizeWeapon(raw);
            row.weaponReady = true;
            turnManager.NotifySheetChanged();
            turnManager.TryAdvance();
        }

        public void SubmitTarget(int playerId, int targetId)
        {
            if (turnManager == null) return;
            var row = turnManager.GetOrCreatePlayerRow(playerId);
            if (row == null) return;

            row.targetId = targetId;
            row.missileReady = true;
            turnManager.NotifySheetChanged();
            turnManager.TryAdvance();
        }

        public void SubmitMissileProfile(int playerId, string profileCode)
        {
            if (turnManager == null) return;
            var row = turnManager.GetOrCreatePlayerRow(playerId);
            if (row == null) return;

            // Do NOT use SanitizeWeapon here, because it forces "X" for anything not "M".
            // We want "L1", "S2", etc.
            row.missilePath = (profileCode ?? "").Trim().ToUpperInvariant(); 
            
            row.missileReady = true; 
            turnManager.NotifySheetChanged();
            turnManager.TryAdvance();
        }

        private void HandleTurnStateChanged()
        {
            if (turnManager == null || turnManager.sheet == null) return;
            GameEnum.TurnState state = turnManager.sheet.phase;

            if (logFlow)
                Debug.Log($"[MatchControllerMvp] State={state}");

            switch (state)
            {
                case GameEnum.TurnState.SelectManeuver:
                    // New Turn: Clean up old missiles
                    if (missileManager != null) missileManager.ClearMissiles();
                    break;

                case GameEnum.TurnState.WaitManeuverConfirm:
                    // Step 2: Systema espera todos... proxima fase (DeclareWeapon)
                    // (Managed by TurnManager, just ensure UI/flow continues)
                    // turnManager.TryAdvance() is called by SubmitManeuver
                    break;

                case GameEnum.TurnState.WaitWeaponDeclare:
                     // Step 4: Systema espera todos... proxima fase (RevealAndMove)
                    break;

                case GameEnum.TurnState.RevealAndMoveFighters:
                    // Step 5: Systema plota os aviões
                    RevealAndMoveFighters();
                    // Step 6: Systema espera ele mesmo terminar... fazendo check de colisão aérea
                    turnManager.AdvanceButton(); // Automatically advance to ResolveCollisions after move
                    break;

                case GameEnum.TurnState.ResolveCollisions:
                    // Step 6 continues: Check collision
                    StartCoroutine(ResolveCollisionsRoutine());
                    break;

                case GameEnum.TurnState.WaitMissileSelection:
                    // Step 7: Players select missile from catalog
                    break;

                case GameEnum.TurnState.SpawnMissilesAndResolveEvasion:
                     // Step 8, 9, 10
                    SpawnMissilesAndResolveEvasion();
                    turnManager.AdvanceButton();
                    break;

                case GameEnum.TurnState.ApplyDamageAndCheckVictory:
                    // Step 11
                    CheckVictory();
                    turnManager.AdvanceButton();
                    break;
            }
        }

        private void RevealAndMoveFighters()
        {
            if (turnManager == null || turnManager.sheet == null) return;

            ClearTurnSegments();
            var rows = turnManager.sheet.rows;
            if (rows == null) return;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || !row.isAlive) continue;
                if (!row.maneuverReady) continue;

                var unit = FindUnitByPlayerId(row.playerId);
                if (unit == null) continue;

                string raw = MvpRules.SanitizeManeuver(row.maneuverRaw);
                ManeuverDef def = ManeuverCatalog.Resolve(raw);
                string validRaw = string.IsNullOrWhiteSpace(raw) ? def.id : raw;
                
                int g = 1; 
                if (validRaw.Length > 0 && char.IsDigit(validRaw[0])) int.TryParse(validRaw[0].ToString(), out g);

                var log = new AircraftUnit.FlightLog
                {
                    TurnIndex = turnManager != null ? turnManager.sheet.turnIndex : 0,
                    ManeuverCode = validRaw,
                    RawInput = validRaw,
                    Speed = 1.0f,
                    GForce = g
                };
                unit.maneuverHistory.Add(log);

                TurnDir dir = ParseDirFromRaw(raw);
                ExecuteManeuver(unit, def, dir, GetTeamColor(unit.teamId));
            }
        }

        private bool ResolveCollisions()
        {
            // User requested Token collision (Bounds), determining purely by final overlap.
            // "Path" collision (trails) is considered ridiculous.
             return CheckBoundsCollisions();
        }

        private System.Collections.IEnumerator ResolveCollisionsRoutine()
        {
            // Execute checks immediately so effects (Die) happen
            bool collisionFound = ResolveCollisions();

            if (collisionFound)
            {
                float delay = (turnManager != null) ? turnManager.endRoundDelaySeconds : 2.0f;
                Debug.Log($"[MatchControllerMvp] Collision detected! Pausing {delay}s.");
                yield return new WaitForSeconds(delay);
            }
            else
            {
                // Optional: small delay or just next frame if no collision
                yield return null; 
            }

            // Move to next state
            if (turnManager != null) turnManager.AdvanceButton();
        }

        private void SpawnMissilesAndResolveEvasion()
        {
            if (missileManager == null || turnManager == null || turnManager.sheet == null) return;
            var rows = turnManager.sheet.rows;
            if (rows == null) return;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || !row.isAlive) continue;
                if (MvpRules.SanitizeWeapon(row.weaponCode) != "M") continue;

                var unit = FindUnitByPlayerId(row.playerId);
                if (unit == null || unit.currentHp <= 0) continue;

                // Resolve user input (e.g. "M1", "L1") to actual ID (e.g. "M10F", "M10L1")
                MissilePathDef def = MissilePathCatalog.Resolve(row.missilePath);
                string pathId = (def != null) ? def.id : MissilePathCatalog.DefaultId;

                missileManager.FireFromAircraft(unit, unit.teamId, pathId);
            }
        }

        private void CheckVictory()
        {
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            var aliveByTeam = new Dictionary<int, int>();
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                if (u == null) continue;
                bool alive = u.currentHp > 0;
                if (turnManager != null)
                    turnManager.SetAlive(u.playerId, alive);

                if (!alive) continue;
                if (!aliveByTeam.ContainsKey(u.teamId))
                    aliveByTeam[u.teamId] = 0;
                aliveByTeam[u.teamId] += 1;
            }

            if (aliveByTeam.Count == 1)
            {
                int winnerTeam = aliveByTeam.Keys.First();
                Debug.Log($"[MatchControllerMvp] Victory: team {winnerTeam}");
            }
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

        private void ExecuteManeuver(AircraftUnit unit, ManeuverDef m, TurnDir dir, Color teamColor)
        {
            if (trailManager == null || unit == null || m == null) return;

            if (!lastEndByUnit.TryGetValue(unit, out Vector3 start))
                start = unit.ExhaustAnchor != null ? unit.ExhaustAnchor.position : unit.transform.position;

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
                lastEndByUnit[unit] = end;
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
                    lastEndByUnit[unit] = endStraight;
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

                lastEndByUnit[unit] = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : p2;
            }
        }

        private bool CheckPathCollisions()
        {
            var aircrafts = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            bool anyCollision = false;
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
                        Debug.Log($"[MatchControllerMvp] Path collision: {a.unitId} vs {b.unitId}");
                        a.currentHp = 0;
                        b.currentHp = 0;
                        a.Die();
                        b.Die();
                        anyCollision = true;

                        if (turnManager != null)
                        {
                            turnManager.SetAlive(a.playerId, false);
                            turnManager.SetAlive(b.playerId, false);
                        }
                    }
                }
            }
            return anyCollision;
        }

        private bool CheckBoundsCollisions()
        {
            var aircrafts = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            bool anyCollision = false;
            for (int i = 0; i < aircrafts.Length; i++)
            {
                var a = aircrafts[i];
                if (a == null || a.currentHp <= 0) continue;
                for (int j = i + 1; j < aircrafts.Length; j++)
                {
                    var b = aircrafts[j];
                    if (b == null || b.currentHp <= 0) continue;
                    if (BoundsIntersect(a, b))
                    {
                        Debug.Log($"[MatchControllerMvp] Bounds collision: {a.unitId} vs {b.unitId}");
                        a.currentHp = 0;
                        b.currentHp = 0;
                        a.Die();
                        b.Die();
                        anyCollision = true;

                        if (turnManager != null)
                        {
                            turnManager.SetAlive(a.playerId, false);
                            turnManager.SetAlive(b.playerId, false);
                        }
                    }
                }
            }
            return anyCollision;
        }

        private bool HasPathCollision(AircraftUnit a, AircraftUnit b)
        {
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

        private static bool BoundsIntersect(AircraftUnit a, AircraftUnit b)
        {
            var srA = a.GetComponentInChildren<SpriteRenderer>();
            var srB = b.GetComponentInChildren<SpriteRenderer>();
            if (srA == null || srB == null) return false;
            return srA.bounds.Intersects(srB.bounds);
        }

        private static AircraftUnit FindUnitByPlayerId(int playerId)
        {
            if (playerId <= 0) return null;
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                if (u != null && u.playerId == playerId) return u;
            }
            return null;
        }

        private static Color GetTeamColor(int teamId)
        {
            return GameEnum.GameColors.GetColorForTeam(teamId);
        }

        private enum TurnDir { F, D, E }

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

        private static Color GetStrongTeamColor(Color baseColor)
        {
            return new Color(
                Mathf.Clamp01(baseColor.r * 1.25f),
                Mathf.Clamp01(baseColor.g * 1.25f),
                Mathf.Clamp01(baseColor.b * 1.25f),
                baseColor.a
            );
        }
    }
}
