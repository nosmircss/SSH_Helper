namespace SSH_Helper.Models
{
    /// <summary>
    /// Per-preset (and global-default) Flow Canvas layout behavior.
    /// AutoFlow: the canvas re-lays-out on edits/reopen (positions are transient).
    /// Manual: the user's arrangement is preserved and never auto-reflowed.
    /// </summary>
    public enum LayoutMode
    {
        AutoFlow,
        Manual,
    }
}
