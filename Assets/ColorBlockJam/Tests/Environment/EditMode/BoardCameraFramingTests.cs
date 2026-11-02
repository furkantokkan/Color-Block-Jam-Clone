using System;
using System.Reflection;
using ColorBlockJam.Environment;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Environment
{
    public sealed class BoardCameraFramingTests
    {
        private const float k_fieldOfView = 60f;
        private const float k_portraitAspect = 1080f / 1920f;
        private const float k_tallAspect = 1080f / 2400f;

        [Test]
        public void RotationIsThePitchAndTheCameraLooksAtTheBoundsCentre()
        {
            Bounds bounds = new Bounds(new Vector3(1.5f, 0.36f, -2f), new Vector3(5f, 0.72f, 6f));

            Calculate(bounds, k_portraitAspect, out Vector3 position, out Quaternion rotation);

            Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(75f, 0f, 0f)), Is.LessThan(1e-3f));
            Vector3 toCentre = (bounds.center - position).normalized;
            Assert.That(Vector3.Angle(toCentre, rotation * Vector3.forward), Is.LessThan(1e-3f));
        }

        [TestCase(5f, 6f, k_portraitAspect)]
        [TestCase(9f, 5f, k_portraitAspect)]
        [TestCase(5f, 11f, k_portraitAspect)]
        [TestCase(9f, 11f, k_portraitAspect)]
        [TestCase(9f, 11f, k_tallAspect)]
        public void WidthAndDepthFitInsideTheFrustum(float width, float depth, float aspect)
        {
            Bounds bounds = new Bounds(new Vector3(0f, 0.36f, 0f), new Vector3(width, 0.72f, depth));

            Calculate(bounds, aspect, out Vector3 position, out Quaternion rotation);

            float tanVertical = Mathf.Tan(k_fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * aspect;
            Quaternion toCamera = Quaternion.Inverse(rotation);
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 view = toCamera * (world - position);
                Assert.That(view.z, Is.GreaterThan(0f), $"corner {world} is in front of the camera");
                Assert.That(Mathf.Abs(view.x / view.z), Is.LessThanOrEqualTo(tanHorizontal), $"corner {world} fits the width");
                Assert.That(Mathf.Abs(view.y / view.z), Is.LessThanOrEqualTo(tanVertical), $"corner {world} fits the height");
            }
        }

        // 9 x 5 is width-limited: (9 + 1.5) / (2 tan 30 * 0.5625) = 16.166 beats (5 + 5) / (2 tan 30) = 8.660.
        // 5 x 11 is depth-limited: (11 + 5) / (2 tan 30) = 13.856 beats (5 + 1.5) / (2 tan 30 * 0.5625) = 10.007.
        [TestCase(9f, 5f, 16.166f)]
        [TestCase(5f, 11f, 13.856f)]
        public void DistanceIsTheLargerOfTheWidthAndDepthFits(float width, float depth, float expectedDistance)
        {
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(width, 0.72f, depth));
            float frustum = 2f * Mathf.Tan(30f * Mathf.Deg2Rad);
            float expected = Mathf.Max(
                (width + BoardCameraFraming.ReferenceSideMargin) / (frustum * k_portraitAspect),
                (depth + BoardCameraFraming.ReferenceVerticalMargin) / frustum);

            Calculate(bounds, k_portraitAspect, out Vector3 position, out _);

            Assert.That(Vector3.Distance(position, bounds.center), Is.EqualTo(expected).Within(1e-3f));
            Assert.That(expected, Is.EqualTo(expectedDistance).Within(1e-2f));
        }

        [Test]
        public void BoardCameraPoseIgnoresThePreviousCameraPosition()
        {
            GameObject cameraObject = new GameObject("Board Camera Test", typeof(Camera), typeof(BoardCamera));
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.fieldOfView = k_fieldOfView;
                camera.aspect = k_portraitAspect;
                BoardCamera boardCamera = cameraObject.GetComponent<BoardCamera>();
                Bounds bounds = new Bounds(new Vector3(0f, 0.36f, 0f), new Vector3(5f, 0.72f, 6f));
                Calculate(bounds, k_portraitAspect, out Vector3 expectedPosition, out Quaternion expectedRotation);

                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1f, -10f), Quaternion.identity);
                boardCamera.Frame(bounds);
                Vector3 first = cameraObject.transform.position;
                Quaternion firstRotation = cameraObject.transform.rotation;

                cameraObject.transform.SetPositionAndRotation(new Vector3(40f, -7f, 90f), Quaternion.Euler(10f, 80f, 0f));
                boardCamera.Frame(bounds);

                Assert.That(Vector3.Distance(first, expectedPosition), Is.LessThan(1e-4f));
                Assert.That(Vector3.Distance(cameraObject.transform.position, first), Is.LessThan(1e-4f));
                Assert.That(Quaternion.Angle(firstRotation, expectedRotation), Is.LessThan(1e-3f));
                Assert.That(Quaternion.Angle(cameraObject.transform.rotation, firstRotation), Is.LessThan(1e-3f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void InvalidLensIsRejected()
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardCameraFraming.Calculate(
                bounds, 0f, k_portraitAspect, BoardCameraFraming.ReferencePitch,
                BoardCameraFraming.ReferenceSideMargin, BoardCameraFraming.ReferenceVerticalMargin, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardCameraFraming.Calculate(
                bounds, k_fieldOfView, 0f, BoardCameraFraming.ReferencePitch,
                BoardCameraFraming.ReferenceSideMargin, BoardCameraFraming.ReferenceVerticalMargin, out _, out _));
        }

        [Test]
        public void BoardCameraNeverTicks()
        {
            Type type = typeof(BoardCamera);
            foreach (string loop in new[] { "Update", "LateUpdate", "FixedUpdate" })
            {
                MethodInfo method = type.GetMethod(loop, BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Assert.That(method, Is.Null, $"BoardCamera.{loop}");
            }

            Assert.That(type.GetInterface("IOnityTickable"), Is.Null);
        }

        private static void Calculate(Bounds bounds, float aspect, out Vector3 position, out Quaternion rotation)
        {
            BoardCameraFraming.Calculate(bounds, k_fieldOfView, aspect, BoardCameraFraming.ReferencePitch,
                BoardCameraFraming.ReferenceSideMargin, BoardCameraFraming.ReferenceVerticalMargin,
                out position, out rotation);
        }
    }
}
