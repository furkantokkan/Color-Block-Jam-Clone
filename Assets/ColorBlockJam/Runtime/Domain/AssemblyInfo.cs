using System.Runtime.CompilerServices;

// BoardState's mutators are internal, so only Domain command handlers change the board during play
// (DEC-122). The Gameplay tests arrange board states through them directly.
[assembly: InternalsVisibleTo("ColorBlockJam.Gameplay.EditModeTests")]
[assembly: InternalsVisibleTo("ColorBlockJam.Gameplay.PlayModeTests")]
