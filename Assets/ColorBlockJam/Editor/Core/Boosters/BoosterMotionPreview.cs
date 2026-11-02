using System.IO;
using ColorBlockJam.Presentation.Boosters;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Boosters
{
    // Renders each booster effect prefab's pose sequence to PNG frames in isolated Unity preview
    // scenes, for reference during the Blender motion bake (DEC-152); never touches the active scene.
    public static class BoosterMotionPreview
    {
        private const int k_frameCount = 24;
        private const string k_outputDirectory = "docs/art/boosters/motion";
        private const string k_effectPrefabFolder = "Assets/ColorBlockJam/Prefabs/Boosters/Effects/";

        /// <summary>One booster effect prefab's camera framing and preview block placement for the render.</summary>
        private readonly struct PreviewConfig
        {
            public readonly string Name;
            public readonly bool IsVacuum;
            public readonly bool IsRocket;
            public readonly Vector3 Target;
            public readonly float OrthographicSize;
            public readonly float BlockZOffset;

            public PreviewConfig(string name, bool isVacuum, bool isRocket, Vector3 target, float orthographicSize, float blockZOffset)
            {
                Name = name;
                IsVacuum = isVacuum;
                IsRocket = isRocket;
                Target = target;
                OrthographicSize = orthographicSize;
                BlockZOffset = blockZOffset;
            }
        }

        private static readonly PreviewConfig[] s_previews =
        {
            new PreviewConfig("Hammer", isVacuum: false, isRocket: false, target: new Vector3(.35f, .95f, 0), orthographicSize: 2.1f, blockZOffset: 0f),
            new PreviewConfig("RocketPurple", isVacuum: false, isRocket: true, target: new Vector3(0, 1.25f, 0), orthographicSize: 2.7f, blockZOffset: 1f),
            new PreviewConfig("Vacuum", isVacuum: true, isRocket: false, target: Vector3.zero, orthographicSize: 1.45f, blockZOffset: 0f)
        };

        [MenuItem("Color Block Jam/Art/Render Booster Motion Previews")]
        public static void Render()
        {
            string output = Path.GetFullPath(k_outputDirectory);
            foreach (PreviewConfig config in s_previews)
            {
                RenderPreview(config, output);
            }
            Debug.Log("Rendered booster pose previews without editing the active scene.");
        }

        private static void RenderPreview(PreviewConfig config, string output)
        {
            PreviewRenderUtility utility = new PreviewRenderUtility();
            Material previewMaterial = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_effectPrefabFolder + config.Name + "Effect.prefab");
                GameObject root = utility.InstantiatePrefabInScene(prefab);
                BoosterEffectView view = root.GetComponent<BoosterEffectView>();

                if (config.IsVacuum)
                {
                    utility.camera.transform.position = new Vector3(0, 0, -5);
                    utility.camera.transform.rotation = Quaternion.identity;
                    utility.camera.orthographicSize = config.OrthographicSize;
                }
                else
                {
                    utility.camera.transform.position = config.Target + new Vector3(-3, 3, 6);
                    utility.camera.transform.LookAt(config.Target);
                    utility.camera.orthographicSize = config.OrthographicSize;

                    GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    utility.AddSingleGO(block);
                    block.transform.localScale = new Vector3(.8f, .20f, .8f);
                    block.transform.position = new Vector3(0, -.11f, config.BlockZOffset);
                    previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                    previewMaterial.SetColor("_BaseColor", new Color(.28f, .36f, .50f, 1));
                    block.GetComponent<Renderer>().sharedMaterial = previewMaterial;

                    if (config.IsRocket)
                    {
                        root.transform.position = new Vector3(0, .3f, -2f);
                        SerializedObject fields = new SerializedObject(view);
                        fields.FindProperty("m_localDestination").vector3Value = new Vector3(0, -.3f, 3f);
                        fields.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                utility.camera.orthographic = true;
                utility.camera.nearClipPlane = .01f;
                utility.camera.farClipPlane = 50f;
                utility.camera.clearFlags = CameraClearFlags.SolidColor;
                utility.camera.backgroundColor = new Color(.10f, .13f, .19f, 1);
                utility.ambientColor = new Color(.35f, .35f, .35f, 1);
                utility.lights[0].intensity = 1.3f;
                utility.lights[0].transform.rotation = Quaternion.LookRotation(new Vector3(3, -4, -6));
                utility.lights[1].intensity = .6f;
                utility.lights[1].transform.rotation = Quaternion.LookRotation(new Vector3(-3, -2, -4));

                // Fit the entire sweep, so anticipation and launch poses stay inside the frame.
                if (!config.IsVacuum)
                {
                    FitSweepBounds(utility, root, view);
                }

                for (int frame = 0; frame < k_frameCount; frame++)
                {
                    view.SampleForPreview(frame / (k_frameCount - 1f));
                    utility.BeginStaticPreview(new Rect(0, 0, 384, 384));
                    utility.Render(true, true);
                    Texture2D image = utility.EndStaticPreview();
                    File.WriteAllBytes(Path.Combine(output, config.Name + "_" + frame.ToString("D2") + ".png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                view.Stop();
            }
            finally
            {
                utility.Cleanup();
                if (previewMaterial != null)
                {
                    Object.DestroyImmediate(previewMaterial);
                }
            }
        }

        private static void FitSweepBounds(PreviewRenderUtility utility, GameObject root, BoosterEffectView view)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            for (int sample = 0; sample < k_frameCount; sample++)
            {
                view.SampleForPreview(sample / (k_frameCount - 1f));
                foreach (GameObject sceneRoot in root.scene.GetRootGameObjects())
                {
                    foreach (Renderer renderer in sceneRoot.GetComponentsInChildren<Renderer>())
                    {
                        Bounds bounds = renderer.bounds;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Vector3 sign = new Vector3(
                                (corner & 1) == 0 ? -1 : 1,
                                (corner & 2) == 0 ? -1 : 1,
                                (corner & 4) == 0 ? -1 : 1);
                            Vector3 point = utility.camera.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                            minX = Mathf.Min(minX, point.x);
                            maxX = Mathf.Max(maxX, point.x);
                            minY = Mathf.Min(minY, point.y);
                            maxY = Mathf.Max(maxY, point.y);
                        }
                    }
                }
            }
            utility.camera.transform.position += utility.camera.transform.right * ((minX + maxX) * .5f)
                + utility.camera.transform.up * ((minY + maxY) * .5f);
            utility.camera.orthographicSize = Mathf.Max(maxX - minX, maxY - minY) * .56f;
        }
    }
}
