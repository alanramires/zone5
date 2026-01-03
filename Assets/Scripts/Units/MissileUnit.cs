// MissileUnit.cs
using System.Collections.Generic;
using UnityEngine;

namespace Zone5
{
    public class MissileUnit : MonoBehaviour
    {
        [Header("Data (assigned at runtime)")]
        [SerializeField] private MissileProfile missileData;

        [Header("Runtime")]
        public int teamId;
        public string missileInstanceId;   // ex: "LRM_00012_T0"
        public string ownerUnitId;         // unitId do caça que disparou

        [Header("Refs")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        public Transform ExhaustL;
        public Transform ExhaustR;
        [SerializeField] private Transform exhaustAnchor; // opcional (meio)
        [SerializeField] private Transform noseAnchor;

        [Header("Trails")]
        public List<LineRenderer> trailSegments = new List<LineRenderer>();
        public List<LineRenderer> debugTrailSegments = new List<LineRenderer>();

        private void Reset()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void ApplyMissileData(MissileProfile data, Color teamColor, string ownerId, int team, int serial = 0)
        {
            missileData = data;
            teamId = team;
            ownerUnitId = ownerId;

            if (data == null)
            {
                Debug.LogError("[MissileUnit] missileData is null.");
                return;
            }

            if (spriteRenderer != null && data.spriteWorld != null)
            {
                spriteRenderer.sprite = data.spriteWorld;

                if (data.tintWithTeam)
                    spriteRenderer.color = Color.Lerp(Color.white, teamColor, Mathf.Clamp01(data.tintStrength));
                else
                    spriteRenderer.color = Color.white;
            }

            if (string.IsNullOrEmpty(missileInstanceId))
            {
                string baseId = string.IsNullOrEmpty(data.missileId) ? "MISSILE" : data.missileId;
                missileInstanceId = $"{baseId}_{serial:00000}_T{teamId}";
            }

            gameObject.name = missileInstanceId;
        }

        public MissileProfile Data => missileData;

        public void AddTrail(LineRenderer lr)
        {
            if (lr == null) return;
            if (trailSegments == null) trailSegments = new List<LineRenderer>();
            trailSegments.Add(lr);
        }

        public void AddDebugTrail(LineRenderer lr)
        {
            if (lr == null) return;
            if (debugTrailSegments == null) debugTrailSegments = new List<LineRenderer>();
            debugTrailSegments.Add(lr);
        }

        public void ClearTrails()
        {
            if (trailSegments == null) return;

            for (int i = trailSegments.Count - 1; i >= 0; i--)
            {
                var lr = trailSegments[i];
                if (lr != null)
                    Destroy(lr.gameObject);
            }

            trailSegments.Clear();
        }

        public void ClearDebugTrails()
        {
            if (debugTrailSegments == null) return;

            for (int i = debugTrailSegments.Count - 1; i >= 0; i--)
            {
                var lr = debugTrailSegments[i];
                if (lr != null)
                    Destroy(lr.gameObject);
            }

            debugTrailSegments.Clear();
        }

        public void Die()
        {
            ClearTrails();
            ClearDebugTrails();
            Destroy(gameObject);
        }

        public Transform ExhaustAnchor => exhaustAnchor != null ? exhaustAnchor : transform;
        public Transform NoseAnchor => noseAnchor != null ? noseAnchor : transform;

        /// 1 FU no míssil = (NoseAnchor -> ExhaustAnchor). Igual ao caça.
        public float GetFUWorld()
        {
            if (NoseAnchor == null || ExhaustAnchor == null) return 1f;
            return Mathf.Max(0.01f, Vector3.Distance(NoseAnchor.position, ExhaustAnchor.position));
        }

        public Vector3 GetForward()
        {
            if (NoseAnchor != null && ExhaustAnchor != null)
            {
                Vector3 v = NoseAnchor.position - ExhaustAnchor.position;
                v.z = 0f;
                if (v.sqrMagnitude > 0.000001f) return v.normalized;
            }
            return transform.up.normalized;
        }
    }
}
