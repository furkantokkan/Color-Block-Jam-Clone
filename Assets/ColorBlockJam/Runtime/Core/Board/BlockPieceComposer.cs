using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Runtime.Board
{
    public enum BlockPieceKind { Center, Edge, OuterCorner, InnerCorner }

    /// <summary>One modular tile to place: its kind, grid vertex (in half-cell units, PositionTwice) and rotation.</summary>
    public readonly struct BlockPiecePlacement
    {
        public readonly BlockPieceKind Kind;
        public readonly Vector2Int PositionTwice;
        public readonly int QuarterTurns;

        public BlockPiecePlacement(BlockPieceKind kind, Vector2Int positionTwice, int quarterTurns)
        {
            Kind = kind;
            PositionTwice = positionTwice;
            QuarterTurns = quarterTurns;
        }
    }

    /// <summary>Turns a 4-connected cell footprint into vertex-centred Center, Edge, OuterCorner and InnerCorner placements of the modular kit. Build-time/footprint-change topology; no scene, named-shape or color dependency.</summary>
    public sealed class BlockPieceComposer
    {
        /// <summary>The most placements Compose can emit per footprint cell; callers size their output list by this.</summary>
        public const int MaxPlacementsPerCell = 9;

        private readonly HashSet<Vector2Int> m_cells;
        private readonly HashSet<Vector2Int> m_visited;
        private readonly Queue<Vector2Int> m_queue;
        private readonly HashSet<Vector2Int> m_vertices;
        private readonly List<Vector2Int> m_vertexOrder;
        private readonly int m_capacity;
        private static readonly Vector2Int[] s_directions =
        {
            Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
        };

        public BlockPieceComposer(int cellCapacity)
        {
            if (cellCapacity < 1) throw new ArgumentOutOfRangeException(nameof(cellCapacity));
            m_capacity = cellCapacity;
            m_cells = new HashSet<Vector2Int>(cellCapacity);
            m_visited = new HashSet<Vector2Int>(cellCapacity);
            m_queue = new Queue<Vector2Int>(cellCapacity);
            m_vertices = new HashSet<Vector2Int>(cellCapacity * 4);
            m_vertexOrder = new List<Vector2Int>(cellCapacity * 4);
        }

        // Vertex-centered tiles use the original FBX children, including their studs.
        // One gameplay cell spans 2x2 source subtiles (four LEGO studs).
        // Canonical occupancy: outer=SE, edge=SW+SE, inner=SW+SE+NE.
        // Positive quarter turns rotate counterclockwise in the grid.
        public void Compose(IReadOnlyList<Vector2Int> cells, List<BlockPiecePlacement> output)
        {
            if (cells == null || cells.Count == 0 || cells.Count > m_capacity)
                throw new ArgumentException("Footprint must fit the prepared non-empty capacity.", nameof(cells));
            if (output == null || output.Capacity < cells.Count * MaxPlacementsPerCell)
                throw new ArgumentException($"Prepare {MaxPlacementsPerCell} placement slots per cell before composition.", nameof(output));
            m_cells.Clear();
            for (int i = 0; i < cells.Count; i++)
                if (!m_cells.Add(cells[i])) throw new ArgumentException("Duplicate footprint cell.", nameof(cells));
            m_queue.Clear();
            m_visited.Clear();
            m_queue.Enqueue(cells[0]);
            m_visited.Add(cells[0]);
            while (m_queue.Count > 0)
            {
                Vector2Int cell = m_queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    Vector2Int next = cell + s_directions[d];
                    if (m_cells.Contains(next) && m_visited.Add(next)) m_queue.Enqueue(next);
                }
            }
            if (m_visited.Count != cells.Count)
                throw new ArgumentException("Footprint must be four-connected; split disconnected survivors first.", nameof(cells));

            output.Clear();
            m_vertices.Clear();
            m_vertexOrder.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                for (int y = -1; y <= 1; y += 2)
                    for (int x = -1; x <= 1; x += 2)
                    {
                        Vector2Int vertex = cell * 2 + new Vector2Int(x, y);
                        if (m_vertices.Add(vertex)) m_vertexOrder.Add(vertex);
                    }
            }
            for (int i = 0; i < m_vertexOrder.Count; i++)
            {
                Vector2Int vertex = m_vertexOrder[i];
                Vector2Int sw = new Vector2Int((vertex.x - 1) / 2, (vertex.y - 1) / 2);
                int mask = (m_cells.Contains(sw) ? 1 : 0)
                    | (m_cells.Contains(sw + Vector2Int.right) ? 2 : 0)
                    | (m_cells.Contains(sw + Vector2Int.one) ? 4 : 0)
                    | (m_cells.Contains(sw + Vector2Int.up) ? 8 : 0);
                if (mask == 15)
                {
                    output.Add(new BlockPiecePlacement(BlockPieceKind.Center, vertex, 0));
                    continue;
                }
                for (int q = 0; q < 4; q++)
                {
                    if (mask == RotateMask(2, q))
                        output.Add(new BlockPiecePlacement(BlockPieceKind.OuterCorner, vertex, q));
                    else if (mask == RotateMask(3, q))
                        output.Add(new BlockPiecePlacement(BlockPieceKind.Edge, vertex, q));
                    else if (mask == RotateMask(7, q))
                        output.Add(new BlockPiecePlacement(BlockPieceKind.InnerCorner, vertex, q));
                    else if ((mask == 5 || mask == 10) && (mask & RotateMask(2, q)) != 0)
                        output.Add(new BlockPiecePlacement(BlockPieceKind.OuterCorner, vertex, q));
                }
            }
        }

        private static int RotateMask(int mask, int turns) => ((mask << turns) | (mask >> (4 - turns))) & 15;
    }
}
