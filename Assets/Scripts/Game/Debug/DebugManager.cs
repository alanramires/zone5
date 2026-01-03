using UnityEngine;
using Zone5;

public class DebugManager : MonoBehaviour
{
    [Header("MASTER")]
    public bool toggleAllDebugsVisible = true;

    [Header("CUSTOM TOGGLES")]
    public bool aircraftHitbox = true;
    public bool missileHitbox = true;
    public bool trailHitboxAlongsidePath = true;
    public bool collisionDetectedCircle = true;

    public enum TrailHitboxMode
    {
        Expert, // same width as visual trail
        Hard,   // slightly thicker
        Basic   // larger tolerance
    }

    [Header("Trail Hitbox Mode")]
    public TrailHitboxMode trailHitboxMode = TrailHitboxMode.Expert;

    [Header("FUTURE")]
    public bool extendedTrailHitboxTolerance = false;

    [Header("MODE")]
    [Tooltip("Deixe TRUE durante o debug. Depois dá pra otimizar com Apply() só quando mudar.")]
    public bool applyEveryFrame = true;

    // Cache simples (pra não spammar Apply se você quiser desligar applyEveryFrame no futuro)
    bool _lastMaster, _lastA, _lastM, _lastT, _lastC, _lastExt;

    void Start()
    {
        Apply(true);
        CacheState();
    }

    void Update()
    {
        if (!applyEveryFrame)
        {
            if (HasStateChanged()) { Apply(false); CacheState(); }
            return;
        }

        Apply(false);
    }

    void CacheState()
    {
        _lastMaster = toggleAllDebugsVisible;
        _lastA = aircraftHitbox;
        _lastM = missileHitbox;
        _lastT = trailHitboxAlongsidePath;
        _lastC = collisionDetectedCircle;
        _lastExt = extendedTrailHitboxTolerance;
    }

    bool HasStateChanged()
    {
        return _lastMaster != toggleAllDebugsVisible
            || _lastA != aircraftHitbox
            || _lastM != missileHitbox
            || _lastT != trailHitboxAlongsidePath
            || _lastC != collisionDetectedCircle
            || _lastExt != extendedTrailHitboxTolerance;
    }

    [ContextMenu("Apply Now")]
    public void ApplyNow() => Apply(false);

    public float GetTrailHitboxWidthMultiplier()
    {
        switch (trailHitboxMode)
        {
            case TrailHitboxMode.Expert:
                return 1f;
            case TrailHitboxMode.Hard:
                return 1.25f;
            case TrailHitboxMode.Basic:
                return 2f;
            default:
                return 1f;
        }
    }

    void Apply(bool isBoot)
    {
        bool master = toggleAllDebugsVisible;

        bool showAircraftHB = master && aircraftHitbox;
        bool showMissileHB  = master && missileHitbox;
        bool showTrailHB    = master && trailHitboxAlongsidePath;
        bool showCollision  = master && collisionDetectedCircle;

        // =========================
        // AIRCRAFT: hitbox overlays
        // =========================
        var aircrafts = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
        foreach (var a in aircrafts)
        {
            if (!a) continue;

            // HitboxOverlay(s) do avião
            var overlays = a.GetComponentsInChildren<HitboxOverlay>(true);
            foreach (var hb in overlays) hb.SetVisible(showAircraftHB);

            // CollisionDebugView (círculo técnico)
            var circle = a.GetComponentInChildren<CollisionDebugView>(true);
            if (circle != null) circle.SetVisible(showCollision);
        }

        // =========================
        // MISSILES: hitbox + trails
        // =========================
        var missiles = FindObjectsByType<MissileUnit>(FindObjectsSortMode.None);
        foreach (var m in missiles)
        {
            if (!m) continue;

            // HitboxOverlay(s) do míssil
            var overlays = m.GetComponentsInChildren<HitboxOverlay>(true);
            foreach (var hb in overlays) hb.SetVisible(showMissileHB);

            // CollisionDebugView (círculo técnico)
            var circle = m.GetComponentInChildren<CollisionDebugView>(true);
            if (circle != null) circle.SetVisible(showCollision);

            // Trail debug (LineRenderer segments)
            if (m.debugTrailSegments != null)
            {
                float widthMul = GetTrailHitboxWidthMultiplier();
                Color dbgColor = new Color(1f, 0.5f, 0f, 0.5f);
                foreach (var lr in m.debugTrailSegments)
                {
                    if (lr != null) lr.enabled = showTrailHB;
                    if (lr != null)
                    {
                        lr.widthMultiplier = widthMul;
                        lr.startColor = dbgColor;
                        lr.endColor = dbgColor;
                        if (lr.material != null) lr.material.color = dbgColor;
                    }
                }
            }

            // FUTURE: extended trail tolerance (fica como “flag” central)
            // Aqui você só está guardando o estado.
            // Quando você implementar tolerance radius, você lê DebugManager.extendedTrailHitboxTolerance.
            // (não faz nada ainda)
        }

        // Se existir algum sistema antigo global de HB_Debug instanciado por outros scripts,
        // você pode forçar esconder aqui também procurando pelo nome:
        // (opcional, mas útil pra matar “lixo” legado)
        if (!master)
        {
            var hbDebug = GameObject.Find("HB_Debug");
            if (hbDebug != null) hbDebug.SetActive(false);
        }
    }
}
