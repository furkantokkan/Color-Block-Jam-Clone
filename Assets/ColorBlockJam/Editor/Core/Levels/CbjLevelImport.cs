using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Thin menu command: picks one or more CBJ level files and a target level database (its manifest), asks the
    /// timer for each import, then converts and appends through the validated database save, which writes only the
    /// packs that change (LevelDatabaseAssets, DEC-196). It writes nothing when any selected file fails to convert
    /// (DEC-101).
    /// </summary>
    public static class CbjLevelImport
    {
        private const int k_defaultTimerSeconds = 60;
        private const string k_colorTablePath = "Assets/ColorBlockJam/Data/Levels/CbjColorTable.asset";
        private const string k_dialogTitle = "Import CBJ Level";

        [MenuItem("Color Block Jam/Levels/Import CBJ Level...")]
        public static void ImportFromMenu()
        {
            CbjColorTable colorTable = AssetDatabase.LoadAssetAtPath<CbjColorTable>(k_colorTablePath);
            if (colorTable == null)
            {
                EditorUtility.DisplayDialog(k_dialogTitle,
                    $"The CBJ colour table is missing at {k_colorTablePath}.", "OK");
                return;
            }

            string pickedPath = EditorUtility.OpenFilePanel(
                "Select Level Database Manifest", LevelDatabaseAddressables.DatabaseDirectory, "json");
            if (string.IsNullOrEmpty(pickedPath))
            {
                return;
            }

            string targetPath = FileUtil.GetProjectRelativePath(pickedPath.Replace('\\', '/'));
            if (!LevelJsonFiles.IsManifest(targetPath))
            {
                EditorUtility.DisplayDialog(k_dialogTitle,
                    $"Pick the {LevelJsonFiles.ManifestFile} of a level database under {LevelJsonFiles.Folder}.", "OK");
                return;
            }

            if (!LevelDatabaseFiles.TryRead(targetPath, out LevelDatabaseSnapshot target, out string readError))
            {
                EditorUtility.DisplayDialog(k_dialogTitle, readError, "OK");
                return;
            }

            HashSet<string> takenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelData existing in target.Levels)
            {
                takenIds.Add(existing.Id);
            }

            List<LevelData> imported = new List<LevelData>();
            List<string> allWarnings = new List<string>();
            bool importMore = true;
            while (importMore)
            {
                string sourcePath = EditorUtility.OpenFilePanel("Select CBJ Level File", "docs/levels", "json");
                if (string.IsNullOrEmpty(sourcePath))
                {
                    break;
                }

                if (!ImportOneFile(sourcePath, colorTable, takenIds, imported, allWarnings, out string fileError))
                {
                    EditorUtility.DisplayDialog(k_dialogTitle,
                        $"{Path.GetFileName(sourcePath)}: {fileError}\n\nNothing was written.", "OK");
                    return;
                }

                importMore = EditorUtility.DisplayDialog(k_dialogTitle,
                    $"Imported {Path.GetFileNameWithoutExtension(sourcePath)}. Import another CBJ file?",
                    "Yes", "No");
            }

            if (imported.Count == 0)
            {
                return;
            }

            List<LevelData> combined = new List<LevelData>(target.Levels);
            combined.AddRange(imported);

            // Its own backup folder, apart from the Level Editor's Restore Previous Save.
            string backupFolder = LevelDatabaseAssets.GetBackupFolder(AssetDatabase.AssetPathToGUID(targetPath))
                                  + ".cbj-import";
            if (!LevelDatabaseAssets.TrySave(target, combined, backupFolder, out _, out string note,
                    out string saveError))
            {
                EditorUtility.DisplayDialog(k_dialogTitle, $"Nothing was written: {saveError}", "OK");
                return;
            }

            string summary = $"Imported {imported.Count} level(s) into "
                             + $"{LevelDatabaseAddressables.GetDatabaseName(targetPath)}.";
            if (note != null)
            {
                allWarnings.Add(note);
            }

            if (allWarnings.Count > 0)
            {
                summary += "\n" + string.Join("\n", allWarnings);
            }

            EditorUtility.DisplayDialog(k_dialogTitle, summary, "OK");
        }

        private static bool ImportOneFile(
            string sourcePath, CbjColorTable colorTable,
            HashSet<string> takenIds, List<LevelData> imported, List<string> allWarnings, out string error)
        {
            string json;
            try
            {
                json = File.ReadAllText(sourcePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"Could not read the file: {exception.Message}";
                return false;
            }

            if (!CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanicViolations,
                    out error))
            {
                return false;
            }

            if (!CbjTimerPrompt.TryShow(k_defaultTimerSeconds, out int timerSeconds))
            {
                error = "Import cancelled.";
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string levelId = MakeUniqueId(baseName, takenIds);

            if (!CbjLevelConverter.TryConvert(source, mechanicViolations, colorTable, levelId,
                    baseName, timerSeconds, out LevelData level, out List<string> warnings, out error))
            {
                return false;
            }

            takenIds.Add(levelId);
            imported.Add(level);
            allWarnings.AddRange(warnings);
            error = null;
            return true;
        }

        private static string MakeUniqueId(string baseName, HashSet<string> takenIds)
        {
            string candidate = "cbj-" + baseName;
            int suffix = 2;
            while (takenIds.Contains(candidate))
            {
                candidate = $"cbj-{baseName}-{suffix}";
                suffix++;
            }

            return candidate;
        }
    }

    /// <summary>One small modal UI Toolkit window: the imported level's timer in seconds (PRD default 60 s).</summary>
    internal sealed class CbjTimerPrompt : EditorWindow
    {
        private const int k_minSeconds = 10;
        private const int k_maxSeconds = 300;

        private int m_timerSeconds;
        private bool m_confirmed;

        internal static bool TryShow(int defaultSeconds, out int timerSeconds)
        {
            CbjTimerPrompt window = CreateInstance<CbjTimerPrompt>();
            window.m_timerSeconds = defaultSeconds;
            window.titleContent = new GUIContent("CBJ Import Timer");
            window.minSize = new Vector2(280f, 90f);
            window.maxSize = window.minSize;
            window.ShowModalUtility();

            timerSeconds = window.m_timerSeconds;
            bool confirmed = window.m_confirmed;
            DestroyImmediate(window);
            return confirmed;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;

            IntegerField field = new IntegerField("Timer (seconds)") { value = m_timerSeconds };
            field.RegisterValueChangedCallback(changeEvent =>
                m_timerSeconds = Mathf.Clamp(changeEvent.newValue, k_minSeconds, k_maxSeconds));
            root.Add(field);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 8f;

            Button cancel = new Button(() =>
            {
                m_confirmed = false;
                Close();
            })
            { text = "Cancel" };
            Button ok = new Button(() =>
            {
                m_confirmed = true;
                Close();
            })
            { text = "OK" };
            buttons.Add(cancel);
            buttons.Add(ok);
            root.Add(buttons);
        }
    }
}
