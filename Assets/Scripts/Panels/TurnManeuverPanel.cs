using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;
using Zone5;

[MovedFrom(true, "", "Assembly-CSharp", "PanelManeuver")]
public class TurnManeuverPanel : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text pilotText;
    [SerializeField] private TMP_InputField maneuverInput;
    [SerializeField] private Button confirmButton;

    [Header("Game Refs")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private MatchControllerMvp matchController;

    private List<AircraftUnit> pendingUnits = new List<AircraftUnit>();
    private int currentIndex = 0;

    private void Awake()
    {
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        if (matchController == null) matchController = FindFirstObjectByType<MatchControllerMvp>();
        
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);
    }

    private void OnEnable()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged += HandleStateChanged;
            turnManager.OnSheetChanged += HandleSheetChanged;
        }
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged -= HandleStateChanged;
            turnManager.OnSheetChanged -= HandleSheetChanged;
        }
    }

    private void Start()
    {
        // Initial check
        CheckPhaseAndSetup();
    }

    private void HandleStateChanged()
    {
        if (turnManager == null) return;
        CheckPhaseAndSetup();
    }

    private void HandleSheetChanged()
    {
        if (turnManager == null) return;
        
        // If we represent the current phase, check for late spawns
        if (IsActivePhase(turnManager.sheet.phase))
        {
            var currentUnits = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
            int aliveCount = 0;
            foreach(var u in currentUnits) if (u != null && u.currentHp > 0) aliveCount++;

            if (pendingUnits.Count != aliveCount || IsInvalidList())
            {
                RefreshListStayingSafe();
            }
        }
    }

    private bool IsActivePhase(GameEnum.TurnState phase)
    {
        return phase == GameEnum.TurnState.SelectManeuver || 
               phase == GameEnum.TurnState.DeclareWeapon ||
               phase == GameEnum.TurnState.SelectMissileProfile;
    }

    private void CheckPhaseAndSetup()
    {
        if (turnManager == null || turnManager.sheet == null) return;
        
        var phase = turnManager.sheet.phase;

        if (IsActivePhase(phase))
        {
            // Only reset if we are not already in a valid cycle for this phase?
            // Simpler strategy: if index == 0 and list empty/invalid, setup. 
            // BUT: switching phases requires a full setup.
            // How do we detect phase SWITCH vs update? 
            // We can check if pendingUnits is empty.
            // Or we assume HandleStateChanged only fires on change.
            SetupRound();
        }
        else
        {
            // Disable if not in relevant phase
            if (confirmButton != null) confirmButton.interactable = false;
            if (maneuverInput != null) maneuverInput.interactable = false;
            if (pilotText != null) pilotText.text = "Waiting Phase...";
        }
    }

    private void RefreshListStayingSafe()
    {
        // Capture current pilot ID if possible
        int currentPilotId = -1;
        if (currentIndex >= 0 && currentIndex < pendingUnits.Count && pendingUnits[currentIndex] != null)
             currentPilotId = pendingUnits[currentIndex].playerId;

        SetupRound(); // Rebuilds list and resets index to 0

        // Restore index if possible
        if (currentPilotId != -1)
        {
            for(int i=0; i<pendingUnits.Count; i++)
            {
                if (pendingUnits[i].playerId == currentPilotId)
                {
                    currentIndex = i;
                    break;
                }
            }
        }
        ShowCurrent();
    }

    private bool IsInvalidList()
    {
        if (pendingUnits == null) return true;
        foreach(var u in pendingUnits) if (u == null) return true;
        return false;
    }

    private void SetupRound()
    {
        pendingUnits.Clear();
        var units = FindObjectsByType<AircraftUnit>(FindObjectsSortMode.None);
        
        var list = new List<AircraftUnit>();
        foreach(var u in units) {
            // Only alive units need to input maneuvers
            if (u != null && u.currentHp > 0)
            {
                 // Filter for Missile Phase: Only those who declared 'M'
                if (turnManager != null && turnManager.sheet.phase == GameEnum.TurnState.SelectMissileProfile)
                {
                    var row = turnManager.GetOrCreatePlayerRow(u.playerId);
                    // Check if declared weapon is Missile
                    // Assuming "M" is the code. Use Sanitize to be safe.
                    if (row == null || MvpRules.SanitizeWeapon(row.weaponCode) != "M") 
                        continue;
                }
                
                list.Add(u);
            }
        }
        
        // Sort by Team then PlayerID to have a consistent order
        list.Sort((a,b) => {
            int t = a.teamId.CompareTo(b.teamId);
            if(t != 0) return t;
            return a.playerId.CompareTo(b.playerId);
        });

        pendingUnits = list;
        currentIndex = 0; // Always start from 0 on new phase setup
        
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        // For SelectMissileProfile, if list is empty, it means NO ONE selected missiles.
        // In that case, we should probably auto-advance? 
        // But for this UI panel, let's just show "No Missile Pilots" or similar.
        // Actually, TurnManager usually handles skip if AllReady returns true (which it does for empty list).
        // So the game might auto-advance.
        
        if (pendingUnits.Count == 0)
        {
            if (pilotText != null) pilotText.text = "Waiting...";
            if (maneuverInput != null) maneuverInput.interactable = false;
            if (confirmButton != null) confirmButton.interactable = false;
            return;
        }

        if (currentIndex < pendingUnits.Count)
        {
            var unit = pendingUnits[currentIndex];
            if (pilotText != null)
                pilotText.text = !string.IsNullOrEmpty(unit.callSign) ? unit.callSign : unit.unitId;
            
            if (maneuverInput != null)
            {
                maneuverInput.text = ""; 
                maneuverInput.interactable = true;
                maneuverInput.ActivateInputField();

                // Update placeholder based on Phase
                var placeholder = maneuverInput.placeholder as TMP_Text;
                if (placeholder != null && turnManager != null)
                {
                    if (turnManager.sheet.phase == GameEnum.TurnState.SelectManeuver)
                        placeholder.text = "Maneuver (e.g. 1G18)";
                    else if (turnManager.sheet.phase == GameEnum.TurnState.DeclareWeapon)
                        placeholder.text = "Weapon (M, X, N)";
                    else if (turnManager.sheet.phase == GameEnum.TurnState.SelectMissileProfile)
                        placeholder.text = "Profile (e.g. M1)";
                }
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = true;
                var btnText = confirmButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null && turnManager != null)
                {
                    if (turnManager.sheet.phase == GameEnum.TurnState.SelectManeuver)
                        btnText.text = "Confirm Maneuver";
                    else if (turnManager.sheet.phase == GameEnum.TurnState.DeclareWeapon)
                        btnText.text = "Confirm Weapon";
                    else if (turnManager.sheet.phase == GameEnum.TurnState.SelectMissileProfile)
                        btnText.text = "Confirm Profile";
                }
            }
        }
        else
        {
            // All players entered
            if (pilotText != null) pilotText.text = "All Pilots Ready";
            if (maneuverInput != null) maneuverInput.interactable = false;
            
            // "quando o ultimo digitar sua manobra o botao_confirmar desabilita"
            if (confirmButton != null) confirmButton.interactable = false;
        }
    }

    private void OnConfirm()
    {
        if (currentIndex >= pendingUnits.Count) return;
        if (turnManager == null || turnManager.sheet == null) return;

        string code = maneuverInput != null ? maneuverInput.text : "";
        if (string.IsNullOrWhiteSpace(code)) return; 

        var unit = pendingUnits[currentIndex];
        var initPhase = turnManager.sheet.phase; // Capture phase BEFORE
        
        Debug.Log($"[TurnManeuverPanel] Submitting for {unit.callSign}: {code} (Phase: {initPhase})");

        if (matchController != null)
        {
            if (initPhase == GameEnum.TurnState.SelectManeuver)
                matchController.SubmitManeuver(unit.playerId, code);
            else if (initPhase == GameEnum.TurnState.DeclareWeapon)
                matchController.SubmitWeapon(unit.playerId, code);
            else if (initPhase == GameEnum.TurnState.SelectMissileProfile)
                matchController.SubmitMissileProfile(unit.playerId, code);
        }

        // Check if phase changed during submission (Synchronous event)
        if (turnManager.sheet.phase != initPhase)
        {
            // Phase changed! HandleStateChanged has already reset index to 0.
            // Do NOT increment. Do NOT refresh.
            Debug.Log($"[TurnManeuverPanel] Phase changed to {turnManager.sheet.phase}. preventing index increment.");
            return;
        }

        currentIndex++;
        ShowCurrent();
    }
}
