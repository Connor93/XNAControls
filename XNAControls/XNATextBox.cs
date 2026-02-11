using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using MonoGame.Extended.Input.InputListeners;
using System;
using System.Collections.Generic;
using System.Linq;
using XNAControls.Input;

namespace XNAControls
{
    /// <summary>
    /// Represents a text input control
    /// </summary>
    public class XNATextBox : XNAControl, IXNATextBox
    {
        internal static IXNATextBox FocusedTextbox { get; set; }

        private readonly Texture2D _textBoxBG;
        private readonly Texture2D _textBoxLeft;
        private readonly Texture2D _textBoxRight;
        private readonly Texture2D _caretTexture;

        private readonly XNATextBoxLabel _textLabel;
        private readonly XNATextBoxLabel _defaultTextLabel;

        private Rectangle _drawArea;
        private string _actualText;
        private bool _selected;

        private int _lastLeftPadding;

        private bool _multiline;

        private int _cursorPosition;

        /// <inheritdoc />
        public override Rectangle DrawArea
        {
            get => _drawArea;
            set
            {
                _drawArea = value;
                _textLabel.DrawArea = new Rectangle(LeftPadding, 0, _drawArea.Width, _drawArea.Height);
                _defaultTextLabel.DrawArea = new Rectangle(LeftPadding, 0, _drawArea.Width, _drawArea.Height);
            }
        }

        /// <inheritdoc />
        public int MaxChars { get; set; }

        /// <inheritdoc />
        public int? MaxWidth
        {
            get => _textLabel.TextWidth;
            set
            {
                _textLabel.TextWidth = value;
                _defaultTextLabel.TextWidth = value;
            }
        }

        /// <inheritdoc />
        public int? HardBreak
        {
            get => _textLabel.HardBreak;
            set
            {
                _textLabel.HardBreak = value;
                _defaultTextLabel.HardBreak = value;
            }
        }

        /// <inheritdoc />
        public bool PasswordBox { get; set; }

        /// <inheritdoc />
        public int LeftPadding { get; set; }

        /// <inheritdoc />
        public string Text
        {
            get => _actualText;
            set
            {
                if (MaxChars > 0 && value.Length > MaxChars)
                    return;

                _actualText = value;
                _cursorPosition = value.Length; // Set cursor to end when text is assigned
                _textLabel.Text = PasswordBox ? new string(value.Select(x => '*').ToArray()) : value;
                OnTextChanged?.Invoke(this, EventArgs.Empty);

                _textLabel.Visible = _actualText.Length > 0;
                _defaultTextLabel.Visible = _actualText.Length == 0;
            }
        }

        /// <inheritdoc />
        public string DefaultText
        {
            get => _defaultTextLabel.Text;
            set => _defaultTextLabel.Text = value;
        }

        /// <inheritdoc />
        public Color TextColor
        {
            get => _textLabel.ForeColor;
            set => _textLabel.ForeColor = value;
        }

        /// <inheritdoc />
        public Color DefaultTextColor
        {
            get => _defaultTextLabel.ForeColor;
            set => _defaultTextLabel.ForeColor = value;
        }

        /// <inheritdoc />
        public LabelAlignment TextAlignment
        {
            get => _textLabel.TextAlign;
            set
            {
                _textLabel.TextAlign = value;
                _defaultTextLabel.TextAlign = value;
            }
        }

        /// <summary>
        /// Gets the current cursor position within the text (character index).
        /// </summary>
        public int CursorPosition => _cursorPosition;

        /// <summary>
        /// Gets the cursor position as (row, column) for multiline text.
        /// </summary>
        public (int row, int column) GetCursorRowColumn() => GetCursorRowAndColumn();

        /// <inheritdoc />
        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;

                FocusedTextbox?.PostMessage(EventType.LostFocus, EventArgs.Empty);
                PostMessage(EventType.GotFocus, EventArgs.Empty);
            }
        }

        /// <inheritdoc />
        public int TabOrder { get; set; }

        /// <inheritdoc />
        public bool Multiline
        {
            get => _multiline;
            set
            {
                _multiline = value;
                _textLabel.WrapBehavior = _defaultTextLabel.WrapBehavior = 
                    _multiline ? WrapBehavior.WrapToNewLine : WrapBehavior.ScrollText;
            }
        }

        /// <inheritdoc />
        public IScrollHandler ScrollHandler
        {
            get => _textLabel.ScrollHandler;
            set => _textLabel.ScrollHandler = value;
        }

        /// <inheritdoc />
        public int? RowSpacing
        {
            get => _textLabel.RowSpacing;
            set => _textLabel.RowSpacing = value;
        }

        /// <inheritdoc />
        public event EventHandler OnGotFocus = delegate { };

        /// <inheritdoc />
        public event EventHandler OnLostFocus = delegate { };

        /// <inheritdoc />
        public event EventHandler OnTextChanged = delegate { };

        /// <inheritdoc />
        public event EventHandler OnEnterPressed = delegate { };

        /// <inheritdoc />
        public event EventHandler<MouseEventArgs> OnMouseDown = delegate { };

        /// <inheritdoc />
        public event EventHandler<MouseEventArgs> OnMouseUp = delegate { };

        /// <inheritdoc />
        public event EventHandler<MouseEventArgs> OnClicked = delegate { };

        /// <summary>
        /// Create a new text box with the specified area and font (content name)
        /// </summary>
        public XNATextBox(Rectangle area, 
                          string spriteFontContentName,
                          Texture2D backgroundTexture = null,
                          Texture2D leftSideTexture = null,
                          Texture2D rightSideTexture = null,
                          Texture2D caretTexture = null)
        {
            _textBoxBG = backgroundTexture;
            _textBoxLeft = leftSideTexture;
            _textBoxRight = rightSideTexture;
            _caretTexture = caretTexture;

            _textLabel = new XNATextBoxLabel(spriteFontContentName)
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                TextAlign = LabelAlignment.MiddleLeft,
                DrawArea = new Rectangle(0, 0, area.Width, area.Height),
                WrapBehavior = WrapBehavior.ScrollText,
            };
            _textLabel.SetParentControl(this);

            _defaultTextLabel = new XNATextBoxLabel(spriteFontContentName)
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                TextAlign = LabelAlignment.MiddleLeft,
                DrawArea = new Rectangle(0, 0, area.Width, area.Height),
                WrapBehavior = WrapBehavior.ScrollText,
            };
            _defaultTextLabel.SetParentControl(this);

            DrawArea = area;

            _actualText = "";
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            _textLabel.Initialize();
            _defaultTextLabel.Initialize();

            base.Initialize();
        }

        /// <inheritdoc />
        protected override void OnUpdateControl(GameTime gameTime)
        {
            if (_lastLeftPadding != LeftPadding)
            {
                _lastLeftPadding = LeftPadding;
                _textLabel.DrawPosition = new Vector2(LeftPadding, 0);
                _defaultTextLabel.DrawPosition = new Vector2(LeftPadding, 0);
            }

            if (Enabled)
            {
                base.OnUpdateControl(gameTime);
            }
        }

        /// <inheritdoc />
        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            if(_textBoxBG != null)
                _spriteBatch.Draw(_textBoxBG, DrawAreaWithParentOffset, Color.White);

            if (_textBoxLeft != null)
                _spriteBatch.Draw(_textBoxLeft, DrawPositionWithParentOffset, Color.White);

            if (_textBoxRight != null)
            {
                var drawPosition = new Vector2(DrawPositionWithParentOffset.X + DrawArea.Width - _textBoxRight.Width,
                                               DrawPositionWithParentOffset.Y);

                _spriteBatch.Draw(_textBoxRight, drawPosition, Color.White);
            }

            if (_caretTexture != null && _textLabel != null && Selected)
            {
                var caretVisible = !(gameTime.TotalGameTime.TotalMilliseconds % 1000 < 500);
                if (caretVisible)
                {
                    Vector2 caretAdjust;

                    if (Multiline && _textLabel.TextRows.Count > 0)
                    {
                        // Find which display row the cursor is on and its position within that row
                        // Account for both word wrap and explicit newlines
                        var charCount = 0;
                        var cursorRow = 0;
                        var cursorColInRow = 0;
                        var sourceIndex = 0;

                        for (var row = 0; row < _textLabel.TextRows.Count; row++)
                        {
                            var rowText = _textLabel.TextRows[row];
                            var rowLen = rowText.Length;

                            // Check if cursor is within this row
                            if (_cursorPosition <= charCount + rowLen)
                            {
                                cursorRow = row;
                                cursorColInRow = _cursorPosition - charCount;
                                break;
                            }

                            charCount += rowLen;
                            sourceIndex += rowLen;

                            // Account for newline if this row ends with one, or if next row starts at a newline boundary
                            if (sourceIndex < _actualText.Length && _actualText[sourceIndex] == '\n')
                            {
                                charCount++; // The newline itself
                                sourceIndex++;
                            }

                            // If we've processed all rows and cursor is at the very end
                            if (row == _textLabel.TextRows.Count - 1)
                            {
                                cursorRow = row;
                                cursorColInRow = rowLen;
                            }
                        }

                        // Measure text on current row up to cursor column
                        var currentRowText = _textLabel.TextRows[cursorRow];
                        var textBeforeCursor = cursorColInRow <= currentRowText.Length
                            ? currentRowText.Substring(0, cursorColInRow)
                            : currentRowText;
                        var textWidth = _textLabel.MeasureString(textBeforeCursor).X;

                        // Calculate row offset (account for scroll)
                        var scrollOffset = _textLabel.ScrollHandler?.ScrollOffset ?? 0;
                        var displayRow = cursorRow - scrollOffset;
                        var textHeight = displayRow * (_textLabel.RowSpacing ?? (int)_textLabel.ActualHeight);

                        caretAdjust = new Vector2(textWidth, textHeight);
                    }
                    else
                    {
                        // For single-line, measure text up to cursor position
                        var clampedCursor = Math.Min(_cursorPosition, _actualText.Length);
                        var textUpToCursor = _actualText.Substring(0, clampedCursor);
                        var textWidth = _textLabel.MeasureString(textUpToCursor).X;

                        caretAdjust = new Vector2(textWidth, 0);
                    }

                    _spriteBatch.Draw(_caretTexture,
                                      _textLabel.AdjustedDrawPosition + caretAdjust,
                                      Color.White);
                }
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }

        /// <inheritdoc />
        protected override bool HandleMouseDown(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (OnMouseDown == null)
                return false;

            OnMouseDown(control, eventArgs);

            return true;
        }

        /// <inheritdoc />
        protected override bool HandleMouseUp(IXNAControl control, MouseEventArgs eventArgs)
        {
            if (OnMouseUp == null)
                return false;

            OnMouseUp(control, eventArgs);

            return true;
        }

        /// <inheritdoc />
        protected override bool HandleClick(IXNAControl control, MouseEventArgs eventArgs)
        {
            FocusedTextbox?.PostMessage(EventType.LostFocus, EventArgs.Empty);
            FocusedTextbox = this;
            FocusedTextbox.PostMessage(EventType.GotFocus, EventArgs.Empty);

            return true;
        }

        /// <inheritdoc />
        protected override bool HandleKeyTyped(IXNAControl control, KeyboardEventArgs eventArgs)
        {
            if (eventArgs.Key == Keys.Tab && FocusedTextbox != null)
            {
                IXNATextBox nextTextBox;

                var dialogStack = Singleton<DialogRepository>.Instance.OpenDialogs;
                var textBoxes = dialogStack.Any()
                    ? dialogStack.Peek().FlattenedChildren.OfType<IXNATextBox>()
                    : Game.Components.OfType<IXNATextBox>().Concat(Game.Components.OfType<IXNAControl>().SelectMany(x => x.FlattenedChildren.OfType<IXNATextBox>()));

                var state = KeyboardExtended.GetState();
                if (state.IsShiftDown())
                {
                    var orderTextBoxesEnumerable = textBoxes.OrderByDescending(x => x.TabOrder);
                    nextTextBox = orderTextBoxesEnumerable
                        .SkipWhile(x => x.TabOrder >= FocusedTextbox.TabOrder)
                        .FirstOrDefault();
                    nextTextBox ??= orderTextBoxesEnumerable.FirstOrDefault();
                }
                else
                {
                    var orderTextBoxesEnumerable = textBoxes.OrderBy(x => x.TabOrder);
                    nextTextBox = orderTextBoxesEnumerable
                        .SkipWhile(x => x.TabOrder <= FocusedTextbox.TabOrder)
                        .FirstOrDefault();
                    nextTextBox ??= orderTextBoxesEnumerable.FirstOrDefault();
                }

                FocusedTextbox?.PostMessage(EventType.LostFocus, EventArgs.Empty);
                FocusedTextbox = nextTextBox;
                FocusedTextbox?.PostMessage(EventType.GotFocus, EventArgs.Empty);
            }
            else if(eventArgs.Character.HasValue)
            {
                HandleTextInput(eventArgs);
            }

            return true;
        }

        /// <summary>
        /// Handles key press events for navigation keys (arrows, Home, End, Delete)
        /// </summary>
        protected override bool HandleKeyPressed(IXNAControl control, KeyboardEventArgs eventArgs)
        {
            switch (eventArgs.Key)
            {
                case Keys.Left:
                    if (_cursorPosition > 0)
                        _cursorPosition--;
                    return true;
                case Keys.Right:
                    if (_cursorPosition < _actualText.Length)
                        _cursorPosition++;
                    return true;
                case Keys.Up:
                    if (Multiline)
                        MoveCursorVertically(-1);
                    return true;
                case Keys.Down:
                    if (Multiline)
                        MoveCursorVertically(1);
                    return true;
                case Keys.Home:
                    _cursorPosition = 0;
                    return true;
                case Keys.End:
                    _cursorPosition = _actualText.Length;
                    return true;
                case Keys.Delete:
                    if (_cursorPosition < _actualText.Length)
                    {
                        var newText = _actualText.Remove(_cursorPosition, 1);
                        var savedCursor = _cursorPosition;
                        SetTextDirect(newText);
                        _cursorPosition = savedCursor;
                    }
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Moves the cursor up or down by the specified number of rows
        /// </summary>
        private void MoveCursorVertically(int rowDelta)
        {
            if (_textLabel.TextRows.Count <= 1)
                return;

            // Find current row and column
            var (currentRow, currentCol) = GetCursorRowAndColumn();

            // Calculate target row
            var targetRow = Math.Clamp(currentRow + rowDelta, 0, _textLabel.TextRows.Count - 1);
            if (targetRow == currentRow)
                return;

            // Calculate new cursor position
            var targetRowText = _textLabel.TextRows[targetRow];
            var targetCol = Math.Min(currentCol, targetRowText.Length);

            // Calculate character offset to start of target row
            var charOffset = 0;
            for (var row = 0; row < targetRow; row++)
            {
                charOffset += _textLabel.TextRows[row].Length;
                // Account for newlines between rows
                var rowEndPos = charOffset;
                if (rowEndPos < _actualText.Length && _actualText[rowEndPos] == '\n')
                    charOffset++;
            }

            _cursorPosition = charOffset + targetCol;
        }

        /// <summary>
        /// Gets the current cursor row and column within the display rows
        /// </summary>
        private (int row, int col) GetCursorRowAndColumn()
        {
            var charCount = 0;
            var sourceIndex = 0;

            for (var row = 0; row < _textLabel.TextRows.Count; row++)
            {
                var rowText = _textLabel.TextRows[row];
                var rowLen = rowText.Length;

                // Check if cursor is within this row
                if (_cursorPosition <= charCount + rowLen)
                {
                    return (row, _cursorPosition - charCount);
                }

                charCount += rowLen;
                sourceIndex += rowLen;

                // Account for newline
                if (sourceIndex < _actualText.Length && _actualText[sourceIndex] == '\n')
                {
                    charCount++;
                    sourceIndex++;
                }
            }

            // Cursor at the very end
            var lastRow = _textLabel.TextRows.Count - 1;
            return (lastRow, _textLabel.TextRows[lastRow].Length);
        }

        /// <inheritdoc />
        protected virtual bool HandleTextInput(KeyboardEventArgs eventArgs)
        {
            switch (eventArgs.Key)
            {
                case Keys.Tab: break;
                case Keys.Enter:
                    {
                        if (Multiline)
                        {
                            InsertTextAtCursor("\n");
                        }

                        OnEnterPressed?.Invoke(this, EventArgs.Empty);
                    }
                    break;
                case Keys.Back:
                    {
                        if (!string.IsNullOrEmpty(Text) && _cursorPosition > 0)
                        {
                            var newText = _actualText.Remove(_cursorPosition - 1, 1);
                            var newCursor = _cursorPosition - 1;
                            SetTextDirect(newText);
                            _cursorPosition = newCursor;
                        }
                    }
                    break;
                default:
                    {
                        if (eventArgs.Character != null)
                        {
                            InsertTextAtCursor(eventArgs.Character.ToString());
                        }
                    }
                    break;
            }

            return true;
        }

        /// <summary>
        /// Inserts text at the current cursor position and advances the cursor
        /// </summary>
        private void InsertTextAtCursor(string textToInsert)
        {
            var newText = _actualText.Insert(_cursorPosition, textToInsert);
            var newCursor = _cursorPosition + textToInsert.Length;
            var oldText = _actualText;
            SetTextDirect(newText);
            // Only advance cursor if SetTextDirect actually accepted the change
            // (it silently rejects when MaxChars is exceeded)
            if (_actualText != oldText)
                _cursorPosition = newCursor;
        }

        /// <summary>
        /// Sets the text without resetting cursor position (used for internal editing)
        /// </summary>
        private void SetTextDirect(string value)
        {
            if (MaxChars > 0 && value.Length > MaxChars)
                return;

            _actualText = value;
            _textLabel.Text = PasswordBox ? new string(value.Select(x => '*').ToArray()) : value;
            OnTextChanged?.Invoke(this, EventArgs.Empty);

            _textLabel.Visible = _actualText.Length > 0;
            _defaultTextLabel.Visible = _actualText.Length == 0;
        }

        /// <inheritdoc />
        protected override bool HandleLostFocus(IXNAControl control, EventArgs eventArgs)
        {
            OnLostFocus?.Invoke(this, eventArgs);
            _selected = false;
            if (FocusedTextbox == this)
                FocusedTextbox = null;

            return true;
        }

        /// <inheritdoc />
        protected override bool HandleGotFocus(IXNAControl control, EventArgs eventArgs)
        {
            OnGotFocus?.Invoke(this, eventArgs);
            _selected = true;
            if (FocusedTextbox != this)
                FocusedTextbox = this;

            return true;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing && FocusedTextbox == this)
            {
                FocusedTextbox = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// A class wth logic to handle multiline textboxes
        /// </summary>
        protected internal class XNATextBoxLabel : XNALabel
        {
            /// <inheritdoc />
            public IReadOnlyList<string> TextRows => DrawStrings.Count == 0 ? new List<string> { Text } : DrawStrings;

            /// <inheritdoc />
            public IScrollHandler ScrollHandler { get; set; }

            /// <inheritdoc />
            public XNATextBoxLabel(string spriteFontName)
                : base(spriteFontName)
            {
            }

            /// <inheritdoc />
            public new Vector2 MeasureString(string input) => base.MeasureString(input);

            /// <inheritdoc />
            protected override void DrawMultiLine(float adjustedX, float adjustedY)
            {
                var start = ScrollHandler == null || DrawStrings.Count <= ScrollHandler.LinesToRender
                    ? 0
                    : ScrollHandler.ScrollOffset;
                var end = ScrollHandler == null || DrawStrings.Count <= ScrollHandler.LinesToRender
                    ? DrawStrings.Count
                    : ScrollHandler.LinesToRender + ScrollHandler.ScrollOffset;

                for (int i = start; i < Math.Min(DrawStrings.Count, end); i++)
                {
                    var line = DrawStrings[i];
                    DrawTextLine(line, adjustedX, adjustedY + (RowSpacing ?? LineHeight) * (i - start));
                }
            }

            /// <inheritdoc/>
            protected override void OnWrappedTextUpdated()
            {
                if (!AutoSize || !DrawStrings.Any())
                {
                    ScrollHandler?.UpdateDimensions(TextRows.Count);

                    if (ImmediateParent.Enabled)
                    {
                        ScrollHandler?.ScrollToEnd();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Interface for a text input control
    /// </summary>
    public interface IXNATextBox : IXNAControl
    {
        /// <summary>
        /// The maximum number of characters that can be entered in this text box
        /// </summary>
        int MaxChars { get; set; }

        /// <summary>
        /// The maximum width of text as measured by the font before text starts scrolling
        /// </summary>
        int? MaxWidth { get; set; }

        /// <summary>
        /// Set this textbox as a password box
        /// </summary>
        bool PasswordBox { get; set; }

        /// <summary>
        /// Width of empty space to the left of the first character displayed in this text box
        /// </summary>
        int LeftPadding { get; set; }

        /// <summary>
        /// The text to display
        /// </summary>
        string Text { get; set; }

        /// <summary>
        /// The default text to display. This text shows before any text is entered.
        /// </summary>
        string DefaultText { get; set; }

        /// <summary>
        /// Color of the text
        /// </summary>
        Color TextColor { get; set; }

        /// <summary>
        /// Color of the default text
        /// </summary>
        Color DefaultTextColor { get; set; }

        /// <summary>
        /// Alignment of the text
        /// </summary>
        LabelAlignment TextAlignment { get; set; }

        /// <summary>
        /// Gets or sets whether this text box is selected. Selecting this text box gives it focus.
        /// </summary>
        bool Selected { get; set; }

        /// <summary>
        /// TabOrder of this text box. Pressing 'tab' will cycle through text boxes based on their tab order.
        /// </summary>
        int TabOrder { get; set; }

        /// <summary>
        /// Gets or sets whether the control is a multiline textbox. Text behavior will wrap instead of scrolling.
        /// </summary>
        bool Multiline { get; set; }

        /// <summary>
        /// Gets or sets the scroll handler, which handles vertical scrolling text in multiline text box controls.
        /// </summary>
        IScrollHandler ScrollHandler { get; set; }

        /// <summary>
        /// Gets or sets the spacing between rows in a multiline textbox, in pixels
        /// </summary>
        int? RowSpacing { get; set; }

        /// <summary>
        /// Gets or sets the maximum text width for hard breaks (force-wrap long words)
        /// </summary>
        int? HardBreak { get; set; }

        /// <summary>
        /// Event fired when this text box gets focus
        /// </summary>
        event EventHandler OnGotFocus;

        /// <summary>
        /// Event fired when this text box loses focus
        /// </summary>
        event EventHandler OnLostFocus;

        /// <summary>
        /// Event fired when text changes
        /// </summary>
        event EventHandler OnTextChanged;

        /// <summary>
        /// Event fired when the enter key is pressed
        /// </summary>
        event EventHandler OnEnterPressed;

        /// <summary>
        /// Event fired when a mouse button is pressed on a button control
        /// </summary>
        event EventHandler<MouseEventArgs> OnMouseDown;

        /// <summary>
        /// Event fired when a mouse button is released on a button control
        /// </summary>
        event EventHandler<MouseEventArgs> OnMouseUp;

        /// <summary>
        /// Event fired when this text box is clicked
        /// </summary>
        event EventHandler<MouseEventArgs> OnClicked;
    }
}