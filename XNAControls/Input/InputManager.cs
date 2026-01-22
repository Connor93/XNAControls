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
                foreach (var component in comps)
                {
                    // Use transformed position for hit detection
                    if (component.EventArea.Contains(transformedPosition))
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
