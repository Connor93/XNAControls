using Microsoft.Xna.Framework;

namespace XNAControls
{
    /// <summary>
    /// Provides the logical game viewport dimensions for dialog centering.
    /// In scaled rendering mode, this returns the game dimensions (e.g., 640x480 or 1280x720)
    /// rather than the physical window dimensions.
    /// </summary>
    public interface IGameViewportProvider
    {
        /// <summary>
        /// Gets the logical game viewport width for UI positioning.
        /// </summary>
        int GameWidth { get; }

        /// <summary>
        /// Gets the logical game viewport height for UI positioning.
        /// </summary>
        int GameHeight { get; }
    }
}
