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

        public int currentHp;
        public int currentFuel;
        public int currentMissiles;

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

            if (data == null)
            {
                Debug.LogError("[AircraftUnit] unitData is null.");
                return;
            }

            if (spriteRenderer != null && data.spriteDefault != null)
            {
                spriteRenderer.sprite = data.spriteDefault;
                spriteRenderer.color = teamColor;
            }

            currentHp = data.maxHp;
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

        public void Die()
        {
            ClearTrails();
            Destroy(gameObject);
        }

        public Transform ExhaustAnchor => exhaustAnchor != null ? exhaustAnchor : transform;
        public Transform NoseAnchor => noseAnchor != null ? noseAnchor : transform;
    }
}
