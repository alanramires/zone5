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

        [Header("Bounds")]
        public bool clampToBounds = true;
        public Collider2D boundsCollider;
        public Renderer boundsRenderer;

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

            if (clampToBounds)
                ClampCameraToBounds();
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

            targetCamera.transform.position += move;
        }

        private void ClampCameraToBounds()
        {
            Bounds bounds;
            if (boundsCollider != null)
                bounds = boundsCollider.bounds;
            else if (boundsRenderer != null)
                bounds = boundsRenderer.bounds;
            else
                return;

            float halfH = targetCamera.orthographicSize;
            float halfW = halfH * targetCamera.aspect;

            Vector3 pos = targetCamera.transform.position;

            float minX = bounds.min.x + halfW;
            float maxX = bounds.max.x - halfW;
            float minY = bounds.min.y + halfH;
            float maxY = bounds.max.y - halfH;

            if (minX > maxX)
                pos.x = bounds.center.x;
            else
                pos.x = Mathf.Clamp(pos.x, minX, maxX);

            if (minY > maxY)
                pos.y = bounds.center.y;
            else
                pos.y = Mathf.Clamp(pos.y, minY, maxY);

            targetCamera.transform.position = new Vector3(pos.x, pos.y, targetCamera.transform.position.z);
        }
    }
}
