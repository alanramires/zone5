using UnityEngine;
using UnityEngine.EventSystems;

namespace Zone5
{
    public class PanelStanceCardClick : MonoBehaviour, IPointerClickHandler
    {
        public PanelStanceController controller;
        public Stance stance = Stance.FlightMode;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (controller == null) return;
            controller.SetStance(stance);
        }
    }
}
