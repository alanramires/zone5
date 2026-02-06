using UnityEngine;
using TMPro;
using UnityEngine.UI;

// --- Esse script precisa existir no seu projeto ---
// Coloque num arquivo: Scripts/Panels/PilotRowView.cs
// (se voce ja tem algo assim, pode ignorar e so ajustar a referencia no Panel_Tatical)

public class PilotRowView : MonoBehaviour
{
    [Header("Columns")]
    [SerializeField] private TMP_Text colId;
    [SerializeField] private TMP_Text colName;
    [SerializeField] private TMP_Text colStatus;
    [SerializeField] private Image colPlane;
    [SerializeField] private Image colIcon;

    [Header("Status Icons")]
    [SerializeField] private Sprite iconTimer;
    [SerializeField] private Sprite iconCheck;
    [SerializeField] private Sprite iconDamage;
    [SerializeField] private Sprite iconLocked;

    private void Awake()
    {
        CacheRefsIfNeeded();
    }

    public void Setup(string rowId, string callsign, string unitProfileId, string status, Sprite planeSprite)
    {
        CacheRefsIfNeeded();

        if (colId != null) colId.text = rowId;
        if (colName != null)
        {
            string name = string.IsNullOrWhiteSpace(unitProfileId) ? callsign : $"{callsign}\n({unitProfileId})";
            colName.text = name;
        }
        if (colStatus != null) colStatus.text = status;
        if (colPlane != null) colPlane.sprite = planeSprite;
        SetStatusIcon(status);
    }

    public void SetColors(Color teamColor, bool isEliminated)
    {
        CacheRefsIfNeeded();

        Color textColor = isEliminated ? new Color(0.8f, 0.8f, 0.8f) : teamColor;
        if (colId != null) colId.color = textColor;
        if (colName != null) colName.color = textColor;
        if (colStatus != null) colStatus.color = textColor;
        if (colPlane != null) colPlane.color = textColor;
    }

    private void CacheRefsIfNeeded()
    {
        if (colId == null)
            colId = transform.Find("Col_Id")?.GetComponent<TMP_Text>();
        if (colName == null)
            colName = transform.Find("Col_Name")?.GetComponent<TMP_Text>();
        if (colStatus == null)
            colStatus = transform.Find("Col_Status")?.GetComponent<TMP_Text>();
        if (colPlane == null)
            colPlane = transform.Find("Col_Plane")?.GetComponent<Image>();
        if (colIcon == null)
            colIcon = transform.Find("Col_Icon")?.GetComponent<Image>();
    }

    private void SetStatusIcon(string status)
    {
        if (colIcon == null) return;

        switch (status)
        {
            case "Aguardando":
                colIcon.sprite = iconTimer;
                break;
            case "Pronto":
                colIcon.sprite = iconCheck;
                break;
            case "Eliminado":
                colIcon.sprite = iconDamage;
                break;
            case "Travado":
                colIcon.sprite = iconLocked;
                break;
            default:
                colIcon.sprite = null;
                break;
        }
    }
}
