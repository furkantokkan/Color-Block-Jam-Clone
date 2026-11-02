// AC-3 check for TASK-20260924-224137662-d526d5ad-board-camera-framing.
// Not part of the game: copy it into an isolated project copy under
// Assets/CameraVerify/Editor/ (with the CameraVerify.Editor.asmdef beside it) and run
//   Unity.exe -batchmode -projectPath <copy> -executeMethod CameraVerify.BoardCameraPreviewCheck.Run
//     -logFile <copy>/camera-verify.log -checkOut <folder>
// It drives the real LevelPreviewOwner path (the same call the Level Editor window
// makes), captures the preview camera to 1080x1920 PNGs, checks the pose against
// BoardCameraFraming, checks DontSave coverage, and saves LevelEditor.unity as a copy
// while the preview is shown to prove the scene bytes do not change.
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ColorBlockJam.Environment;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CameraVerify
{
    /// <summary>Batchmode AC-3 check of the LevelEditor preview camera; exits 0 on pass, 1 on fail.</summary>
    public static class BoardCameraPreviewCheck
    {
        private const string k_scenePath = "Assets/ColorBlockJam/Scenes/LevelEditor.unity";
        private const string k_copyPath = "Temp/BoardCameraLevelEditorCopy.unity";
        private const int k_width = 1080;
        private const int k_height = 1920;

        public static void Run()
        {
            var log = new StringBuilder();
            int failures = 0;
            try
            {
                string outDir = GetArg("-checkOut") ?? "Temp/CameraVerify";
                Directory.CreateDirectory(outDir);

                byte[] sceneBefore = File.ReadAllBytes(k_scenePath);
                log.AppendLine("LevelEditor.unity sha256 before=" + Sha(sceneBefore));
                Scene scene = EditorSceneManager.OpenScene(k_scenePath, OpenSceneMode.Single);
                int rootsBefore = scene.rootCount;

                if (!LevelDatabaseJson.TryRead(File.ReadAllText("Assets/ColorBlockJam/Data/Levels/LevelDatabase.json"),
                        out LevelData[] levels, out string readError))
                {
                    throw new InvalidOperationException("Level JSON read failed: " + readError);
                }

                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                Type owner = Type.GetType("ColorBlockJam.Editor.LevelPreview.LevelPreviewOwner, ColorBlockJam.Editor.LevelPreview", true);
                MethodInfo publish = owner.GetMethod("OnLevelPublished", flags);
                FieldInfo hostField = owner.GetField("s_host", flags);

                // The Editor frames with the Game view's aspect; batchmode has no Game view,
                // so the first build creates the camera and a 1080x1920 target gives it that aspect.
                publish.Invoke(null, new object[] { levels[0] });
                object host = hostField.GetValue(null);
                Camera camera = GetRoot(host).transform.Find("Preview Camera").GetComponent<Camera>();
                var target = new RenderTexture(k_width, k_height, 24);
                camera.targetTexture = target;

                try
                {
                    int largest = 0;
                    for (int index = 0; index < levels.Length; index++)
                    {
                        publish.Invoke(null, new object[] { levels[index] });
                        host = hostField.GetValue(null);
                        failures += CheckLevel(host, camera, target, outDir, log);
                        if (levels[index].Width * levels[index].Height > levels[largest].Width * levels[largest].Height)
                        {
                            largest = index;
                        }
                    }

                    // Save-as-copy while the largest preview is on screen.
                    publish.Invoke(null, new object[] { levels[largest] });
                    failures += CheckSceneBytes(scene, sceneBefore, "shown " + levels[largest].Id, log);
                }
                finally
                {
                    camera.targetTexture = null;
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }

                publish.Invoke(null, new object[] { null });
                bool disposed = hostField.GetValue(null) == null && scene.rootCount == rootsBefore;
                log.AppendLine("after null publish: host=null and roots back to " + rootsBefore + ": " + disposed);
                failures += disposed ? 0 : 1;
                failures += CheckSceneBytes(scene, sceneBefore, "disposed", log);
            }
            catch (Exception exception)
            {
                failures++;
                log.AppendLine("EXCEPTION " + exception);
            }

            log.AppendLine(failures == 0 ? "RESULT PASS" : "RESULT FAIL (" + failures + ")");
            Debug.Log("[BoardCameraPreviewCheck]\n" + log);
            File.WriteAllText(Path.Combine(GetArg("-checkOut") ?? "Temp/CameraVerify", "ac3-check.txt"), log.ToString());
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int CheckLevel(object host, Camera camera, RenderTexture target, string outDir, StringBuilder log)
        {
            int failures = 0;
            GameObject root = GetRoot(host);
            string levelId = (string)host.GetType().GetProperty("LevelId").GetValue(host);
            var args = new object[] { null };
            host.GetType().GetMethod("TryGetBounds").Invoke(host, args);
            var bounds = (Bounds)args[0];

            BoardCameraFraming.Calculate(bounds, camera.fieldOfView, camera.aspect, BoardCameraFraming.ReferencePitch,
                BoardCameraFraming.ReferenceSideMargin, BoardCameraFraming.ReferenceVerticalMargin,
                out Vector3 expectedPosition, out Quaternion expectedRotation);
            bool poseMatches = (camera.transform.position - expectedPosition).sqrMagnitude < 1e-6f
                && Quaternion.Angle(camera.transform.rotation, expectedRotation) < 0.01f;
            bool pitchIs75 = Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(75f, 0f, 0f)) < 0.01f;

            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 viewport = camera.WorldToViewportPoint(point);
                minX = Mathf.Min(minX, viewport.x);
                maxX = Mathf.Max(maxX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxY = Mathf.Max(maxY, viewport.y);
            }

            bool inView = minX >= 0f && maxX <= 1f && minY >= 0f && maxY <= 1f;
            int missingDontSave = CountMissingDontSave(root.transform);

            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(k_width, k_height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, k_width, k_height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            string png = Path.Combine(outDir, "ac3-leveleditor-preview-" + levelId + ".png");
            File.WriteAllBytes(png, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            log.AppendLine(levelId + ": bounds c=" + bounds.center.ToString("F3") + " s=" + bounds.size.ToString("F3")
                + " cam pos=" + camera.transform.position.ToString("F4") + " euler=" + camera.transform.eulerAngles.ToString("F2")
                + " fov=" + camera.fieldOfView + " aspect=" + camera.aspect.ToString("F4")
                + " viewport x[" + minX.ToString("F3") + "," + maxX.ToString("F3") + "] y[" + minY.ToString("F3") + "," + maxY.ToString("F3") + "]"
                + " poseMatchesFraming=" + poseMatches + " pitch75=" + pitchIs75 + " wholeBoardInView=" + inView
                + " missingDontSave=" + missingDontSave + " png=" + Path.GetFileName(png));
            failures += poseMatches ? 0 : 1;
            failures += pitchIs75 ? 0 : 1;
            failures += inView ? 0 : 1;
            failures += missingDontSave == 0 ? 0 : 1;
            return failures;
        }

        private static int CheckSceneBytes(Scene scene, byte[] sceneBefore, string label, StringBuilder log)
        {
            bool saved = EditorSceneManager.SaveScene(scene, k_copyPath, true);
            byte[] copy = saved ? File.ReadAllBytes(k_copyPath) : Array.Empty<byte>();
            bool identical = saved && Sha(copy) == Sha(sceneBefore);
            byte[] onDisk = File.ReadAllBytes(k_scenePath);
            bool untouched = Sha(onDisk) == Sha(sceneBefore);
            log.AppendLine("save-as-copy (" + label + "): saved=" + saved + " dirty=" + scene.isDirty
                + " copy sha256=" + (saved ? Sha(copy) : "-") + " identicalToLevelEditor=" + identical
                + " LevelEditor.unity unchanged=" + untouched);
            File.Delete(k_copyPath);
            return (identical ? 0 : 1) + (untouched ? 0 : 1) + (scene.isDirty ? 1 : 0);
        }

        private static int CountMissingDontSave(Transform transform)
        {
            int missing = (transform.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave ? 0 : 1;
            foreach (Component component in transform.GetComponents<Component>())
            {
                missing += (component.hideFlags & HideFlags.DontSave) == HideFlags.DontSave ? 0 : 1;
            }

            for (int index = 0; index < transform.childCount; index++)
            {
                missing += CountMissingDontSave(transform.GetChild(index));
            }

            return missing;
        }

        private static GameObject GetRoot(object host)
        {
            return (GameObject)host.GetType().GetProperty("Root").GetValue(host);
        }

        private static string Sha(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string GetArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name)
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
