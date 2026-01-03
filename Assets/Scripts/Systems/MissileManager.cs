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

        [Header("Visual")]
        public Transform missilesRoot;
        public Color missileColor = Color.white;
        [Tooltip("Trail do míssil deve ser mais fino que o do caça.")]
        public float missileTrailWidth = 0.18f;

        [Header("Gameplay (MVP test)")]
        [Min(0.1f)] public float defaultRangeFU = 10f; // Phoenix / R-27
        public MissileProfile defaultProfile;          // opcional, se quiser usar profile já

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

        public void FireFromAircraft(AircraftUnit shooter, int teamId, string pathRaw = "M10F")
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

            // (MVP) coloca o missil no final
            Vector3 end = smoothPts[smoothPts.Length - 1];
            missile.transform.position += (end - start);
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
    }
}


