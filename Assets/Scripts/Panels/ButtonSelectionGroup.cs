using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zone5
{
    public class ButtonSelectionGroup : MonoBehaviour
    {
        public event System.Action<string> OnSelectionChanged;

        [System.Serializable]
        private class Option
        {
            public string id;
            public Button button;
        }

        [Header("Options")]
        [SerializeField] private List<Option> options = new List<Option>();
        [SerializeField] private int defaultIndex = 0;
        [SerializeField] private string defaultId = "";
        [SerializeField] private bool selectDefaultOnEnable = false;
        [SerializeField] private bool autoCollectButtons = true;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private string buttonNamePrefix = "Button_";

        [Header("Visuals")]
        [SerializeField] private bool applyGraphicColor = true;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.5f, 1f);
        [SerializeField] private bool applyTextStyle = true;
        [SerializeField] private Color normalTextColor = Color.black;
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private FontStyles normalTextStyle = FontStyles.Normal;
        [SerializeField] private FontStyles selectedTextStyle = FontStyles.Bold;

        public int CurrentIndex { get; private set; } = -1;
        public string CurrentId { get; private set; } = "";

        public Color NormalTextColor => normalTextColor;
            
        private readonly List<UnityAction> _handlers = new List<UnityAction>();

        private void OnEnable()
        {
            if (autoCollectButtons)
                CollectButtons();
            BindButtons();
            if (selectDefaultOnEnable)
                ApplyDefaultSelection();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        public void SelectById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] != null && options[i].id == id)
                {
                    SelectIndex(i, true);
                    return;
                }
            }
        }

        public void ApplyDefaultSelection()
        {
            if (!string.IsNullOrWhiteSpace(defaultId))
            {
                SelectById(defaultId);
                return;
            }
            if (options.Count > 0 && defaultIndex >= 0 && defaultIndex < options.Count)
                SelectIndex(defaultIndex, true);
        }

        public void ClearSelection()
        {
            Debug.Log($"[ButtonSelectionGroup] ClearSelection on {gameObject.name} (Count={options.Count})");
            for (int i = 0; i < options.Count; i++)
                ApplyVisual(options[i], false);
            CurrentIndex = -1;
            CurrentId = "";
            OnSelectionChanged?.Invoke(CurrentId);
        }

        public void SelectIndex(int index)
        {
            SelectIndex(index, false);
        }

        private void BindButtons()
        {
            _handlers.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                if (options[index] == null || options[index].button == null)
                {
                    _handlers.Add(null);
                    continue;
                }
                UnityAction handler = () => SelectIndex(index, true);
                _handlers.Add(handler);
                options[index].button.onClick.AddListener(handler);
                AutoAssignIdWithPrefix(options[index]);
            }
        }

        private void UnbindButtons()
        {
            for (int i = 0; i < options.Count && i < _handlers.Count; i++)
            {
                if (options[i] == null || options[i].button == null || _handlers[i] == null) continue;
                options[i].button.onClick.RemoveListener(_handlers[i]);
            }
            _handlers.Clear();
        }

        private void SelectIndex(int index, bool force)
        {
            if (index < 0 || index >= options.Count) return;
            if (!force && index == CurrentIndex) return;

            for (int i = 0; i < options.Count; i++)
                ApplyVisual(options[i], i == index);

            CurrentIndex = index;
            CurrentId = options[index] != null ? options[index].id : "";
            OnSelectionChanged?.Invoke(CurrentId);
        }

        private void ApplyVisual(Option option, bool selected)
        {
            if (option == null) return;
            if (applyGraphicColor)
            {
                var graphic = option.button != null ? option.button.targetGraphic : null;
                if (graphic != null)
                    graphic.color = selected ? selectedColor : normalColor;
            }

            if (applyTextStyle)
            {
                var label = option.button != null ? option.button.GetComponentInChildren<TMP_Text>(true) : null;
                if (label != null)
                {
                    label.color = selected ? selectedTextColor : normalTextColor;
                    label.fontStyle = selected ? selectedTextStyle : normalTextStyle;
                }
            }
        }

        private void AutoAssignIdWithPrefix(Option option)
        {
            if (option == null || option.button == null) return;
            if (!string.IsNullOrWhiteSpace(option.id)) return;
            string name = option.button.name;
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!string.IsNullOrEmpty(buttonNamePrefix) && name.StartsWith(buttonNamePrefix))
                option.id = name.Substring(buttonNamePrefix.Length);
            else
                option.id = name;
        }

        private void CollectButtons()
        {
            options.Clear();
            var buttons = GetComponentsInChildren<Button>(includeInactive);
            if (buttons == null || buttons.Length == 0) return;

            foreach (var button in buttons)
            {
                if (button == null) continue;
                string name = button.name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!string.IsNullOrEmpty(buttonNamePrefix) && !name.StartsWith(buttonNamePrefix)) continue;
                var option = new Option { button = button };
                AutoAssignIdWithPrefix(option);
                options.Add(option);
            }
        }
    }
}
