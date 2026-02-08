using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using SSH_Helper.Models;
using SSH_Helper.Services.Editor;
using SSH_Helper.Services.Scripting;

namespace SSH_Helper.UI
{
    internal sealed class ScriptEditorControl : UserControl, IScriptEditor
    {
        private static readonly Regex VariableTokenRegex =
            new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}|\{\{(?<column>[^}\s]+)\}\}",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private const int WmSetRedraw = 0x000B;
        private const int EmLineScroll = 0x00B6;
        private const int EmGetFirstVisibleLine = 0x00CE;
        private const int WmUser = 0x0400;
        private const int EmGetScrollPos = WmUser + 221;
        private const int EmSetScrollPos = WmUser + 222;
        private const int CompletionPopupWidth = 262;

        private readonly RichTextBox _editor;
        private readonly ToolTip _toolTip;
        private readonly NonFocusableCompletionListBox _completionList;
        private readonly Panel _completionPopup;

        private IReadOnlyList<EditorDiagnostic> _diagnostics = Array.Empty<EditorDiagnostic>();
        private List<CompletionItem> _activeCompletionItems = new();
        private ScriptAutocompleteProvider? _autocompleteProvider;
        private YamlSshSyntaxHighlighter? _syntaxHighlighter;
        private ScriptEditorValidationService? _validationService;
        private CommandEditorSettings _settings = new();
        private bool _isDarkMode;
        private bool _suppressTextProcessing;
        private int _completionReplaceStart;
        private int _completionReplaceLength;
        private string _lastTooltipMessage = string.Empty;
        private Func<string, string?>? _variableResolver;
        private Func<string, string?>? _columnResolver;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        private readonly struct TextReplacement
        {
            public TextReplacement(int start, int oldLength, string newText)
            {
                Start = start;
                OldLength = oldLength;
                NewText = newText ?? string.Empty;
            }

            public int Start { get; }
            public int OldLength { get; }
            public string NewText { get; }
            public int NewLength => NewText.Length;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref NativePoint lParam);

        public ScriptEditorControl()
        {
            _editor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Multiline = true,
                AcceptsTab = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            _editor.TextChanged += Editor_TextChanged;
            _editor.KeyDown += Editor_KeyDown;
            _editor.KeyUp += Editor_KeyUp;
            _editor.MouseUp += Editor_MouseUp;
            _editor.MouseMove += Editor_MouseMove;
            _editor.MouseLeave += (_, _) => HideTooltip();
            _editor.Click += (_, _) => OnClick(EventArgs.Empty);

            _toolTip = new ToolTip
            {
                IsBalloon = false,
                ShowAlways = false
            };

            _completionList = new NonFocusableCompletionListBox
            {
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                Height = 180,
                Width = 260,
                SelectionMode = SelectionMode.One,
                TabStop = false
            };
            _completionList.DoubleClick += (_, _) => AcceptCurrentCompletion();
            _completionList.MouseClick += (_, _) => AcceptCurrentCompletion();
            _completionList.GotFocus += (_, _) =>
            {
                // Keep keyboard focus on the editor so typing continues while popup is visible.
                BeginInvoke(new Action(EnsureEditorFocus));
            };

            _completionPopup = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(CompletionPopupWidth, _completionList.Height + 2),
                Visible = false,
                TabStop = false
            };
            _completionPopup.Controls.Add(_completionList);
            _completionList.Dock = DockStyle.Fill;
            _completionPopup.MouseDown += (_, _) =>
            {
                BeginInvoke(new Action(EnsureEditorFocus));
            };

            Controls.Add(_editor);
            Controls.Add(_completionPopup);
            _completionPopup.BringToFront();
            ApplyTheme(false);
        }

        [AllowNull]
        public override string Text
        {
            get => _editor.Text;
            set
            {
                var newText = value ?? string.Empty;
                if (string.Equals(_editor.Text, newText, StringComparison.Ordinal))
                {
                    return;
                }

                _suppressTextProcessing = true;
                try
                {
                    _editor.Text = newText;
                }
                finally
                {
                    _suppressTextProcessing = false;
                }

                if (_settings.EnableSyntaxHighlighting)
                {
                    ApplySyntaxHighlightingToAllLines();
                }
                else
                {
                    ResetSyntaxFormatting();
                }

                if (_settings.EnableInlineValidation && _validationService != null)
                {
                    _validationService.RequestValidation(_editor.Text);
                }
                else if (!_settings.EnableInlineValidation)
                {
                    ClearDiagnostics();
                }
            }
        }

        public bool ReadOnly
        {
            get => _editor.ReadOnly;
            set => _editor.ReadOnly = value;
        }

        public bool WordWrap
        {
            get => _editor.WordWrap;
            set => _editor.WordWrap = value;
        }

        public new bool Focused => _editor.Focused || base.Focused;

        public new Font Font
        {
            get => _editor.Font;
            set
            {
                _editor.Font = value;
                base.Font = value;
            }
        }

        public new Color BackColor
        {
            get => _editor.BackColor;
            set
            {
                _editor.BackColor = value;
                base.BackColor = value;
            }
        }

        public new Color ForeColor
        {
            get => _editor.ForeColor;
            set
            {
                _editor.ForeColor = value;
                base.ForeColor = value;
            }
        }

        public int SelectionStart
        {
            get => _editor.SelectionStart;
            set => _editor.SelectionStart = Math.Clamp(value, 0, _editor.TextLength);
        }

        public int SelectionLength
        {
            get => _editor.SelectionLength;
            set => _editor.SelectionLength = Math.Clamp(value, 0, _editor.TextLength - _editor.SelectionStart);
        }

        public string SelectedText => _editor.SelectedText;

        public bool AcceptsTab
        {
            get => _editor.AcceptsTab;
            set => _editor.AcceptsTab = value;
        }

        public bool Multiline
        {
            get => true;
            set { }
        }

        public ScrollBars ScrollBars
        {
            get => _editor.ScrollBars switch
            {
                RichTextBoxScrollBars.None => ScrollBars.None,
                RichTextBoxScrollBars.Horizontal => ScrollBars.Horizontal,
                RichTextBoxScrollBars.Vertical => ScrollBars.Vertical,
                _ => ScrollBars.Both
            };
            set => _editor.ScrollBars = value switch
            {
                ScrollBars.None => RichTextBoxScrollBars.None,
                ScrollBars.Horizontal => RichTextBoxScrollBars.Horizontal,
                ScrollBars.Vertical => RichTextBoxScrollBars.Vertical,
                _ => RichTextBoxScrollBars.Both
            };
        }

        public new BorderStyle BorderStyle
        {
            get => _editor.BorderStyle;
            set => _editor.BorderStyle = value;
        }

        public new ContextMenuStrip? ContextMenuStrip
        {
            get => _editor.ContextMenuStrip;
            set
            {
                base.ContextMenuStrip = value;
                _editor.ContextMenuStrip = value;
            }
        }

        public Control AsControl() => this;

        public bool FocusEditor() => _editor.Focus();

        public new bool Focus() => _editor.Focus();

        public void Clear() => _editor.Clear();

        public void SelectAll() => _editor.SelectAll();

        public void Copy() => _editor.Copy();

        public void Cut() => _editor.Cut();

        public void Paste() => _editor.Paste();

        public int GetLineFromCharIndex(int charIndex)
        {
            return _editor.GetLineFromCharIndex(Math.Clamp(charIndex, 0, _editor.TextLength));
        }

        public int GetFirstCharIndexOfCurrentLine()
        {
            var lineIndex = _editor.GetLineFromCharIndex(_editor.SelectionStart);
            var first = _editor.GetFirstCharIndexFromLine(lineIndex);
            return first < 0 ? 0 : first;
        }

        public (int Line, int Column) GetCaretPosition()
        {
            var lineIndex = _editor.GetLineFromCharIndex(_editor.SelectionStart);
            var first = _editor.GetFirstCharIndexFromLine(lineIndex);
            if (first < 0)
                first = 0;

            return (lineIndex + 1, _editor.SelectionStart - first + 1);
        }

        public void SetDiagnostics(IReadOnlyList<EditorDiagnostic> diagnostics)
        {
            var safeDiagnostics = diagnostics ?? Array.Empty<EditorDiagnostic>();
            if (AreDiagnosticsEquivalent(_diagnostics, safeDiagnostics))
            {
                return;
            }

            _diagnostics = safeDiagnostics;
            ApplyDiagnosticsVisuals();
        }

        public void ClearDiagnostics()
        {
            if (_diagnostics.Count == 0)
            {
                return;
            }

            _diagnostics = Array.Empty<EditorDiagnostic>();
            ApplyDiagnosticsVisuals();
        }

        public void SetAutocompleteProvider(ScriptAutocompleteProvider provider)
        {
            _autocompleteProvider = provider;
        }

        public void SetSyntaxHighlighter(YamlSshSyntaxHighlighter highlighter)
        {
            _syntaxHighlighter = highlighter;
            if (_settings.EnableSyntaxHighlighting)
            {
                ApplySyntaxHighlightingToAllLines();
            }
        }

        public void SetValidationService(ScriptEditorValidationService validationService)
        {
            if (_validationService != null)
            {
                _validationService.DiagnosticsUpdated -= ValidationService_DiagnosticsUpdated;
            }

            _validationService = validationService;
            if (_validationService != null)
            {
                _validationService.DiagnosticsUpdated += ValidationService_DiagnosticsUpdated;
                _validationService.ApplySettings(_settings);
            }
        }

        public void SetVariableTooltipResolvers(
            Func<string, string?>? variableResolver,
            Func<string, string?>? columnResolver)
        {
            _variableResolver = variableResolver;
            _columnResolver = columnResolver;
        }

        public void ApplyCommandEditorSettings(CommandEditorSettings settings)
        {
            _settings = (settings ?? new CommandEditorSettings()).CloneNormalized();

            _validationService?.ApplySettings(_settings);
            if (!_settings.EnableInlineValidation)
            {
                _validationService?.CancelPendingValidation();
                ClearDiagnostics();
            }
            else if (_validationService != null && ScriptParser.IsYamlScript(_editor.Text))
            {
                _validationService.RequestValidation(_editor.Text);
            }

            if (_settings.EnableSyntaxHighlighting)
            {
                ApplySyntaxHighlightingToAllLines();
            }
            else
            {
                ResetSyntaxFormatting();
            }
        }

        public void ApplyTheme(bool darkMode)
        {
            _isDarkMode = darkMode;
            if (darkMode)
            {
                _editor.BackColor = Color.FromArgb(37, 37, 38);
                _editor.ForeColor = Color.FromArgb(220, 220, 220);
                _completionPopup.BackColor = Color.FromArgb(55, 55, 56);
                _completionList.BackColor = Color.FromArgb(45, 45, 46);
                _completionList.ForeColor = Color.FromArgb(220, 220, 220);
            }
            else
            {
                _editor.BackColor = Color.FromArgb(253, 253, 253);
                _editor.ForeColor = Color.FromArgb(33, 37, 41);
                _completionPopup.BackColor = Color.FromArgb(214, 214, 214);
                _completionList.BackColor = Color.White;
                _completionList.ForeColor = Color.Black;
            }

            if (_settings.EnableSyntaxHighlighting)
            {
                ApplySyntaxHighlightingToAllLines();
            }
            ApplyDiagnosticsVisuals();
        }

        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            OnTextChanged(EventArgs.Empty);
            if (_suppressTextProcessing)
                return;

            var changedLine = _editor.GetLineFromCharIndex(_editor.SelectionStart);
            ApplySyntaxHighlightingForLines([changedLine]);

            if (_settings.EnableInlineValidation && _validationService != null)
            {
                _validationService.RequestValidation(_editor.Text);
            }
            else if (!_settings.EnableInlineValidation)
            {
                ClearDiagnostics();
            }
        }

        private void Editor_KeyDown(object? sender, KeyEventArgs e)
        {
            OnKeyDown(e);

            if (HandleCompletionNavigation(e))
                return;

            if (HandleIndentationKeys(e))
                return;

            if (HandleSmartEnter(e))
                return;

            if (_settings.EnableAutocomplete && e.Control && e.KeyCode == Keys.Space)
            {
                ShowCompletionPopup();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void Editor_KeyUp(object? sender, KeyEventArgs e)
        {
            OnKeyUp(e);
            if (!_settings.EnableAutocomplete || !_settings.AutocompleteShowOnTyping)
                return;

            if (ShouldTriggerAutocompleteOnKeyUp(e))
            {
                ShowCompletionPopup();
            }
        }

        private void Editor_MouseUp(object? sender, MouseEventArgs e)
        {
            HideCompletionPopup();
            OnMouseUp(e);
        }

        private void Editor_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_editor.TextLength == 0)
            {
                HideTooltip();
                return;
            }

            var charIndex = _editor.GetCharIndexFromPosition(e.Location);
            if (charIndex < 0 || charIndex > _editor.TextLength)
            {
                HideTooltip();
                return;
            }

            var lineNumber = _editor.GetLineFromCharIndex(charIndex) + 1;
            var firstChar = _editor.GetFirstCharIndexFromLine(lineNumber - 1);
            if (firstChar < 0)
                firstChar = 0;
            var column = charIndex - firstChar + 1;

            if (_settings.EnableDiagnosticTooltips)
            {
                var diagnostic = _diagnostics.FirstOrDefault(d => d.Contains(lineNumber, column));
                if (diagnostic != null)
                {
                    ShowTooltip(diagnostic.Message, e.Location);
                    return;
                }
            }

            if (_settings.EnableVariableInspectorTooltips)
            {
                var variableTooltip = ResolveVariableTooltip(charIndex);
                if (!string.IsNullOrEmpty(variableTooltip))
                {
                    ShowTooltip(variableTooltip, e.Location);
                    return;
                }
            }

            HideTooltip();
        }

        private bool HandleCompletionNavigation(KeyEventArgs e)
        {
            if (!_completionPopup.Visible)
                return false;

            if (e.KeyCode == Keys.Down)
            {
                if (_completionList.Items.Count > 0)
                {
                    _completionList.SelectedIndex = Math.Min(_completionList.Items.Count - 1, _completionList.SelectedIndex + 1);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return true;
            }

            if (e.KeyCode == Keys.Up)
            {
                if (_completionList.Items.Count > 0)
                {
                    _completionList.SelectedIndex = Math.Max(0, _completionList.SelectedIndex - 1);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return true;
            }

            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                AcceptCurrentCompletion();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return true;
            }

            if (e.KeyCode == Keys.Escape)
            {
                HideCompletionPopup();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return true;
            }

            return false;
        }

        private bool HandleIndentationKeys(KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Tab || e.Control || e.Alt)
                return false;

            var result = EditorTextUtilities.ApplyIndentation(
                _editor.Text,
                _editor.SelectionStart,
                _editor.SelectionLength,
                _settings.IndentSize,
                e.Shift,
                _settings.UseSpacesForTab);

            ApplyTextEdit(result);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return true;
        }

        private bool HandleSmartEnter(KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || !_settings.EnableSmartEnter || e.Control || e.Alt)
                return false;

            var result = EditorTextUtilities.ApplySmartEnter(
                _editor.Text,
                _editor.SelectionStart,
                _editor.SelectionLength,
                _settings.IndentSize,
                _settings.PreserveBlankLineBetweenSteps);

            ApplyTextEdit(result);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return true;
        }

        private static bool ShouldTriggerAutocompleteOnKeyUp(KeyEventArgs e)
        {
            if (e.Control || e.Alt)
                return false;

            if (e.KeyCode is
                Keys.Left or
                Keys.Right or
                Keys.Up or
                Keys.Down or
                Keys.Escape or
                Keys.Enter or
                Keys.Tab or
                Keys.ShiftKey or
                Keys.ControlKey or
                Keys.Menu)
                return false;

            return true;
        }

        private void ShowCompletionPopup()
        {
            if (_autocompleteProvider == null)
                return;

            var completion = _autocompleteProvider.GetCompletion(_editor.Text, _editor.SelectionStart);
            if (completion.Items.Count == 0)
            {
                HideCompletionPopup();
                EnsureEditorFocus();
                return;
            }

            _completionReplaceStart = completion.ReplaceStart;
            _completionReplaceLength = completion.ReplaceLength;
            _activeCompletionItems = completion.Items.ToList();

            UpdateCompletionPopupSize(_activeCompletionItems.Count);

            _completionList.BeginUpdate();
            _completionList.Items.Clear();
            foreach (var item in _activeCompletionItems)
            {
                _completionList.Items.Add(item);
            }
            _completionList.SelectedIndex = 0;
            _completionList.EndUpdate();

            var caretPoint = _editor.GetPositionFromCharIndex(_editor.SelectionStart);
            var popupLocation = CalculateCompletionPopupLocation(caretPoint);
            if (!_completionPopup.Visible)
            {
                _completionPopup.Location = popupLocation;
                _completionPopup.Visible = true;
                _completionPopup.BringToFront();
            }
            else
            {
                if (_completionPopup.Location != popupLocation)
                {
                    _completionPopup.Location = popupLocation;
                }
            }

            EnsureEditorFocus();

            BeginInvoke(new Action(() =>
            {
                EnsureEditorFocus();
            }));
        }

        private void HideCompletionPopup()
        {
            if (_completionPopup.Visible)
            {
                _completionPopup.Visible = false;
            }
        }

        private void UpdateCompletionPopupSize(int itemCount)
        {
            var safeItemCount = Math.Max(1, itemCount);
            var rowHeight = Math.Max(18, _completionList.ItemHeight);
            var maxVisibleRows = 8;
            var visibleRows = Math.Min(maxVisibleRows, safeItemCount);
            var desiredListHeight = (visibleRows * rowHeight) + 2;
            var desiredPopupHeight = desiredListHeight + 2;
            var maxPopupHeight = Math.Max(rowHeight + 4, ClientSize.Height - 4);
            var popupHeight = Math.Min(desiredPopupHeight, maxPopupHeight);
            var popupWidth = Math.Min(CompletionPopupWidth, Math.Max(120, ClientSize.Width - 4));

            _completionPopup.Size = new Size(popupWidth, popupHeight);
        }

        private Point CalculateCompletionPopupLocation(Point caretPoint)
        {
            var caretInControl = PointToClient(_editor.PointToScreen(caretPoint));
            var lineHeight = Math.Max(18, _editor.Font.Height + 4);
            var verticalGap = 2;
            var popupHeight = _completionPopup.Height;
            var popupWidth = _completionPopup.Width;

            var preferredBelowY = caretInControl.Y + lineHeight;
            var preferredAboveY = caretInControl.Y - popupHeight - verticalGap;

            var availableBelow = ClientSize.Height - preferredBelowY;
            var availableAbove = caretInControl.Y - verticalGap;

            var y = (availableBelow >= popupHeight || availableBelow >= availableAbove)
                ? preferredBelowY
                : preferredAboveY;
            y = Math.Clamp(y, 0, Math.Max(0, ClientSize.Height - popupHeight));

            var x = Math.Clamp(caretInControl.X, 0, Math.Max(0, ClientSize.Width - popupWidth));
            return new Point(x, y);
        }

        private void AcceptCurrentCompletion()
        {
            if (_completionList.SelectedIndex < 0 || _completionList.SelectedIndex >= _activeCompletionItems.Count)
                return;

            var completion = _activeCompletionItems[_completionList.SelectedIndex];
            var safeStart = Math.Clamp(_completionReplaceStart, 0, _editor.TextLength);
            var safeLength = Math.Clamp(_completionReplaceLength, 0, _editor.TextLength - safeStart);

            _suppressTextProcessing = true;
            try
            {
                _editor.Select(safeStart, safeLength);
                _editor.SelectedText = completion.InsertText;
                _editor.SelectionStart = safeStart + completion.InsertText.Length;
                _editor.SelectionLength = 0;
            }
            finally
            {
                _suppressTextProcessing = false;
            }

            HideCompletionPopup();
            Editor_TextChanged(this, EventArgs.Empty);
        }

        private void ApplyTextEdit(EditorTextEdit edit)
        {
            var replacement = BuildTextReplacement(_editor.Text, edit.Text);
            var previousFirstVisibleLine = GetFirstVisibleLine();
            var previousScrollPosition = TryGetScrollPosition();

            SetRedraw(enabled: false);
            _suppressTextProcessing = true;
            try
            {
                if (replacement.HasValue)
                {
                    var change = replacement.Value;
                    var safeStart = Math.Clamp(change.Start, 0, _editor.TextLength);
                    var safeOldLength = Math.Clamp(change.OldLength, 0, _editor.TextLength - safeStart);
                    _editor.Select(safeStart, safeOldLength);
                    _editor.SelectedText = change.NewText;
                }
                else
                {
                    _editor.Text = edit.Text;
                }

                _editor.SelectionStart = Math.Clamp(edit.SelectionStart, 0, _editor.TextLength);
                _editor.SelectionLength = Math.Clamp(edit.SelectionLength, 0, _editor.TextLength - _editor.SelectionStart);

                if (previousScrollPosition.HasValue)
                {
                    RestoreScrollPosition(previousScrollPosition.Value);
                }
                else
                {
                    RestoreFirstVisibleLine(previousFirstVisibleLine);
                }
            }
            finally
            {
                _suppressTextProcessing = false;
                SetRedraw(enabled: true);
                _editor.Invalidate();
            }

            if (_settings.EnableSyntaxHighlighting)
            {
                if (replacement.HasValue)
                {
                    ApplySyntaxHighlightingForReplacement(replacement.Value);
                }
                else
                {
                    ApplySyntaxHighlightingToAllLines();
                }
            }
            else
            {
                ResetSyntaxFormatting();
            }

            if (_settings.EnableInlineValidation && _validationService != null)
            {
                _validationService.RequestValidation(_editor.Text);
            }
            else if (!_settings.EnableInlineValidation)
            {
                ClearDiagnostics();
            }
        }

        private void ApplySyntaxHighlightingForReplacement(TextReplacement replacement)
        {
            if (_editor.TextLength == 0)
            {
                return;
            }

            var lines = _editor.Lines;
            if (lines.Length == 0)
            {
                return;
            }

            var safeStart = Math.Clamp(replacement.Start, 0, _editor.TextLength);
            var safeEnd = Math.Clamp(replacement.Start + Math.Max(0, replacement.NewLength), 0, _editor.TextLength);
            var startLine = _editor.GetLineFromCharIndex(safeStart);
            var endLine = _editor.GetLineFromCharIndex(safeEnd);

            var firstLine = Math.Max(0, startLine - 1);
            var lastLine = Math.Min(lines.Length - 1, endLine + 1);
            if (lastLine < firstLine)
            {
                lastLine = firstLine;
            }

            ApplySyntaxHighlightingForLines(Enumerable.Range(firstLine, (lastLine - firstLine) + 1));
        }

        private static TextReplacement? BuildTextReplacement(string originalText, string updatedText)
        {
            originalText ??= string.Empty;
            updatedText ??= string.Empty;

            if (string.Equals(originalText, updatedText, StringComparison.Ordinal))
            {
                return null;
            }

            var maxPrefix = Math.Min(originalText.Length, updatedText.Length);
            var prefixLength = 0;
            while (prefixLength < maxPrefix && originalText[prefixLength] == updatedText[prefixLength])
            {
                prefixLength++;
            }

            var originalSuffixIndex = originalText.Length - 1;
            var updatedSuffixIndex = updatedText.Length - 1;
            while (
                originalSuffixIndex >= prefixLength &&
                updatedSuffixIndex >= prefixLength &&
                originalText[originalSuffixIndex] == updatedText[updatedSuffixIndex])
            {
                originalSuffixIndex--;
                updatedSuffixIndex--;
            }

            var oldLength = Math.Max(0, originalSuffixIndex - prefixLength + 1);
            var newLength = Math.Max(0, updatedSuffixIndex - prefixLength + 1);
            var newText = newLength == 0
                ? string.Empty
                : updatedText.Substring(prefixLength, newLength);

            return new TextReplacement(prefixLength, oldLength, newText);
        }

        private void ApplySyntaxHighlightingToAllLines()
        {
            var lineCount = _editor.Lines.Length;
            ApplySyntaxHighlightingForLines(Enumerable.Range(0, lineCount));
        }

        private void ApplySyntaxHighlightingForLines(IEnumerable<int> lineIndices)
        {
            if (!_settings.EnableSyntaxHighlighting || _syntaxHighlighter == null)
                return;

            var lines = _editor.Lines;
            if (lines.Length == 0)
                return;

            var safeLineIndices = lineIndices.Distinct().Where(i => i >= 0 && i < lines.Length).ToList();
            if (safeLineIndices.Count == 0)
                return;

            ApplyVisualUpdatePreservingView(() =>
            {
                foreach (var lineIndex in safeLineIndices)
                {
                    var lineStart = _editor.GetFirstCharIndexFromLine(lineIndex);
                    if (lineStart < 0)
                        continue;

                    var lineText = lines[lineIndex];
                    _editor.Select(lineStart, lineText.Length);
                    _editor.SelectionColor = _editor.ForeColor;

                    var highlights = _syntaxHighlighter.BuildLineHighlights(lineText, lineStart, _isDarkMode);
                    foreach (var highlight in highlights)
                    {
                        if (highlight.Length <= 0)
                            continue;
                        var start = Math.Clamp(highlight.Start, 0, _editor.TextLength);
                        var length = Math.Clamp(highlight.Length, 0, _editor.TextLength - start);
                        if (length <= 0)
                            continue;

                        _editor.Select(start, length);
                        _editor.SelectionColor = highlight.Color;
                    }
                }
            });
        }

        private void ResetSyntaxFormatting()
        {
            ApplyVisualUpdatePreservingView(() =>
            {
                _editor.SelectAll();
                _editor.SelectionColor = _editor.ForeColor;
            });
        }

        private void ApplyDiagnosticsVisuals()
        {
            ApplyVisualUpdatePreservingView(() =>
            {
                _editor.SelectAll();
                _editor.SelectionBackColor = _editor.BackColor;

                foreach (var diagnostic in _diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Warning && !_settings.ShowInlineWarnings)
                        continue;

                    var start = GetCharIndexForDiagnostic(diagnostic.LineNumber, diagnostic.ColumnStart);
                    var end = GetCharIndexForDiagnostic(diagnostic.LineNumber, diagnostic.ColumnEnd);
                    if (start < 0 || end < start)
                        continue;

                    var length = Math.Max(1, end - start + 1);
                    _editor.Select(start, Math.Min(length, _editor.TextLength - start));
                    _editor.SelectionBackColor = GetDiagnosticBackground(diagnostic.Severity);
                }
            });
        }

        private void ApplyVisualUpdatePreservingView(Action action)
        {
            if (action == null || _editor.IsDisposed)
                return;

            var previousSelectionStart = _editor.SelectionStart;
            var previousSelectionLength = _editor.SelectionLength;
            var previousFirstVisibleLine = GetFirstVisibleLine();
            var previousScrollPosition = TryGetScrollPosition();
            var wasSuppressingTextProcessing = _suppressTextProcessing;

            SetRedraw(enabled: false);
            _suppressTextProcessing = true;
            try
            {
                action();
            }
            finally
            {
                var safeSelectionStart = Math.Clamp(previousSelectionStart, 0, _editor.TextLength);
                var safeSelectionLength = Math.Clamp(previousSelectionLength, 0, _editor.TextLength - safeSelectionStart);
                _editor.Select(safeSelectionStart, safeSelectionLength);

                if (previousScrollPosition.HasValue)
                {
                    RestoreScrollPosition(previousScrollPosition.Value);
                }
                else
                {
                    RestoreFirstVisibleLine(previousFirstVisibleLine);
                }

                _suppressTextProcessing = wasSuppressingTextProcessing;
                SetRedraw(enabled: true);
                _editor.Invalidate();
            }
        }

        private void SetRedraw(bool enabled)
        {
            if (!_editor.IsHandleCreated)
                return;

            _ = SendMessage(
                _editor.Handle,
                WmSetRedraw,
                enabled ? new IntPtr(1) : IntPtr.Zero,
                IntPtr.Zero);
        }

        private int GetFirstVisibleLine()
        {
            if (!_editor.IsHandleCreated)
                return 0;

            return SendMessage(
                _editor.Handle,
                EmGetFirstVisibleLine,
                IntPtr.Zero,
                IntPtr.Zero).ToInt32();
        }

        private void RestoreFirstVisibleLine(int targetLine)
        {
            if (!_editor.IsHandleCreated)
                return;

            var currentLine = GetFirstVisibleLine();
            var delta = targetLine - currentLine;
            if (delta == 0)
                return;

            _ = SendMessage(
                _editor.Handle,
                EmLineScroll,
                IntPtr.Zero,
                new IntPtr(delta));
        }

        private NativePoint? TryGetScrollPosition()
        {
            if (!_editor.IsHandleCreated)
                return null;

            var point = new NativePoint();
            var success = SendMessage(
                _editor.Handle,
                EmGetScrollPos,
                IntPtr.Zero,
                ref point);

            return success == IntPtr.Zero ? null : point;
        }

        private void RestoreScrollPosition(NativePoint point)
        {
            if (!_editor.IsHandleCreated)
                return;

            _ = SendMessage(
                _editor.Handle,
                EmSetScrollPos,
                IntPtr.Zero,
                ref point);
        }

        private void EnsureEditorFocus()
        {
            if (IsDisposed || _editor.IsDisposed)
            {
                return;
            }

            if (!_editor.Focused)
            {
                ActiveControl = _editor;
                _editor.Focus();
            }
        }

        private static bool AreDiagnosticsEquivalent(
            IReadOnlyList<EditorDiagnostic> current,
            IReadOnlyList<EditorDiagnostic> incoming)
        {
            if (ReferenceEquals(current, incoming))
            {
                return true;
            }

            if (current.Count != incoming.Count)
            {
                return false;
            }

            for (var i = 0; i < current.Count; i++)
            {
                var left = current[i];
                var right = incoming[i];
                if (left.LineNumber != right.LineNumber ||
                    left.ColumnStart != right.ColumnStart ||
                    left.ColumnEnd != right.ColumnEnd ||
                    left.Severity != right.Severity ||
                    !string.Equals(left.Message, right.Message, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private int GetCharIndexForDiagnostic(int lineNumber, int column)
        {
            var lineIndex = Math.Max(0, lineNumber - 1);
            if (lineIndex >= _editor.Lines.Length)
                return -1;

            var lineStart = _editor.GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0)
                return -1;

            var safeColumn = Math.Max(1, column);
            var lineLength = _editor.Lines[lineIndex].Length;
            return lineStart + Math.Min(lineLength, safeColumn - 1);
        }

        private Color GetDiagnosticBackground(DiagnosticSeverity severity)
        {
            if (_isDarkMode)
            {
                return severity switch
                {
                    DiagnosticSeverity.Error => Color.FromArgb(90, 60, 60),
                    DiagnosticSeverity.Warning => Color.FromArgb(90, 82, 40),
                    _ => Color.FromArgb(50, 72, 92)
                };
            }

            return severity switch
            {
                DiagnosticSeverity.Error => Color.FromArgb(255, 228, 228),
                DiagnosticSeverity.Warning => Color.FromArgb(255, 245, 204),
                _ => Color.FromArgb(220, 240, 255)
            };
        }

        private void ValidationService_DiagnosticsUpdated(object? sender, IReadOnlyList<EditorDiagnostic> diagnostics)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetDiagnostics(diagnostics)));
                return;
            }

            SetDiagnostics(diagnostics);
        }

        private string ResolveVariableTooltip(int charIndex)
        {
            var lineIndex = _editor.GetLineFromCharIndex(charIndex);
            if (lineIndex < 0 || lineIndex >= _editor.Lines.Length)
                return string.Empty;

            var lineStart = _editor.GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0)
                return string.Empty;

            var lineText = _editor.Lines[lineIndex];
            var localIndex = charIndex - lineStart;

            foreach (Match match in VariableTokenRegex.Matches(lineText))
            {
                if (localIndex < match.Index || localIndex > match.Index + match.Length)
                    continue;

                if (match.Groups["name"].Success)
                {
                    var variableName = match.Groups["name"].Value;
                    var value = _variableResolver?.Invoke(variableName);
                    return value == null
                        ? $"${{{variableName}}} = [unresolved]"
                        : $"${{{variableName}}} = {value}";
                }

                if (match.Groups["column"].Success)
                {
                    var columnName = match.Groups["column"].Value;
                    var value = _columnResolver?.Invoke(columnName);
                    return value == null
                        ? $"{{{{{columnName}}}}} = [column not found]"
                        : $"{{{{{columnName}}}}} = {value}";
                }
            }

            return string.Empty;
        }

        private void ShowTooltip(string message, Point location)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                HideTooltip();
                return;
            }

            if (string.Equals(_lastTooltipMessage, message, StringComparison.Ordinal))
                return;

            _lastTooltipMessage = message;
            _toolTip.Show(message, _editor, location.X + 12, location.Y + 16, 3000);
        }

        private void HideTooltip()
        {
            _lastTooltipMessage = string.Empty;
            _toolTip.Hide(_editor);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_validationService != null)
                {
                    _validationService.DiagnosticsUpdated -= ValidationService_DiagnosticsUpdated;
                }

                _completionPopup.Dispose();
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        private sealed class NonFocusableCompletionListBox : ListBox
        {
            public NonFocusableCompletionListBox()
            {
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
            }

            protected override void OnEnter(EventArgs e)
            {
                // Keep focus with editor host; completion list is visual only.
            }
        }
    }
}
