using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ColorBlockJam.Player.Storage
{
    /// <summary>
    /// Stores the player snapshot as one encrypted file with a backup (design 4.3). Save encrypts the JSON, writes
    /// player-save.dat.tmp, flushes it, then replaces player-save.dat keeping the previous file as .bak (a move on the
    /// first save), so an interrupted save keeps the last committed snapshot. Load reads the main file, falls back to
    /// .bak, and deletes a stale .tmp. File work runs on the thread pool and checks the token before it starts.
    /// </summary>
    public sealed class FilePlayerSnapshotStore : IPlayerSnapshotStore
    {
        public const string FileName = "player-save.dat";

        private const string k_temporarySuffix = ".tmp";
        private const string k_backupSuffix = ".bak";

        private readonly string m_directory;
        private readonly IFileOperations m_files;
        private readonly PlayerSaveCipher m_cipher;

        public FilePlayerSnapshotStore(string directory, IFileOperations files, PlayerSaveCipher cipher)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Pass the player save directory.", nameof(directory));
            }

            m_directory = directory;
            m_files = files ?? throw new ArgumentNullException(nameof(files));
            m_cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
            FilePath = Path.Combine(directory, FileName);
            BackupPath = FilePath + k_backupSuffix;
            TemporaryPath = FilePath + k_temporarySuffix;
        }

        public string FilePath { get; }
        public string BackupPath { get; }
        public string TemporaryPath { get; }

        public Task<PlayerSnapshot> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => Load(cancellationToken), cancellationToken);
        }

        public Task SaveAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return Task.Run(() => Save(snapshot, cancellationToken), cancellationToken);
        }

        private PlayerSnapshot Load(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteStaleTemporary();

            string mainProblem = null;
            if (m_files.Exists(FilePath))
            {
                if (TryRead(FilePath, out PlayerSnapshot snapshot, out mainProblem))
                {
                    return snapshot;
                }
            }

            if (m_files.Exists(BackupPath))
            {
                if (TryRead(BackupPath, out PlayerSnapshot backup, out string backupProblem))
                {
                    // Debug.Log* is thread-safe; it is the only Unity API this thread-pool work calls.
                    Debug.LogWarning("The player save " + (mainProblem ?? "was missing")
                                     + "; the previous save was loaded from its backup.");
                    return backup;
                }

                Debug.LogWarning("The player save and its backup cannot be read (" + (mainProblem ?? "save missing")
                                 + "; backup " + backupProblem + "); starting from the default player data.");
                return null;
            }

            if (mainProblem != null)
            {
                Debug.LogWarning("The player save " + mainProblem + " and has no backup; starting from the default "
                                 + "player data.");
            }

            return null;
        }

        // A version refusal propagates (PlayerSnapshotVersionException), so newer data is never overwritten.
        private bool TryRead(string path, out PlayerSnapshot snapshot, out string problem)
        {
            snapshot = null;
            byte[] bytes;
            try
            {
                bytes = m_files.ReadAllBytes(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                problem = "could not be read (" + exception.Message + ")";
                return false;
            }

            if (!m_cipher.TryDecrypt(bytes, out string json))
            {
                problem = "failed its authenticity check";
                return false;
            }

            try
            {
                snapshot = PlayerSnapshotJson.Read(json);
                problem = null;
                return true;
            }
            catch (InvalidDataException exception)
            {
                problem = "is invalid (" + exception.Message + ")";
                return false;
            }
        }

        private void DeleteStaleTemporary()
        {
            if (!m_files.Exists(TemporaryPath))
            {
                return;
            }

            try
            {
                m_files.Delete(TemporaryPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // The next save overwrites it; a stale temporary file is never read.
            }
        }

        private void Save(PlayerSnapshot snapshot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] container = m_cipher.Encrypt(PlayerSnapshotJson.Write(snapshot));
            m_files.CreateDirectory(m_directory);
            m_files.WriteAndFlush(TemporaryPath, container);

            // Started writes finish (design 4.4 rule 5); the replace is the commit point.
            if (m_files.Exists(FilePath))
            {
                m_files.Replace(TemporaryPath, FilePath, BackupPath);
            }
            else
            {
                m_files.Move(TemporaryPath, FilePath);
            }
        }
    }
}
