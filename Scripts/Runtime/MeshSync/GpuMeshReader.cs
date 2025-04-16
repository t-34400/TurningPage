#nullable enable

using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace TurningPage.MeshSync
{
    public class GpuMeshReader : MonoBehaviour
    {
        [SerializeField] private TurningPageSimulation turningPageSimulation = default!;
        [SerializeField] private UnityEvent<Mesh> meshUpdated = default!;

        private AsyncGPUReadbackRequest? _request;
        
        private Mesh? mesh;

        public Mesh Mesh
        {
            get
            {
                if (mesh != null) return mesh;

                mesh = turningPageSimulation.GenerateGridMesh();
                mesh.name = "GpuSyncMesh";

                meshUpdated?.Invoke(mesh);

                return mesh;
            }
        }

        public async Task<bool> ForceSyncMesh(Mesh mesh)
        {
            var vertexBuffer = turningPageSimulation.VertexBuffer;
            if (vertexBuffer == null)
                return false;

            var request = await AsyncGPUReadback.RequestAsync(vertexBuffer);

            if (!request.done || request.hasError)
                return false;

            var vertexData = request.GetData<Vertex>();

            ApplyVertexData(mesh, vertexData);

            return true;
        }

        private void Update()
        {
            if (_request == null)
            {
                RequestReadVertexData();
                return;
            }

            var request = _request.Value;

            if (!request.done)
                return;

            _request = null;

            if (!request.hasError)
            {
                var vertexData = request.GetData<Vertex>();

                ApplyVertexData(Mesh, vertexData);
                meshUpdated?.Invoke(Mesh);

                RequestReadVertexData();
            }
        }

        private void RequestReadVertexData()
        {
            if (_request != null)
                return;

            var vertexBuffer = turningPageSimulation.VertexBuffer;
            if (vertexBuffer == null)
                return;
            
            _request = AsyncGPUReadback.Request(vertexBuffer);
        }

        private static void ApplyVertexData(Mesh mesh, NativeArray<Vertex> vertexData)
        {
            mesh.MarkDynamic();
            mesh.SetVertexBufferData(vertexData, 0, 0, vertexData.Length);
            mesh.RecalculateBounds();
        }
    }
}