// DemoCamera.cs
// Top-down orthographic camera controller for the symbol demo scene.
//
// Controls:
//   Scroll wheel          — zoom in/out
//   Right-click drag      — pan
//   Middle-click drag     — pan (alternate)
//   R                     — reset to default position and zoom

using UnityEngine;

namespace Strategos.Demo
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class DemoCamera : MonoBehaviour
    {
        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed    = 4f;
        [SerializeField] private float _minOrthoSize = 2f;
        [SerializeField] private float _maxOrthoSize = 60f;

        [Header("Pan")]
        [SerializeField] private float _panSensitivity = 1f;

        [Header("Defaults (set by SceneBootstrapper)")]
        [SerializeField] private Vector3 _defaultPosition = new(0f, 0f, -20f);
        [SerializeField] private float   _defaultOrthoSize = 14f;

        // -------------------------------------------------------------------------
        // Private state
        // -------------------------------------------------------------------------

        private Camera  _cam;
        private Vector3 _panAnchor;          // world-space position at drag start
        private bool    _isPanning;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        private void Start()
        {
            ResetView();
        }

        private void Update()
        {
            HandleZoom();
            HandlePan();
            HandleReset();
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Called by SceneBootstrapper to set where the camera should rest by default.
        /// </summary>
        public void SetDefaults(Vector3 worldCenter, float orthoSize)
        {
            _defaultPosition = new Vector3(worldCenter.x, worldCenter.y, -20f);
            _defaultOrthoSize = orthoSize;
        }

        public void ResetView()
        {
            transform.position      = _defaultPosition;
            _cam.orthographicSize   = _defaultOrthoSize;
        }

        // -------------------------------------------------------------------------
        // Input handlers
        // -------------------------------------------------------------------------

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            // Zoom toward cursor position in world space.
            Vector3 cursorWorldBefore = _cam.ScreenToWorldPoint(Input.mousePosition);

            _cam.orthographicSize = Mathf.Clamp(
                _cam.orthographicSize - scroll * _zoomSpeed,
                _minOrthoSize, _maxOrthoSize);

            // Shift camera so the cursor stays at the same world point.
            Vector3 cursorWorldAfter = _cam.ScreenToWorldPoint(Input.mousePosition);
            transform.position += cursorWorldBefore - cursorWorldAfter;
        }

        private void HandlePan()
        {
            bool panButtonDown = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            bool panButton     = Input.GetMouseButton(1)     || Input.GetMouseButton(2);
            bool panButtonUp   = Input.GetMouseButtonUp(1)   || Input.GetMouseButtonUp(2);

            if (panButtonDown)
            {
                _panAnchor = ScreenToWorld(Input.mousePosition);
                _isPanning = true;
            }

            if (panButton && _isPanning)
            {
                Vector3 current = ScreenToWorld(Input.mousePosition);
                transform.position += (_panAnchor - current) * _panSensitivity;
            }

            if (panButtonUp)
                _isPanning = false;
        }

        private void HandleReset()
        {
            if (Input.GetKeyDown(KeyCode.R))
                ResetView();
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private Vector3 ScreenToWorld(Vector3 screenPos)
        {
            var wp = _cam.ScreenToWorldPoint(screenPos);
            wp.z = transform.position.z;
            return wp;
        }
    }
}
