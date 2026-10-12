using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Zone5
{
    public class PanelStanceController : MonoBehaviour
    {
        [Serializable]
        public class StanceCard
        {
            public Stance stance;
            public RectTransform root;
            public Image highlight;
        }

        [Header("Cards (Top to Bottom)")]
        public StanceCard[] cards;

        [Header("Layout")]
        [Tooltip("Vertical step between cards (e.g. half the card height).")]
        public float yStep = 70f;
        public float activeXOffset = 40f;
        public float inactiveXOffset = 0f;
        public bool stackFromBottom = true;
        public float expandedAlpha = 1f;
        public float collapsedAlpha = 0.5f;

        [Header("Motion")]
        public bool animate = true;
        public float slideSpeed = 12f;

        [Header("Runtime")]
        public Stance activeStance = Stance.FlightMode;

        [Header("Tooltip (Optional)")]
        public TMP_Text tooltipText;
        public Image tooltipBackground;
        public bool tintTooltipToActive = true;

        public event Action<Stance> OnStanceChanged;

        private void Awake()
        {
            AutoBindRoots();
            AutoBindClicks();
            AutoInferStep();
            ApplyVisuals();
        }

        private void OnValidate()
        {
            if (yStep < 1f) yStep = 1f;
            if (slideSpeed < 0f) slideSpeed = 0f;
        }

        private void Update()
        {
            ApplyCardPositions();
        }

        public void SetStance(Stance stance)
        {
            if (activeStance == stance) return;
            activeStance = stance;
            ApplyVisuals();
            OnStanceChanged?.Invoke(activeStance);
        }

        private void AutoBindRoots()
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null || c.root != null) continue;
                var child = transform.childCount > i ? transform.GetChild(i) : null;
                if (child != null)
                    c.root = child.GetComponent<RectTransform>();
            }
        }

        private void AutoInferStep()
        {
            if (yStep > 1f) return;
            if (cards == null || cards.Length == 0) return;
            if (cards[0] == null || cards[0].root == null) return;
            float h = cards[0].root.rect.height;
            if (h > 1f) yStep = h * 0.5f;
        }

        private void AutoBindClicks()
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null || c.root == null) continue;
                var click = c.root.GetComponent<PanelStanceCardClick>();
                if (click == null) click = c.root.gameObject.AddComponent<PanelStanceCardClick>();
                click.controller = this;
                click.stance = c.stance;
            }
        }

        private void ApplyCardPositions()
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null || c.root == null) continue;
                bool isActive = c.stance == activeStance;

                float x = isActive ? activeXOffset : inactiveXOffset;
                float y = (stackFromBottom ? 1f : -1f) * i * yStep;
                Vector2 target = new Vector2(x, y);

                if (animate)
                {
                    Vector2 cur = c.root.anchoredPosition;
                    c.root.anchoredPosition = Vector2.Lerp(cur, target, Time.unscaledDeltaTime * slideSpeed);
                }
                else
                {
                    c.root.anchoredPosition = target;
                }
            }
        }

        private void ApplyVisuals()
        {
            if (cards == null) return;
            Image activeHighlight = null;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                bool isActive = c.stance == activeStance;

                if (c.highlight != null)
                {
                    var col = c.highlight.color;
                    col.a = isActive ? expandedAlpha : collapsedAlpha;
                    c.highlight.color = col;
                    if (isActive)
                        activeHighlight = c.highlight;
                }
            }
            UpdateTooltip(activeHighlight);
        }

        private void UpdateTooltip(Image activeHighlight)
        {
            if (tooltipText != null)
                tooltipText.text = $"STANCE: {GetStanceLabel(activeStance)}";

            if (tintTooltipToActive && tooltipBackground != null && activeHighlight != null)
            {
                var c = activeHighlight.color;
                c.a = tooltipBackground.color.a;
                tooltipBackground.color = c;
            }
        }

        private static string GetStanceLabel(Stance stance)
        {
            return stance switch
            {
                Stance.FlightMode => "FLIGHT MODE",
                Stance.AfterBurner => "AFTER BURNER",
                Stance.LowAcrobatics => "LOW ACROBATICS",
                Stance.HighAcrobatics => "HIGH ACROBATICS",
                _ => stance.ToString()
            };
        }
    }
}
