using System.Threading;
using System.Threading.Tasks;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// The full-screen black fade that covers a scene change (DEC-198); a seam so the scene transition can be tested
    /// without a UI document.
    /// </summary>
    public interface IScreenFade
    {
        /// <summary>True while any black shows; the fade then blocks presses on every UI Toolkit panel below it.</summary>
        bool IsVisible { get; }

        /// <summary>
        /// Fades the screen out to opaque black and completes once nothing else shows. Owner: the caller, which awaits
        /// it. Stops: the fade's end, or <paramref name="cancellationToken"/>, which leaves the black where it stopped.
        /// </summary>
        Task FadeOutAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Fades the screen back in from black and completes once the fade is hidden and lets presses through again.
        /// Owner and stop points as <see cref="FadeOutAsync"/>.
        /// </summary>
        Task FadeInAsync(CancellationToken cancellationToken);
    }
}
