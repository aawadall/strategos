// OrbitRig.cs
// Yaw / pitch / distance around a pivot, for the 3D drape preview.
//
// Not a reuse of DemoCamera: that is an orthographic 2D pan/zoom rig and this is a
// perspective orbit. The shared idea is zoom feeling the same at every scale, which here
// means panning proportionally to distance.

using UnityEngine;

namespace Strategos.UI
{
    public sealed class OrbitRig
    {
        // Below about 8 degrees the drape is edge-on and reads as a line; above 85 the orbit
        // gimbals as forward approaches straight down.
        private const float MinPitch = 8f;
        private const float MaxPitch = 85f;

        private float _diagonal = 1f;

        public Vector3 Pivot { get; set; }
        public float Distance { get; set; } = 10f;
        public float Yaw { get; set; }
        public float Pitch { get; set; } = 35f;

        /// <summary>
        /// Frames <paramref name="bounds"/>: pivot at its centre, looking down from the
        /// south-west at a shallow enough angle to read relief.
        /// </summary>
        public void Frame(Bounds bounds)
        {
            _diagonal = Mathf.Max(1f, bounds.size.magnitude);
            Pivot = bounds.center;
            Yaw = 30f;
            Pitch = 35f;
            Distance = _diagonal * 1.1f;
        }

        public void Orbit(Vector2 deltaPixels)
        {
            Yaw += deltaPixels.x * 0.3f;
            Pitch = Mathf.Clamp(Pitch - deltaPixels.y * 0.3f, MinPitch, MaxPitch);
        }

        public void Zoom(float steps)
        {
            Distance = Mathf.Clamp(Distance * Mathf.Pow(0.88f, steps),
                _diagonal * 0.05f, _diagonal * 3f);
        }

        /// <summary>
        /// Slides the pivot in the camera's own screen plane, scaled by distance so a drag
        /// moves the ground by the same apparent amount at every zoom.
        /// </summary>
        public void Pan(Vector2 deltaPixels)
        {
            Quaternion rot = Rotation;
            Vector3 right = rot * Vector3.right;

            // Ground-projected forward, so panning tracks the terrain rather than drifting
            // up into the sky as the pitch flattens.
            Vector3 forward = rot * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();

            float scale = Distance * 0.0015f;
            Pivot -= right * (deltaPixels.x * scale) + forward * (deltaPixels.y * scale);
        }

        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);

        public Vector3 Position => Pivot - Rotation * Vector3.forward * Distance;

        public void Apply(Transform t)
        {
            t.rotation = Rotation;
            t.position = Position;
        }
    }
}
