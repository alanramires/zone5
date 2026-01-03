using System;
using System.Linq;
using UnityEngine;

namespace Zone5
{
    public class MissileManager : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<MissileUnit> _activeMissiles = new();

        [Header("Prefab")]
        public MissileUnit missilePrefab;

        [Header("Refs")]
        public TrailManager trailManager;

        [Header("Hitbox Overlay (Game View)")]
        public bool showHitboxes = false;     // default false


        [Header("Visual")]
        public Transform missilesRoot;
        public Color missileColor = Color.white;
        [Tooltip("Trail do míssil deve ser mais fino que o do caça.")]
        public float missileTrailWidth = 0.18f;

        [Header("Gameplay (MVP test)")]
        [Min(0.1f)] public float defaultRangeFU = 10f; // Phoenix / R-27
        public MissileProfile defaultProfile;          // opcional, se quiser usar profile já

        [Header("Collision (MVP)")]
        [Tooltip("Raio do hitbox do caça em Fighter Units (FU). Independe do sprite.")]
        [Range(0.10f, 1.00f)] public float aircraftHitRadiusFU = 0.30f;
        [Tooltip("Raio do mÇðssil em FU. Soma no raio do aviÇœo (hit mais intuitivo).")]
        [Range(0f, 0.50f)] public float missileRadiusFU = 0.08f;
        [Tooltip("Se true, desenha linhas debug do teste de colisão por alguns segundos.")]
        public bool debugCollision = true;
        public float debugLineSeconds = 2f;

        private float _savedTrailWidth;

        private void Awake()
        {
            if (trailManager == null)
                trailManager = FindFirstObjectByType<TrailManager>();
        }

        /// TESTE: dispara 1 míssil por time, do ponto atual do avião, seguindo o forward atual.
        public void FireTestBoth(string bluePathRaw = "M10F", string redPathRaw = "M10F")
        {
            ClearMissiles(); // apaga os anteriores

            var blue = FindTeamUnit(0);
            var red  = FindTeamUnit(1);

            if (blue != null) FireFromAircraft(blue, teamId: 0, pathRaw: bluePathRaw);
            if (red  != null) FireFromAircraft(red,  teamId: 1, pathRaw: redPathRaw);
        }

        public void FireFromAircraft(AircraftUnit shooter, int teamId, string pathRaw = null)
        {
            if (missilePrefab == null)
            {
                Debug.LogError("[MissileManager] missilePrefab not assigned.");
                return;
            }
            if (trailManager == null)
            {
                Debug.LogError("[MissileManager] trailManager not found.");
                return;
            }
            if (shooter == null || shooter.ExhaustL == null || shooter.ExhaustR == null)
            {
                Debug.LogError("[MissileManager] shooter or its ExhaustL/ExhaustR is null.");
                return;
            }
            if (string.IsNullOrWhiteSpace(pathRaw))
                return;

            // --- 1) Spawn missile object ---
            Transform parent = missilesRoot != null ? missilesRoot : transform;
            var missile = Instantiate(missilePrefab, parent);
            _activeMissiles.Add(missile);

            // aplica profile (se tiver), senão fica só branco mesmo
            MissileProfile profile = defaultProfile;

            Color teamColor = (teamId == 0) ? GameEnum.GameColors.TeamBlue : GameEnum.GameColors.TeamRed;

            if (profile != null)
            {
                missile.ApplyMissileData(
                    data: profile,
                    teamColor: teamColor,
                    ownerId: shooter.unitId,
                    team: teamId,
                    serial: UnityEngine.Random.Range(0, 99999)
                );
            }
            else
            {
                missile.teamId = teamId;
                missile.ownerUnitId = shooter.unitId;
            }

            // --- 2) Magnet: cola o míssil exatamente no par L/R do caça ---
            Vector3 startL = shooter.ExhaustL.position;
            Vector3 startR = shooter.ExhaustR.position;

            MagnetAlignByTwoAnchors(missile.transform, missile.ExhaustL, missile.ExhaustR, startL, startR);
            // --- 2.5) Garantir que o nariz do missil aponta pro mesmo forward do caca ---
            Vector3 pinned = missile.ExhaustAnchor != null ? missile.ExhaustAnchor.position : missile.transform.position;

            Vector3 desiredForward = (shooter.NoseAnchor.position - shooter.ExhaustAnchor.position);
            desiredForward.z = 0f;
            desiredForward = desiredForward.sqrMagnitude > 0.000001f ? desiredForward.normalized : shooter.transform.up;

            Vector3 missileForward = missile.GetForward();
            missileForward.z = 0f;
            missileForward = missileForward.sqrMagnitude > 0.000001f ? missileForward.normalized : missile.transform.up;

            // Se estiver apontando "pra tras", gira 180 em Z
            if (Vector3.Dot(missileForward, desiredForward) < 0f)
            {
                missile.transform.rotation = Quaternion.AngleAxis(180f, Vector3.forward) * missile.transform.rotation;
            }

            // Recola o exhaustAnchor no mesmo lugar (nao perde o encaixe)
            Vector3 after = missile.ExhaustAnchor != null ? missile.ExhaustAnchor.position : missile.transform.position;
            missile.transform.position += (pinned - after);

            // --- 3) Determina start/forward do missil ---
            Vector3 start = missile.ExhaustAnchor != null ? missile.ExhaustAnchor.position : missile.transform.position;

            // Forward do missil DEVE ser o forward do caca.
            Vector3 forward = (shooter.NoseAnchor.position - shooter.ExhaustAnchor.position);
            forward.z = 0f;
            forward = forward.sqrMagnitude > 0.000001f ? forward.normalized : shooter.transform.up;

            // "Left" para yNorm positivo = esquerda (L1/L2)
            Vector3 left = Rotate2D(forward, +90f).normalized;

            // 1 FU do tabuleiro = comprimento do CACA
            float fuWorld = GetFUWorldFromAircraft(shooter);

            // resolve path no catalogo (M10F, L1, S2, etc)
            var pathDef = MissilePathCatalog.Resolve(pathRaw);

            // rangeFU vem do profile (ou default)
            float rangeFU = (defaultProfile != null) ? defaultProfile.rangeFU : defaultRangeFU;

            // converte pointsNorm -> world
            var controlPts = BuildWorldPoints(start, forward, left, fuWorld, rangeFU, pathDef);

            // suaviza com Catmull-Rom
            var smoothPts = SmoothCatmullRom(controlPts, samplesPerSegment: 10);

            // desenha segmentos curtinhos (curva continua)
            for (int i = 0; i < smoothPts.Length - 1; i++)
            {
                DrawMissileSegment(missile, smoothPts[i], smoothPts[i + 1], missileColor);
            }

            // --- 4) Colisão ao longo do caminho (MVP) ---
            var target = FindTeamUnit(teamId == 0 ? 1 : 0);
            bool hit = false;
            if (target != null)
            {
                float debugSeconds = debugLineSeconds;
                var dbg = FindFirstObjectByType<DebugManager>();
                if (dbg == null && !debugCollision)
                    debugSeconds = 0f;

                hit = CheckMissilePathHitAircraft(
                    smoothPts, target,
                    aircraftHitRadiusFU, missileRadiusFU,
                    fuWorld,
                    debugSeconds);
                if (hit)
                {
                    Debug.Log($"[Missile] HIT along path! shooterTeam={teamId} target={target.unitId} path={pathRaw}");

                    // MVP: por enquanto só log. Amanhã: rodar dodge + aplicar missileDamage se falhar.
                    // Você pode também mudar a cor do trail ou piscar o alvo aqui.
                }

            }

            // (MVP) coloca o missil no final
            start = missile.transform.position;
            Vector3 end = smoothPts[smoothPts.Length - 1];
            Vector3 finalPos = start + (end - start); // (mesmo que end, mas deixa explicito)
            missile.transform.position = finalPos;

            // check extra: posicao final do missil tambem pode colidir
            if (target != null && !hit)
            {
                hit = CheckMissileFinalHitAircraft(
                    missile.transform.position,
                    target,
                    aircraftHitRadiusFU,
                    missileRadiusFU,
                    fuWorld);
                if (hit)
                    Debug.Log($"[Missile] HIT at final position! shooter={shooter.name} target={target.name}");
            }

        }
        public void ClearMissiles()
        {
            for (int i = _activeMissiles.Count - 1; i >= 0; i--)
            {
                var m = _activeMissiles[i];
                if (m != null) m.Die(); // ja limpa trails e destroi
            }
            _activeMissiles.Clear();
        }

        private void DrawMissileSegment(MissileUnit missile, Vector3 start, Vector3 end, Color color)
        {
            // TrailManager hoje aceita AircraftUnit. Pra não refatorar agora,
            // criamos um LineRenderer manual aqui (igual TrailManager.CreateSegment),
            // mas registrando no missile para limpar depois.

            Transform parent = trailManager.trailsRoot != null ? trailManager.trailsRoot : trailManager.transform;
            int index = missile.trailSegments != null ? missile.trailSegments.Count : 0;
            string id = string.IsNullOrEmpty(missile.missileInstanceId) ? missile.name : missile.missileInstanceId;

            var go = new GameObject($"MissileTrail_{id}_{index}");
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = missileTrailWidth;
            lr.endWidth = missileTrailWidth;
            lr.sortingLayerName = "Background";
            lr.sortingOrder = 2;

            // material (reaproveita do TrailManager)
            if (trailManager.lineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) trailManager.lineMaterial = new Material(shader);
            }
            if (trailManager.lineMaterial != null)
                lr.material = trailManager.lineMaterial;

            lr.startColor = color;
            lr.endColor = color;
            if (lr.material != null) lr.material.color = color;

            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            missile.AddTrail(lr);

            // debug trail (orange, 25% opacity) - separado do trail visual
            var dbgGo = new GameObject($"MissileTrailDbg_{id}_{index}");
            dbgGo.transform.SetParent(parent, false);

            var dbgLr = dbgGo.AddComponent<LineRenderer>();
            dbgLr.useWorldSpace = true;
            dbgLr.positionCount = 2;
            var dbg = FindFirstObjectByType<DebugManager>();
            float widthMul = dbg != null ? dbg.GetTrailHitboxWidthMultiplier() : 1f;
            dbgLr.startWidth = missileTrailWidth;
            dbgLr.endWidth = missileTrailWidth;
            dbgLr.widthMultiplier = widthMul;
            dbgLr.sortingLayerName = "FX";
            dbgLr.sortingOrder = 999;

            if (trailManager.lineMaterial != null)
                dbgLr.material = new Material(trailManager.lineMaterial);

            Color dbgColor = new Color(1f, 0.5f, 0f, 0.5f);
            dbgLr.startColor = dbgColor;
            dbgLr.endColor = dbgColor;
            if (dbgLr.material != null) dbgLr.material.color = dbgColor;

            dbgLr.SetPosition(0, start);
            dbgLr.SetPosition(1, end);

            dbgLr.enabled = dbg != null && dbg.toggleAllDebugsVisible && dbg.trailHitboxAlongsidePath;

            missile.AddDebugTrail(dbgLr);
        }

        private static AircraftUnit FindTeamUnit(int teamId)
        {
            string key = $"_T{teamId}";
            return FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None)
                .FirstOrDefault(u => u != null && !string.IsNullOrEmpty(u.unitId) && u.unitId.Contains(key));
        }

        /// Cola um transform para que (anchorL, anchorR) coincidam com (targetL, targetR).
        /// Mesma lógica do seu magnet do caça, só que genérico pra qualquer prefab.
        private static void MagnetAlignByTwoAnchors(Transform root, Transform anchorL, Transform anchorR, Vector3 targetL, Vector3 targetR)
        {
            if (root == null || anchorL == null || anchorR == null) return;

            Vector3 aL = anchorL.position;
            Vector3 aR = anchorR.position;

            Vector3 vA = (aR - aL); vA.z = 0f;
            Vector3 vB = (targetR - targetL); vB.z = 0f;

            if (vA.sqrMagnitude < 0.000001f || vB.sqrMagnitude < 0.000001f) return;

            // anti-inversão
            if (Vector3.Dot(vA, vB) < 0f)
            {
                (targetL, targetR) = (targetR, targetL);
                vB = (targetR - targetL); vB.z = 0f;
            }

            Quaternion rotDelta = Quaternion.FromToRotation(vA, vB);
            root.rotation = rotDelta * root.rotation;

            // cola o L
            Vector3 newAL = anchorL.position;
            Vector3 delta = targetL - newAL;
            root.position += delta;
        }

        private static float GetFUWorldFromAircraft(AircraftUnit unit)
        {
            if (unit == null || unit.NoseAnchor == null || unit.ExhaustAnchor == null) return 1f;
            return Mathf.Max(0.01f, Vector3.Distance(unit.NoseAnchor.position, unit.ExhaustAnchor.position));
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

        private static Vector3[] BuildWorldPoints(
            Vector3 start,
            Vector3 forward,
            Vector3 left,
            float fuWorld,
            float rangeFU,
            MissilePathDef def)
        {
            if (def == null || def.pointsNorm == null || def.pointsNorm.Count < 2)
                return new[] { start, start + forward * (rangeFU * fuWorld) };

            var pts = new Vector3[def.pointsNorm.Count];

            for (int i = 0; i < def.pointsNorm.Count; i++)
            {
                var p = def.pointsNorm[i];

                float xFU = p.x * rangeFU;
                float yFU = p.y * rangeFU;

                pts[i] = start + forward * (xFU * fuWorld) + left * (yFU * fuWorld);
            }

            return pts;
        }

        private static Vector3[] SmoothCatmullRom(Vector3[] controlPoints, int samplesPerSegment = 16)
        {
            if (controlPoints == null || controlPoints.Length < 2)
                return controlPoints ?? Array.Empty<Vector3>();

            if (samplesPerSegment < 1) samplesPerSegment = 1;

            int n = controlPoints.Length;

            var result = new System.Collections.Generic.List<Vector3>(n * (samplesPerSegment + 1));
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
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            return result.ToArray();
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

        private static bool CheckMissilePathHitAircraft(
            Vector3[] missilePts,
            AircraftUnit target,
            float hitRadiusFU,
            float missileRadiusFU,
            float fallbackFuWorld,
            float debugSeconds = 0f)
        {
            if (missilePts == null || missilePts.Length < 2) return false;
            if (target == null || target.NoseAnchor == null || target.ExhaustAnchor == null) return false;

            Vector3 A = target.ExhaustAnchor.position; A.z = 0f;
            Vector3 B = target.NoseAnchor.position;    B.z = 0f;

            // 1 FU do alvo (melhor do que usar o shooter)
            float fuWorldTarget = Mathf.Max(0.01f, Vector3.Distance(A, B));
            if (fuWorldTarget <= 0.00001f) fuWorldTarget = Mathf.Max(0.01f, fallbackFuWorld);

            float missileRadiusWorld = missileRadiusFU * fuWorldTarget; // mesma rÇ¸gua FU do alvo (ok pro MVP)
            float radiusWorld = (hitRadiusFU * fuWorldTarget) + missileRadiusWorld;

            var dbg = FindFirstObjectByType<DebugManager>();
            bool useManager = dbg != null;
            bool master = useManager ? dbg.toggleAllDebugsVisible : (debugSeconds > 0f);
            bool showTrail = useManager ? (master && dbg.trailHitboxAlongsidePath) : (debugSeconds > 0f);
            bool showCollision = useManager ? (master && dbg.collisionDetectedCircle) : (debugSeconds > 0f);
            float drawSeconds = (showTrail || showCollision) ? debugSeconds : 0f;

            if (showCollision && drawSeconds > 0f)
            {
                DebugDrawCapsule2D(A, B, radiusWorld, Color.magenta, drawSeconds);
            }

            UpdateAircraftHitboxDebug(target, radiusWorld, showCollision);


            for (int i = 0; i < missilePts.Length - 1; i++)
            {
                Vector3 P = missilePts[i];     P.z = 0f;
                Vector3 Q = missilePts[i + 1]; Q.z = 0f;

                float d = MinDistanceSegmentToSegment2D(A, B, P, Q);

                if (showTrail && drawSeconds > 0f)
                {
                    // linha do segmento do míssil
                    Debug.DrawLine(P, Q, Color.white, drawSeconds);
                }

                if (d <= radiusWorld)
                    return true;
            }

            return false;
        }

        private static bool CheckMissileFinalHitAircraft(
            Vector3 missilePos,
            AircraftUnit target,
            float aircraftHitRadiusFU,
            float missileRadiusFU,
            float fuWorld)
        {
            if (target == null || target.ExhaustAnchor == null || target.NoseAnchor == null) return false;

            // A-B = "espinha" do aviao
            Vector3 A = target.ExhaustAnchor.position; A.z = 0f;
            Vector3 B = target.NoseAnchor.position;    B.z = 0f;
            Vector3 P = missilePos;                    P.z = 0f;

            float radiusWorld = (aircraftHitRadiusFU + missileRadiusFU) * fuWorld;

            float d = DistancePointToSegment(P, A, B);
            return d <= radiusWorld;
        }

        // distancia de um ponto P ao segmento AB
        private static float DistancePointToSegment(Vector3 P, Vector3 A, Vector3 B)
        {
            Vector3 AB = B - A;
            float ab2 = Vector3.Dot(AB, AB);
            if (ab2 <= 1e-6f) return Vector3.Distance(P, A);

            float t = Vector3.Dot(P - A, AB) / ab2;
            t = Mathf.Clamp01(t);
            Vector3 Q = A + t * AB;
            return Vector3.Distance(P, Q);
        }

        /// Menor distância entre dois segmentos em 2D: AB (avião) e PQ (míssil).
        private static float MinDistanceSegmentToSegment2D(Vector3 A, Vector3 B, Vector3 P, Vector3 Q)
        {
            // Se qualquer segmento for degenerado, cai para distância ponto-segmento
            if ((B - A).sqrMagnitude < 1e-8f) return DistancePointToSegment2D(A, P, Q);
            if ((Q - P).sqrMagnitude < 1e-8f) return DistancePointToSegment2D(P, A, B);

            // Se intersecta, distância = 0
            if (SegmentsIntersect2D(A, B, P, Q)) return 0f;

            // Caso contrário, menor das distâncias ponto->segmento
            float d1 = DistancePointToSegment2D(A, P, Q);
            float d2 = DistancePointToSegment2D(B, P, Q);
            float d3 = DistancePointToSegment2D(P, A, B);
            float d4 = DistancePointToSegment2D(Q, A, B);

            return Mathf.Min(Mathf.Min(d1, d2), Mathf.Min(d3, d4));
        }

        private static float DistancePointToSegment2D(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; ab.z = 0f;
            Vector3 ap = point - a; ap.z = 0f;

            float abLen2 = ab.sqrMagnitude;
            if (abLen2 < 1e-8f) return (point - a).magnitude;

            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / abLen2);
            Vector3 proj = a + t * ab;
            return (point - proj).magnitude;
        }

        private static bool SegmentsIntersect2D(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // orientação (cross) em 2D
            float o1 = Cross2D(b - a, c - a);
            float o2 = Cross2D(b - a, d - a);
            float o3 = Cross2D(d - c, a - c);
            float o4 = Cross2D(d - c, b - c);

            // interseção geral
            if ((o1 > 0f && o2 < 0f || o1 < 0f && o2 > 0f) &&
                (o3 > 0f && o4 < 0f || o3 < 0f && o4 > 0f))
                return true;

            // casos colineares (tolerância pequena)
            const float eps = 1e-6f;
            if (Mathf.Abs(o1) < eps && OnSegment2D(a, b, c)) return true;
            if (Mathf.Abs(o2) < eps && OnSegment2D(a, b, d)) return true;
            if (Mathf.Abs(o3) < eps && OnSegment2D(c, d, a)) return true;
            if (Mathf.Abs(o4) < eps && OnSegment2D(c, d, b)) return true;

            return false;
        }

        private static void DebugDrawCapsule2D(Vector3 A, Vector3 B, float radius, Color c, float seconds)
        {
            Vector3 ab = (B - A); ab.z = 0f;
            Vector3 dir = ab.sqrMagnitude > 1e-6f ? ab.normalized : Vector3.up;

            // normal 2D (perp)
            Vector3 n = new Vector3(-dir.y, dir.x, 0f);

            Vector3 A1 = A + n * radius;
            Vector3 A2 = A - n * radius;
            Vector3 B1 = B + n * radius;
            Vector3 B2 = B - n * radius;

            // "retangulo" central da capsula
            Debug.DrawLine(A1, B1, c, seconds);
            Debug.DrawLine(A2, B2, c, seconds);
            Debug.DrawLine(A1, A2, c, seconds);
            Debug.DrawLine(B1, B2, c, seconds);

            // "circulos" nas pontas (aproximados com 12 segmentos)
            const int seg = 12;
            for (int i = 0; i < seg; i++)
            {
                float t0 = (i / (float)seg) * Mathf.PI * 2f;
                float t1 = ((i + 1) / (float)seg) * Mathf.PI * 2f;

                Vector3 o0 = new Vector3(Mathf.Cos(t0), Mathf.Sin(t0), 0f) * radius;
                Vector3 o1 = new Vector3(Mathf.Cos(t1), Mathf.Sin(t1), 0f) * radius;

                Debug.DrawLine(A + o0, A + o1, c, seconds);
                Debug.DrawLine(B + o0, B + o1, c, seconds);
            }
        }

        private static float Cross2D(Vector3 u, Vector3 v) => u.x * v.y - u.y * v.x;

        private static bool OnSegment2D(Vector3 a, Vector3 b, Vector3 p)
        {
            return p.x >= Mathf.Min(a.x, b.x) - 1e-6f &&
                   p.x <= Mathf.Max(a.x, b.x) + 1e-6f &&
                   p.y >= Mathf.Min(a.y, b.y) - 1e-6f &&
                   p.y <= Mathf.Max(a.y, b.y) + 1e-6f;
        }

        private static void UpdateAircraftHitboxDebug(AircraftUnit target, float radiusWorld, bool show)
        {
            if (target == null) return;
            var dbg = target.GetComponentInChildren<Zone5.CollisionDebugView>(true);
            if (dbg == null) return;

            dbg.SetRadiusWorld(radiusWorld);
            dbg.SetVisible(show);
        }

    }
}
