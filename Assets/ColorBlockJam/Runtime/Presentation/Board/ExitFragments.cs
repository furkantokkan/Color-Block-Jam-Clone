using System;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using Onity.Pooling;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// Owns one board's pooled gate-exit fragment emitters (DEC-131): it warms them on Home, plays one per
    /// valid exit, returns each once its last fragment dies, and pauses, stops or returns them all with
    /// the session's Suspend and Teardown. It never creates an emitter while Playing; a full pool skips.
    /// </summary>
    public sealed class ExitFragments : IDisposable
    {
        private readonly OnityObjectPool<ExitFragmentEmitter> m_pool;

        // The emitters checked out of the pool, first m_activeCount slots; sized to PreparedCount on Home.
        private ExitFragmentEmitter[] m_active = Array.Empty<ExitFragmentEmitter>();
        private int m_activeCount;
        private bool m_disposed;

        // Owner: the board presenter. Stops: Dispose, when the presenter is disposed.
        public ExitFragments(Mesh mesh, Material material, Transform storage)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            m_pool = new OnityObjectPool<ExitFragmentEmitter>(
                () => new ExitFragmentEmitter(mesh, material, storage),
                emitter => emitter.OnPoolGet(), emitter => emitter.OnPoolRelease(), emitter => emitter.Dispose(),
                false, ExitFragmentSettings.k_maxEmitters, ExitFragmentSettings.k_maxEmitters, "Exit fragments");
        }

        /// <summary>Emitters created so far; Restart and smaller levels reuse them.</summary>
        public int PreparedCount { get; private set; }

        /// <summary>Emitters playing a burst or waiting for its last fragment.</summary>
        public int ActiveCount => m_activeCount;

        /// <summary>
        /// Builds the pool over Unity's built-in Cube, the mesh the reference effect draws, and the shared
        /// board material that the art's block pieces render with, so a burst matches its block.
        /// </summary>
        public static ExitFragments Create(BoardArt art, Transform storage)
        {
            if (art == null) throw new ArgumentNullException(nameof(art));

            // The engine's own primitive, not a project asset; GameObject.CreatePrimitive uses the same mesh.
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Material material = art.Catalog.Get(BlockPieceKind.Center).Renderer.sharedMaterial;
            return new ExitFragments(cube, material, storage);
        }

        /// <summary>
        /// Warms emitters on Home up to capacity, capped at ExitFragmentSettings.k_maxEmitters. It never
        /// shrinks, so Restart and later levels create nothing new unless they need more.
        /// </summary>
        public void Prepare(int capacity)
        {
            ThrowIfDisposed();
            capacity = Math.Min(Math.Max(capacity, 0), ExitFragmentSettings.k_maxEmitters);
            if (capacity <= PreparedCount)
            {
                return;
            }

            ReturnAll();
            ExitFragmentEmitter[] warm = new ExitFragmentEmitter[capacity];
            int created = 0;
            try
            {
                for (; created < capacity; created++)
                {
                    warm[created] = m_pool.Get();
                }
            }
            finally
            {
                for (int i = 0; i < created; i++)
                {
                    m_pool.Release(warm[i]);
                }
            }

            m_active = new ExitFragmentEmitter[capacity];
            PreparedCount = capacity;
        }

        /// <summary>
        /// Plays one burst at the door line; false when every prepared emitter is busy, so that exit shows
        /// no fragments rather than growing the pool during Playing.
        /// </summary>
        public bool TryPlay(Vector3 localPosition, Quaternion localRotation, int lanes, float seconds, AtlasColor color)
        {
            ThrowIfDisposed();
            if (m_activeCount >= PreparedCount)
            {
                return false;
            }

            ExitFragmentEmitter emitter = m_pool.Get();
            m_active[m_activeCount++] = emitter;
            emitter.Play(localPosition, localRotation, lanes, seconds, color);
            return true;
        }

        /// <summary>The view sync phase: returns every emitter whose burst ended. Polled, so no callback outlives an attempt.</summary>
        public void Sync()
        {
            for (int i = m_activeCount - 1; i >= 0; i--)
            {
                if (m_active[i].IsFinished)
                {
                    Return(i);
                }
            }
        }

        /// <summary>Suspend step 3 and Resume: holds or continues every burst in flight.</summary>
        public void SetPaused(bool paused)
        {
            for (int i = 0; i < m_activeCount; i++)
            {
                m_active[i].SetPaused(paused);
            }
        }

        /// <summary>Teardown step 3: stops and clears every burst; ReturnAll then returns them.</summary>
        public void Stop()
        {
            for (int i = 0; i < m_activeCount; i++)
            {
                m_active[i].Stop();
            }
        }

        /// <summary>Teardown step 6: returns every emitter still out of the pool.</summary>
        public void ReturnAll()
        {
            for (int i = m_activeCount - 1; i >= 0; i--)
            {
                Return(i);
            }
        }

        /// <summary>Returns every emitter and destroys the pooled ones; a scene unload may have destroyed them already.</summary>
        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            ReturnAll();
            m_pool.Dispose();
            PreparedCount = 0;
        }

        private void Return(int index)
        {
            ExitFragmentEmitter emitter = m_active[index];
            m_activeCount--;
            m_active[index] = m_active[m_activeCount];
            m_active[m_activeCount] = null;
            m_pool.Release(emitter);
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(ExitFragments));
            }
        }
    }
}
