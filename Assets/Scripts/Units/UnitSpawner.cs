using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public class UnitSpawner : MonoBehaviour
    {
        [Serializable]
        public class TeamConfig
        {
            public string teamName = "Team";
            public int teamId = 0;
            public UnitProfile unitData;
            public int count = 2;
            public Vector2 basePos = Vector2.zero;
            public Vector2 spawnDirection = Vector2.right; // (1,0) = right, (-1,0) = left
            public List<string> pilotNames;
        }

        [Header("One Prefab to rule them all")]
        public AircraftUnit aircraftPrefab;

        [Header("Teams Configuration")]
        public List<TeamConfig> teams = new List<TeamConfig>();

        [Header("Trail")]
        public TrailManager trailManager;
        public float spawnTrailLengthFU = 1.5f;

        [Header("Common Settings")]
        [Tooltip("Distance from center (0,0) for each team. Total distance = 2x this value.")]
        public float startDistanceFromCenter = 10f;
        public float rowSpacingY = 4f;

        [Header("Auto")]
        public bool spawnOnStart = true;
        public bool clearPrevious = true;

        private int nextPlayerId = 1;

        [Header("Callsigns")]
        [SerializeField] private string[] defaultCallsigns =
        {
            "Maverick", "Iceman", "Viper", "Goose", "Slider",
            "Woodsman", "Spirit", "Ghost Striker", "Razor", "Foxhound",
            "Jester", "Sundown", "Hollywood", "Wolfman", "Cougar"
        };

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        [ContextMenu("Spawn Now")]
        public void Spawn()
        {
            if (clearPrevious) ClearChildren();
            nextPlayerId = 1;

            if (teams == null || teams.Count == 0)
            {
                SetupMvpDefaults();
            }

            if (aircraftPrefab == null)
            {
                Debug.LogError("[UnitSpawner] aircraftPrefab not assigned.");
                return;
            }

            foreach (var team in teams)
            {
                SpawnTeam(team);
            }
            
            // Panel updates itself via events now
        }

        private void SetupMvpDefaults()
        {
            teams = new List<TeamConfig>();

            // TEAM 0 (Left, USA, Facing Right)
            var t0 = new TeamConfig();
            t0.teamName = "USA";
            t0.teamId = 0;
            t0.count = 3; 
            t0.basePos = new Vector2(-35f, 0f); // ~Left side (Far Edge)
            t0.spawnDirection = Vector2.right;
            // Default pilot names?
            // "Maverick", "Iceman", "Viper" handled by default fallback if list empty
            // But let's add them explicit if user wants "pilotos participantes"
            t0.pilotNames = new List<string> { "Maverick", "Iceman", "Goose" };
            // F-14 logic handled by Spawner assigning prefab if unitData is null (or create dummy data)
            // But we need unitData not to be null for spawn to work... 
            // We'll fix SpawnTeam to handle null unitData by using a default internal profile
            teams.Add(t0);

            // TEAM 1 (Right, USSR/Opfor, Facing Left)
            var t1 = new TeamConfig();
            t1.teamName = "OPFOR";
            t1.teamId = 1;
            t1.count = 3;
            t1.basePos = new Vector2(35f, 0f); // ~Right side (Far Edge)
            t1.spawnDirection = Vector2.left;
            t1.pilotNames = new List<string> { "Uri", "Boris", "Dimitri" };
            teams.Add(t1);
        }

        private void SpawnTeam(TeamConfig config)
        {
            // Determine FU Factor from prefab
            float fuFactor = 1f;
            if (aircraftPrefab != null)
            {
                // Instantiate a dummy to measure FU correctly in world space
                // (Prefab transform.position might not reflect world spacing correctly if scaled)
                var dummy = Instantiate(aircraftPrefab, Vector3.zero, Quaternion.identity);
                dummy.gameObject.SetActive(false); 
                dummy.gameObject.hideFlags = HideFlags.HideAndDontSave; // Prevent scene searches finding it easily
                fuFactor = MovementCore.GetFUWorld(dummy);
                
                // Cleanup dummy immediately - ALWAYS to prevent ghost
                if (Application.isPlaying) Destroy(dummy.gameObject); // Destroy is delayed...
                DestroyImmediate(dummy.gameObject); // Force kill now
            }

            // Restore MVP Defaults if Inspector values are Zero (Auto-Layout)
            if (config.basePos == Vector2.zero && config.spawnDirection == Vector2.zero)
            {
                float dist = startDistanceFromCenter * fuFactor;

                if (config.teamId == 0) // USA / Blue / Left
                {
                    config.basePos = new Vector2(-dist, 0f);
                    config.spawnDirection = Vector2.right;
                }
                else if (config.teamId == 1) // OPFOR / Red / Right
                {
                    config.basePos = new Vector2(dist, 0f);
                    config.spawnDirection = Vector2.left;
                }
            }

            // Fallback for null unitData (generic F-14)
            if (config.unitData == null)
            {
                config.unitData = ScriptableObject.CreateInstance<UnitProfile>();
                config.unitData.unitName = "F-14";
                config.unitData.maxHp = 5;
                config.unitData.maxFuel = 20;
            }

            // Formation Logic (Simple Wedge/Line based on user description)
            // User ASCII:
            // High Y:   --->  (Recessed X)
            // Mid Y:   -------> (Advanced X)
            // Low Y:    --->  (Recessed X)
            // This is a Wedge formation.
            
            float spacingY = rowSpacingY;
            float spacingX = 2f; // Offset X for wedge

            // Center Y is basePos.y
            // i=0 (Top?), i=1 (Mid?), i=2 (Bot?)
            // If count=3:
            // Unit 0: y=+4, x=-2
            // Unit 1: y=0,  x=0 (Lead)
            // Unit 2: y=-4, x=-2
            
            // Generic offset calculator relative to basePos and direction
            Vector3 side = new Vector3(-config.spawnDirection.y, config.spawnDirection.x, 0f); // Left of direction
            Vector3 fwd = new Vector3(config.spawnDirection.x, config.spawnDirection.y, 0f); // Forward

            // We center the formation around basePos
            // Let's assume indices are: 0=Lead(Center), 1=LeftWing, 2=RightWing...
            // Or just iterating linear -> user wants specific visual
            
            // Let's map linear index i to visual position
            // 0 -> Mid (Lead)
            // 1 -> Top (+Y)
            // 2 -> Bot (-Y)

            // Or if we stick to the loop 0..count
            // 0 -> Top
            // 1 -> Mid
            // 2 -> Bot
            // Then X offsets:
            // 0: -X
            // 1: 0
            // 2: -X
            
            float startY = config.basePos.y + (config.count - 1) * spacingY * 0.5f;
            Color teamColor = GameEnum.GameColors.GetColorForTeam(config.teamId);

            // 1. Calculate relative positions (Zig-Zag / Center-Out)
            // Pattern: 0=Center, 1=Up, 2=Down, 3=Up, 4=Down...
            var relativePositions = new System.Collections.Generic.List<Vector2>();
            
            for (int i = 0; i < config.count; i++)
            {
                float yOffsetSteps = 0f;
                if (i > 0)
                {
                    int step = (i + 1) / 2;
                    float sign = (i % 2 != 0) ? 1f : -1f; // Odd=Up(+), Even=Down(-)
                    yOffsetSteps = step * sign;
                }

                float localY = yOffsetSteps * spacingY;
                // X depends on how far we are from center Y (wedge shape)
                float localX = -Mathf.Abs(yOffsetSteps) * spacingX;
                
                relativePositions.Add(new Vector2(localX, localY));
            }
            
            // 2. (Optimization removed: Zig-Zag naturally handles center leader)

            // 3. Spawn Loop
            for (int i = 0; i < config.count; i++)
            {
                Vector2 rel = relativePositions[i];
                
                // Rotated to world
                // Need to use config.basePos.y as center, plus rel.y
                Vector3 pos = (Vector3)config.basePos;
                pos.y += rel.y; // Apply Y offset 
                pos += fwd * rel.x; // Apply X offset (Wedge)

                var unit = Instantiate(aircraftPrefab, pos, Quaternion.identity, transform);

                int playerId = nextPlayerId++;
                if (config.pilotNames != null && i < config.pilotNames.Count)
                    unit.callSign = config.pilotNames[i];
                else 
                    unit.callSign = GetDefaultCallsign(playerId); // Changed from teamName-i
                
                // unit.callSign fallback if GetDefaultCallsign returns empty (it shouldn't)
                if (string.IsNullOrWhiteSpace(unit.callSign))
                    unit.callSign = $"{config.teamName}-{i + 1}";
                unit.unitId = $"{config.unitData.unitName}_P{playerId}_T{config.teamId}";

                Color softColor = GetSoftTeamColor(teamColor);
                unit.ApplyUnitData(config.unitData, softColor, playerId, config.teamId);
                
                // Init History (Turn 0)
                unit.maneuverHistory.Add(new AircraftUnit.FlightLog 
                { 
                    TurnIndex = 0, 
                    ManeuverCode = "1G18", 
                    RawInput = "SPAWN", 
                    GForce = 1, 
                    Speed = 1.0f 
                });

                Debug.Log($"Spawned {unit.unitId} callSign={unit.callSign} team={config.teamId} playerId={playerId}");

                // Register to TurnManager so UI updates
                var tm = FindFirstObjectByType<TurnManager>();
                if (tm != null)
                {
                    tm.RegisterUnit(unit);
                }

                // Orient
                Vector3 desired = config.spawnDirection;
                if (desired.sqrMagnitude < 0.01f) desired = Vector3.right;
                OrientUnit(unit, desired);

                CreateSpawnTrail(unit, teamColor);
            }
        }

        private void CreateSpawnTrail(AircraftUnit unit, Color teamColor)
        {
            if (trailManager == null || unit == null) return;

            Vector3 exhaust = unit.ExhaustAnchor != null ? unit.ExhaustAnchor.position : unit.transform.position;
            Vector3 nosePos = unit.NoseAnchor != null ? unit.NoseAnchor.position : unit.transform.up;

            Vector3 forward = (nosePos - exhaust);
            float forwardLen = forward.magnitude;

            if (forwardLen < 0.0001f)
            {
                forward = unit.transform.up;
            }
            else
            {
                forward /= forwardLen;
            }

            float fuWorld = MovementCore.GetFUWorld(unit);
            Vector3 start = exhaust - forward * (spawnTrailLengthFU * fuWorld);

            Color strongColor = GetStrongTeamColor(teamColor);
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

        private string GetDefaultCallsign(int playerId)
        {
            if (defaultCallsigns == null || defaultCallsigns.Length == 0)
                return $"Pilot {playerId}";

            int index = Mathf.Abs(playerId - 1) % defaultCallsigns.Length;
            string name = defaultCallsigns[index];
            return string.IsNullOrWhiteSpace(name) ? $"Pilot {playerId}" : name;
        }

        private static void OrientUnit(AircraftUnit unit, Vector3 desiredForward)
        {
            if (unit == null) return;
            desiredForward.z = 0f;
            desiredForward.Normalize();

            Vector3 currentForward = MovementCore.GetForward(unit);
            currentForward.z = 0f;

            if (currentForward.sqrMagnitude < 0.000001f)
                currentForward = unit.transform.up;

            currentForward.Normalize();

            Quaternion rotDelta = Quaternion.FromToRotation(currentForward, desiredForward);
            unit.transform.rotation = rotDelta * unit.transform.rotation;
        }
    }
}
