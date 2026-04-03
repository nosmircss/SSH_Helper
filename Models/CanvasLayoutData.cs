using System.Collections.Generic;
using System.Linq;

namespace SSH_Helper.Models
{
    /// <summary>
    /// Persisted Flow Canvas layout data stored alongside a preset.
    /// Contains node positions, comment nodes, and disabled block state.
    /// Layout is gated by a structure hash — only applied when the script
    /// structure (block types and step paths) matches.
    /// </summary>
    public class CanvasLayoutData
    {
        /// <summary>
        /// SHA256 hash of the script structure (block types + step paths).
        /// Used to detect structural changes that invalidate stored positions.
        /// </summary>
        public string StructureHash { get; set; } = string.Empty;

        /// <summary>
        /// Node positions keyed by node ID (e.g., "__start__", "node-0", "node-1").
        /// </summary>
        public Dictionary<string, NodePosition> Positions { get; set; } = new();

        /// <summary>
        /// Comment nodes placed on the canvas.
        /// </summary>
        public List<CanvasComment> Comments { get; set; } = new();

        /// <summary>
        /// Node IDs of blocks that are disabled (skipped during execution).
        /// </summary>
        public List<string> DisabledBlockIds { get; set; } = new();

        public CanvasLayoutData Clone()
        {
            return new CanvasLayoutData
            {
                StructureHash = StructureHash,
                Positions = Positions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new NodePosition { X = kvp.Value.X, Y = kvp.Value.Y }),
                Comments = Comments.Select(c => new CanvasComment
                {
                    Id = c.Id,
                    Text = c.Text,
                    Color = c.Color,
                    X = c.X,
                    Y = c.Y,
                    Width = c.Width,
                    Height = c.Height,
                    AttachedToNodeId = c.AttachedToNodeId,
                }).ToList(),
                DisabledBlockIds = new List<string>(DisabledBlockIds),
            };
        }
    }

    public class NodePosition
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class CanvasComment
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Color { get; set; } = "#e0c040";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 200;
        public double Height { get; set; } = 100;
        public string? AttachedToNodeId { get; set; }
    }
}
