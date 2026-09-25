using UnityEngine;
using UnityEngine.InputSystem;

namespace SoapSlide
{
    public static class SoapSlideInput
    {
        /// <summary>Horizontal aim: mouse ray onto the plane at <paramref name="planeY"/>, direction from player.</summary>
        public static bool TryReadMouseSlideDirection(Transform player, Camera cam, out Vector2 xzNormalized)
        {
            xzNormalized = Vector2.up;
            if (player == null || cam == null)
                return false;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return false;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, player.position.y, 0f));
            if (!plane.Raycast(ray, out float distance))
                return false;

            Vector3 hit = ray.GetPoint(distance);
            Vector3 delta = hit - player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0004f)
                return false;

            xzNormalized = new Vector2(delta.x, delta.z).normalized;
            return true;
        }

        /// <summary>
        /// Aim without a player transform: mouse ray onto the horizontal plane at <paramref name="planeY"/> from arena center on that plane.
        /// </summary>
        public static bool TryReadMouseSlideDirectionArenaCenter(Camera cam, float planeY, out Vector2 xzNormalized)
        {
            xzNormalized = Vector2.up;
            if (cam == null) return false;

            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (!plane.Raycast(ray, out float distance))
                return false;

            Vector3 hit = ray.GetPoint(distance);
            var origin = new Vector3(0f, planeY, 0f);
            Vector3 delta = hit - origin;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0004f)
                return false;

            xzNormalized = new Vector2(delta.x, delta.z).normalized;
            return true;
        }

        public static Vector2 ReadMoveXZ()
        {
            Vector2 v = Vector2.zero;
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
            }

            Gamepad g = Gamepad.current;
            if (g != null)
            {
                Vector2 stick = g.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                    v = stick;
            }

            if (v.sqrMagnitude > 1f)
                v.Normalize();
            return v;
        }

        /// <summary>Discrete force 1–10: number keys 1–9 and 0 for 10, mouse wheel, +/-.</summary>
        public static int ReadForceLevel1To10(int current)
        {
            current = Mathf.Clamp(current, 1, 10);
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.digit1Key.wasPressedThisFrame) return 1;
                if (k.digit2Key.wasPressedThisFrame) return 2;
                if (k.digit3Key.wasPressedThisFrame) return 3;
                if (k.digit4Key.wasPressedThisFrame) return 4;
                if (k.digit5Key.wasPressedThisFrame) return 5;
                if (k.digit6Key.wasPressedThisFrame) return 6;
                if (k.digit7Key.wasPressedThisFrame) return 7;
                if (k.digit8Key.wasPressedThisFrame) return 8;
                if (k.digit9Key.wasPressedThisFrame) return 9;
                if (k.digit0Key.wasPressedThisFrame) return 10;

                if (k.numpad1Key.wasPressedThisFrame) return 1;
                if (k.numpad2Key.wasPressedThisFrame) return 2;
                if (k.numpad3Key.wasPressedThisFrame) return 3;
                if (k.numpad4Key.wasPressedThisFrame) return 4;
                if (k.numpad5Key.wasPressedThisFrame) return 5;
                if (k.numpad6Key.wasPressedThisFrame) return 6;
                if (k.numpad7Key.wasPressedThisFrame) return 7;
                if (k.numpad8Key.wasPressedThisFrame) return 8;
                if (k.numpad9Key.wasPressedThisFrame) return 9;
                if (k.numpad0Key.wasPressedThisFrame) return 10;
            }

            Mouse m = Mouse.current;
            if (m != null)
            {
                Vector2 scroll = m.scroll.ReadValue();
                // Vertical scroll is typical; some devices use horizontal (trackpads).
                float s = Mathf.Abs(scroll.y) >= Mathf.Abs(scroll.x) ? scroll.y : scroll.x;
                if (s > 0.0001f) return Mathf.Clamp(current + 1, 1, 10);
                if (s < -0.0001f) return Mathf.Clamp(current - 1,  1, 10);
            }

            if (k != null)
            {
                if (k.numpadPlusKey.wasPressedThisFrame || k.equalsKey.wasPressedThisFrame)
                    return Mathf.Clamp(current + 1, 1, 10);
                if (k.numpadMinusKey.wasPressedThisFrame || k.minusKey.wasPressedThisFrame)
                    return Mathf.Clamp(current - 1, 1, 10);
            }

            return current;
        }
    }
}
