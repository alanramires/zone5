using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public class AircraftUnit : MonoBehaviour
    {
        [Header("Data (assigned at runtime)")]
        [SerializeField] private UnitProfile unitData;

        [Header("Runtime")]
        public int teamId;
        public string unitId;
        public string callSign;
        public int playerId;

        public int currentHp;
        public int currentFuel;
        public int currentMissiles;
        [System.Serializable]
        public struct FlightLog
        {
            public int TurnIndex;
            public string ManeuverCode; // "3G06"
            public float Speed;         // 1.0
            public int GForce;          // 3
            public string RawInput;     // "448"
        }

        public List<FlightLog> maneuverHistory = new List<FlightLog>();
        public string LastManeuver => (maneuverHistory != null && maneuverHistory.Count > 0) ? maneuverHistory[maneuverHistory.Count - 1].ManeuverCode : "";

        [Header("Refs")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        public Transform ExhaustL;
        public Transform ExhaustR;
        [SerializeField] private Transform exhaustAnchor; // opcional (meio)
        [SerializeField] private Transform noseAnchor;

        [Header("Trails")]
        public List<LineRenderer> trailSegments = new List<LineRenderer>();

        private void Reset()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void ApplyUnitData(UnitProfile data, Color teamColor, int playerId, int team)
        {
            unitData = data;
            teamId = team;
            this.playerId = playerId;

            if (data == null)
            {
                Debug.LogError("[AircraftUnit] unitData is null.");
                return;
            }

            if (spriteRenderer != null)
            {
                if (data.spriteDefault != null)
                    spriteRenderer.sprite = data.spriteDefault;
                
                spriteRenderer.color = teamColor;
            }

            var box = GetComponent<BoxCollider2D>();
            if (box != null && data.colliderSize.x > 0f && data.colliderSize.y > 0f)
            {
                box.size = data.colliderSize;
                box.offset = data.colliderOffset;
            }

            currentHp = MvpRules.IsMvp ? 1 : data.maxHp;
            currentFuel = data.maxFuel;
            currentMissiles = data.missilesMax;

            if (string.IsNullOrEmpty(unitId))
            {
                unitId = $"{data.unitName}_P{playerId}_T{teamId}";
            }
            gameObject.name = unitId;
        }

        public void AddTrail(LineRenderer lr)
        {
            if (lr == null) return;
            if (trailSegments == null) trailSegments = new List<LineRenderer>();
            trailSegments.Add(lr);
        }

        public void ClearTrails()
        {
            if (trailSegments == null) return;

            for (int i = trailSegments.Count - 1; i >= 0; i--)
            {
                var lr = trailSegments[i];
                if (lr != null)
                {
                    Destroy(lr.gameObject);
                }
            }

            trailSegments.Clear();
        }

        public Dictionary<string, int> roundDamageReceived = new Dictionary<string, int>();
        public bool isDestroyed = false;

        public void RegisterDamage(string shooter, int dmg)
        {
            if (!roundDamageReceived.ContainsKey(shooter))
                roundDamageReceived[shooter] = 0;
            roundDamageReceived[shooter] += dmg;
        }

        public void Die()
        {
            // Do NOT destroy immediately. Just mark as dead/destroyed so logic can continue (overkill).
            isDestroyed = true;
            
            // Visual feedback: 
            // User requested to keep the token visible for screenshots (wreckage).
            // if (spriteRenderer != null) spriteRenderer.enabled = false; 
            
            // Keep trails? Or clear? 
            // ClearTrails(); // Maybe keep trails for the replay/validation? Let's leave them.
        }

        public void Cleanup()
        {
             ClearTrails();
             if (gameObject != null) Destroy(gameObject);
        }

        public Transform ExhaustAnchor => exhaustAnchor != null ? exhaustAnchor : transform;
        public Transform NoseAnchor => noseAnchor != null ? noseAnchor : transform;
        public UnitProfile UnitData => unitData;
    }
}
