#nullable enable

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

        public void Update()
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

                Mesh.MarkDynamic();
                Mesh.SetVertexBufferData(vertexData, 0, 0, vertexData.Length);
                Mesh.RecalculateNormals();

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
    }
}