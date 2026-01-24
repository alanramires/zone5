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
        public int currentGunAmmo;
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
        public Transform visualRoot;
        public SpriteRenderer visualSprite;
        public Transform ExhaustL;
        public Transform ExhaustR;
        [SerializeField] private Transform exhaustAnchor; // opcional (meio)
        [SerializeField] private Transform noseAnchor;

        [Header("Trails")]
        public List<LineRenderer> trailSegments = new List<LineRenderer>();
        private Quaternion _visualBaseRotation = Quaternion.identity;
        private Vector3 _visualBaseScale = Vector3.one;
        private bool _hasVisualBase;

        private void Reset()
        {
            visualSprite = GetComponentInChildren<SpriteRenderer>(true);
            if (visualSprite != null && visualSprite.transform != transform)
                visualRoot = visualSprite.transform;
        }

        private void Awake()
        {
            EnsureVisualRefs();
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

            EnsureVisualRefs();
            if (visualSprite != null)
            {
                if (data.spriteDefault != null)
                    visualSprite.sprite = data.spriteDefault;
                visualSprite.color = teamColor;
            }

            var box = GetComponent<BoxCollider2D>();
            if (box != null && data.colliderSize.x > 0f && data.colliderSize.y > 0f)
            {
                box.size = data.colliderSize;
                box.offset = data.colliderOffset;
            }

            currentHp = Mathf.Max(1, data.maxHp);
            currentFuel = data.maxFuel;
            currentMissiles = data.missilesMax;
            currentGunAmmo = data.gunAmmoMax;

            if (string.IsNullOrEmpty(unitId))
            {
                unitId = $"{data.unitName}_P{playerId}_T{teamId}";
            }
            gameObject.name = unitId;
        }

        public void ApplyVisualPose(float rollXDeg, float rollYDeg, Vector2 scale, Color tint)
        {
            EnsureVisualRefs();
            if (visualRoot != null)
            {
                if (!_hasVisualBase)
                    CaptureVisualBase();
                Quaternion vfxRotation = Quaternion.AngleAxis(rollYDeg, Vector3.up)
                    * Quaternion.AngleAxis(rollXDeg, Vector3.right);
                visualRoot.localRotation = _visualBaseRotation * vfxRotation;
                visualRoot.localScale = Vector3.Scale(_visualBaseScale, new Vector3(scale.x, scale.y, 1f));
            }
            if (visualSprite != null)
                visualSprite.color = tint;
        }

        public void ResetVisualPose(Color tintNormal)
        {
            EnsureVisualRefs();
            if (visualRoot != null)
            {
                if (!_hasVisualBase)
                    CaptureVisualBase();
                visualRoot.localRotation = _visualBaseRotation;
                visualRoot.localScale = _visualBaseScale;
            }
            if (visualSprite != null)
                visualSprite.color = tintNormal;
        }

        private void EnsureVisualRefs()
        {
            if (visualSprite != null && (visualRoot != null || visualSprite.transform == transform)) return;

            SpriteRenderer childSprite = null;
            var sprites = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].transform != transform)
                {
                    childSprite = sprites[i];
                    break;
                }
            }

            if (visualSprite == null)
            {
                if (childSprite != null)
                    visualSprite = childSprite;
                else
                    visualSprite = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (visualRoot == null && childSprite != null)
                visualRoot = childSprite.transform;

            if (visualRoot == null && visualSprite != null && visualSprite.transform == transform)
                CreateRuntimeVisualChild(visualSprite);

            if (!_hasVisualBase && visualRoot != null)
                CaptureVisualBase();
        }

        private void CreateRuntimeVisualChild(SpriteRenderer source)
        {
            if (source == null) return;

            var go = new GameObject("VisualRoot");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.color = source.color;
            sr.material = source.sharedMaterial;
            sr.sortingLayerName = source.sortingLayerName;
            sr.sortingOrder = source.sortingOrder;
            sr.flipX = source.flipX;
            sr.flipY = source.flipY;
            sr.drawMode = source.drawMode;
            sr.size = source.size;
            sr.maskInteraction = source.maskInteraction;
            sr.spriteSortPoint = source.spriteSortPoint;
            sr.enabled = source.enabled;

            source.enabled = false;

            visualRoot = go.transform;
            visualSprite = sr;
        }

        private void CaptureVisualBase()
        {
            if (visualRoot == null) return;
            _visualBaseRotation = visualRoot.localRotation;
            _visualBaseScale = visualRoot.localScale;
            _hasVisualBase = true;
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
            // if (visualSprite != null) visualSprite.enabled = false; 
            
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
