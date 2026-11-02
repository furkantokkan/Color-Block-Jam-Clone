// AC-2 / AC-4 PlayMode check for TASK-20260924-224137662-d526d5ad-board-camera-framing
// (independent verifier). Not part of the game: copy it with
// CameraVerify.PlayMode.asmdef into Assets/CameraVerifyPlay/ of an isolated project copy
// and run Unity.exe -batchmode -runTests -testPlatform PlayMode
//   -assemblyNames CameraVerify.PlayMode -projectPath <copy> -logFile <copy>/x.log
// Env var CAMERA_VERIFY_OUT names the folder for the PNG and the text report.
using System;
using System.Collections;
using System.IO;
using System.Text;
using ColorBlockJam.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CameraVerify
{
    /// <summary>Plays Game directly and checks that the Main Camera is framed once, at 75 degrees, with the whole board in a 1080x1920 view.</summary>
    public sealed class GameCameraPlayCheck
    {
        [UnityTest]
        public IEnumerator GameMainCameraIsFramedOnceAndShowsTheWholeBoard()
        {
            var log = new StringBuilder();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "Main Camera");
            Assert.That(camera.GetComponent<BoardCamera>(), Is.Not.Null, "BoardCamera on Main Camera");
            EnvironmentView view = Object.FindObjectOfType<EnvironmentView>();
            Assert.That(view, Is.Not.Null, "EnvironmentView");
            log.AppendLine("after load: built=" + view.IsBuilt + " saved pose=" + camera.transform.position.ToString("F4")
                + " euler=" + camera.transform.eulerAngles.ToString("F2"));

            // A 1080x1920 target gives the camera the portrait Game view aspect before the build.
            var target = new RenderTexture(1080, 1920, 24);
            camera.targetTexture = target;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1f, -10f), Quaternion.identity);
            bool builtBeforeMove = view.IsBuilt;

            int frames = 0;
            while (!view.IsBuilt && frames < 1200)
            {
                frames++;
                yield return null;
            }

            Assert.That(view.IsBuilt, Is.True, "environment built");
            Assert.That(builtBeforeMove, Is.False, "the build must happen after the camera is displaced");
            yield return null;

            Bounds local = view.Layout.LocalBounds;
            var bounds = new Bounds(view.BoardRoot.TransformPoint(local.center), local.size);
            BoardCameraFraming.Calculate(bounds, camera.fieldOfView, camera.aspect, 75f, 4f, 12f,
                out Vector3 expectedPosition, out Quaternion expectedRotation);
            Vector3 framedPosition = camera.transform.position;
            Quaternion framedRotation = camera.transform.rotation;
            bool poseMatches = (framedPosition - expectedPosition).sqrMagnitude < 1e-6f
                && Quaternion.Angle(framedRotation, expectedRotation) < 0.01f;
            bool pitch75 = Quaternion.Angle(framedRotation, Quaternion.Euler(75f, 0f, 0f)) < 0.01f;

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

            // Every rendered environment piece (walls and doors included) must also be in view.
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>();
            Bounds rendered = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            for (int index = 1; index < renderers.Length; index++)
            {
                rendered.Encapsulate(renderers[index].bounds);
            }

            float rMinX = 1f, rMinY = 1f, rMaxX = 0f, rMaxY = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? rendered.min.x : rendered.max.x,
                    (corner & 2) == 0 ? rendered.min.y : rendered.max.y,
                    (corner & 4) == 0 ? rendered.min.z : rendered.max.z);
                Vector3 viewport = camera.WorldToViewportPoint(point);
                rMinX = Mathf.Min(rMinX, viewport.x);
                rMaxX = Mathf.Max(rMaxX, viewport.x);
                rMinY = Mathf.Min(rMinY, viewport.y);
                rMaxY = Mathf.Max(rMaxY, viewport.y);
            }

            log.AppendLine("built after " + frames + " frames; pieces=" + view.ActivePieceCount + " renderers=" + renderers.Length);
            log.AppendLine("board bounds c=" + bounds.center.ToString("F3") + " s=" + bounds.size.ToString("F3"));
            log.AppendLine("camera pos=" + framedPosition.ToString("F4") + " euler=" + framedRotation.eulerAngles.ToString("F2")
                + " fov=" + camera.fieldOfView + " aspect=" + camera.aspect.ToString("F4")
                + " poseMatchesFraming=" + poseMatches + " pitch75=" + pitch75);
            log.AppendLine("board viewport x[" + minX.ToString("F3") + "," + maxX.ToString("F3") + "] y["
                + minY.ToString("F3") + "," + maxY.ToString("F3") + "]");
            log.AppendLine("rendered pieces viewport x[" + rMinX.ToString("F3") + "," + rMaxX.ToString("F3") + "] y["
                + rMinY.ToString("F3") + "," + rMaxY.ToString("F3") + "]");

            string outDir = System.Environment.GetEnvironmentVariable("CAMERA_VERIFY_OUT");
            if (!string.IsNullOrEmpty(outDir))
            {
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(outDir, "verifier2-game-play-level-01.png"), texture.EncodeToPNG());
                Object.Destroy(texture);
            }

            // AC-4: displace the camera after the build; nothing may re-frame it on later frames.
            var displaced = new Vector3(3f, 2f, -20f);
            Quaternion displacedRotation = Quaternion.Euler(10f, 5f, 0f);
            camera.transform.SetPositionAndRotation(displaced, displacedRotation);
            for (int index = 0; index < 60; index++)
            {
                yield return null;
            }

            bool stayed = (camera.transform.position - displaced).sqrMagnitude < 1e-8f
                && Quaternion.Angle(camera.transform.rotation, displacedRotation) < 1e-3f;
            log.AppendLine("AC-4: after 60 more frames the displaced camera stayed put: " + stayed);

            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);

            Debug.Log("[GameCameraPlayCheck]\n" + log);
            if (!string.IsNullOrEmpty(outDir))
            {
                File.WriteAllText(Path.Combine(outDir, "ac2-ac4-play-check.txt"), log.ToString());
            }

            Assert.That(poseMatches, Is.True, "pose equals BoardCameraFraming for the board bounds");
            Assert.That(pitch75, Is.True, "Euler(75,0,0)");
            Assert.That(minX >= 0f && maxX <= 1f && minY >= 0f && maxY <= 1f, Is.True, "board bounds in view");
            Assert.That(rMinX >= 0f && rMaxX <= 1f && rMinY >= 0f && rMaxY <= 1f, Is.True, "rendered pieces in view");
            Assert.That(stayed, Is.True, "no per-frame re-framing");
        }
    }
}
