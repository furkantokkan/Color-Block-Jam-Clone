using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Hub.Launch
{
    /// <summary>Starts at most one Hub-to-Game transition at a time, so a second Play press does nothing.</summary>
    public interface ILevelLauncher
    {
        /// <summary>True from the start of a launch until it fails or is cancelled; stays true after it completes.</summary>
        bool IsLaunching { get; }

        Task<LevelLaunchResult> LaunchAsync(LevelLaunchRequest request, CancellationToken cancellationToken);
    }
}
