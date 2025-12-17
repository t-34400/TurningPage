#nullable enable

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace TurningPage.MeshSync
{
    public class GpuMeshReader : MonoBehaviour
    {
        [SerializeField] private TurningPageSimulation turningPageSimulation = default!;
        [SerializeField] private UnityEvent<Mesh> meshUpdated = default!;

        private const float PARAM_THRESHOLD_APEX = 0.05f;
        private const float PARAM_THRESHOLD_ANGLE_DEG = 1f;
        private const float PARAM_THRESHOLD_AXISROLL_DEG = 1f;

        private AsyncGPUReadbackRequest? _request;

        private ConicalSurfaceParameters? _requestedParams;
        private ConicalSurfaceParameters? _appliedParams;

        private Mesh? _mesh;

        public Mesh Mesh
        {
            get
            {
                if (_mesh != null) return _mesh;

                _mesh = turningPageSimulation.GenerateGridMesh();
                _mesh.name = "GpuSyncMesh";

                meshUpdated?.Invoke(_mesh);

                return _mesh;
            }
        }

        public event Action<Mesh>? MeshUpdated;

        private void Update()
        {
            if (_request == null)
            {
                TryRequestReadVertexDataIfParamsChanged();
                return;
            }

            var request = _request.Value;
            if (!request.done)
                return;

            _request = null;

            if (request.hasError)
                return;

            var vertexData = request.GetData<Vertex>();
            ApplyVertexData(Mesh, vertexData);

            if (_requestedParams != null)
            {
                _appliedParams = _requestedParams;
                _requestedParams = null;
            }

            MeshUpdated?.Invoke(Mesh);
            meshUpdated.Invoke(Mesh);

            // Immediately check again; if params moved while GPU was working, queue another request.
            TryRequestReadVertexDataIfParamsChanged();
        }

        private void TryRequestReadVertexDataIfParamsChanged()
        {
            if (_request != null)
                return;

            var vertexBuffer = turningPageSimulation.VertexBuffer;
            if (vertexBuffer == null)
                return;

            var current = turningPageSimulation.ConicalSurfaceParameters;
            var currentSnapshot = Snapshot(current);

            if (_appliedParams != null && ApproximatelyEqual(currentSnapshot, _appliedParams))
                return;

            _requestedParams = currentSnapshot;
            _request = AsyncGPUReadback.Request(vertexBuffer);
        }

        private static ConicalSurfaceParameters Snapshot(ConicalSurfaceParameters src)
            => new ConicalSurfaceParameters(src.Apex, src.Angle, src.AxisRoll);

        private static bool ApproximatelyEqual(ConicalSurfaceParameters a, ConicalSurfaceParameters b)
        {
            return Mathf.Abs(a.Apex - b.Apex) < PARAM_THRESHOLD_APEX
                   && Mathf.Abs(Mathf.DeltaAngle(a.Angle, b.Angle)) < PARAM_THRESHOLD_ANGLE_DEG
                   && Mathf.Abs(Mathf.DeltaAngle(a.AxisRoll, b.AxisRoll)) < PARAM_THRESHOLD_AXISROLL_DEG;
        }

        private static void ApplyVertexData(Mesh mesh, Unity.Collections.NativeArray<Vertex> vertexData)
        {
            mesh.MarkDynamic();
            mesh.SetVertexBufferData(vertexData, 0, 0, vertexData.Length);
            mesh.RecalculateBounds();
        }
    }
}
