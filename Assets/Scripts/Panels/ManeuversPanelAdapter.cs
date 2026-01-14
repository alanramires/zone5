using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zone5;

public class ManeuversPanelAdapter : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public int unitIdOverride;
        public TMP_InputField maneuverInput;
        public TMP_InputField weaponInput;
        public TMP_Text statusText;
        public Image checkConfirmed;
    }

    [Header("Slots")]
    [SerializeField] private List<Slot> slots = new();
    [SerializeField] private bool autoAssignUnitIds = true;

    private const string DefaultManeuver = "1G12";
    private const string DefaultWeapon = "X";

    private TurnManager turnManager;
    private readonly List<int> resolvedUnitIds = new();

    private void OnEnable()
    {
        BindTurnManager();
    }

    private void Start()
    {
        RefreshUnitIds();
    }

    public void SubmitManeuvers()
    {
        if (turnManager == null || turnManager.sheet == null) return;
        RefreshUnitIds();

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            var row = GetRowForSlot(i);
            if (row == null) continue;
            string text = slots[i].maneuverInput != null ? slots[i].maneuverInput.text : null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                row.maneuverRaw = MvpRules.SanitizeManeuver(text);
                row.maneuverReady = false;
                changed = true;
            }
        }

        if (changed)
            turnManager.NotifySheetChanged();
    }

    public void ConfirmManeuvers()
    {
        if (turnManager == null || turnManager.sheet == null) return;
        RefreshUnitIds();

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            var row = GetRowForSlot(i);
            if (row == null) continue;
            if (!row.maneuverReady)
            {
                if (string.IsNullOrWhiteSpace(row.maneuverRaw))
                    row.maneuverRaw = DefaultManeuver;
                row.maneuverRaw = MvpRules.SanitizeManeuver(row.maneuverRaw);
                row.maneuverReady = true;
                changed = true;
            }
        }

        if (changed)
            turnManager.NotifySheetChanged();

        turnManager.TryAdvance();
    }

    public void SubmitWeapons()
    {
        if (turnManager == null || turnManager.sheet == null) return;
        RefreshUnitIds();

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            var row = GetRowForSlot(i);
            if (row == null) continue;
            string text = slots[i].weaponInput != null ? slots[i].weaponInput.text : null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                row.weaponCode = MvpRules.SanitizeWeapon(text);
                row.weaponReady = false;
                changed = true;
            }
        }

        if (changed)
            turnManager.NotifySheetChanged();
    }

    public void ConfirmWeapons()
    {
        if (turnManager == null || turnManager.sheet == null) return;
        RefreshUnitIds();

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            var row = GetRowForSlot(i);
            if (row == null) continue;
            if (!row.weaponReady)
            {
                if (string.IsNullOrWhiteSpace(row.weaponCode))
                    row.weaponCode = DefaultWeapon;
                row.weaponCode = MvpRules.SanitizeWeapon(row.weaponCode);
                row.weaponReady = true;
                changed = true;
            }
        }

        if (changed)
            turnManager.NotifySheetChanged();

        turnManager.TryAdvance();
    }

    private void BindTurnManager()
    {
        turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager == null)
            Debug.LogWarning("[ManeuversPanelAdapter] TurnManager not found in scene.");
    }

    private void RefreshUnitIds()
    {
        resolvedUnitIds.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            int id = slots[i].unitIdOverride;
            resolvedUnitIds.Add(id);
        }

        if (!autoAssignUnitIds) return;

        var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
        var ordered = new List<AircraftUnit>(units.Length);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null) ordered.Add(units[i]);
        }

        ordered.Sort((a, b) =>
        {
            int team = a.teamId.CompareTo(b.teamId);
            if (team != 0) return team;
            return a.playerId.CompareTo(b.playerId);
        });

        for (int i = 0; i < slots.Count && i < ordered.Count; i++)
        {
            if (resolvedUnitIds[i] > 0) continue;
            resolvedUnitIds[i] = ordered[i].playerId;
        }
    }

    private int GetSlotUnitId(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= resolvedUnitIds.Count) return -1;
        return resolvedUnitIds[slotIndex];
    }

    private TurnSheet.PlayerRow GetRowForSlot(int slotIndex)
    {
        int unitId = GetSlotUnitId(slotIndex);
        if (unitId <= 0) return null;
        return turnManager != null ? turnManager.GetOrCreatePlayerRow(unitId) : null;
    }
}
