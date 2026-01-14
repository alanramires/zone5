using UnityEngine;

namespace Zone5
{
    public class CollisionDebugView : MonoBehaviour
    {
        [Header("Debug")]
        public bool show = false;                 // default false (como você quer)
        public SpriteRenderer view;              // arrasta o HitboxView aqui

        [Header("Shape")]
        [Tooltip("Raio em unidades de mundo (não FU).")]
        public float radiusWorld = 1f;

        [Tooltip("Se true, mostra como círculo (escala uniforme). Se false, pode virar cápsula/retângulo depois.")]
        public bool uniformScale = true;

        void Reset()
        {
            view = GetComponentInChildren<SpriteRenderer>();
        }

        void LateUpdate()
        {
            if (view == null) return;

            view.enabled = show;

            if (!show) return;

            // Assumindo que seu sprite é um círculo/quadrado "unitário" (1 unidade = diâmetro 1).
            // Então diâmetro = 2 * radius.
            float diameter = Mathf.Max(0.001f, radiusWorld * 2f);

            if (uniformScale)
                view.transform.localScale = new Vector3(diameter, diameter, 1f);
            else
                view.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        public void SetVisible(bool value) => show = value;

        public void SetRadiusWorld(float r) => radiusWorld = Mathf.Max(0.001f, r);
    }
}
