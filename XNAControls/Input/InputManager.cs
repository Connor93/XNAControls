using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

using MonoGame.Extended.Input;
using MonoGame.Extended.Input.InputListeners;

namespace XNAControls.Input
{
    /// <summary>
    /// Component that handles input for the game. Sends messages to controls based on the input event that occurred.
    /// </summary>
    public class InputManager : GameComponent
    {
        private readonly KeyboardListener _keyboardListener;
        private readonly MouseListener _mouseListener;
        private readonly IMouseCoordinateTransformer _coordinateTransformer;

        private IEventReceiver _dragTarget;
        private bool _componentsChanged;

        /// <summary>
        /// Create a new InputManager using the default game previously set in the GameRepository
        /// </summary>
        public InputManager()
            : this (GameRepository.GetGame()) { }

        /// <summary>
        /// Create a new InputManager using the specified game and default MouseListenerSettings (60ms double-click, 1px drag threshold)
        /// </summary>
        public InputManager(Game game)
            : this(game, new MouseListenerSettings { DoubleClickMilliseconds = 60, DragThreshold = 1 })
        {
        }

        /// <summary>
        /// Create a new InputManager using the specified game and MouseListenerSettings
        /// </summary>
        public InputManager(Game game, MouseListenerSettings mouseListenerSettings)
            : this(game, mouseListenerSettings, null)
        {
        }

        /// <summary>
        /// Create a new InputManager using the specified game, MouseListenerSettings, and optional coordinate transformer
        /// </summary>
        /// <param name="game">The game instance</param>
        /// <param name="mouseListenerSettings">Settings for mouse listener</param>
        /// <param name="coordinateTransformer">Optional transformer for scaled rendering. When provided, mouse coordinates are transformed before hit detection.</param>
        public InputManager(Game game, MouseListenerSettings mouseListenerSettings, IMouseCoordinateTransformer coordinateTransformer)
            : base(game)
        {
            _coordinateTransformer = coordinateTransformer;
            _keyboardListener = new KeyboardListener();
            _mouseListener = new MouseListener(mouseListenerSettings);

            UpdateOrder = int.MinValue;

            Game.Components.ComponentAdded += Components_ComponentAdded;
            Game.Components.ComponentRemoved += Components_ComponentRemoved;

            void Components_ComponentRemoved(object sender, GameComponentCollectionEventArgs e)
            {
                _componentsChanged = true;
            }

            void Components_ComponentAdded(object sender, GameComponentCollectionEventArgs e)
            {
                _componentsChanged = true;
            }
        }

        /// <summary>
        /// Gets the transformed mouse position for hit detection.
        /// If a coordinate transformer is configured, transforms from window to game coordinates.
        /// </summary>
        private Point GetTransformedMousePosition()
        {
            var rawPosition = MouseExtended.GetState().Position;
            if (_coordinateTransformer != null)
            {
                return _coordinateTransformer.TransformMousePosition(rawPosition);
            }
            return rawPosition;
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            _keyboardListener.KeyTyped += Keyboard_KeyTyped;
            _keyboardListener.KeyPressed += Keyboard_KeyPressed;
            _keyboardListener.KeyReleased += Keyboard_KeyReleased;
            _mouseListener.MouseDown += Mouse_Down;
            _mouseListener.MouseUp += Mouse_Up;
            _mouseListener.MouseClicked += Mouse_Click;
            _mouseListener.MouseDoubleClicked += Mouse_DoubleClick;
            _mouseListener.MouseDragStart += Mouse_DragStart;
            _mouseListener.MouseDragEnd += Mouse_DragEnd;
            _mouseListener.MouseDrag += Mouse_Drag;
            _mouseListener.MouseWheelMoved += Mouse_WheelMoved;

            base.Initialize();
        }

        /// <inheritdoc />
        public override void Update(GameTime gameTime)
        {
            var mouseState = MouseExtended.GetState();
            var transformedPosition = GetTransformedMousePosition();

            if (mouseState.PositionChanged || _componentsChanged)
            {
                var comps = InputTargetFinder.GetMouseOverEventTargetControl(Game.Components);

                // Find all controls under the mouse position, excluding:
                // - The drag target (for hold-to-drag mode)
                // - Controls with very high ZOrder (>= 10000) which are likely click-to-drag items
                // This allows drop targets to receive MouseOver during any drag operation
                var controlsUnderMouse = comps
                    .Where(c => c.EventArea.Contains(transformedPosition))
                    .Where(c => c != _dragTarget && c.ZOrder < 10000) // Exclude drag-like items from ZOrder calculation
                    .ToList();

                // Find high-ZOrder controls under mouse (likely dragged items via click-to-drag)
                var highZOrderControls = comps
                    .Where(c => c.EventArea.Contains(transformedPosition) && c.ZOrder >= 10000)
                    .ToList();

                // Determine which controls should receive MouseOver by filtering out those
                // obscured by higher-ZOrder controls
                var allowedControls = new HashSet<IEventReceiver>();

                // Always allow the drag target to receive MouseOver
                if (_dragTarget != null)
                {
                    allowedControls.Add(_dragTarget);
                }

                // Always allow high-ZOrder controls (click-to-drag items) to receive MouseOver
                // so they can handle click-to-drop events
                foreach (var highZControl in highZOrderControls)
                {
                    allowedControls.Add(highZControl);
                }

                if (controlsUnderMouse.Count > 0)
                {
                    // Find the maximum ZOrder among controls under mouse (excluding drag target)
                    var maxZOrder = controlsUnderMouse.Max(c => c.ZOrder);

                    // Allow the highest-ZOrder control(s) and all their parent controls
                    var topControls = controlsUnderMouse.Where(c => c.ZOrder == maxZOrder);
                    foreach (var topControl in topControls)
                    {
                        allowedControls.Add(topControl);

                        // Also allow all parent controls in the hierarchy
                        if (topControl is IXNAControl xnaControl)
                        {
                            var parent = xnaControl.ImmediateParent;
                            while (parent != null)
                            {
                                allowedControls.Add(parent);
                                parent = parent.ImmediateParent;
                            }
                        }
                    }

                    // Also allow child controls of the topmost controls
                    foreach (var topControl in topControls.ToList())
                    {
                        if (topControl is IXNAControl xnaControl)
                        {
                            foreach (var child in xnaControl.FlattenedChildren)
                            {
                                if (child.EventArea.Contains(transformedPosition))
                                {
                                    allowedControls.Add(child);
                                }
                            }
                        }
                    }
                }

                foreach (var component in comps)
                {
                    // Use transformed position for hit detection
                    if (component.EventArea.Contains(transformedPosition) && allowedControls.Contains(component))
                    {
                        if (!InputTargetFinder.MouseOverState.TryGetValue(component, out var value) || !value)
                        {
                            InputTargetFinder.MouseOverState[component] = true;
                            Mouse_Enter(component, mouseState);
                        }
                        else
                        {
                            InputTargetFinder.MouseOverState[component] = true;
                            Mouse_Over(component, mouseState);
                        }
                    }
                    else if (InputTargetFinder.MouseOverState.TryGetValue(component, out var value) && value)
                    {
                        InputTargetFinder.MouseOverState[component] = false;
                        Mouse_Leave(component, mouseState);
                    }
                }

                _componentsChanged = false;
            }

            _keyboardListener.Update(gameTime);
            _mouseListener.Update(gameTime);

            base.Update(gameTime);
        }

        /// <summary>
        /// Gets the click target at the transformed mouse position.
        /// Uses MouseOverState (set during Update with transformed coordinates) and respects ZOrder.
        /// </summary>
        private IEventReceiver GetClickTargetAtTransformedPosition()
        {
            // Get all components that could be valid targets
            var comps = InputTargetFinder.GetMouseOverEventTargetControl(Game.Components);

            // Filter to only those that are currently mouse-over (using the already-transformed check from Update)
            var validTargets = new List<IEventReceiver>();
            foreach (var component in comps)
            {
                if (InputTargetFinder.MouseOverState.TryGetValue(component, out var mouseOver) && mouseOver)
                {
                    // Also check that parents are visible
                    if (component is IDrawable drawable && !drawable.Visible)
                        continue;

                    validTargets.Add(component);
                }
            }

            if (validTargets.Count == 0) return null;
            if (validTargets.Count == 1) return validTargets[0];

            // Find the one with highest ZOrder
            var max = validTargets.Max(x => x.ZOrder);
            var maxTargets = validTargets.Where(x => x.ZOrder == max).ToList();

            if (maxTargets.Count == 1) return maxTargets[0];

            // Tie breaker: lowest UpdateOrder (first to update)
            if (maxTargets.All(x => x is IUpdateable))
            {
                var updateables = maxTargets.OfType<IUpdateable>();
                var minValue = updateables.Min(x => x.UpdateOrder);

                if (!updateables.All(x => x.UpdateOrder == minValue))
                {
                    return maxTargets.MinBy(x => ((IUpdateable)x).UpdateOrder);
                }
            }

            return maxTargets.Last();
        }

        private void Keyboard_KeyTyped(object sender, KeyboardEventArgs e)
        {
            // todo: is there a better place to store which textbox is focused?
            XNATextBox.FocusedTextbox?.PostMessage(EventType.KeyTyped, e);
        }

        private void Keyboard_KeyPressed(object sender, KeyboardEventArgs e)
        {
            XNATextBox.FocusedTextbox?.PostMessage(EventType.KeyPressed, e);
        }

        private void Keyboard_KeyReleased(object sender, KeyboardEventArgs e)
        {
            XNATextBox.FocusedTextbox?.PostMessage(EventType.KeyReleased, e);
        }

        private void Mouse_Down(object sender, MouseEventArgs e)
        {
            var clickTarget = _coordinateTransformer != null
                ? GetClickTargetAtTransformedPosition()
                : InputTargetFinder.GetMouseButtonEventTargetControl(Game.Components);
            clickTarget?.PostMessage(EventType.MouseDown, e);
        }

        private void Mouse_Up(object sender, MouseEventArgs e)
        {
            var clickTarget = _coordinateTransformer != null
                ? GetClickTargetAtTransformedPosition()
                : InputTargetFinder.GetMouseButtonEventTargetControl(Game.Components);
            clickTarget?.PostMessage(EventType.MouseUp, e);
        }

        private void Mouse_Click(object sender, MouseEventArgs e)
        {
            var clickTarget = _coordinateTransformer != null
                ? GetClickTargetAtTransformedPosition()
                : InputTargetFinder.GetMouseButtonEventTargetControl(Game.Components);
            clickTarget?.PostMessage(EventType.Click, e);
        }

        private void Mouse_DoubleClick(object sender, MouseEventArgs e)
        {
            var clickTarget = _coordinateTransformer != null
                ? GetClickTargetAtTransformedPosition()
                : InputTargetFinder.GetMouseButtonEventTargetControl(Game.Components);
            clickTarget?.PostMessage(EventType.DoubleClick, e);
        }

        private void Mouse_DragStart(object sender, MouseEventArgs e)
        {
            if (_dragTarget != null)
                return;

            _dragTarget = _coordinateTransformer != null
                ? GetClickTargetAtTransformedPosition()
                : InputTargetFinder.GetMouseButtonEventTargetControl(Game.Components);
            _dragTarget?.PostMessage(EventType.DragStart, e);
        }

        private void Mouse_DragEnd(object sender, MouseEventArgs e)
        {
            if (_dragTarget == null)
                return;

            _dragTarget.PostMessage(EventType.DragEnd, e);
            _dragTarget = null;
        }

        private void Mouse_Drag(object sender, MouseEventArgs e)
        {
            if (_dragTarget == null)
                return;

            _dragTarget.PostMessage(EventType.Drag, e);
        }

        private void Mouse_WheelMoved(object sender, MouseEventArgs e)
        {
            var clickTarget = _coordinateTransformer != null
                ? GetClickTargetAtTransformedPosition()
                : InputTargetFinder.GetMouseButtonEventTargetControl(Game.Components);
            clickTarget?.PostMessage(EventType.MouseWheelMoved, e);
        }

        private static void Mouse_Enter(IEventReceiver component, MouseStateExtended mouseState)
        {
            component.PostMessage(EventType.MouseEnter, mouseState);
        }

        private static void Mouse_Over(IEventReceiver component, MouseStateExtended mouseState)
        {
            component.PostMessage(EventType.MouseOver, mouseState);
        }

        private static void Mouse_Leave(IEventReceiver component, MouseStateExtended mouseState)
        {
            component.PostMessage(EventType.MouseLeave, mouseState);
        }
    }
}
