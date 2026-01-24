using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zone5;

public class PanelPilot : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text txtCallsign;
    [SerializeField] private Image imgAircraft;
    [SerializeField] private TMP_Text txtHpQty;
    [SerializeField] private TMP_Text txtFuelQty;
    [SerializeField] private TMP_Text txtMissileCount;
    [SerializeField] private TMP_Text txtGunCount;
    [SerializeField] private Image fuelFill;

    [Header("Blocks")]
    [SerializeField] private List<Image> hpBlocks = new();
    [SerializeField] private List<Image> missileBlocks = new();
    [SerializeField] private List<Image> gunBlocks = new();

    [Header("Behavior")]
    [SerializeField] private bool hideEmptyMissiles = false;
    [SerializeField] private bool hideEmptyGuns = false;
    [SerializeField] private float inactiveAlpha = 0.25f;
    [SerializeField] private int defaultGunAmmo = 4;

    [Header("Refs")]
    [SerializeField] private TurnManager turnManager;

    private readonly List<Color> _hpColors = new();
    private readonly List<Color> _missileColors = new();
    private readonly List<Color> _gunColors = new();

    private AircraftUnit _lastUnit;
    private bool _isBound;
    private float _nextRefreshTime;

    private void Awake()
    {
        CacheColors(hpBlocks, _hpColors);
        CacheColors(missileBlocks, _missileColors);
        CacheColors(gunBlocks, _gunColors);

        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
    }

    private void OnEnable()
    {
        BindTurnManager();

        Refresh();
    }

    private void OnDisable()
    {
        UnbindTurnManager();
    }

    private void HandleTurnUpdate()
    {
        Refresh();
    }

    private void Update()
    {
        if (turnManager == null)
            BindTurnManager();

        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + 0.25f;

        if (NeedsRuntimeRefresh())
            Refresh();
    }

    private void Refresh()
    {
        var unit = ResolveCurrentUnit();
        if (unit == null)
        {
            SetEmpty();
            return;
        }

        _lastUnit = unit;
        ApplyUnit(unit);
    }

    private void BindTurnManager()
    {
        if (_isBound) return;
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager == null) return;

        turnManager.OnStateChanged += HandleTurnUpdate;
        turnManager.OnSheetChanged += HandleTurnUpdate;
        _isBound = true;
    }

    private void UnbindTurnManager()
    {
        if (!_isBound) return;
        if (turnManager != null)
        {
            turnManager.OnStateChanged -= HandleTurnUpdate;
            turnManager.OnSheetChanged -= HandleTurnUpdate;
        }
        _isBound = false;
    }

    private bool NeedsRuntimeRefresh()
    {
        if (_lastUnit == null) return true;
        if (_lastUnit.UnitData == null) return true;
        if (_lastUnit.currentHp <= 0) return true;
        return false;
    }

    private AircraftUnit ResolveCurrentUnit()
    {
        var alive = GetAliveUnitsSorted();
        if (alive.Count == 0)
            return null;

        if (turnManager == null || turnManager.sheet == null)
            return alive[0];

        var phase = turnManager.sheet.phase;
        if (IsInputPhase(phase))
        {
            for (int i = 0; i < alive.Count; i++)
            {
                var unit = alive[i];
                if (unit == null) continue;

                var row = turnManager.GetOrCreatePlayerRow(unit.playerId);
                if (row == null) continue;

                if (IsPendingForPhase(row, phase))
                    return unit;
            }
        }

        if (_lastUnit != null && _lastUnit.currentHp > 0)
            return _lastUnit;

        return alive[0];
    }

    private static bool IsInputPhase(GameEnum.TurnState phase)
    {
        return phase == GameEnum.TurnState.SelectManeuver
            || phase == GameEnum.TurnState.DeclareWeapon
            || phase == GameEnum.TurnState.SelectMissileProfile;
    }

    private static bool IsPendingForPhase(TurnSheet.PlayerRow row, GameEnum.TurnState phase)
    {
        if (row == null || !row.isAlive) return false;

        switch (phase)
        {
            case GameEnum.TurnState.SelectManeuver:
                return !row.maneuverReady;
            case GameEnum.TurnState.DeclareWeapon:
                return !row.weaponReady;
            case GameEnum.TurnState.SelectMissileProfile:
                if (MvpRules.SanitizeWeapon(row.weaponCode) != "M")
                    return false;
                return !row.missileReady;
            default:
                return false;
        }
    }

    private void ApplyUnit(AircraftUnit unit)
    {
        string callsign = !string.IsNullOrWhiteSpace(unit.callSign) ? unit.callSign : unit.unitId;
        if (txtCallsign != null)
            txtCallsign.text = $"Callsign: \"{callsign}\"";

        if (imgAircraft != null)
        {
            if (unit.UnitData != null && unit.UnitData.spriteDefault != null)
                imgAircraft.sprite = unit.UnitData.spriteDefault;
            else if (unit.visualSprite != null)
                imgAircraft.sprite = unit.visualSprite.sprite;

            imgAircraft.color = GameEnum.GameColors.GetColorForTeam(unit.teamId);
        }

        int maxHp = unit.UnitData != null ? unit.UnitData.maxHp : hpBlocks.Count;
        int curHp = Mathf.Clamp(unit.currentHp, 0, maxHp);
        if (txtHpQty != null) txtHpQty.text = $"{curHp}/{maxHp}";
        ApplyBlocks(hpBlocks, _hpColors, curHp, maxHp, false);

        int maxFuel = unit.UnitData != null ? unit.UnitData.maxFuel : 0;
        int curFuel = Mathf.Clamp(unit.currentFuel, 0, maxFuel);
        if (txtFuelQty != null) txtFuelQty.text = $"{curFuel}/{maxFuel}";
        if (fuelFill != null)
            fuelFill.fillAmount = maxFuel > 0 ? (float)curFuel / maxFuel : 0f;

        int maxMissiles = unit.UnitData != null ? unit.UnitData.missilesMax : missileBlocks.Count;
        int curMissiles = Mathf.Clamp(unit.currentMissiles, 0, maxMissiles);
        if (txtMissileCount != null) txtMissileCount.text = $"x{curMissiles}";
        ApplyBlocks(missileBlocks, _missileColors, curMissiles, maxMissiles, hideEmptyMissiles);

        int maxGun = unit.UnitData != null ? unit.UnitData.gunAmmoMax : defaultGunAmmo;
        if (maxGun <= 0) maxGun = defaultGunAmmo;
        int curGun = unit.currentGunAmmo > 0 ? unit.currentGunAmmo : maxGun;

        if (unit.UnitData != null && unit.UnitData.vulcanUnlimited)
        {
            maxGun = Mathf.Max(maxGun, gunBlocks.Count);
            curGun = maxGun;
            if (txtGunCount != null) txtGunCount.text = "xINF";
        }
        else
        {
            if (txtGunCount != null) txtGunCount.text = $"x{curGun}";
        }
        ApplyBlocks(gunBlocks, _gunColors, curGun, maxGun, hideEmptyGuns);
    }

    private void SetEmpty()
    {
        if (txtCallsign != null) txtCallsign.text = "Callsign: \"---\"";
        if (txtHpQty != null) txtHpQty.text = "0/0";
        if (txtFuelQty != null) txtFuelQty.text = "0/0";
        if (txtMissileCount != null) txtMissileCount.text = "x0";
        if (txtGunCount != null) txtGunCount.text = "x0";

        ApplyBlocks(hpBlocks, _hpColors, 0, hpBlocks.Count, false);
        ApplyBlocks(missileBlocks, _missileColors, 0, missileBlocks.Count, true);
        ApplyBlocks(gunBlocks, _gunColors, 0, gunBlocks.Count, true);

        if (fuelFill != null) fuelFill.fillAmount = 0f;
    }

    private void ApplyBlocks(List<Image> blocks, List<Color> baseColors, int current, int max, bool hideEmpty)
    {
        if (blocks == null) return;

        int usableMax = max > 0 ? max : blocks.Count;
        for (int i = 0; i < blocks.Count; i++)
        {
            var img = blocks[i];
            if (img == null) continue;

            bool withinMax = i < usableMax;
            bool active = i < current;

            if (!withinMax)
            {
                img.enabled = false;
                continue;
            }

            if (hideEmpty && !active)
            {
                img.enabled = false;
                continue;
            }

            img.enabled = true;
            var c = (i < baseColors.Count) ? baseColors[i] : img.color;
            c.a = active ? 1f : inactiveAlpha;
            img.color = c;
        }
    }

    private static void CacheColors(List<Image> blocks, List<Color> target)
    {
        target.Clear();
        if (blocks == null) return;
        for (int i = 0; i < blocks.Count; i++)
        {
            target.Add(blocks[i] != null ? blocks[i].color : Color.white);
        }
    }

    private static List<AircraftUnit> GetAliveUnitsSorted()
    {
        var list = new List<AircraftUnit>();
        var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
        foreach (var u in units)
        {
            if (u != null && u.currentHp > 0)
                list.Add(u);
        }

        list.Sort((a, b) =>
        {
            int t = a.teamId.CompareTo(b.teamId);
            if (t != 0) return t;
            return a.playerId.CompareTo(b.playerId);
        });

        return list;
    }
}
