using System;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// One authored board object (catalog section 5): an ID, a set of absolute board cells in authored
    /// order, and exactly one effect that gives the cells their behaviour.
    /// </summary>
    [Serializable]
    public sealed class LevelCellObjectData
    {
        [SerializeField] private string m_id;
        [SerializeField] private Vector2Int[] m_cells = Array.Empty<Vector2Int>();
        [SerializeField] private LevelEffectData m_effect;

        public string Id => m_id;
        public int CellCount => m_cells?.Length ?? 0;
        public LevelEffectData Effect => m_effect;

        public LevelCellObjectData()
        {
        }

        public LevelCellObjectData(string id, Vector2Int[] cells, LevelEffectData effect)
        {
            m_id = id;
            m_cells = cells == null ? null : (Vector2Int[])cells.Clone();
            m_effect = effect?.Clone();
        }

        public Vector2Int GetCellAt(int index)
        {
            return m_cells[index];
        }

        public bool ContainsCell(Vector2Int cell)
        {
            for (int index = 0; index < CellCount; index++)
            {
                if (m_cells[index] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The typed parameters when the effect has type T, for example BarrierObject.</summary>
        public bool TryGetEffect<T>(out T parameters) where T : LevelEffectParameters
        {
            parameters = m_effect?.Parameters as T;
            return parameters != null;
        }

        public LevelCellObjectData Clone()
        {
            return new LevelCellObjectData(m_id, m_cells, m_effect);
        }
    }
}
