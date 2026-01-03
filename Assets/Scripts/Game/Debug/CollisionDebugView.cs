using UnityEngine;

namespace Zone5
{
    public class CollisionDebugView : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float radiusWorld = 1f;

        public void SetRadiusWorld(float radius)
        {
            radiusWorld = Mathf.Max(0f, radius);
            ApplyScale();
        }

        public void SetVisible(bool show)
        {
            gameObject.SetActive(show);
        }

        private void Awake()
        {
            ApplyScale();
        }

        private void ApplyScale()
        {
            Transform root = visualRoot != null ? visualRoot : transform;
            float d = radiusWorld * 2f;
            root.localScale = new Vector3(d, d, 1f);
        }
    }
}
