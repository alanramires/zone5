using UnityEngine;
using Zone5;

public class PanelPhaseController : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField] private TurnManager turnManager;

    [Header("Panels")]
    [SerializeField] private GameObject panelManche;
    [SerializeField] private GameObject panelThrottle;
    [SerializeField] private GameObject panelWeapons;

    [Header("HUD Elements")]
    [SerializeField] private GameObject hudManeuverSelected;

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
    }

    private void OnEnable()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged += UpdatePanelVisibility;
            // Also listen to SheetChanged just in case, but StateChanged should be enough for phase switching
            // turnManager.OnSheetChanged += Refresh; 
        }
        UpdatePanelVisibility();
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.OnStateChanged -= UpdatePanelVisibility;
        }
    }

    private void UpdatePanelVisibility()
    {
        if (turnManager == null || turnManager.sheet == null) return;

        var phase = turnManager.sheet.phase;

        switch (phase)
        {
            case GameEnum.TurnState.SelectManeuver:
            case GameEnum.TurnState.WaitManeuverConfirm:
                SetPanels(manche: true, throttle: true, weapons: false);
                break;

            case GameEnum.TurnState.DeclareWeapon:
            case GameEnum.TurnState.WaitWeaponDeclare:
                SetPanels(manche: false, throttle: false, weapons: true);
                break;

            default:
                // For resolution phases (Reveal, Resolve, etc.), you might want to hide everything
                // to let the user watch the board animations.
                SetPanels(manche: false, throttle: false, weapons: false);
                break;
        }
    }

    private void SetPanels(bool manche, bool throttle, bool weapons)
    {
        SetPanelState(panelManche, manche);
        SetPanelState(panelThrottle, throttle);
        SetPanelState(panelWeapons, weapons);

        if (hudManeuverSelected != null)
        {
            if (hudManeuverSelected.activeSelf != manche)
                hudManeuverSelected.SetActive(manche);
        }
    }

    private void SetPanelState(GameObject panel, bool active)
    {
        if (panel == null) return;

        // Ensure it is VISIBLE (Active) always, as requested
        if (!panel.activeSelf) panel.SetActive(true);

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            // Auto-fix: Add CanvasGroup if missing so we can control interaction
            Debug.Log($"[PanelPhaseController] Adding missing CanvasGroup to '{panel.name}' to control interactivity.");
            cg = panel.AddComponent<CanvasGroup>();
        }

        if (cg != null)
        {
            // Only toggle interactivity
            cg.interactable = active;
            cg.blocksRaycasts = active;
            
            // Debug the state change
            // Debug.Log($"[PanelPhaseController] Set '{panel.name}' Interactable={active}");
        }
    }
}
