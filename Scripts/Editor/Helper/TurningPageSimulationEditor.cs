#nullable enable

using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurningPage.Editor
{
    [CustomEditor(typeof(TurningPageSimulation))]
    public class TurningPageSimulationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var simulation = (TurningPageSimulation)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Stacked Previous Page"))
            {
                Debug.Log("[TurningPage] GenerateStackedPage (Previous) started");
                _ = GenerateStackedPage(simulation, true);
            }

            if (GUILayout.Button("Generate Stacked Next Page"))
            {
                Debug.Log("[TurningPage] GenerateStackedPage (Next) started");
                _ = GenerateStackedPage(simulation, false);
            }
        }

        private async Task GenerateStackedPage(TurningPageSimulation simulation, bool isPageFlipped)
        {
            try
            {
                await Awaitable.MainThreadAsync();

                if (!InitializeVertexBuffer(simulation, isPageFlipped, out var mesh, out var vertexBuffer))
                {
                    Debug.LogWarning($"[TurningPage] VertexBuffer initialization failed for {(isPageFlipped ? "Previous" : "Next")} page");
                    return;
                }

                var requestTask = AsyncGPUReadback.RequestAsync(vertexBuffer);

                await Awaitable.BackgroundThreadAsync();

                var request = await requestTask;

                await Awaitable.MainThreadAsync();

                if (!request.done || request.hasError)
                {
                    Debug.LogWarning($"[TurningPage] GPU readback failed for {(isPageFlipped ? "Previous" : "Next")} page");
                    return;
                }

                var vertexData = request.GetData<Vertex>();

                mesh.MarkDynamic();
                mesh.SetVertexBufferData(vertexData, 0, 0, vertexData.Length);
                mesh.RecalculateBounds();

                CreatePageObject(simulation, mesh, isPageFlipped ? "PreviousPage" : "NextPage");

                Debug.Log($"[TurningPage] Successfully updated mesh for {(isPageFlipped ? "Previous" : "Next")} page");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TurningPage] Exception during GenerateStackedPage: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private bool InitializeVertexBuffer(TurningPageSimulation simulation, bool isPageFlipped, out Mesh mesh, out GraphicsBuffer vertexBuffer)
        {
            var initializeVerticesDispatcher = new InitializeVerticesDispatcher();

            var initializerShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.INITIALIZER_SHADER_FILENAME);
            if (initializerShader == null)
            {
                Debug.LogWarning($"Initializer Shader {Constants.INITIALIZER_SHADER_FILENAME} not found.");

                mesh = default!;
                vertexBuffer = default!;
                return false;
            }

            initializeVerticesDispatcher.SetComputeShader_Editor(initializerShader);

            mesh = simulation.GenerateGridMesh();

            int vertexCount = mesh.vertexCount;
            int float3Size = sizeof(float) * 3;

            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
            vertexBuffer = mesh.GetVertexBuffer(0);
            var dummyVelocityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, float3Size);

            initializeVerticesDispatcher.Register(
                vertexBuffer,
                dummyVelocityBuffer,
                simulation.GridSize,
                simulation.GridCount
            );

            initializeVerticesDispatcher.Dispatch(isPageFlipped);

            return true;
        }

        private void CreatePageObject(TurningPageSimulation simulation, Mesh mesh, string name)
        {
            var parent = simulation.transform;

            var existing = parent.Find(name);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            var meshAsset = CreateAndSaveAsset(mesh, $"{Constants.PACKAGE_DIR}Models/{name}.asset");

            var pageGO = new GameObject(name);
            pageGO.transform.SetParent(parent, false);

            var meshFilter = pageGO.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = meshAsset;

            var meshRenderer = pageGO.AddComponent<MeshRenderer>();

            if (simulation.FrontMaterial != null && simulation.BackMaterial != null)
            {
                meshRenderer.sharedMaterials = new[]
                {
                    CreateAndSaveAsset(Instantiate(simulation.FrontMaterial), $"{Constants.PACKAGE_DIR}Materials/{name}_FrontMaterial.asset"),
                    CreateAndSaveAsset(Instantiate(simulation.BackMaterial), $"{Constants.PACKAGE_DIR}Materials/{name}_BackMaterial.asset")
                };
            }
            else
            {
                Debug.LogWarning($"[TurningPage] One or more materials not assigned in {simulation.name}");
            }

            Debug.Log($"[TurningPage] Created page GameObject '{name}' with mesh and materials");
        }

        public static T CreateAndSaveAsset<T>(T asset, string path) where T : UnityEngine.Object
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return asset;
        }
    }
}