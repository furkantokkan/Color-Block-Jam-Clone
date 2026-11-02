#if UNITY_EDITOR
using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures
{
    /// <summary>
    /// A real Environment prefab clone, the real board art and a BoardPresenter over them, plus a
    /// 3 x 3 test level whose red single block (block 0) sits flush below a red one-cell top door.
    /// </summary>
    public sealed class PresenterScene : IDisposable
    {
        private const string k_environmentPrefabPath = "Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab";
        private const string k_environmentDatabasePath = "Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset";
        private const string k_palettePath = "Assets/ColorBlockJam/Data/Art/GamePalette.asset";
        private const string k_catalogPath = "Assets/ColorBlockJam/Data/Board/BoardPieceCatalog.asset";
        private const int k_size = 3;

        /// <summary>The exiting block's origin: column 1 of the top row, under the door.</summary>
        public static readonly Cell ExitBlockOrigin = new Cell(1, 2);

        private readonly GameObject m_root;

        public PresenterScene()
        {
            m_root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(k_environmentPrefabPath));
            Environment = m_root.GetComponent<EnvironmentView>();
            Environment.Construct(AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(k_environmentDatabasePath));
            Presenter = new BoardPresenter(Environment, CreateArt());
        }

        /// <summary>The board art the Game scene binds: the shared palette and piece catalog.</summary>
        public static BoardArt CreateArt()
        {
            return new BoardArt(AssetDatabase.LoadAssetAtPath<GamePalette>(k_palettePath),
                AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(k_catalogPath));
        }

        public EnvironmentView Environment { get; }

        public BoardPresenter Presenter { get; }

        /// <summary>
        /// Block 0 is a red Single at (1, 2), flush with the red top door over column 1. With a second
        /// block, block 1 is a blue DoubleHorizontal at (0, 0), so exiting block 0 does not clear the board.
        /// </summary>
        public static AttemptLevel CreateLevel(bool withSecondBlock)
        {
            BlockColor red = new BlockColor((int)PaletteColor.Red);
            int blockCount = withSecondBlock ? 2 : 1;
            BlockDefinition[] blocks = new BlockDefinition[blockCount];
            BlockVisualSpec[] visuals = new BlockVisualSpec[blockCount];
            string[] ids = new string[blockCount];

            blocks[0] = new BlockDefinition(0, red, ExitBlockOrigin, new[] { new Cell(0, 0) });
            visuals[0] = new BlockVisualSpec(PaletteColor.Red, new[] { Vector2Int.zero },
                new Vector2Int(ExitBlockOrigin.X, ExitBlockOrigin.Y));
            ids[0] = "exit";
            if (withSecondBlock)
            {
                blocks[1] = new BlockDefinition(1, new BlockColor((int)PaletteColor.Blue), new Cell(0, 0),
                    new[] { new Cell(0, 0), new Cell(1, 0) });
                visuals[1] = new BlockVisualSpec(PaletteColor.Blue,
                    new[] { Vector2Int.zero, Vector2Int.right }, Vector2Int.zero);
                ids[1] = "stay";
            }

            bool[] floor = new bool[k_size * k_size];
            for (int i = 0; i < floor.Length; i++)
            {
                floor[i] = true;
            }

            DoorDefinition[] doors = { new DoorDefinition(0, Side.Top, 1, 1, red) };
            if (!LevelModel.TryCreate(k_size, k_size, floor, doors, blocks, 60, out LevelModel model, out string error)
                || !BoardLayout.TryCreate(k_size, k_size, Array.Empty<Vector2Int>(),
                    new[] { new LevelDoorData("top", BoardSide.Top, 1, 1, "Red") }, out BoardLayout layout, out error))
            {
                throw new InvalidOperationException(error);
            }

            return new AttemptLevel(model, layout, "presenter-test", visuals, ids);
        }

        public void Dispose()
        {
            Presenter.Dispose();
            Object.DestroyImmediate(m_root);
        }
    }
}
#endif
