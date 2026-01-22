using Microsoft.Xna.Framework;

namespace XNAControls.Input
{
    /// <summary>
    /// Interface for transforming mouse coordinates from window space to game space.
    /// Used for scaled rendering where the game is rendered to a smaller render target
    /// and then scaled up to fill the window.
    /// </summary>
    public interface IMouseCoordinateTransformer
    {
        /// <summary>
        /// Transforms a mouse position from window coordinates to game coordinates.
        /// </summary>
        /// <param name="windowPosition">The mouse position in window coordinates.</param>
        /// <returns>The transformed position in game coordinates.</returns>
        Point TransformMousePosition(Point windowPosition);
    }
}
