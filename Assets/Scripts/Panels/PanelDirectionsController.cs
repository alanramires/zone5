using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zone5
{
    public class PanelDirectionsController : MonoBehaviour
    {
        public enum DirectionCardKind
        {
            BreakLeft,
            Left,
            Forward,
            Right,
            BreakRight
        }

        [Serializable]
        public class DirectionCard
        {
            public DirectionCardKind kind;
            public RectTransform root;
            public Image image;
        }

        [Header("Refs")]
        public PanelStanceController stancePanel;
        public Image currentStanceImage;
        public Sprite flightModeSprite;
        public Sprite afterBurnerSprite;
        public Sprite lowAcrobaticsSprite;
        public Sprite highAcrobaticsSprite;

        [Serializable]
        public class DirectionSpriteSet
        {
            public Sprite breakLeft;
            public Sprite left;
            public Sprite forward;
            public Sprite right;
            public Sprite breakRight;
        }

        [Header("Direction Sprites")]
        public DirectionSpriteSet flightSprites;
        public DirectionSpriteSet afterBurnerSprites;
        public DirectionSpriteSet lowAcrobaticsSprites;
        public DirectionSpriteSet highAcrobaticsSprites;

        [Header("Cards (Left to Right)")]
        public DirectionCard[] cards;

        [Header("Layout")]
        public float spacing = 0f;
        public bool autoStepFromCardWidth = true;
        public float xStep = 120f;
        public bool stackFromLeft = true;
        public bool centerAlign = true;

        [Header("Runtime")]
        public Stance activeStance = Stance.FlightMode;

        private void Awake()
        {
            if (stancePanel != null)
                stancePanel.OnStanceChanged += HandleStanceChanged;

            AutoBindRoots();
        }

        private void Start()
        {
            ApplyStance(activeStance);
        }

        private void OnDestroy()
        {
            if (stancePanel != null)
                stancePanel.OnStanceChanged -= HandleStanceChanged;
        }

        private void HandleStanceChanged(Stance stance)
        {
            ApplyStance(stance);
        }

        public void ApplyStance(Stance stance)
        {
            activeStance = stance;
            UpdateCurrentStanceImage();
            UpdateDirectionSprites();
            ApplyDirectionVisibility();
            ApplyCardPositions();
        }

        private void UpdateCurrentStanceImage()
        {
            if (currentStanceImage == null) return;
            currentStanceImage.sprite = activeStance switch
            {
                Stance.FlightMode => flightModeSprite,
                Stance.AfterBurner => afterBurnerSprite,
                Stance.LowAcrobatics => lowAcrobaticsSprite,
                Stance.HighAcrobatics => highAcrobaticsSprite,
                _ => currentStanceImage.sprite
            };
        }

        private void ApplyDirectionVisibility()
        {
            if (cards == null) return;
            bool flight = activeStance == Stance.FlightMode;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null || c.root == null) continue;
                bool show = flight || (c.kind == DirectionCardKind.Left || c.kind == DirectionCardKind.Forward || c.kind == DirectionCardKind.Right);
                c.root.gameObject.SetActive(show);
            }
        }

        private void ApplyCardPositions()
        {
            if (cards == null) return;

            float step = xStep;
            if (autoStepFromCardWidth)
            {
                for (int i = 0; i < cards.Length; i++)
                {
                    var c = cards[i];
                    if (c != null && c.root != null)
                    {
                        float w = c.root.rect.width;
                        if (w > 1f)
                        {
                            step = w + spacing;
                            break;
                        }
                    }
                }
            }

            int index = 0;
            int visibleCount = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null || c.root == null) continue;
                if (!c.root.gameObject.activeSelf) continue;
                visibleCount++;
            }

            float startX = 0f;
            if (centerAlign && visibleCount > 0)
            {
                float totalWidth = (visibleCount - 1) * step;
                startX = -totalWidth * 0.5f;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null || c.root == null) continue;
                if (!c.root.gameObject.activeSelf) continue;

                float x = (stackFromLeft ? 1f : -1f) * index * step;
                x += startX;
                c.root.anchoredPosition = new Vector2(x, c.root.anchoredPosition.y);
                index++;
            }
        }

        private void UpdateDirectionSprites()
        {
            if (cards == null) return;
            var set = activeStance switch
            {
                Stance.FlightMode => flightSprites,
                Stance.AfterBurner => afterBurnerSprites,
                Stance.LowAcrobatics => lowAcrobaticsSprites,
                Stance.HighAcrobatics => highAcrobaticsSprites,
                _ => flightSprites
            };

            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                if (c.image == null) continue;
                c.image.sprite = c.kind switch
                {
                    DirectionCardKind.BreakLeft => set.breakLeft,
                    DirectionCardKind.Left => set.left,
                    DirectionCardKind.Forward => set.forward,
                    DirectionCardKind.Right => set.right,
                    DirectionCardKind.BreakRight => set.breakRight,
                    _ => c.image.sprite
                };
            }
        }

        private void AutoBindRoots()
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                if (c.root == null)
                    c.root = FindRootForKind(c.kind);
                if (c.root != null && c.image == null)
                    c.image = c.root.GetComponent<Image>();
            }
        }

        private RectTransform FindRootForKind(DirectionCardKind kind)
        {
            string name = kind switch
            {
                DirectionCardKind.BreakLeft => "Dir_BL",
                DirectionCardKind.Left => "Dir_L",
                DirectionCardKind.Forward => "Dir_F",
                DirectionCardKind.Right => "Dir_R",
                DirectionCardKind.BreakRight => "Dir_BR",
                _ => null
            };
            if (string.IsNullOrEmpty(name)) return null;
            var t = transform.Find(name);
            return t != null ? t.GetComponent<RectTransform>() : null;
        }
    }
}
