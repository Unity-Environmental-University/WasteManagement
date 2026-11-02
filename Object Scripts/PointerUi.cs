using System;
using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Shared pointer helper for world objects that handle clicks through OnMouseDown.
    ///     EventSystem.IsPointerOverGameObject() is unusable here: the Main Camera carries a
    ///     PhysicsRaycaster, so it returns true whenever the pointer is over *any* 3D collider —
    ///     including the very object being clicked — which silently swallows every world click.
    /// </summary>
    public static class PointerUi
    {
        private static readonly List<RaycastResult> RaycastResults = new();
        private static readonly RaycastHit[] WorldHits = new RaycastHit[32];

        /// <summary>
        ///     True when the pointer is over a canvas element that actually handles a press or
        ///     click (a button, a shop entry, a tool switch). Passive graphics — panel backdrops,
        ///     labels, meters — are ignored so they don't block valid world clicks, and 3D hits
        ///     from the camera's PhysicsRaycaster are excluded entirely.
        /// </summary>
        public static bool IsPointerOverInteractiveUi()
        {
            var eventSystem = EventSystem.current;
            var pointer = Pointer.current;
            if (!eventSystem || pointer == null) return false;

            var eventData = new PointerEventData(eventSystem)
            {
                position = pointer.position.ReadValue()
            };

            RaycastResults.Clear();
            eventSystem.RaycastAll(eventData, RaycastResults);

            var overUi = false;
            foreach (var result in RaycastResults)
            {
                if (result.module is not GraphicRaycaster) continue;
                if (!HandlesPointerInput(result.gameObject)) continue;
                overUi = true;
                break;
            }

            RaycastResults.Clear();
            return overUi;
        }

        /// <summary>
        ///     The nearest <typeparamref name="T" /> under the pointer that <paramref name="match" />
        ///     accepts, looking through anything in front of it. Lets one world object hand a click on
        ///     to another it overlaps on screen.
        /// </summary>
        public static T FindUnderPointer<T>(Predicate<T> match) where T : Component
        {
            var pointer = Pointer.current;
            var gm = GameMaster.Instance;
            var viewCamera = gm && gm.cameraController ? gm.cameraController.GetCurrentCamera() : Camera.main;
            if (pointer == null || !viewCamera) return null;

            var ray = viewCamera.ScreenPointToRay(pointer.position.ReadValue());
            var count = Physics.RaycastNonAlloc(ray, WorldHits, viewCamera.farClipPlane, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            T nearest = null;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                if (WorldHits[i].distance >= nearestDistance) continue;
                var candidate = WorldHits[i].collider.GetComponentInParent<T>();
                if (!candidate || !match(candidate)) continue;

                nearest = candidate;
                nearestDistance = WorldHits[i].distance;
            }

            return nearest;
        }

        private static bool HandlesPointerInput(GameObject target)
        {
            return ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) ||
                   ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
        }
    }
}