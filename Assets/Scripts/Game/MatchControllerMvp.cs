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
        [SerializeField] private ManeuverManager maneuverManager;

        [Header("Debug")]
        [SerializeField] private bool logFlow = true;

        [Header("Movement")]
        [SerializeField] private bool animateMovement = true;
        [SerializeField] private float movementDurationSeconds = 2.0f;

        [Header("Collision (MVP)")]
        [Range(0.10f, 1.00f)] public float aircraftHitRadiusFU = 0.10f;

        private readonly Dictionary<AircraftUnit, Vector3> lastEndByUnit = new();
        private readonly Dictionary<AircraftUnit, List<Segment>> turnSegmentsByUnit = new();
        private bool didResolveAttacksThisTurn = false;

        private void Awake()
        {
            if (turnManager == null)
                turnManager = FindFirstObjectByType<TurnManager>();
            if (trailManager == null)
                trailManager = FindFirstObjectByType<TrailManager>();
            if (missileManager == null)
                missileManager = FindFirstObjectByType<MissileManager>();
            if (maneuverManager == null)
                maneuverManager = FindFirstObjectByType<ManeuverManager>();
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
            row.maneuverComboRaw = row.maneuverRaw;
            row.maneuverReady = !string.IsNullOrWhiteSpace(row.maneuverRaw);
            turnManager.NotifySheetChanged();
            turnManager.TryAdvance();
        }

        public void SubmitManeuverList(int playerId, List<string> codes)
        {
            if (turnManager == null) return;
            if (codes == null || codes.Count == 0) return;
            var row = turnManager.GetOrCreatePlayerRow(playerId);
            if (row == null) return;

            string first = "";
            for (int i = 0; i < codes.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(codes[i]))
                {
                    first = codes[i].Trim().ToUpperInvariant();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(first)) return;

            row.maneuverRaw = MvpRules.SanitizeManeuver(first);
            row.maneuverComboRaw = string.Join("+", codes);
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
                    didResolveAttacksThisTurn = false;
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
                    StartCoroutine(RevealAndMoveFightersRoutine());
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
                    StartCoroutine(SpawnMissilesAndResolveEvasionRoutine());
                    // Note: Routine calls AdvanceButton
                    break;

                case GameEnum.TurnState.ApplyDamageAndCheckVictory:
                    // Step 11
                    if (!didResolveAttacksThisTurn)
                        CheckVictory();
                    turnManager.AdvanceButton();
                    break;
            }
        }

        private System.Collections.IEnumerator RevealAndMoveFightersRoutine()
        {
            int moved = RevealAndMoveFighters();
            if (animateMovement && moved > 0 && movementDurationSeconds > 0f)
                yield return new WaitForSeconds(movementDurationSeconds);

            // Step 6: Systema espera ele mesmo terminar... fazendo check de colisao aerea
            if (turnManager != null) turnManager.AdvanceButton();
        }

        private int RevealAndMoveFighters()
        {
            if (turnManager == null || turnManager.sheet == null) return 0;

            ClearTurnSegments();
            var rows = turnManager.sheet.rows;
            if (rows == null) return 0;

            int moved = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || !row.isAlive) continue;
                if (!row.maneuverReady) continue;

                var unit = FindUnitByPlayerId(row.playerId);
                if (unit == null) continue;
                string raw = MvpRules.SanitizeManeuver(row.maneuverRaw);
                ManeuverProfile profile = ResolveManeuverProfile(raw);
                ManeuverDef def = null;
                if (profile == null)
                {
                    WarnProfileFallbackOnce();
                    def = ManeuverCatalog.Resolve(raw);
                }
                string fallbackId = def != null ? def.id : "";
                string validRaw = string.IsNullOrWhiteSpace(raw)
                    ? (profile != null ? profile.maneuverId : fallbackId)
                    : raw;

                int g = profile != null ? Mathf.RoundToInt(profile.gForce) : 1;
                if (g <= 0 && validRaw.Length > 0 && char.IsDigit(validRaw[0])) int.TryParse(validRaw[0].ToString(), out g);

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
                if (profile != null)
                    ExecuteManeuver(unit, profile, dir, GetTeamColor(unit.teamId), ref moved);
                else if (def != null)
                    ExecuteManeuverLegacy(unit, def, dir, GetTeamColor(unit.teamId), ref moved);
            }

            return moved;
        }

        private System.Collections.IEnumerator ResolveCollisionsRoutine()
        {
            var victims = new HashSet<AircraftUnit>();

            // 1. Detect Collisions (ONLY detection, no killing yet)
            // Aircraft should not collide with aircraft trails (no TRON behavior).
            CollectBoundsCollisions(victims);

            // 2. If collision, Log & Wait
            if (victims.Count > 0)
            {
                float delay = (turnManager != null) ? turnManager.endRoundDelaySeconds : 5.0f; 
                Debug.Log($"[MatchControllerMvp] Collision detected ({victims.Count} units). Waiting {delay}s before resolving...");
                
                // Show visuals? They are already overlapping.
                yield return new WaitForSeconds(delay);

                // 3. Resolve (Die)
                foreach (var u in victims)
                {
                    if (u == null) continue;
                    u.currentHp = 0;
                    u.Die();
                    if (turnManager != null) turnManager.SetAlive(u.playerId, false);
                }
            }
            else
            {
                yield return null; 
            }

            // Move to next state
            if (turnManager != null) turnManager.AdvanceButton();
        }

        private void CollectPathCollisions(HashSet<AircraftUnit> victims)
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
                        Debug.Log($"[MatchControllerMvp] Path collision detected: {a.unitId} vs {b.unitId}");
                        victims.Add(a);
                        victims.Add(b);
                    }
                }
            }
        }

        private void CollectBoundsCollisions(HashSet<AircraftUnit> victims)
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
                    if (BoundsIntersect(a, b))
                    {
                        Debug.Log($"[MatchControllerMvp] Bounds collision detected: {a.unitId} vs {b.unitId}");
                        victims.Add(a);
                        victims.Add(b);
                    }
                }
            }
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
            var colA = a.GetComponentInChildren<Collider2D>();
            var colB = b.GetComponentInChildren<Collider2D>();
            if (colA != null && colB != null)
                return colA.Distance(colB).isOverlapped;

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

        private ManeuverProfile ResolveManeuverProfile(string raw)
        {
            if (maneuverManager != null)
                return maneuverManager.Resolve(raw);
            return ManeuverProfileCatalog.Resolve(raw);
        }

        // ... Restored Methods ...
        // ... Restored Methods ...
        private System.Collections.IEnumerator SpawnMissilesAndResolveEvasionRoutine()
        {
            if (missileManager == null || turnManager == null || turnManager.sheet == null) yield break;
            var rows = turnManager.sheet.rows;
            if (rows == null) yield break;
            didResolveAttacksThisTurn = true;

            // 1. Identify all valid shooters FIRST (Simultaneous Logic)
            var shooters = new System.Collections.Generic.List<(AircraftUnit unit, int teamId, string pathId)>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || !row.isAlive) continue;
                if (MvpRules.SanitizeWeapon(row.weaponCode) != "M") continue;

                var unit = FindUnitByPlayerId(row.playerId);
                if (unit == null || unit.currentHp <= 0) continue;

                MissilePathDef def = MissilePathCatalog.Resolve(row.missilePath);
                string pathId = (def != null) ? def.id : MissilePathCatalog.DefaultId;
                
                shooters.Add((unit, unit.teamId, pathId));
            }
            
            // --- FASE DE SELECIONAR MISSIL (Já ocorreu na UI) ---
            
            // --- FASE FIRING (Apenas Sistema) ---
            // 1. Plota todos os misseis na tela
            var transactions = new System.Collections.Generic.List<MissileTransaction>();
            foreach (var s in shooters)
            {
                if (s.unit != null)
                {
                    var t = missileManager.SpawnMissile(s.unit, s.teamId, s.pathId);
                    if (t != null) transactions.Add(t);
                }
            }

            // --- HIT CHECK (Quais misseis atingiram quais alvos?) ---
            var pendingHits = new System.Collections.Generic.List<Zone5.MissileManager.MissileHit>();
            foreach (var t in transactions)
            {
                var hit = missileManager.CheckCollision(t);
                if (hit != null)
                {
                    pendingHits.Add(hit);
                    // Log inicial: registro de hit
                    Debug.Log($"[Hit Check] {GetPrettyName(hit.Transaction.Shooter)} HAS HIT {GetPrettyName(hit.Target)} with {hit.Transaction.PathRaw}");
                }
            }

            // --- DODGE CHECK (Rolling Dice) ---
            // Resolve pendencias uma a uma
            foreach (var hit in pendingHits)
            {
                missileManager.ResolveHit(hit); // Calcula Evasion e aplica Dano (HP reduz, mas obj nao somes)
            }

            // --- FASE EVALUATING (Pontos Computados) ---
            // Misseis sao retirados (visualmente depois?), pontos computados
            // "Misseis sao retirados": User disse isso aqui, mas disse que espera 10s depois.
            // Vou computar pontos AGORA, mas limpar DEPOIS dos 10s.
            CalculateDiesAndScores(); 

            // --- RESOLUÇÃO DO TURNO (Aguarda 10s) ---
            if (transactions.Count > 0)
            {
                 Debug.Log("[MatchControllerMvp] Waiting 10s for visual inspection...");
                 yield return new WaitForSeconds(10f);
                 
                 // --- CLEANUP (Remove Misseis e Aviões Abatidos) ---
                 missileManager.ClearMissiles();
                 CleanupBattlefield();
            }
            else
            {
                 yield return null;
            }

            // --- VICTORY CHECK ---
            CheckVictoryCondition();

            turnManager.AdvanceButton();
        }

        private string GetPrettyName(AircraftUnit u)
        {
             if (u == null) return "null";
             string cs = !string.IsNullOrEmpty(u.callSign) ? u.callSign : "Pilot";
             string plane = (u.UnitData != null) ? u.UnitData.unitName : "Aircraft";
             return $"{cs} ({plane})"; // Helper duplicating MissileManager one, useful for debug here
        }

        private string GetPrettyNameByUnitId(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return "Unknown";
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                if (u != null && u.unitId == unitId)
                    return GetPrettyName(u);
            }
            return unitId;
        }
        private void CalculateDiesAndScores()
        {
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            
            foreach (var u in units)
            {
                if (u == null) continue;
                // Determine death (HP 0 or already destroyed flag)
                bool isDead = u.currentHp <= 0 || u.isDestroyed;
                
                if (!isDead) continue; // Alive units don't award points yet

                // Calculate Score/Assist if we have damage data
                if (u.roundDamageReceived != null && u.roundDamageReceived.Count > 0)
                {
                    int maxDmg = -1;
                    foreach (var kvp in u.roundDamageReceived)
                    {
                        if (kvp.Value > maxDmg) maxDmg = kvp.Value;
                    }

                    string targetName = GetPrettyName(u);
                    var contributors = new List<(string unitId, string name, int points)>();

                    foreach (var kvp in u.roundDamageReceived)
                    {
                        if (kvp.Value <= 0) continue;
                        string shooterUnitId = kvp.Key;
                        string shooterName = GetPrettyNameByUnitId(shooterUnitId);
                        contributors.Add((shooterUnitId, shooterName, kvp.Value));
                    }

                    var winners = contributors.Where(c => c.points == maxDmg).ToList();
                    var assists = contributors.Where(c => c.points < maxDmg).ToList();

                    foreach (var w in winners)
                    {
                        int pId = ParsePlayerId(w.unitId);
                        var pRow = (turnManager != null) ? turnManager.GetOrCreatePlayerRow(pId) : null;
                        if (pRow != null) pRow.score++;
                    }

                    foreach (var a in assists)
                    {
                        int pId = ParsePlayerId(a.unitId);
                        var pRow = (turnManager != null) ? turnManager.GetOrCreatePlayerRow(pId) : null;
                        if (pRow != null) pRow.assists++;
                    }

                    if (contributors.Count > 0)
                    {
                        var parts = new List<string>();
                        for (int i = 0; i < contributors.Count; i++)
                        {
                            var c = contributors[i];
                            parts.Add($"{c.name} has {c.points} temporary points");
                        }

                        string winnersText = winners.Count > 0 ? string.Join(", ", winners.Select(w => w.name)) : "none";
                        string assistsText = assists.Count > 0 ? string.Join(", ", assists.Select(a => a.name)) : "none";
                        string tieText = winners.Count > 1 ? " (tie)" : "";
                        Debug.Log($"[TEMP RESOLUTION] {targetName} death: {string.Join(", ", parts)}. Winners{tieText}: {winnersText} (+1 score each). Assists: {assistsText}.");
                    }
                    // Clear damage after processing so we don't score again next turn
                    u.roundDamageReceived.Clear();
                }
                
                // Mark as dead in UI, but DO NOT DESTROY OBJECT YET
                if (turnManager != null) turnManager.SetAlive(u.playerId, false);
            }
        }

        private void CleanupBattlefield()
        {
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (u == null) continue;
                if (u.currentHp <= 0 || u.isDestroyed)
                {
                    u.Cleanup(); // Destroys game object
                }
            }
        }

        private void CheckVictoryCondition()
        {
            var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            var aliveByTeam = new Dictionary<int, int>();

             // Re-scan alive units after cleanup
            foreach (var u in units)
            {
                if (u == null) continue;
                // Ignore ghost data
                if (u.teamId < 0 || u.playerId <= 0) continue;

                 if (u.currentHp > 0 && !u.isDestroyed)
                 {
                    if (!aliveByTeam.ContainsKey(u.teamId)) aliveByTeam[u.teamId] = 0;
                    aliveByTeam[u.teamId]++;
                 }
            }
            
            // Print Standings
            if (turnManager != null && turnManager.sheet != null && turnManager.sheet.rows != null)
            {
                string msg = "TURN RESULTS:\n";
                foreach(var r in turnManager.sheet.rows)
                {
                     // Filter out dummies from UI/Log if they snuck in
                     if (r.playerId <= 0) continue; 
                     
                     string status = r.isAlive ? "(alive)" : "(dead)";
                     Debug.Log($"[SCORE] Player {r.playerId} ({r.callSign}): {r.score} score, {r.assists} assist {status}");
                }
            }

            // 3. Check Team Victory
            int teamsAlive = aliveByTeam.Count;
            // Debug.Log($"[MatchControllerMvp] Teams Alive: {teamsAlive} ({string.Join(",", aliveByTeam.Keys)})");
            
            if (teamsAlive == 0)
            {
                Debug.Log("DRAW! No planes left!");
                if (turnManager != null)
                    turnManager.SetMatchEnded(-1);
            }
            else if (teamsAlive == 1)
            {
                int winnerTeam = aliveByTeam.Keys.First();
                Debug.Log($"VICTORY! Team {winnerTeam} won the match!");
                if (turnManager != null)
                    turnManager.SetMatchEnded(winnerTeam);
            }
            else
            {
                 Debug.Log($"Match continues... {teamsAlive} teams fighting.");
            }
        }

        // Wrapper for compatibility if needed
        private void CheckVictory()
        {
            CalculateDiesAndScores();
            CleanupBattlefield();
            CheckVictoryCondition();
        }

        private int ParsePlayerId(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return -1;
            // Format: "F-14_P1_T0"
            var parts = unitId.Split('_');
            foreach (var p in parts)
            {
                if (p.StartsWith("P") && int.TryParse(p.Substring(1), out int id))
                    return id;
            }
            return -1;
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

        public bool TryGetLastEnd(AircraftUnit unit, out Vector3 end)
        {
            return lastEndByUnit.TryGetValue(unit, out end);
        }

        public void SetLastEnd(AircraftUnit unit, Vector3 end)
        {
            if (unit == null) return;
            lastEndByUnit[unit] = end;
        }

        public void SyncLastEndFromTransform(AircraftUnit unit)
        {
            if (unit == null) return;
            lastEndByUnit[unit] = (unit.ExhaustAnchor != null) ? unit.ExhaustAnchor.position : unit.transform.position;
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

        private void ExecuteManeuver(AircraftUnit unit, ManeuverProfile profile, TurnDir dir, Color teamColor, ref int moved)
        {
            if (trailManager == null || unit == null || profile == null) return;

            if (!lastEndByUnit.TryGetValue(unit, out Vector3 start))
                start = unit.ExhaustAnchor != null ? unit.ExhaustAnchor.position : unit.transform.position;

            float fuWorld = MovementCore.GetFUWorld(unit);
            Vector3 forward = MovementCore.GetForward(unit);
            Color strongColor = GetStrongTeamColor(teamColor);

            var path = new List<Vector3>();
            profile.BuildWorldPoints(start, forward, fuWorld, dir, path);
            if (path.Count < 2) return;

            for (int i = 0; i < path.Count - 1; i++)
                AddTurnSegment(unit, path[i], path[i + 1]);

            Vector3 end = path[path.Count - 1];
            Vector3 endHeading = profile.ResolveEndHeading(forward, dir, path);

            lastEndByUnit[unit] = end;

            if (animateMovement)
            {
                StartAircraftAnimation(unit, path, strongColor, profile, dir, start, forward, fuWorld, endHeading);
                moved++;
            }
            else
            {
                for (int i = 0; i < path.Count - 1; i++)
                    trailManager.CreateSegment(unit, path[i], path[i + 1], strongColor);
                MovementCore.AlignAndTeleportToEnd(unit, start, end, forward, endHeading);
                unit.ResetVisualPose(unit.visualSprite != null ? unit.visualSprite.color : GetTeamColor(unit.teamId));
            }
        }

        private void ExecuteManeuverLegacy(AircraftUnit unit, ManeuverDef m, TurnDir dir, Color teamColor, ref int moved)
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

                AddTurnSegment(unit, start, end);
                lastEndByUnit[unit] = end;
                
                    if (animateMovement)
                    {
                        StartAircraftAnimation(unit, new List<Vector3> { start, end }, strongColor, null, dir, start, forward, fuWorld, forward);
                        moved++;
                    }
                    else
                {
                    trailManager.CreateSegment(unit, start, end, strongColor);
                    MovementCore.AlignAndTeleportToEnd(unit, start, end, forward);
                    unit.ResetVisualPose(unit.visualSprite != null ? unit.visualSprite.color : GetTeamColor(unit.teamId));
                }
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
                    AddTurnSegment(unit, p0, endStraight);
                    lastEndByUnit[unit] = endStraight;

                    if (animateMovement)
                    {
                        StartAircraftAnimation(unit, new List<Vector3> { p0, endStraight }, strongColor, null, dir, p0, forward0, fuWorld, forward0);
                        moved++;
                    }
                    else
                    {
                        trailManager.CreateSegment(unit, p0, endStraight, strongColor);
                        MovementCore.AlignAndTeleportToEnd(unit, p0, endStraight, forward0);
                    }
                    return;
                }

                float radius = totalDist / Mathf.Abs(thetaRad);
                Vector3 right0 = MovementCore.Rotate2D(forward0, -90f).normalized;
                float thetaSign = Mathf.Sign(thetaRad);
                Vector3 center = p0 - right0 * (radius * thetaSign);
                Vector3 p2 = center + MovementCore.Rotate2D(p0 - center, theta);

                int steps = Mathf.Max(16, Mathf.CeilToInt(Mathf.Abs(theta) / 5f));
                var path = new List<Vector3> { p0 };
                Vector3 prev = p0;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float angDeg = theta * t;
                    Vector3 pt = center + MovementCore.Rotate2D(p0 - center, angDeg);
                    AddTurnSegment(unit, prev, pt);
                    path.Add(pt);
                    prev = pt;
                }

                lastEndByUnit[unit] = p2;

                if (animateMovement)
                {
                    Vector3 endHeading = MovementCore.Rotate2D(forward0, theta).normalized;
                    StartAircraftAnimation(unit, path, strongColor, null, dir, start, forward0, fuWorld, endHeading);
                    moved++;
                }
                else
                {
                    for (int i = 0; i < path.Count - 1; i++)
                        trailManager.CreateSegment(unit, path[i], path[i + 1], strongColor);

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
                    unit.ResetVisualPose(unit.visualSprite != null ? unit.visualSprite.color : GetTeamColor(unit.teamId));
                }
            }
        }

        private void StartAircraftAnimation(
            AircraftUnit unit,
            List<Vector3> path,
            Color color,
            ManeuverProfile profile,
            TurnDir dir,
            Vector3 start,
            Vector3 forward,
            float fuWorld,
            Vector3? endHeading = null)
        {
            if (unit == null || path == null || path.Count < 2) return;

            var view = unit.GetComponent<AircraftView>();
            if (view == null) view = unit.gameObject.AddComponent<AircraftView>();

            view.ConfigureTrail(trailManager, color, unit);
            view.ConfigureVfx(profile, dir, start, forward, fuWorld);
            view.SetPath(path.ToArray());
            if (endHeading.HasValue)
                view.SetFinalHeading(endHeading.Value);
            view.AnimatePath(movementDurationSeconds);
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
            Debug.LogWarning("[MatchControllerMvp] ManeuverProfile not found. Falling back to ManeuverCatalog.");
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



