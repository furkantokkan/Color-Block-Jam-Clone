using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>Explicit asset setup through the pinned Shader Graph 14 API; never runs on import or Play.</summary>
    public static class BoardIceAssets
    {
        public const string GraphPath = "Assets/ColorBlockJam/Art/Shaders/Ice.shadergraph";
        public const string MaterialPath = "Assets/ColorBlockJam/Art/Materials/Effects/MAT_Ice.mat";
        public const string PanelPath = "Assets/ColorBlockJam/Data/UI/BoardOverlayPanelSettings.asset";
        private const string k_texturePath = "Assets/ColorBlockJam/Art/Textures/icetexture.jpg";
        private const string k_gamePath = "Assets/ColorBlockJam/Scenes/Game.unity";
        private const BindingFlags k_flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        // Shared between the saved material and the Shader Graph property defaults, so they cannot drift apart.
        private const float k_iceOpacity = .98f;
        private const float k_iceTextureScale = .45f;
        private static readonly Color s_iceTint = new Color(.14f, .73f, .98f, 1f);
        private static readonly Color s_iceHighlight = new Color(.48f, .95f, 1f, 1f);

        public static string RebuildIceMaterial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before Ice asset setup.");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_texturePath);
            if (texture == null) throw new InvalidOperationException("The supplied icetexture is missing.");
            CreateGraph(texture);
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Ice Shader Graph did not compile.");
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = shader;
            material.SetTexture("_IceTexture", texture);
            material.SetColor("_IceTint", s_iceTint);
            material.SetColor("_IceHighlight", s_iceHighlight);
            material.SetFloat("_IceOpacity", k_iceOpacity);
            material.SetFloat("_IceTextureScale", k_iceTextureScale);
            material.SetFloat("_IceCracks", 0f);
            material.SetFloat("_ClipEnabled", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return "Ice Shader Graph and shared material saved.";
        }

        public static string CreateApprovedAssets()
        {
            RebuildIceMaterial();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            Directory.CreateDirectory(Path.GetDirectoryName(PanelPath));
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                string[] panels = AssetDatabase.FindAssets("t:PanelSettings", new[] { "Assets/ColorBlockJam/Data/UI" });
                if (panels.Length == 0) throw new InvalidOperationException("The project UI theme PanelSettings is missing.");
                PanelSettings template = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(panels[0]));
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                EditorUtility.CopySerialized(template, panel);
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.name = "BoardOverlayPanelSettings";
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);
            panel.targetTexture = null;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssetIfDirty(panel);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(k_texturePath);
            importer.maxTextureSize = 1024;
            SetTexturePlatform(importer, "Android");
            SetTexturePlatform(importer, "iPhone");
            importer.SaveAndReimport();
            WireGame(material, panel);
            return "Ice Shader Graph, material, overlay panel, texture settings and Game bindings saved.";
        }

        private static void SetTexturePlatform(TextureImporter importer, string platform)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(settings);
        }

        private static void WireGame(Material material, PanelSettings panel)
        {
            Scene original = SceneManager.GetActiveScene();
            Scene game = SceneManager.GetSceneByPath(k_gamePath);
            bool opened = !game.IsValid() || !game.isLoaded;
            if (!opened && game.isDirty) throw new InvalidOperationException("Game has unsaved changes; preserve them before explicit setup.");
            if (opened) game = EditorSceneManager.OpenScene(k_gamePath, OpenSceneMode.Additive);
            try
            {
                MonoBehaviour installer = null;
                Camera camera = null;
                foreach (GameObject root in game.GetRootGameObjects())
                {
                    foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (component != null && component.GetType().FullName == "ColorBlockJam.Gameplay.Composition.GameSceneInstaller")
                            installer = component;
                    if (camera == null) camera = root.GetComponentInChildren<Camera>(true);
                }
                if (installer == null || camera == null) throw new InvalidOperationException("Game installer or camera is missing.");
                var serialized = new SerializedObject(installer);
                UIDocument document = serialized.FindProperty("m_counterDocument").objectReferenceValue as UIDocument;
                if (document == null)
                {
                    var root = new GameObject("Board effect counters", typeof(UIDocument));
                    SceneManager.MoveGameObjectToScene(root, game);
                    document = root.GetComponent<UIDocument>();
                }
                document.panelSettings = panel;
                document.sortingOrder = 20;
                serialized.FindProperty("m_iceMaterial").objectReferenceValue = material;
                serialized.FindProperty("m_counterDocument").objectReferenceValue = document;
                serialized.FindProperty("m_camera").objectReferenceValue = camera;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(game);
                if (!EditorSceneManager.SaveScene(game)) throw new InvalidOperationException("Could not save Game scene.");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (opened && game.IsValid()) EditorSceneManager.CloseScene(game, true);
            }
        }

        // Reflection keeps the authoring assembly free of runtime dependencies on internal Shader Graph packages.
        private sealed class Graph
        {
            public readonly object Data = New("GraphData");
            public object Node(string type) { object node = New(type); Call(Data, "AddNode", node); return node; }
            public object Number(float value)
            {
                object node = New("Vector1Node");
                node.GetType().GetField("m_Value", k_flags).SetValue(node, value);
                Call(node, "UpdateNodeAfterDeserialization");
                // AddSlot preserves the constructor-created input value when only its default changes.
                object input = node.GetType().GetMethod("FindInputSlot", k_flags)
                    .MakeGenericMethod(Type("Vector1MaterialSlot")).Invoke(node, new object[] { 1 });
                Set(input, "value", value);
                Call(Data, "AddNode", node);
                return node;
            }
            public object Property(string type, string name, object value)
            {
                object property = New("Internal." + type + "ShaderProperty");
                Set(property, "displayName", name.Substring(1));
                Set(property, "overrideReferenceName", name);
                Set(property, "generatePropertyBlock", true);
                if (type == "Texture2D") Set(Get(property, "value"), "texture", value);
                else Set(property, "value", value);
                Call(Data, "AddGraphInput", property, -1);
                object node = Node("PropertyNode");
                Set(node, "property", property);
                return node;
            }
            public void Edge(object from, int output, object to, int input)
                => Call(Data, "Connect", Call(from, "GetSlotReference", output), Call(to, "GetSlotReference", input));
            public void Output(string name, object from, int slot, string stage = "SurfaceDescription")
            {
                MethodInfo method = Data.GetType().GetMethod("GetNodes", k_flags);
                IEnumerable nodes = (IEnumerable)method.MakeGenericMethod(Type("AbstractMaterialNode")).Invoke(Data, null);
                foreach (object node in nodes)
                    if ((string)Get(node, "name") == stage + "." + name) { Edge(from, slot, node, 0); return; }
                throw new InvalidOperationException("Missing graph output " + name);
            }
        }

        private static void CreateGraph(Texture2D texture)
        {
            var graph = new Graph();
            Set(graph.Data, "path", "ColorBlockJam");
            Call(graph.Data, "AddContexts");
            Type targetType = System.Type.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget, Unity.RenderPipelines.Universal.Editor", true);
            object target = Activator.CreateInstance(targetType, true);
            Type lit = targetType.Assembly.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget", true);
            Call(target, "TrySetActiveSubTarget", lit);
            // Fade the complete lit result, including emission/specular, on thaw and gate clipping.
            Set(Get(target, "activeSubTarget"), "blendModePreserveSpecular", false);
            SetEnum(target, "surfaceType", "Transparent");
            SetEnum(target, "alphaMode", "Alpha");
            Set(target, "castShadows", false);
            Set(target, "receiveShadows", false);
            Array targets = Array.CreateInstance(Type("Target"), 1);
            targets.SetValue(target, 0);
            string[] names = { "VertexDescription.Position", "VertexDescription.Normal", "VertexDescription.Tangent",
                "SurfaceDescription.BaseColor", "SurfaceDescription.NormalTS", "SurfaceDescription.Metallic",
                "SurfaceDescription.Smoothness", "SurfaceDescription.Emission", "SurfaceDescription.Occlusion", "SurfaceDescription.Alpha" };
            Array descriptors = Array.CreateInstance(Type("BlockFieldDescriptor"), names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                string[] parts = names[i].Split('.');
                Type owner = Type("BlockFields").GetNestedType(parts[0], k_flags);
                descriptors.SetValue(owner.GetField(parts[1], k_flags).GetValue(null), i);
            }
            Call(graph.Data, "InitializeOutputs", targets, descriptors);
            // Expand along each mesh normal: scaling the whole footprint shifts stud
            // centres and leaves the opposite base-colour crescent exposed.
            object shellPosition = graph.Node("PositionNode");
            FieldInfo geometrySpace = Type("GeometryNode").GetField("m_Space", k_flags);
            object objectSpace = Enum.Parse(geometrySpace.FieldType, "Object");
            geometrySpace.SetValue(shellPosition, objectSpace);
            object shellNormal = graph.Node("NormalVectorNode");
            geometrySpace.SetValue(shellNormal, objectSpace);
            object shellOffset = graph.Node("MultiplyNode");
            graph.Edge(shellNormal, 0, shellOffset, 0);
            graph.Edge(graph.Number(.03f), 0, shellOffset, 1);
            object shellSurface = graph.Node("AddNode");
            graph.Edge(shellPosition, 0, shellSurface, 0);
            graph.Edge(shellOffset, 2, shellSurface, 1);
            graph.Output("Position", shellSurface, 2, "VertexDescription");
            object position = graph.Node("PositionNode");
            // GeometryNode defaults to World in the pinned 14.0.12 package.
            object uv = graph.Node("SwizzleNode");
            Set(uv, "maskInput", "xz");
            graph.Edge(position, 0, uv, 0);
            object sample = graph.Node("SampleTexture2DNode");
            graph.Edge(graph.Property("Texture2D", "_IceTexture", texture), 0, sample, 1);
            object tiling = graph.Node("MultiplyNode");
            graph.Edge(uv, 1, tiling, 0);
            graph.Edge(graph.Property("Vector1", "_IceTextureScale", k_iceTextureScale), 0, tiling, 1);
            graph.Edge(tiling, 2, sample, 2);
            // The supplied sRGB tile has soft blue detail. Remap its linear red channel
            // into pale frost instead of multiplying it into a second dark blue tint.
            object frost = graph.Node("SmoothstepNode");
            graph.Edge(graph.Number(.14f), 0, frost, 0);
            graph.Edge(graph.Number(.34f), 0, frost, 1);
            graph.Edge(sample, 4, frost, 2);
            object crackStrength = graph.Node("LerpNode");
            graph.Edge(graph.Number(.06f), 0, crackStrength, 0);
            graph.Edge(graph.Number(.12f), 0, crackStrength, 1);
            graph.Edge(graph.Property("Vector1", "_IceCracks", 0f), 0, crackStrength, 2);
            object cracks = graph.Node("MultiplyNode");
            graph.Edge(frost, 3, cracks, 0);
            graph.Edge(crackStrength, 3, cracks, 1);
            object fresnel = graph.Node("FresnelNode");
            graph.Edge(graph.Number(1.3f), 0, fresnel, 2);
            // Light the rounded bevel independently of the camera, keeping vertical
            // side walls and flat stud caps cyan. Both bevel directions retain a rim.
            object surfaceNormal = graph.Node("NormalVectorNode");
            geometrySpace.SetValue(surfaceNormal, Enum.Parse(geometrySpace.FieldType, "World"));
            object normalY = graph.Node("SwizzleNode"); Set(normalY, "maskInput", "y");
            graph.Edge(surfaceNormal, 0, normalY, 0);
            object slope = graph.Node("AbsoluteNode");
            graph.Edge(normalY, 1, slope, 0);
            object sideFade = graph.Node("SmoothstepNode");
            graph.Edge(graph.Number(.05f), 0, sideFade, 0);
            graph.Edge(graph.Number(.35f), 0, sideFade, 1);
            graph.Edge(slope, 1, sideFade, 2);
            object capFade = graph.Node("SmoothstepNode");
            graph.Edge(graph.Number(.9f), 0, capFade, 0);
            graph.Edge(graph.Number(.999f), 0, capFade, 1);
            graph.Edge(slope, 1, capFade, 2);
            object bevel = graph.Node("OneMinusNode");
            graph.Edge(capFade, 3, bevel, 0);
            object rimMask = graph.Node("MultiplyNode");
            graph.Edge(sideFade, 3, rimMask, 0);
            graph.Edge(bevel, 1, rimMask, 1);
            object rim = graph.Node("MultiplyNode");
            graph.Edge(rimMask, 2, rim, 0);
            graph.Edge(graph.Number(.9f), 0, rim, 1);
            object detail = graph.Node("AddNode");
            graph.Edge(cracks, 2, detail, 0);
            graph.Edge(rim, 2, detail, 1);
            object highlight = graph.Node("SaturateNode");
            graph.Edge(detail, 2, highlight, 0);
            object color = graph.Node("LerpNode");
            graph.Edge(graph.Property("Color", "_IceTint", s_iceTint), 0, color, 0);
            graph.Edge(graph.Property("Color", "_IceHighlight", s_iceHighlight), 0, color, 1);
            graph.Edge(highlight, 1, color, 2);
            graph.Output("BaseColor", color, 3);
            graph.Output("Smoothness", graph.Number(.55f), 0);
            // Reference Ice has luminous stud/footprint rims, without scene refraction.
            object rimGlow = graph.Node("MultiplyNode");
            graph.Edge(fresnel, 3, rimGlow, 0);
            graph.Edge(graph.Number(.5f), 0, rimGlow, 1);
            object bevelGlow = graph.Node("MultiplyNode");
            graph.Edge(rimGlow, 2, bevelGlow, 0);
            graph.Edge(sideFade, 3, bevelGlow, 1);
            object glow = graph.Node("AddNode");
            graph.Edge(graph.Number(.12f), 0, glow, 0);
            graph.Edge(bevelGlow, 2, glow, 1);
            object emission = graph.Node("MultiplyNode");
            graph.Edge(color, 3, emission, 0);
            graph.Edge(glow, 2, emission, 1);
            graph.Output("Emission", emission, 2);
            // Cracks brighten as the count falls; they do not erase the frozen body.
            // Only the owned thaw opacity fades the entire shell.
            object coverage = graph.Node("LerpNode");
            graph.Edge(graph.Number(.98f), 0, coverage, 0);
            graph.Edge(graph.Number(1f), 0, coverage, 1);
            graph.Edge(highlight, 1, coverage, 2);
            object alpha = graph.Node("MultiplyNode");
            graph.Edge(coverage, 3, alpha, 0);
            graph.Edge(graph.Property("Vector1", "_IceOpacity", k_iceOpacity), 0, alpha, 1);
            object plane = graph.Property("Vector4", "_ClipPlane", Vector4.zero);
            object normal = graph.Node("SwizzleNode"); Set(normal, "maskInput", "xyz");
            object distance = graph.Node("SwizzleNode"); Set(distance, "maskInput", "w");
            graph.Edge(plane, 0, normal, 0); graph.Edge(plane, 0, distance, 0);
            object dot = graph.Node("DotProductNode");
            graph.Edge(position, 0, dot, 0); graph.Edge(normal, 1, dot, 1);
            object sum = graph.Node("AddNode");
            graph.Edge(dot, 2, sum, 0); graph.Edge(distance, 1, sum, 1);
            object step = graph.Node("StepNode");
            graph.Edge(graph.Number(0f), 0, step, 0); graph.Edge(sum, 2, step, 1);
            object clip = graph.Node("LerpNode");
            graph.Edge(graph.Number(1f), 0, clip, 0); graph.Edge(step, 2, clip, 1);
            graph.Edge(graph.Property("Vector1", "_ClipEnabled", 0f), 0, clip, 2);
            object result = graph.Node("MultiplyNode");
            graph.Edge(alpha, 2, result, 0); graph.Edge(clip, 3, result, 1);
            graph.Output("Alpha", result, 2);
            Call(graph.Data, "ValidateGraph");
            Type json = Type("Serialization.MultiJson");
            string serialized = (string)json.GetMethod("Serialize", k_flags).Invoke(null, new[] { graph.Data });
            Directory.CreateDirectory(Path.GetDirectoryName(GraphPath));
            File.WriteAllText(GraphPath, serialized);
        }

        private static Type Type(string name) => System.Type.GetType("UnityEditor.ShaderGraph." + name + ", Unity.ShaderGraph.Editor", true);
        private static object New(string name) => Activator.CreateInstance(Type(name), true);
        private static object Get(object value, string property) => value.GetType().GetProperty(property, k_flags).GetValue(value);
        private static void Set(object value, string property, object data) => value.GetType().GetProperty(property, k_flags).SetValue(value, data);
        private static void SetEnum(object value, string property, string data)
        {
            PropertyInfo info = value.GetType().GetProperty(property, k_flags);
            info.SetValue(value, Enum.Parse(info.PropertyType, data));
        }
        private static object Call(object value, string method, params object[] args)
        {
            foreach (MethodInfo candidate in value.GetType().GetMethods(k_flags))
            {
                if (candidate.Name != method || candidate.IsGenericMethod || candidate.GetParameters().Length != args.Length) continue;
                ParameterInfo[] parameters = candidate.GetParameters();
                bool fits = true;
                for (int i = 0; i < args.Length; i++) fits &= args[i] == null || parameters[i].ParameterType.IsInstanceOfType(args[i]);
                if (fits) return candidate.Invoke(value, args);
            }
            throw new MissingMethodException(value.GetType().FullName, method);
        }
    }
}
