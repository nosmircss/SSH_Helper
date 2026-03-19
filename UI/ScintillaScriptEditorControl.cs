using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SSH_Helper.Models;
using SSH_Helper.Services.Editor;
using SSH_Helper.Services.Scripting;
using ScintillaNET;
using ScintillaBorderStyle = ScintillaNET.BorderStyle;
using WinFormsBorderStyle = System.Windows.Forms.BorderStyle;

namespace SSH_Helper.UI
{
    internal sealed class ScintillaScriptEditorControl : UserControl, IScriptEditor
    {
        private static readonly Regex VariableTokenRegex =
            new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}|\{\{(?<column>[^}\s]+)\}\}",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private const int FirstCustomStyleIndex = Style.BraceBad + 1;
        private const int LineNumberMarginIndex = 0;
        private const int LineNumberSpacerMarginIndex = 1;
        private const int FoldMarginIndex = 2;
        private const int MinimumLineNumberDigits = 2;
        private const int MinimumLineNumberMarginWidth = 24;
        private const int LineNumberRightPaddingSpaces = 1;
        private const int LineNumberMarginExtraPaddingPixels = 4;
        private const int LineNumberSpacerWidthPixels = 10;
        private const int FoldMarginWidthPixels = 14;
        private const int FoldLevelBase = 1024;
        private const int ErrorIndicatorIndex = 8;
        private const int WarningIndicatorIndex = 9;
        private const int CompletionPopupWidth = 262;
        private const int WmLButtonDown = 0x0201;
        private const int WmRButtonDown = 0x0204;
        private const int WmMButtonDown = 0x0207;
        private const int WmXButtonDown = 0x020B;
        private const int WmNcLButtonDown = 0x00A1;
        private const int WmNcRButtonDown = 0x00A4;
        private const int WmNcMButtonDown = 0x00A7;
        private const int WmNcXButtonDown = 0x00AB;
        private static readonly Color DarkLineNumberTextColor = Color.FromArgb(160, 160, 160);
        private static readonly Color LightLineNumberTextColor = Color.FromArgb(115, 115, 115);
        private static readonly Color DarkLineNumberBackColor = Color.FromArgb(30, 30, 30);
        private static readonly Color LightLineNumberBackColor = Color.FromArgb(242, 242, 242);
        private static readonly Color DarkCurrentLineColor = Color.FromArgb(48, 48, 51);
        private static readonly Color LightCurrentLineColor = Color.FromArgb(241, 246, 255);
        private static readonly Color DarkIndentGuideColor = Color.FromArgb(78, 78, 82);
        private static readonly Color LightIndentGuideColor = Color.FromArgb(205, 210, 219);
        private static readonly Color DarkLongLineGuideColor = Color.FromArgb(72, 72, 76);
        private static readonly Color LightLongLineGuideColor = Color.FromArgb(206, 211, 219);
        private static readonly Color DarkBraceMatchBackColor = Color.FromArgb(66, 66, 70);
        private static readonly Color LightBraceMatchBackColor = Color.FromArgb(225, 238, 255);
        private static readonly Color DarkBraceMismatchBackColor = Color.FromArgb(90, 43, 43);
        private static readonly Color LightBraceMismatchBackColor = Color.FromArgb(255, 233, 233);

        private readonly Scintilla _editor;
        private readonly ToolTip _toolTip;
        private readonly NonFocusableCompletionListBox _completionList;
        private readonly Panel _completionPopup;
        private readonly CompletionDismissMessageFilter _completionDismissFilter;
        private bool _completionDismissFilterRegistered;

        private IReadOnlyList<EditorDiagnostic> _diagnostics = Array.Empty<EditorDiagnostic>();
        private List<CompletionItem> _activeCompletionItems = new();
        private readonly Dictionary<Color, int> _styleByColor = new();
        private ScriptAutocompleteProvider? _autocompleteProvider;
        private YamlSshSyntaxHighlighter? _syntaxHighlighter;
        private ScriptEditorValidationService? _validationService;
        private CommandEditorSettings _settings = new();

        private bool _isDarkMode;
        private Color? _borderColor;
        private bool _acceptsTab = true;
        private bool _suppressTextProcessing;
        private bool _refreshAndValidationQueued;
        private int _nextStyleIndex = FirstCustomStyleIndex;
        private int _completionReplaceStart;
        private int _completionReplaceLength;
        private CompletionContextKind _activeCompletionContext;
        private string _lastTooltipMessage = string.Empty;
        private Func<string, string?>? _variableResolver;
        private Func<string, string?>? _columnResolver;

        public ScintillaScriptEditorControl()
        {
            _editor = new Scintilla
            {
                Dock = DockStyle.Fill,
                WrapMode = WrapMode.None,
                HScrollBar = true,
                VScrollBar = true,
                EndAtLastLine = false,
                MouseDwellTime = 250,
                BorderStyle = ScintillaBorderStyle.None
            };

            ConfigureLineNumberMargin();

            _editor.AutoCSeparator = '\n';
            _editor.AutoCCancelAtStart = false;
            _editor.AutoCAutoHide = true;
            _editor.AutoCDropRestOfWord = false;
            _editor.AutoCIgnoreCase = true;
            _editor.AutoCChooseSingle = false;
            _editor.AutoCMaxHeight = 8;
            _editor.TabIndents = false;
            _editor.UsePopup(false);
            _editor.HandleCreated += (_, _) =>
            {
                ApplyScrollbarTheme(_editor, _isDarkMode);
                UpdateLineNumberMarginWidth();
            };

            _editor.TextChanged += Editor_TextChanged;
            _editor.KeyDown += Editor_KeyDown;
            _editor.KeyUp += Editor_KeyUp;
            _editor.UpdateUI += Editor_UpdateUI;
            _editor.MouseUp += Editor_MouseUp;
            _editor.MarginClick += Editor_MarginClick;
            _editor.MouseLeave += (_, _) => HideTooltip();
            _editor.LostFocus += (_, _) => HideCompletionPopup();
            _editor.DwellStart += Editor_DwellStart;
            _editor.DwellEnd += (_, _) => HideTooltip();
            _editor.Click += (_, _) => OnClick(EventArgs.Empty);

            _toolTip = new ToolTip
            {
                IsBalloon = false,
                ShowAlways = false
            };

            _completionList = new NonFocusableCompletionListBox
            {
                BorderStyle = WinFormsBorderStyle.None,
                IntegralHeight = false,
                Height = 180,
                Width = 260,
                SelectionMode = SelectionMode.One,
                DrawMode = DrawMode.OwnerDrawFixed,
                TabStop = false
            };
            _completionList.DrawItem += CompletionList_DrawItem;
            _completionList.DoubleClick += (_, _) => AcceptCurrentCompletion();
            _completionList.MouseClick += (_, _) => AcceptCurrentCompletion();
            _completionList.GotFocus += (_, _) =>
            {
                BeginInvoke(new Action(EnsureEditorFocus));
            };
            _completionList.HandleCreated += (_, _) => ApplyScrollbarTheme(_completionList, _isDarkMode);

            _completionPopup = new Panel
            {
                BorderStyle = WinFormsBorderStyle.None,
                Padding = new Padding(1),
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
            _completionPopup.HandleCreated += (_, _) => ApplyScrollbarTheme(_completionPopup, _isDarkMode);

            Controls.Add(_editor);
            Controls.Add(_completionPopup);
            _completionPopup.BringToFront();
            _completionDismissFilter = new CompletionDismissMessageFilter(this);
            Application.AddMessageFilter(_completionDismissFilter);
            _completionDismissFilterRegistered = true;
            ApplyTheme(darkMode: false);
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
                    SetEditorTextPreservingReadOnly(newText);
                }
                finally
                {
                    _suppressTextProcessing = false;
                }

                RefreshEditorVisuals();
                RequestValidationOrClear();
            }
        }

        public bool ReadOnly
        {
            get => _editor.ReadOnly;
            set => _editor.ReadOnly = value;
        }

        public bool WordWrap
        {
            get => _editor.WrapMode != WrapMode.None;
            set => _editor.WrapMode = value ? WrapMode.Word : WrapMode.None;
        }

        public new bool Focused => _editor.Focused || base.Focused;

        public new Font Font
        {
            get => _editor.Font;
            set
            {
                _editor.Font = value;
                _completionList.Font = value;
                _completionList.ItemHeight = Math.Max(18, value.Height + 4);
                base.Font = value;
                ConfigureBaseStyles();
                RefreshEditorVisuals();
            }
        }

        public new Color BackColor
        {
            get => _editor.BackColor;
            set
            {
                _editor.BackColor = value;
                base.BackColor = value;
                ConfigureBaseStyles();
                ApplyDiagnosticsVisuals();
            }
        }

        public new Color ForeColor
        {
            get => _editor.ForeColor;
            set
            {
                _editor.ForeColor = value;
                base.ForeColor = value;
                ConfigureBaseStyles();
                RefreshEditorVisuals();
            }
        }

        public int SelectionStart
        {
            get => Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
            set
            {
                var safeStart = Math.Clamp(value, 0, _editor.TextLength);
                SetSelectionRange(safeStart, SelectionLength);
            }
        }

        public int SelectionLength
        {
            get => Math.Abs(_editor.SelectionEnd - _editor.SelectionStart);
            set => SetSelectionRange(SelectionStart, value);
        }

        public string SelectedText => _editor.SelectedText;

        public bool AcceptsTab
        {
            get => _acceptsTab;
            set => _acceptsTab = value;
        }

        public bool Multiline
        {
            get => true;
            set { }
        }

        public ScrollBars ScrollBars
        {
            get
            {
                if (_editor.HScrollBar && _editor.VScrollBar)
                {
                    return ScrollBars.Both;
                }

                if (_editor.HScrollBar)
                {
                    return ScrollBars.Horizontal;
                }

                if (_editor.VScrollBar)
                {
                    return ScrollBars.Vertical;
                }

                return ScrollBars.None;
            }
            set
            {
                _editor.HScrollBar = value is ScrollBars.Horizontal or ScrollBars.Both;
                _editor.VScrollBar = value is ScrollBars.Vertical or ScrollBars.Both;
            }
        }

        public new WinFormsBorderStyle BorderStyle
        {
            get => _editor.BorderStyle switch
            {
                ScintillaBorderStyle.None => WinFormsBorderStyle.None,
                ScintillaBorderStyle.Fixed3D => WinFormsBorderStyle.Fixed3D,
                _ => WinFormsBorderStyle.FixedSingle
            };
            set
            {
                _editor.BorderStyle = value switch
                {
                    WinFormsBorderStyle.None => ScintillaBorderStyle.None,
                    WinFormsBorderStyle.Fixed3D => ScintillaBorderStyle.Fixed3D,
                    _ => ScintillaBorderStyle.FixedSingle
                };
            }
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

        public void Clear() => Text = string.Empty;

        public void SelectAll() => _editor.SelectAll();

        public void Copy() => _editor.Copy();

        public void Cut() => _editor.Cut();

        public void Paste() => _editor.Paste();

        public int GetLineFromCharIndex(int charIndex)
        {
            return _editor.LineFromPosition(Math.Clamp(charIndex, 0, _editor.TextLength));
        }

        public int GetFirstCharIndexOfCurrentLine()
        {
            var lineIndex = _editor.LineFromPosition(_editor.CurrentPosition);
            if (lineIndex < 0 || lineIndex >= _editor.Lines.Count)
            {
                return 0;
            }

            return _editor.Lines[lineIndex].Position;
        }

        public (int Line, int Column) GetCaretPosition()
        {
            var position = Math.Clamp(_editor.CurrentPosition, 0, _editor.TextLength);
            var lineIndex = _editor.LineFromPosition(position);
            var column = _editor.GetColumn(position);
            return (lineIndex + 1, column + 1);
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
                ApplySyntaxHighlighting();
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
            _editor.TabWidth = _settings.IndentSize;
            _editor.IndentWidth = _settings.IndentSize;
            _editor.UseTabs = !_settings.UseSpacesForTab;

            _validationService?.ApplySettings(_settings);
            if (!_settings.EnableAutocomplete)
            {
                HideCompletionPopup();
            }

            ConfigureVisualOptions();
            RequestValidationOrClear();
            RefreshEditorVisuals();
        }

        public void ApplyTheme(bool darkMode)
        {
            _isDarkMode = darkMode;
            if (darkMode)
            {
                _editor.BackColor = Color.FromArgb(37, 37, 38);
                _editor.ForeColor = Color.FromArgb(220, 220, 220);
                _editor.CaretForeColor = Color.FromArgb(220, 220, 220);
                _editor.SelectionBackColor = Color.FromArgb(62, 62, 64);
                _editor.AutocompleteListBackColor = Color.FromArgb(45, 45, 46);
                _editor.AutocompleteListTextColor = Color.FromArgb(220, 220, 220);
                _editor.AutocompleteListSelectedBackColor = Color.FromArgb(14, 99, 156);
                _editor.AutocompleteListSelectedTextColor = Color.White;
                _completionPopup.BackColor = Color.FromArgb(88, 88, 91);
                _completionList.BackColor = Color.FromArgb(45, 45, 46);
                _completionList.ForeColor = Color.FromArgb(220, 220, 220);
                _borderColor = Color.FromArgb(65, 65, 65);
                Padding = new Padding(1);
                base.BackColor = _editor.BackColor;
            }
            else
            {
                _editor.BackColor = Color.FromArgb(253, 253, 253);
                _editor.ForeColor = Color.FromArgb(33, 37, 41);
                _editor.CaretForeColor = Color.FromArgb(33, 37, 41);
                _editor.SelectionBackColor = Color.FromArgb(173, 214, 255);
                _editor.AutocompleteListBackColor = Color.White;
                _editor.AutocompleteListTextColor = Color.Black;
                _editor.AutocompleteListSelectedBackColor = Color.FromArgb(13, 110, 253);
                _editor.AutocompleteListSelectedTextColor = Color.White;
                _completionPopup.BackColor = Color.FromArgb(214, 214, 214);
                _completionList.BackColor = Color.White;
                _completionList.ForeColor = Color.Black;
                _borderColor = null;
                Padding = Padding.Empty;
                base.BackColor = _editor.BackColor;
            }

            ApplyScrollbarTheme(_editor, darkMode);
            ApplyScrollbarTheme(_completionList, darkMode);
            ApplyScrollbarTheme(_completionPopup, darkMode);
            _completionList.Invalidate();
            ConfigureBaseStyles();
            ConfigureIndicators();
            ConfigureVisualOptions();
            RefreshEditorVisuals();
        }

        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            OnTextChanged(EventArgs.Empty);
            if (_suppressTextProcessing)
            {
                return;
            }

            if (_settings.EnableAutocomplete &&
                _settings.AutocompleteShowOnTyping &&
                _completionPopup.Visible)
            {
                ShowCompletionPopup();
            }

            QueueRefreshAndValidation();
        }

        private void QueueRefreshAndValidation()
        {
            if (_refreshAndValidationQueued)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                RefreshEditorVisuals();
                RequestValidationOrClear();
                return;
            }

            _refreshAndValidationQueued = true;
            BeginInvoke(new Action(() =>
            {
                _refreshAndValidationQueued = false;
                if (IsDisposed || _editor.IsDisposed)
                {
                    return;
                }

                RefreshEditorVisuals();
                RequestValidationOrClear();
            }));
        }

        private void SetEditorTextPreservingReadOnly(string value)
        {
            var wasReadOnly = _editor.ReadOnly;
            if (wasReadOnly)
            {
                _editor.ReadOnly = false;
            }

            try
            {
                _editor.Text = value;
            }
            finally
            {
                if (wasReadOnly)
                {
                    _editor.ReadOnly = true;
                }
            }
        }

        private void Editor_KeyDown(object? sender, KeyEventArgs e)
        {
            OnKeyDown(e);

            if (HandleCompletionNavigation(e))
            {
                return;
            }

            if (HandleIndentationKeys(e))
            {
                return;
            }

            if (HandleSmartEnter(e))
            {
                return;
            }

            if (_settings.EnableAutocomplete && e.Control && e.KeyCode == Keys.Space)
            {
                ShowCompletionPopup();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (_completionPopup.Visible &&
                e.KeyCode is Keys.Left or Keys.Right or Keys.Home or Keys.End)
            {
                HideCompletionPopup();
            }
        }

        private void Editor_KeyUp(object? sender, KeyEventArgs e)
        {
            OnKeyUp(e);
            if (!_settings.EnableAutocomplete || !_settings.AutocompleteShowOnTyping)
            {
                return;
            }

            if (ShouldTriggerAutocompleteOnKeyUp(e))
            {
                ShowCompletionPopup();
            }
        }

        private void Editor_UpdateUI(object? sender, UpdateUIEventArgs e)
        {
            if ((e.Change & UpdateChange.Selection) != 0)
            {
                UpdateBraceHighlighting();
            }

            if (!_completionPopup.Visible)
            {
                return;
            }

            var trackedChanges = UpdateChange.Selection | UpdateChange.VScroll | UpdateChange.HScroll;
            if ((e.Change & trackedChanges) == 0)
            {
                return;
            }

            if ((e.Change & UpdateChange.Selection) != 0)
            {
                RefreshVisibleCompletionPopup();
                return;
            }

            RepositionOrHideCompletionPopup();
        }

        private void Editor_MouseUp(object? sender, MouseEventArgs e)
        {
            HideCompletionPopup();
            OnMouseUp(e);
        }

        private void Editor_MarginClick(object? sender, MarginClickEventArgs e)
        {
            if (!_settings.EnableCodeFolding || e.Margin != FoldMarginIndex)
            {
                return;
            }

            var lineIndex = _editor.LineFromPosition(e.Position);
            if (lineIndex < 0 || lineIndex >= _editor.Lines.Count)
            {
                return;
            }

            var line = _editor.Lines[lineIndex];
            if ((line.FoldLevelFlags & FoldLevelFlags.Header) != 0)
            {
                line.ToggleFold();
            }
        }

        private void Editor_DwellStart(object? sender, DwellEventArgs e)
        {
            if (e.Position < 0 || e.Position > _editor.TextLength)
            {
                HideTooltip();
                return;
            }

            var position = e.Position;
            var lineNumber = _editor.LineFromPosition(position) + 1;
            var column = _editor.GetColumn(position) + 1;
            var location = new Point(Math.Max(0, e.X), Math.Max(0, e.Y));

            if (_settings.EnableDiagnosticTooltips)
            {
                var diagnostic = _diagnostics.FirstOrDefault(d => d.Contains(lineNumber, column));
                if (diagnostic != null)
                {
                    ShowTooltip(diagnostic.Message, location);
                    return;
                }
            }

            if (_settings.EnableVariableInspectorTooltips)
            {
                var variableTooltip = ResolveVariableTooltip(position);
                if (!string.IsNullOrEmpty(variableTooltip))
                {
                    ShowTooltip(variableTooltip, location);
                    return;
                }
            }

            HideTooltip();
        }

        private bool HandleCompletionNavigation(KeyEventArgs e)
        {
            if (!_completionPopup.Visible)
            {
                return false;
            }

            if (e.KeyCode == Keys.Tab && e.Shift)
            {
                HideCompletionPopup();
                return false;
            }

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

            if (e.KeyCode == Keys.Enter || (e.KeyCode == Keys.Tab && !e.Shift))
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
            if (!_acceptsTab || e.KeyCode != Keys.Tab || e.Control || e.Alt)
            {
                return false;
            }

            var result = EditorTextUtilities.ApplyIndentation(
                _editor.Text,
                SelectionStart,
                SelectionLength,
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
            if (e.KeyCode != Keys.Enter || !_settings.EnableSmartEnter || e.Alt)
            {
                return false;
            }

            var result = e.Control
                ? EditorTextUtilities.ApplySiblingStepEnter(
                    _editor.Text,
                    SelectionStart,
                    SelectionLength,
                    _settings.IndentSize)
                : EditorTextUtilities.ApplySmartEnter(
                    _editor.Text,
                    SelectionStart,
                    SelectionLength,
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
            {
                return false;
            }

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
            {
                return false;
            }

            return true;
        }

        private void ShowCompletionPopup()
        {
            ShowCompletionPopupCore();
        }

        private void ShowCompletionPopupCore()
        {
            if (!_settings.EnableAutocomplete || _autocompleteProvider == null)
            {
                HideCompletionPopup();
                return;
            }

            var completion = _autocompleteProvider.GetCompletion(_editor.Text, _editor.CurrentPosition);
            if (completion.Items.Count == 0)
            {
                HideCompletionPopup();
                EnsureEditorFocus();
                return;
            }

            _completionReplaceStart = completion.ReplaceStart;
            _completionReplaceLength = completion.ReplaceLength;
            _activeCompletionContext = completion.Context;
            _activeCompletionItems = new List<CompletionItem>();
            var seenEntries = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in completion.Items)
            {
                var entry = string.IsNullOrWhiteSpace(item.InsertText) ? item.Label : item.InsertText;
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                if (seenEntries.Add(entry))
                {
                    _activeCompletionItems.Add(item);
                }
            }

            if (_activeCompletionItems.Count == 0)
            {
                HideCompletionPopup();
                EnsureEditorFocus();
                return;
            }

            UpdateCompletionPopupSize(_activeCompletionItems.Count);
            _completionList.BeginUpdate();
            _completionList.Items.Clear();
            foreach (var item in _activeCompletionItems)
            {
                _completionList.Items.Add(item);
            }
            _completionList.SelectedIndex = 0;
            _completionList.EndUpdate();

            if (!TryGetCaretViewportPoint(out var caretPoint))
            {
                HideCompletionPopup();
                EnsureEditorFocus();
                return;
            }

            var popupLocation = CalculateCompletionPopupLocation(caretPoint);

            if (!_completionPopup.Visible)
            {
                _completionPopup.Location = popupLocation;
                _completionPopup.Visible = true;
                _completionPopup.BringToFront();
            }
            else if (_completionPopup.Location != popupLocation)
            {
                _completionPopup.Location = popupLocation;
            }

            ApplyScrollbarTheme(_completionPopup, _isDarkMode);
            ApplyScrollbarTheme(_completionList, _isDarkMode);
            EnsureEditorFocus();
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(EnsureEditorFocus));
            }
        }

        private void HideCompletionPopup()
        {
            if (_completionPopup.Visible)
            {
                _completionPopup.Visible = false;
            }

            _activeCompletionContext = CompletionContextKind.None;
        }

        private void RefreshVisibleCompletionPopup()
        {
            if (!_completionPopup.Visible)
            {
                return;
            }

            ShowCompletionPopupCore();
        }

        private void DismissCompletionOnExternalClick(IntPtr targetHandle)
        {
            if (!_completionPopup.Visible)
            {
                return;
            }

            if (targetHandle != IntPtr.Zero &&
                Control.FromHandle(targetHandle) is Control targetControl &&
                IsControlInEditorHierarchy(targetControl))
            {
                return;
            }

            HideCompletionPopup();
        }

        private bool IsControlInEditorHierarchy(Control control)
        {
            for (Control? current = control; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, this))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateCompletionPopupSize(int itemCount)
        {
            var safeItemCount = Math.Max(1, itemCount);
            var rowHeight = Math.Max(18, _completionList.ItemHeight);
            var maxVisibleRows = 8;
            var visibleRows = Math.Min(maxVisibleRows, safeItemCount);
            var desiredListHeight = (visibleRows * rowHeight) + 2;
            var desiredPopupHeight = desiredListHeight + _completionPopup.Padding.Vertical;
            var maxPopupHeight = Math.Max(rowHeight + _completionPopup.Padding.Vertical + 2, ClientSize.Height - 4);
            var popupHeight = Math.Min(desiredPopupHeight, maxPopupHeight);
            var popupWidth = Math.Min(CompletionPopupWidth, Math.Max(140, ClientSize.Width - 4));

            _completionPopup.Size = new Size(popupWidth, popupHeight);
        }

        private Point CalculateCompletionPopupLocation(Point caretPoint)
        {
            var lineIndex = _editor.CurrentLine >= 0 && _editor.CurrentLine < _editor.Lines.Count
                ? _editor.CurrentLine
                : Math.Max(0, _editor.Lines.Count - 1);
            var lineHeight = _editor.Lines.Count > 0
                ? Math.Max(18, _editor.Lines[lineIndex].Height)
                : Math.Max(18, _editor.Font.Height + 4);

            var verticalGap = 2;
            var popupHeight = _completionPopup.Height;
            var popupWidth = _completionPopup.Width;

            var preferredBelowY = caretPoint.Y + lineHeight;
            var preferredAboveY = caretPoint.Y - popupHeight - verticalGap;

            var availableBelow = ClientSize.Height - preferredBelowY;
            var availableAbove = caretPoint.Y - verticalGap;

            var y = (availableBelow >= popupHeight || availableBelow >= availableAbove)
                ? preferredBelowY
                : preferredAboveY;
            y = Math.Clamp(y, 0, Math.Max(0, ClientSize.Height - popupHeight));

            var x = Math.Clamp(caretPoint.X, 0, Math.Max(0, ClientSize.Width - popupWidth));
            return new Point(x, y);
        }

        private void RepositionOrHideCompletionPopup()
        {
            if (!_completionPopup.Visible)
            {
                return;
            }

            if (!TryGetCaretViewportPoint(out var caretPoint))
            {
                HideCompletionPopup();
                return;
            }

            var popupLocation = CalculateCompletionPopupLocation(caretPoint);
            if (_completionPopup.Location != popupLocation)
            {
                _completionPopup.Location = popupLocation;
            }
        }

        private bool TryGetCaretViewportPoint(out Point caretPoint)
        {
            caretPoint = Point.Empty;

            var position = Math.Clamp(_editor.CurrentPosition, 0, _editor.TextLength);
            var lineIndex = _editor.LineFromPosition(position);
            var lineHeight = (_editor.Lines.Count > 0 && lineIndex >= 0 && lineIndex < _editor.Lines.Count)
                ? Math.Max(18, _editor.Lines[lineIndex].Height)
                : Math.Max(18, _editor.Font.Height + 4);

            var rawX = _editor.PointXFromPosition(position);
            var rawY = _editor.PointYFromPosition(position);
            if (rawX < 0 ||
                rawX > ClientSize.Width ||
                rawY + lineHeight < 0 ||
                rawY > ClientSize.Height)
            {
                return false;
            }

            caretPoint = new Point(rawX, rawY);
            return true;
        }

        private void AcceptCurrentCompletion()
        {
            if (_completionList.SelectedIndex < 0 || _completionList.SelectedIndex >= _activeCompletionItems.Count)
            {
                return;
            }

            var completion = _activeCompletionItems[_completionList.SelectedIndex];
            var safeStart = Math.Clamp(_completionReplaceStart, 0, _editor.TextLength);
            var safeLength = Math.Clamp(_completionReplaceLength, 0, _editor.TextLength - safeStart);
            var insertText = BuildCompletionInsertText(completion, safeStart, safeLength);

            _suppressTextProcessing = true;
            _editor.BeginUndoAction();
            try
            {
                _editor.TargetStart = safeStart;
                _editor.TargetEnd = safeStart + safeLength;
                _editor.ReplaceTarget(insertText);
                var newCaret = safeStart + insertText.Length;
                _editor.SetSelection(newCaret, newCaret);
                _editor.ScrollCaret();
            }
            finally
            {
                _editor.EndUndoAction();
                _suppressTextProcessing = false;
            }

            HideCompletionPopup();
            Editor_TextChanged(this, EventArgs.Empty);
        }

        private string BuildCompletionInsertText(CompletionItem completion, int replaceStart, int replaceLength)
        {
            var baseInsertText = string.IsNullOrWhiteSpace(completion.InsertText) ? completion.Label : completion.InsertText;
            if (!ShouldAppendYamlKeySuffix(_activeCompletionContext))
            {
                return baseInsertText;
            }

            var nextIndex = Math.Clamp(replaceStart + replaceLength, 0, _editor.TextLength);
            if (nextIndex < _editor.TextLength && _editor.Text[nextIndex] == ':')
            {
                return baseInsertText;
            }

            return baseInsertText + ": ";
        }

        private static bool ShouldAppendYamlKeySuffix(CompletionContextKind context)
        {
            return context is
                CompletionContextKind.TopLevelKey or
                CompletionContextKind.StepCommand or
                CompletionContextKind.StepOptionKey;
        }

        private void CompletionList_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _completionList.Items.Count)
            {
                return;
            }

            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var background = selected
                ? (_isDarkMode ? Color.FromArgb(14, 99, 156) : Color.FromArgb(13, 110, 253))
                : (_isDarkMode ? Color.FromArgb(45, 45, 46) : Color.White);
            var foreground = selected
                ? Color.White
                : (_isDarkMode ? Color.FromArgb(220, 220, 220) : Color.Black);

            using var bgBrush = new SolidBrush(background);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            var itemText = _completionList.Items[e.Index]?.ToString() ?? string.Empty;
            var textBounds = Rectangle.Inflate(e.Bounds, -4, 0);
            TextRenderer.DrawText(
                e.Graphics,
                itemText,
                _completionList.Font,
                textBounds,
                foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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

        private void ApplyTextEdit(EditorTextEdit edit)
        {
            var replacement = BuildTextReplacement(_editor.Text, edit.Text);

            _suppressTextProcessing = true;
            _editor.BeginUndoAction();
            try
            {
                if (replacement.HasValue)
                {
                    var change = replacement.Value;
                    var safeStart = Math.Clamp(change.Start, 0, _editor.TextLength);
                    var safeOldLength = Math.Clamp(change.OldLength, 0, _editor.TextLength - safeStart);
                    _editor.TargetStart = safeStart;
                    _editor.TargetEnd = safeStart + safeOldLength;
                    _editor.ReplaceTarget(change.NewText);
                }
                else if (!string.Equals(_editor.Text, edit.Text, StringComparison.Ordinal))
                {
                    _editor.Text = edit.Text;
                }

                SetSelectionRange(edit.SelectionStart, edit.SelectionLength);
                _editor.ScrollCaret();
            }
            finally
            {
                _editor.EndUndoAction();
                _suppressTextProcessing = false;
            }

            RefreshEditorVisuals();
            RequestValidationOrClear();
        }

        private void SetSelectionRange(int selectionStart, int selectionLength)
        {
            var safeStart = Math.Clamp(selectionStart, 0, _editor.TextLength);
            var safeEnd = Math.Clamp(safeStart + Math.Max(0, selectionLength), 0, _editor.TextLength);
            _editor.SetSelection(safeEnd, safeStart);
        }

        private void RefreshEditorVisuals()
        {
            UpdateLineNumberMarginWidth();

            if (_settings.EnableSyntaxHighlighting)
            {
                ApplySyntaxHighlighting();
            }
            else
            {
                ResetSyntaxFormatting();
            }

            ApplyDiagnosticsVisuals();

            if (_settings.EnableCodeFolding)
            {
                UpdateFoldLevels();
            }

            UpdateBraceHighlighting();
        }

        private void ApplySyntaxHighlighting()
        {
            if (!_settings.EnableSyntaxHighlighting || _syntaxHighlighter == null)
            {
                return;
            }

            ConfigureBaseStyles();
            if (_editor.TextLength == 0 || _editor.Lines.Count == 0)
            {
                return;
            }

            _editor.StartStyling(0);
            _editor.SetStyling(_editor.TextLength, Style.Default);

            var spans = _syntaxHighlighter
                .BuildHighlights(_editor.Text, Enumerable.Range(0, _editor.Lines.Count), _isDarkMode)
                .OrderBy(span => span.Start)
                .ToList();

            foreach (var span in spans)
            {
                if (span.Length <= 0)
                {
                    continue;
                }

                var start = Math.Clamp(span.Start, 0, _editor.TextLength);
                var length = Math.Clamp(span.Length, 0, _editor.TextLength - start);
                if (length <= 0)
                {
                    continue;
                }

                var styleIndex = GetOrCreateStyleForColor(span.Color);
                _editor.StartStyling(start);
                _editor.SetStyling(length, styleIndex);
            }
        }

        private void ResetSyntaxFormatting()
        {
            ConfigureBaseStyles();
        }

        private int GetOrCreateStyleForColor(Color color)
        {
            if (_styleByColor.TryGetValue(color, out var styleIndex))
            {
                return styleIndex;
            }

            styleIndex = _nextStyleIndex++;
            if (styleIndex > byte.MaxValue)
            {
                return Style.Default;
            }

            _styleByColor[color] = styleIndex;
            var style = _editor.Styles[styleIndex];
            style.ForeColor = color;
            style.BackColor = _editor.BackColor;
            style.Font = _editor.Font.Name;
            style.SizeF = _editor.Font.SizeInPoints;
            return styleIndex;
        }

        private void ConfigureBaseStyles()
        {
            var defaultStyle = _editor.Styles[Style.Default];
            defaultStyle.Font = _editor.Font.Name;
            defaultStyle.SizeF = _editor.Font.SizeInPoints;
            defaultStyle.ForeColor = _editor.ForeColor;
            defaultStyle.BackColor = _editor.BackColor;
            _editor.StyleClearAll();

            var lineNumberStyle = _editor.Styles[Style.LineNumber];
            lineNumberStyle.Font = _editor.Font.Name;
            lineNumberStyle.SizeF = _editor.Font.SizeInPoints;
            lineNumberStyle.ForeColor = _isDarkMode
                ? DarkLineNumberTextColor
                : LightLineNumberTextColor;
            lineNumberStyle.BackColor = _isDarkMode
                ? DarkLineNumberBackColor
                : LightLineNumberBackColor;

            var spacerMargin = _editor.Margins[LineNumberSpacerMarginIndex];
            spacerMargin.BackColor = _isDarkMode ? DarkLineNumberBackColor : LightLineNumberBackColor;

            var indentGuideStyle = _editor.Styles[Style.IndentGuide];
            indentGuideStyle.ForeColor = _isDarkMode
                ? DarkIndentGuideColor
                : LightIndentGuideColor;
            indentGuideStyle.BackColor = _editor.BackColor;

            var braceLightStyle = _editor.Styles[Style.BraceLight];
            braceLightStyle.ForeColor = _editor.ForeColor;
            braceLightStyle.BackColor = _isDarkMode
                ? DarkBraceMatchBackColor
                : LightBraceMatchBackColor;

            var braceBadStyle = _editor.Styles[Style.BraceBad];
            braceBadStyle.ForeColor = _isDarkMode
                ? Color.FromArgb(255, 178, 178)
                : Color.FromArgb(179, 29, 29);
            braceBadStyle.BackColor = _isDarkMode
                ? DarkBraceMismatchBackColor
                : LightBraceMismatchBackColor;

            _styleByColor.Clear();
            _nextStyleIndex = FirstCustomStyleIndex;
        }

        private void ConfigureLineNumberMargin()
        {
            var lineNumberMargin = _editor.Margins[LineNumberMarginIndex];
            lineNumberMargin.Type = MarginType.Number;
            lineNumberMargin.Mask = 0;
            lineNumberMargin.Sensitive = false;
            lineNumberMargin.Width = MinimumLineNumberMarginWidth;

            var spacerMargin = _editor.Margins[LineNumberSpacerMarginIndex];
            spacerMargin.Type = MarginType.Color;
            spacerMargin.Mask = 0;
            spacerMargin.Sensitive = false;
            spacerMargin.Width = LineNumberSpacerWidthPixels;
            spacerMargin.BackColor = _isDarkMode ? DarkLineNumberBackColor : LightLineNumberBackColor;

            var foldMargin = _editor.Margins[FoldMarginIndex];
            foldMargin.Type = MarginType.Symbol;
            foldMargin.Mask = Marker.MaskFolders;
            foldMargin.Sensitive = true;
            foldMargin.Width = FoldMarginWidthPixels;
        }

        private void UpdateLineNumberMarginWidth()
        {
            var lineCount = Math.Max(1, _editor.Lines.Count);
            var digits = Math.Max(
                MinimumLineNumberDigits,
                lineCount.ToString(CultureInfo.InvariantCulture).Length);
            var lineNumberSample = new string('9', digits) + new string(' ', LineNumberRightPaddingSpaces);
            var preferredWidth = _editor.TextWidth(Style.LineNumber, lineNumberSample) + LineNumberMarginExtraPaddingPixels;
            _editor.Margins[LineNumberMarginIndex].Width = Math.Max(
                MinimumLineNumberMarginWidth,
                preferredWidth);
        }

        private void ConfigureVisualOptions()
        {
            ConfigureCurrentLineHighlight();
            ConfigureIndentGuides();
            ConfigureWhitespaceMarkers();
            ConfigureLongLineGuide();
            ConfigureCodeFolding();

            if (!_settings.EnableBraceMatching)
            {
                _editor.BraceHighlight(-1, -1);
                _editor.BraceBadLight(-1);
            }
        }

        private void ConfigureCurrentLineHighlight()
        {
            var baseColor = _isDarkMode
                ? DarkCurrentLineColor
                : LightCurrentLineColor;
            var alpha = _settings.EnableCurrentLineHighlight ? 96 : 0;
            _editor.CaretLineBackColor = Color.FromArgb(alpha, baseColor);
        }

        private void ConfigureIndentGuides()
        {
            _editor.IndentationGuides = _settings.EnableIndentGuides
                ? IndentView.LookBoth
                : IndentView.None;
        }

        private void ConfigureWhitespaceMarkers()
        {
            _editor.ViewWhitespace = _settings.ShowWhitespace
                ? WhitespaceMode.VisibleAlways
                : WhitespaceMode.Invisible;
            _editor.ViewEol = false;

            var whitespaceColor = _isDarkMode
                ? Color.FromArgb(94, 94, 100)
                : Color.FromArgb(196, 200, 208);
            _editor.WhitespaceTextColor = whitespaceColor;
            _editor.WhitespaceBackColor = _editor.BackColor;
        }

        private void ConfigureLongLineGuide()
        {
            _editor.EdgeColumn = _settings.LongLineColumn;
            _editor.EdgeMode = _settings.EnableLongLineGuide
                ? EdgeMode.Line
                : EdgeMode.None;
            _editor.EdgeColor = _isDarkMode
                ? DarkLongLineGuideColor
                : LightLongLineGuideColor;
        }

        private void ConfigureCodeFolding()
        {
            var foldMargin = _editor.Margins[FoldMarginIndex];
            foldMargin.Type = MarginType.Symbol;
            foldMargin.Mask = Marker.MaskFolders;
            foldMargin.Sensitive = _settings.EnableCodeFolding;
            foldMargin.Width = _settings.EnableCodeFolding
                ? FoldMarginWidthPixels
                : 0;

            _editor.SetProperty("fold", _settings.EnableCodeFolding ? "1" : "0");
            _editor.AutomaticFold = _settings.EnableCodeFolding
                ? AutomaticFold.Show | AutomaticFold.Click
                : AutomaticFold.None;

            var foldMarginColor = _isDarkMode
                ? DarkLineNumberBackColor
                : LightLineNumberBackColor;
            foldMargin.BackColor = foldMarginColor;
            _editor.SetFoldMarginColor(true, foldMarginColor);
            _editor.SetFoldMarginHighlightColor(true, foldMarginColor);

            ConfigureFoldMarkers();

            if (!_settings.EnableCodeFolding)
            {
                ResetFoldLevels();
            }
        }

        private void ConfigureFoldMarkers()
        {
            var markerColor = _isDarkMode
                ? DarkLineNumberTextColor
                : LightLineNumberTextColor;

            ConfigureChevronFoldMarker(Marker.Folder, expanded: false, markerColor);
            ConfigureChevronFoldMarker(Marker.FolderOpen, expanded: true, markerColor);
            ConfigureChevronFoldMarker(Marker.FolderEnd, expanded: false, markerColor);
            ConfigureChevronFoldMarker(Marker.FolderOpenMid, expanded: true, markerColor);
            ConfigureFoldMarker(Marker.FolderMidTail, MarkerSymbol.Empty, markerColor, Color.Transparent);
            ConfigureFoldMarker(Marker.FolderTail, MarkerSymbol.Empty, markerColor, Color.Transparent);
            ConfigureFoldMarker(Marker.FolderSub, MarkerSymbol.Empty, markerColor, Color.Transparent);
        }

        private void ConfigureChevronFoldMarker(int markerIndex, bool expanded, Color color)
        {
            using var image = CreateChevronMarkerBitmap(expanded, color);
            _editor.Markers[markerIndex].DefineRgbaImage(image);
        }

        private static Bitmap CreateChevronMarkerBitmap(bool expanded, Color color)
        {
            const int size = 9;
            var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var pen = new Pen(color, 1.4f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };

            if (expanded)
            {
                graphics.DrawLines(pen, new[]
                {
                    new PointF(2.2f, 3.0f),
                    new PointF(4.5f, 5.7f),
                    new PointF(6.8f, 3.0f)
                });
            }
            else
            {
                graphics.DrawLines(pen, new[]
                {
                    new PointF(3.0f, 2.2f),
                    new PointF(5.7f, 4.5f),
                    new PointF(3.0f, 6.8f)
                });
            }

            return bitmap;
        }

        private void ConfigureFoldMarker(int markerIndex, MarkerSymbol symbol, Color foreColor, Color backColor)
        {
            var marker = _editor.Markers[markerIndex];
            marker.Symbol = symbol;
            marker.SetForeColor(foreColor);
            marker.SetBackColor(backColor);
        }

        private void UpdateFoldLevels()
        {
            if (!_settings.EnableCodeFolding)
            {
                return;
            }

            var lineCount = _editor.Lines.Count;
            if (lineCount == 0)
            {
                return;
            }

            var indentLevels = new int[lineCount];
            for (var index = 0; index < lineCount; index++)
            {
                var lineText = _editor.Lines[index].Text?.TrimEnd('\r', '\n') ?? string.Empty;
                indentLevels[index] = string.IsNullOrWhiteSpace(lineText)
                    ? -1
                    : GetIndentationColumns(lineText);
            }

            for (var index = 0; index < lineCount; index++)
            {
                var line = _editor.Lines[index];
                var currentIndent = indentLevels[index];
                if (currentIndent < 0)
                {
                    var inheritedIndent = 0;
                    for (var previousIndex = index - 1; previousIndex >= 0; previousIndex--)
                    {
                        if (indentLevels[previousIndex] >= 0)
                        {
                            inheritedIndent = indentLevels[previousIndex];
                            break;
                        }
                    }

                    line.FoldLevel = FoldLevelBase + Math.Min(4095, inheritedIndent);
                    line.FoldLevelFlags = FoldLevelFlags.White;
                    continue;
                }

                var nextContentLine = FindNextContentLine(indentLevels, index + 1);
                var isHeader = nextContentLine >= 0 &&
                    indentLevels[nextContentLine] > currentIndent;

                line.FoldLevel = FoldLevelBase + Math.Min(4095, currentIndent);
                line.FoldLevelFlags = isHeader
                    ? FoldLevelFlags.Header
                    : (FoldLevelFlags)0;
            }
        }

        private void ResetFoldLevels()
        {
            for (var index = 0; index < _editor.Lines.Count; index++)
            {
                var line = _editor.Lines[index];
                line.FoldLevel = FoldLevelBase;
                line.FoldLevelFlags = (FoldLevelFlags)0;
            }
        }

        private int GetIndentationColumns(string lineText)
        {
            if (string.IsNullOrEmpty(lineText))
            {
                return 0;
            }

            var columns = 0;
            var tabSize = Math.Max(1, _settings.IndentSize);
            foreach (var character in lineText)
            {
                if (character == ' ')
                {
                    columns++;
                    continue;
                }

                if (character == '\t')
                {
                    columns += tabSize - (columns % tabSize);
                    continue;
                }

                break;
            }

            return columns;
        }

        private static int FindNextContentLine(int[] indentLevels, int startIndex)
        {
            for (var index = Math.Max(0, startIndex); index < indentLevels.Length; index++)
            {
                if (indentLevels[index] >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private void UpdateBraceHighlighting()
        {
            if (!_settings.EnableBraceMatching || _editor.TextLength <= 0)
            {
                _editor.BraceHighlight(-1, -1);
                _editor.BraceBadLight(-1);
                return;
            }

            if (!TryGetBracePositionAtCaret(out var bracePosition))
            {
                _editor.BraceHighlight(-1, -1);
                _editor.BraceBadLight(-1);
                return;
            }

            var matchPosition = _editor.BraceMatch(bracePosition);
            if (matchPosition >= 0)
            {
                _editor.BraceBadLight(-1);
                _editor.BraceHighlight(bracePosition, matchPosition);
                return;
            }

            _editor.BraceHighlight(-1, -1);
            _editor.BraceBadLight(bracePosition);
        }

        private bool TryGetBracePositionAtCaret(out int bracePosition)
        {
            bracePosition = -1;

            var caretPosition = Math.Clamp(_editor.CurrentPosition, 0, _editor.TextLength);
            if (caretPosition > 0)
            {
                var previousChar = (char)_editor.GetCharAt(caretPosition - 1);
                if (IsBraceCharacter(previousChar))
                {
                    bracePosition = caretPosition - 1;
                    return true;
                }
            }

            if (caretPosition < _editor.TextLength)
            {
                var currentChar = (char)_editor.GetCharAt(caretPosition);
                if (IsBraceCharacter(currentChar))
                {
                    bracePosition = caretPosition;
                    return true;
                }
            }

            return false;
        }

        private static bool IsBraceCharacter(char character)
        {
            return character is '{' or '}' or '[' or ']' or '(' or ')';
        }

        private void ConfigureIndicators()
        {
            var errorColor = _isDarkMode
                ? Color.FromArgb(255, 121, 121)
                : Color.FromArgb(210, 38, 48);
            var warningColor = _isDarkMode
                ? Color.FromArgb(241, 214, 118)
                : Color.FromArgb(181, 140, 0);

            var errorIndicator = _editor.Indicators[ErrorIndicatorIndex];
            errorIndicator.Style = IndicatorStyle.SquiggleLow;
            errorIndicator.ForeColor = errorColor;
            errorIndicator.Alpha = 255;
            errorIndicator.OutlineAlpha = 255;
            errorIndicator.Under = true;

            var warningIndicator = _editor.Indicators[WarningIndicatorIndex];
            warningIndicator.Style = IndicatorStyle.Squiggle;
            warningIndicator.ForeColor = warningColor;
            warningIndicator.Alpha = 255;
            warningIndicator.OutlineAlpha = 255;
            warningIndicator.Under = true;
        }

        private void ApplyDiagnosticsVisuals()
        {
            if (_editor.TextLength > 0)
            {
                _editor.IndicatorCurrent = ErrorIndicatorIndex;
                _editor.IndicatorClearRange(0, _editor.TextLength);
                _editor.IndicatorCurrent = WarningIndicatorIndex;
                _editor.IndicatorClearRange(0, _editor.TextLength);
            }

            foreach (var diagnostic in _diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Warning && !_settings.ShowInlineWarnings)
                {
                    continue;
                }

                var start = GetCharIndexForDiagnostic(diagnostic.LineNumber, diagnostic.ColumnStart);
                var end = GetCharIndexForDiagnostic(diagnostic.LineNumber, diagnostic.ColumnEnd);
                if (start < 0 || end < start)
                {
                    continue;
                }

                var length = Math.Max(1, end - start + 1);
                _editor.IndicatorCurrent = diagnostic.Severity == DiagnosticSeverity.Warning
                    ? WarningIndicatorIndex
                    : ErrorIndicatorIndex;
                _editor.IndicatorFillRange(start, length);
            }
        }

        private int GetCharIndexForDiagnostic(int lineNumber, int column)
        {
            var lineIndex = Math.Max(0, lineNumber - 1);
            if (lineIndex >= _editor.Lines.Count)
            {
                return -1;
            }

            var line = _editor.Lines[lineIndex];
            var lineText = line.Text?.TrimEnd('\r', '\n') ?? string.Empty;
            var safeColumn = Math.Max(1, column);
            return line.Position + Math.Min(lineText.Length, safeColumn - 1);
        }

        private void RequestValidationOrClear()
        {
            if (!_settings.EnableInlineValidation)
            {
                _validationService?.CancelPendingValidation();
                ClearDiagnostics();
                return;
            }

            var isYamlScript = ScriptParser.IsYamlScript(_editor.Text);

            if (_validationService != null && isYamlScript)
            {
                _validationService.RequestValidation(_editor.Text);
            }
            else if (!isYamlScript)
            {
                _validationService?.CancelPendingValidation();
                ClearDiagnostics();
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

        private void ValidationService_DiagnosticsUpdated(object? sender, IReadOnlyList<EditorDiagnostic> diagnostics)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetDiagnostics(diagnostics)));
                return;
            }

            SetDiagnostics(diagnostics);
        }

        private string ResolveVariableTooltip(int charIndex)
        {
            var lineIndex = _editor.LineFromPosition(charIndex);
            if (lineIndex < 0 || lineIndex >= _editor.Lines.Count)
            {
                return string.Empty;
            }

            var line = _editor.Lines[lineIndex];
            var lineText = line.Text?.TrimEnd('\r', '\n') ?? string.Empty;
            var localIndex = charIndex - line.Position;
            if (localIndex < 0 || localIndex > lineText.Length)
            {
                return string.Empty;
            }

            foreach (Match match in VariableTokenRegex.Matches(lineText))
            {
                if (localIndex < match.Index || localIndex > match.Index + match.Length)
                {
                    continue;
                }

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
            {
                return;
            }

            _lastTooltipMessage = message;
            _toolTip.Show(message, _editor, location.X + 12, location.Y + 16, 3000);
        }

        private void HideTooltip()
        {
            _lastTooltipMessage = string.Empty;
            _toolTip.Hide(_editor);
        }

        private static void ApplyScrollbarTheme(Control control, bool dark)
        {
            if (!control.IsHandleCreated)
            {
                return;
            }

            ApplyScrollbarThemeToHandle(control.Handle, dark);
        }

        private static void ApplyScrollbarThemeToHandle(IntPtr handle, bool dark)
        {
            try
            {
                ScrollbarThemeNative.AllowDarkModeForWindow(handle, dark);
            }
            catch (EntryPointNotFoundException)
            {
                // Older Windows build/API: continue with SetWindowTheme fallback.
            }
            catch (DllNotFoundException)
            {
                // Defensive; uxtheme should exist on Windows.
            }

            var theme = dark ? "DarkMode_Explorer" : "Explorer";
            try
            {
                ScrollbarThemeNative.SetWindowTheme(handle, theme, null);
                ScrollbarThemeNative.EnumChildWindows(handle, (childHwnd, _) =>
                {
                    try
                    {
                        ScrollbarThemeNative.AllowDarkModeForWindow(childHwnd, dark);
                    }
                    catch (EntryPointNotFoundException)
                    {
                        // Ignore; theme application still helps on unsupported systems.
                    }

                    ScrollbarThemeNative.SetWindowTheme(childHwnd, theme, null);
                    return true;
                }, IntPtr.Zero);

                ScrollbarThemeNative.SendMessage(handle, ScrollbarThemeNative.WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                ScrollbarThemeNative.SetWindowPos(
                    handle,
                    IntPtr.Zero,
                    0,
                    0,
                    0,
                    0,
                    ScrollbarThemeNative.SWP_NOMOVE |
                    ScrollbarThemeNative.SWP_NOSIZE |
                    ScrollbarThemeNative.SWP_NOZORDER |
                    ScrollbarThemeNative.SWP_FRAMECHANGED);
            }
            catch
            {
                // Themeing must not break editing if OS theming API behavior varies.
            }
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
                // Keep focus with the editor host so typing continues uninterrupted.
            }
        }

        private sealed class CompletionDismissMessageFilter : IMessageFilter
        {
            private readonly WeakReference<ScintillaScriptEditorControl> _owner;

            public CompletionDismissMessageFilter(ScintillaScriptEditorControl owner)
            {
                _owner = new WeakReference<ScintillaScriptEditorControl>(owner);
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (!IsMouseDownMessage(m.Msg) ||
                    !_owner.TryGetTarget(out var owner) ||
                    owner.IsDisposed)
                {
                    return false;
                }

                owner.DismissCompletionOnExternalClick(m.HWnd);
                return false;
            }

            private static bool IsMouseDownMessage(int message)
            {
                return message is
                    WmLButtonDown or
                    WmRButtonDown or
                    WmMButtonDown or
                    WmXButtonDown or
                    WmNcLButtonDown or
                    WmNcRButtonDown or
                    WmNcMButtonDown or
                    WmNcXButtonDown;
            }
        }

        private static class ScrollbarThemeNative
        {
            [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
            public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

            [DllImport("uxtheme.dll", EntryPoint = "#133", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);

            public delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool EnumChildWindows(IntPtr hwndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

            public const int WM_THEMECHANGED = 0x031A;
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOZORDER = 0x0004;
            public const uint SWP_FRAMECHANGED = 0x0020;
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

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (_borderColor.HasValue)
            {
                using var pen = new Pen(_borderColor.Value);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_completionDismissFilterRegistered)
                {
                    Application.RemoveMessageFilter(_completionDismissFilter);
                    _completionDismissFilterRegistered = false;
                }

                if (_validationService != null)
                {
                    _validationService.DiagnosticsUpdated -= ValidationService_DiagnosticsUpdated;
                }

                _completionPopup.Dispose();
                _toolTip.Dispose();
                _editor.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
