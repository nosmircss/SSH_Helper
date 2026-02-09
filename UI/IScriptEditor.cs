using SSH_Helper.Services.Editor;

namespace SSH_Helper.UI
{
    internal interface IScriptEditor
    {
        string Text { get; set; }
        bool ReadOnly { get; set; }
        bool WordWrap { get; set; }
        int SelectionStart { get; set; }
        int SelectionLength { get; set; }
        string SelectedText { get; }
        bool Focused { get; }
        Font Font { get; set; }
        Color BackColor { get; set; }
        Color ForeColor { get; set; }

        Control AsControl();
        bool FocusEditor();
        void Clear();
        void SelectAll();
        void Copy();
        void Cut();
        void Paste();
        int GetLineFromCharIndex(int charIndex);
        int GetFirstCharIndexOfCurrentLine();
        (int Line, int Column) GetCaretPosition();
        void SetDiagnostics(IReadOnlyList<EditorDiagnostic> diagnostics);
        void ClearDiagnostics();
    }
}
