using UnityEngine;

namespace Zone5
{
    public class UnitSpawner : MonoBehaviour
    {
        [Header("One Prefab to rule them all")]
        public AircraftUnit aircraftPrefab;

        [Header("UnitData per team (MVP)")]
        public UnitProfile unitDataBlue; // F-14 Tomcat
        public UnitProfile unitDataRed;  // MiG-29 Fulcrum

        [Header("Trail")]
        public TrailManager trailManager;
        public float spawnTrailLengthFU = 1.5f;

        [Header("Counts")]
        [Min(1)] public int blueCount = 2;
        [Min(1)] public int redCount = 2;

        [Header("Spawn Layout")]
        public float teamSeparationX = 38f;
        public float rowSpacingY = 4f;
        public float blueYOffset = 0f;
        public float redYOffset = 0f;

        [Header("Auto")]
        public bool spawnOnStart = true;
        public bool clearPrevious = true;

        private int nextPlayerId = 1; // MVP: 1..N

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        [ContextMenu("Spawn Now")]
        public void Spawn()
        {
            if (clearPrevious) ClearChildren();
            nextPlayerId = 1;

            if (aircraftPrefab == null)
            {
                Debug.LogError("[UnitSpawner] aircraftPrefab not assigned.");
                return;
            }
            if (unitDataBlue == null || unitDataRed == null)
            {
                Debug.LogError("[UnitSpawner] unitDataBlue/unitDataRed not assigned.");
                return;
            }

            SpawnTeam(
                count: blueCount,
                basePos: new Vector2(-teamSeparationX * 0.5f, blueYOffset),
                teamId: 0,
                zRotation: 270f,
                color: GameEnum.GameColors.TeamBlue,
                unitData: unitDataBlue
            );

            SpawnTeam(
                count: redCount,
                basePos: new Vector2(+teamSeparationX * 0.5f, redYOffset),
                teamId: 1,
                zRotation: 90f,
                color: GameEnum.GameColors.TeamRed,
                unitData: unitDataRed
            );
        }

        private void SpawnTeam(
            int count,
            Vector2 basePos,
            int teamId,
            float zRotation,
            Color color,
            UnitProfile unitData
        )
        {
            float totalHeight = (count - 1) * rowSpacingY;
            float startY = basePos.y + totalHeight * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = new Vector3(basePos.x, startY - i * rowSpacingY, 0f);

                var unit = Instantiate(aircraftPrefab, pos, Quaternion.identity, transform);

                int playerId = nextPlayerId++;
                unit.unitId = $"{unitData.unitName}_P{playerId}_T{teamId}";

                Color softColor = GetSoftTeamColor(color);
                unit.ApplyUnitData(unitData, softColor, playerId, teamId);

                unit.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);

                CreateSpawnTrail(unit, color);
            }
        }

        // Creates a spawn trail effect for the given unit.
       private void CreateSpawnTrail(AircraftUnit unit, Color teamColor)
        {
            if (trailManager == null || unit == null) return;

            // Ponto final do rastro = bunda do caça
            Vector3 exhaust = unit.ExhaustAnchor.position;

            // Direção do avião baseada em anchors (independe de sprite apontar pra cima/baixo)
            Vector3 nosePos = unit.NoseAnchor.position;

            Vector3 forward = (nosePos - exhaust);
            float forwardLen = forward.magnitude;

            // Se anchors não estiverem setados direito, fallback pro transform.up
            if (forwardLen < 0.0001f)
            {
                forward = unit.transform.up;
                forwardLen = 1f; // evita zero
            }
            else
            {
                forward /= forwardLen; // normalize
            }

            // 1 FU = comprimento do caça (nariz -> bunda)
            // então 3 FU = 3 caças
            float fuWorld = Mathf.Max(0.01f, Vector3.Distance(unit.NoseAnchor.position, unit.ExhaustAnchor.position));

            // Start do rastro = 3 FU pra trás a partir da bunda
            Vector3 start = exhaust - forward * (spawnTrailLengthFU * fuWorld);

            // Cor forte do time para o rastro (se você já tem helper, usa ele)
            Color strongColor = GetStrongTeamColor(teamColor);

            // Cria o segmento persistente (e registra no unit pra limpar quando morrer)
            trailManager.CreateSegment(unit, start, exhaust, strongColor);
        }

        private static Color GetSoftTeamColor(Color baseColor)
        {
            return Color.Lerp(Color.white, baseColor, 0.85f);
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

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
            #if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
            #else
                Destroy(child.gameObject);
            #endif
            }
        }
    }
}
