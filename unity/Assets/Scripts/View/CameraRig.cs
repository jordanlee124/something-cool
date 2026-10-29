using EmberVale.Sim;
using UnityEngine;

namespace EmberVale.View
{
    /// <summary>
    /// The map camera: a fixed tilt, a point on the ground it looks at and a distance. By default it glides to a target
    /// the game supplies (the follow camera). Player input takes over: WASD / arrows and mouse-drag or one-finger drag
    /// pan, mouse wheel and two-finger pinch zoom. After a few idle seconds, or on F, it glides back to following.
    /// Input is read with the legacy Input Manager (Project Settings > Player > Active Input Handling = Input Manager).
    /// </summary>
    public sealed class CameraRig
    {
        const float Pitch = 70f;
        const float IdleResume = 6f;     // seconds without input before the follow camera takes over again
        const float MinDist = 5f;
        const float KeyPanSpeed = 0.7f;  // map widths of view per second, so panning feels the same zoomed in or out
        const float WheelZoom = 0.12f;   // per wheel notch

        Vector3 centre;
        float dist;
        bool ready;
        float manualUntil;               // Time.time until which the player is in charge

        public bool Manual => Time.time < manualUntil;

        /// <summary>
        /// Call once per frame. autoCentre/autoDist is where the follow camera wants to be; fullDist is how far back
        /// shows the whole map and is the zoom-out limit.
        /// </summary>
        public void Update(Camera cam, World map, Vector3 autoCentre, float autoDist, float fullDist)
        {
            if (cam == null) return;
            if (!ready) { centre = autoCentre; dist = autoDist; ready = true; }
            float dt = Time.deltaTime;
            float tanHalf = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
            float groundW = 2 * dist * tanHalf * cam.aspect;                   // width of ground in view
            float groundH = 2 * dist * tanHalf / Mathf.Sin(Pitch * Mathf.Deg2Rad);   // depth of ground in view (tilt stretches it)

            bool input = false;
            float pinch = 1f;            // >1 means the fingers moved apart
            Vector2 pan = Vector2.zero;  // world units to move centre: x east, y north
#if ENABLE_LEGACY_INPUT_MANAGER
            float kx = 0, kz = 0;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) kx -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) kx += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) kz -= 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) kz += 1;
            if (kx != 0 || kz != 0) { pan += new Vector2(kx * groundW, kz * groundW) * (KeyPanSpeed * dt); input = true; }

            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0) { pinch *= Mathf.Exp(wheel * WheelZoom); input = true; }

            if (Input.touchCount >= 2)
            {
                // two fingers: pinch to zoom (touches also fake a mouse button, so the mouse is ignored while any finger is down)
                Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                float now = (a.position - b.position).magnitude;
                float before = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
                if (before > 1f && now > 1f) pinch *= now / before;
                input = true;
            }
            else if (Input.touchCount == 1)
            {
                Vector2 d = Input.GetTouch(0).deltaPosition;
                pan += new Vector2(-d.x * groundW / Screen.width, -d.y * groundH / Screen.height);
                if (d != Vector2.zero) input = true;
            }
            else if (Input.GetMouseButton(0))
            {
                Vector2 d = (Vector2)Input.mousePosition - lastMouse;   // pixels moved since last frame
                pan += new Vector2(-d.x * groundW / Screen.width, -d.y * groundH / Screen.height);
                if (d != Vector2.zero) input = true;
            }
            lastMouse = Input.mousePosition;

            if (Input.GetKeyDown(KeyCode.F)) manualUntil = 0;   // back to following right away
#endif
            if (input) manualUntil = Time.time + IdleResume;

            if (Manual)
            {
                dist = Mathf.Clamp(dist / pinch, MinDist, fullDist);
                centre += new Vector3(pan.x, 0, pan.y);
            }
            else
            {
                float k = 1 - Mathf.Exp(-dt * 1.5f);   // glide, so the view never jumps when a band founds its village
                centre = Vector3.Lerp(centre, autoCentre, k);
                dist = Mathf.Lerp(dist, autoDist, k);
            }
            centre.x = Mathf.Clamp(centre.x, map.X0, map.X1);
            centre.z = Mathf.Clamp(centre.z, map.Y0, map.Y1);

            var rot = Quaternion.Euler(Pitch, 0, 0);
            cam.transform.rotation = rot;
            cam.transform.position = centre - rot * Vector3.forward * dist;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        Vector2 lastMouse;
#endif
    }
}
