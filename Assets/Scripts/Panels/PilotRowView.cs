using UnityEngine;
using TMPro;

// --- Esse script precisa existir no seu projeto ---
// Coloque num arquivo: Scripts/Panels/PilotRowView.cs
// (se voce ja tem algo assim, pode ignorar e so ajustar a referencia no Panel_Tatical)

public class PilotRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text txt;

    public void SetText(string value)
    {
        if (txt != null) txt.text = value;
    }

    public void Setup(string indexStr, string callsign, string aircraft, int hp, string status)
    {
        // Format: "1: Maverick (F-14) HP: 3 [Pronto]"
        string content = $"{indexStr}: {callsign} ({aircraft}) HP: {hp} [{status}]";
        SetText(content);
    }
}
