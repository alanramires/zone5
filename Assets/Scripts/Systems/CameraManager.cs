using UnityEngine;
using UnityEngine.InputSystem;

namespace Zone5
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Refs")]
        public Camera targetCamera;

        [Header("Zoom")]
        public float minSize = 2f;
        public float maxSize = 17f;
        public float zoomSpeed = 1.5f;

        [Header("Pan")]
        public float dragSpeed = 1f;

        private Vector2 lastMousePos;
        private bool isDragging;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void Update()
        {
            if (targetCamera == null) return;

            HandleZoom();
            HandlePan();
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f)) return;

            float planeZ = -targetCamera.transform.position.z;
            Vector3 mouseScreen = mouse.position.ReadValue();
            mouseScreen.z = planeZ;
            Vector3 worldBefore = targetCamera.ScreenToWorldPoint(mouseScreen);

            float size = targetCamera.orthographicSize;
            float nextSize = Mathf.Clamp(size - scroll * zoomSpeed, minSize, maxSize);
            if (Mathf.Approximately(nextSize, size)) return;

            targetCamera.orthographicSize = nextSize;

            Vector3 worldAfter = targetCamera.ScreenToWorldPoint(mouseScreen);
            Vector3 delta = worldBefore - worldAfter;
            targetCamera.transform.position += delta;
        }

        private void HandlePan()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastMousePos = mouse.position.ReadValue();
            }

            if (mouse.rightButton.wasReleasedThisFrame)
                isDragging = false;

            if (!isDragging) return;

            Vector2 mousePos = mouse.position.ReadValue();
            Vector2 delta = mousePos - lastMousePos;
            lastMousePos = mousePos;

            float unitsPerPixel = (targetCamera.orthographicSize * 2f) / Screen.height;
            Vector3 move = new Vector3(-delta.x * unitsPerPixel, -delta.y * unitsPerPixel, 0f);

            if (dragSpeed != 1f)
                move *= dragSpeed;

            transform.position += move;
        }
    }
}
