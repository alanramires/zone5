using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zone5;

public class PanelWeaponsController : MonoBehaviour
{
    [Header("Weapons Buttons")]
    [SerializeField] private Button buttonMissile;
    [SerializeField] private Button buttonGun;
    [SerializeField] private Button buttonOff;
    [SerializeField] private Button buttonConfirm;

    [Header("Highlight / Group")]
    [SerializeField] private ButtonSelectionGroup selectionGroup;

    [Header("Refs")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private MatchControllerMvp matchController;

    private AircraftUnit _currentUnit;
    private string _selectedWeapon = ""; // "M", "G", "X"

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
        if (matchController == null)
            matchController = FindFirstObjectByType<MatchControllerMvp>();
        
        if (selectionGroup == null)
            selectionGroup = GetComponent<ButtonSelectionGroup>();
    }

    private void OnEnable()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged += Refresh;
            turnManager.OnSheetChanged += Refresh;
        }
        
        // Listen to group changes instead of button clicks directly if possible, 
        // OR keep button clicks but trigger group selection.
        // Group auto-collects buttons usually. Let's see if we should rely on Group events.
        // For simplicity, let's keep our bindings but TELL the group what to select.
        if (buttonMissile != null) buttonMissile.onClick.AddListener(OnMissileClicked);
        if (buttonGun != null) buttonGun.onClick.AddListener(OnGunClicked);
        if (buttonOff != null) buttonOff.onClick.AddListener(OnOffClicked);
        if (buttonConfirm != null) buttonConfirm.onClick.AddListener(OnConfirmClicked);

        Refresh();
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged -= Refresh;
            turnManager.OnSheetChanged -= Refresh;
        }

        if (buttonMissile != null) buttonMissile.onClick.RemoveListener(OnMissileClicked);
        if (buttonGun != null) buttonGun.onClick.RemoveListener(OnGunClicked);
        if (buttonOff != null) buttonOff.onClick.RemoveListener(OnOffClicked);
        if (buttonConfirm != null) buttonConfirm.onClick.RemoveListener(OnConfirmClicked);
    }

    [Header("Status Text")]
    [SerializeField] private TMPro.TMP_Text txtWeaponStatus;

    // ... existing Awake/Start ...

    private void Refresh()
    {
        if (turnManager == null || turnManager.sheet == null) return;
        
        // Only active in DeclareWeapon phase
        if (turnManager.sheet.phase != GameEnum.TurnState.DeclareWeapon)
        {
            SetButtonsInteractable(false, false, false, false);
            return;
        }

        // Resolve current unit
        _currentUnit = ResolvePendingUnit();
        if (_currentUnit == null)
        {
            SetButtonsInteractable(false, false, false, false);
            if (txtWeaponStatus != null) txtWeaponStatus.text = "Weapons: --";
            return;
        }

        // Enable buttons based on ammo
        bool canMissile = _currentUnit.currentMissiles > 0;
        bool canGun = _currentUnit.currentGunAmmo > 0 || (_currentUnit.UnitData != null && _currentUnit.UnitData.vulcanUnlimited);
        bool canOff = true; 

        if (_selectedWeapon == "M" && !canMissile) _selectedWeapon = "";
        if (_selectedWeapon == "G" && !canGun) _selectedWeapon = "";
        
        if (buttonMissile != null) buttonMissile.interactable = canMissile;
        if (buttonGun != null) buttonGun.interactable = canGun;
        if (buttonOff != null) buttonOff.interactable = canOff;

        // Restore selection visual
        if (selectionGroup != null)
        {
            if (_selectedWeapon == "M") selectionGroup.SelectById("Missile"); // Assumed ID based on name "Button_Missile" -> "Missile" if prefix "Button_"
            else if (_selectedWeapon == "G") selectionGroup.SelectById("Gun");
            else if (_selectedWeapon == "X") selectionGroup.SelectById("Off");
            else selectionGroup.ClearSelection();
        }

        UpdateStatusText();
        UpdateConfirmButton();
    }

    private void UpdateStatusText()
    {
        if (txtWeaponStatus == null) return;
        
        // "Weapons: Missile", "Weapons: Guns" (plural?), "Weapons: None"
        string status = "None"; // Default or Empty selection?
        if (_selectedWeapon == "M") status = "Missile";
        else if (_selectedWeapon == "G") status = "Guns";
        else if (_selectedWeapon == "X") status = "None";
        else status = "..."; // Waiting selection

        txtWeaponStatus.text = $"Weapons: {status}";
    }
    
    // ... existing helpers ...

    private void UpdateConfirmButton()
    {
        if (buttonConfirm == null) return;
        bool hasSelection = !string.IsNullOrEmpty(_selectedWeapon);
        buttonConfirm.interactable = hasSelection;
    }

    private void SetButtonsInteractable(bool m, bool g, bool o, bool c)
    {
        if (buttonMissile != null) buttonMissile.interactable = m;
        if (buttonGun != null) buttonGun.interactable = g;
        if (buttonOff != null) buttonOff.interactable = o;
        if (buttonConfirm != null) buttonConfirm.interactable = c;
    }

    private AircraftUnit ResolvePendingUnit()
    {
        if (turnManager.sheet == null || turnManager.sheet.rows == null) return null;

        var alive = GetAliveUnitsSorted();
        foreach (var unit in alive)
        {
             var row = turnManager.GetOrCreatePlayerRow(unit.playerId);
             if (row != null && row.isAlive && !row.weaponReady)
             {
                 return unit;
             }
        }
        return null;
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

    // --- Events ---

    private void OnMissileClicked()
    {
        _selectedWeapon = "M";
        if (selectionGroup != null) selectionGroup.SelectById("Missile");
        UpdateStatusText();
        UpdateConfirmButton();
    }

    private void OnGunClicked()
    {
        _selectedWeapon = "G";
        if (selectionGroup != null) selectionGroup.SelectById("Gun");
        UpdateStatusText();
        UpdateConfirmButton();
    }

    private void OnOffClicked()
    {
        _selectedWeapon = "X";
        if (selectionGroup != null) selectionGroup.SelectById("Off");
        UpdateStatusText();
        UpdateConfirmButton();
    }

    private void OnConfirmClicked()
    {
        if (string.IsNullOrEmpty(_selectedWeapon)) return;
        if (_currentUnit == null) return;

        if (matchController != null)
        {
            matchController.SubmitWeapon(_currentUnit.playerId, _selectedWeapon);
        }
        
        _selectedWeapon = "";
        if (selectionGroup != null) selectionGroup.ClearSelection();
        Refresh();
    }
}
