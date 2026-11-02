using System.Collections.Generic;
using _project.Scripts.Core;
using _project.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     The valve on an isolated pipe end that faces regular pipe. It starts closed, with a red lever
    ///     across the pipe and a capped end; clicking it opens the drain, swinging a green lever in line
    ///     and pouring the isolated stream into the regular pipe. Click again to close it. Like laying
    ///     pipe, it only changes between runs: routes are set when a run starts, and rerouting items
    ///     already in the pipes would scramble them. Built and rebuilt by <see cref="IsolatedPipeVisual" />;
    ///     the open/closed state lives on the board.
    /// </summary>
    public class DrainValve : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color ClosedColor = new(0.93f, 0.24f, 0.2f);
        private static readonly Color OpenColor = new(0.26f, 0.88f, 0.4f);
        private static readonly Color SplashColor = new(0.72f, 0.9f, 1f);
        private const float HoverScale = 1.15f;
        private const float RippleSpeed = 1.3f;

        private readonly List<Renderer> _colored = new();
        private readonly List<Renderer> _splashRings = new();
        private readonly List<Vector3> _ringScales = new();
        private PathBuildBoard _board;
        private Vector2Int _cell;
        private Transform _lever;
        private Collider _hitBox;
        private MaterialPropertyBlock _colorBlock;
        private bool _hovered;

        public bool IsOpen { get; private set; }
        public Vector2Int Cell => _cell;

        internal void Init(PathBuildBoard board, Vector2Int cell, bool open, IEnumerable<Renderer> colored,
            Transform lever, Transform splash)
        {
            _board = board;
            _cell = cell;
            IsOpen = open;
            _lever = lever;
            _hitBox = GetComponent<Collider>();
            _colored.AddRange(colored);
            if (!splash) return;

            splash.GetComponentsInChildren(_splashRings);
            foreach (var ring in _splashRings) _ringScales.Add(ring.transform.localScale);
        }

        /// <summary>Opens a closed drain or closes an open one.</summary>
        private void Toggle()
        {
            if (_board) _board.SetDrainOpen(_cell, !IsOpen);
        }

        private void LateUpdate()
        {
            // Removing or panning goes to the board beneath, so the valve steps out of the way.
            var board = _board;
            var tool = board ? board.ActiveTool : PathBuildTool.None;
            if (_hitBox) _hitBox.enabled = tool != PathBuildTool.Break && tool != PathBuildTool.Pan;

            // The board tints a pipe's whole visual; keep the lever and cap in their own colors.
            var color = IsOpen ? OpenColor : ClosedColor;
            if (_hovered) color = Color.Lerp(color, Color.white, 0.3f);
            foreach (var part in _colored)
                if (part)
                    RendererColorUtility.SetColor(part, color, ref _colorBlock);
            if (_lever) _lever.localScale = Vector3.one * (_hovered ? HoverScale : 1f);

            // Ripples spread out from where the drain pours into the channel.
            for (var i = 0; i < _splashRings.Count; i++)
            {
                var ring = _splashRings[i];
                if (!ring) continue;

                var t = Mathf.Repeat(Time.time * RippleSpeed + i / (float)_splashRings.Count, 1f);
                var size = _ringScales[i];
                ring.transform.localScale = new Vector3(size.x * (0.4f + t * 1.2f), size.y, size.z * (0.4f + t * 1.2f));
                RendererColorUtility.SetColor(ring, SplashColor, ref _colorBlock);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var cameraController = GameMaster.Instance ? GameMaster.Instance.cameraController : null;
            if (cameraController && cameraController.IsPanArmed) return;
            if (!CanChange) return;

            Toggle();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            UtilityHoverStatsPopup.Instance?.Show(transform, HoverText());
        }

        public void OnPointerExit(PointerEventData eventData) => StopHover();

        private void OnDisable() => StopHover();

        private void StopHover()
        {
            if (!_hovered) return;

            _hovered = false;
            UtilityHoverStatsPopup.Instance?.Hide(transform);
        }

        /// <summary>True outside a run: in the build phase, or with no game running (tests, editor tools).</summary>
        private static bool CanChange
        {
            get
            {
                var turnController = GameMaster.Instance ? GameMaster.Instance.turnController : null;
                return !turnController || turnController.currentPhase == GamePhase.Card;
            }
        }

        private string HoverText()
        {
            var state = IsOpen ? "State: Open, draining" : "State: Closed";
            var action = !CanChange ? "Change: Between runs"
                : IsOpen ? "Click: Close" : "Click: Open to drain";
            return $"DRAIN VALVE\n{state}\n{action}";
        }
    }
}
