using UnityEngine;
using UnityEngine.EventSystems;

namespace Zone5
{
    public class EventSystemSelectionLogger : MonoBehaviour
    {
        private GameObject _lastSelected;

        private void Update()
        {
            if (EventSystem.current == null) return;
            var current = EventSystem.current.currentSelectedGameObject;
            if (current == _lastSelected) return;
            _lastSelected = current;
            Debug.Log($"[EventSystem] Selected: {(current != null ? current.name : "null")}");
        }
    }
}
