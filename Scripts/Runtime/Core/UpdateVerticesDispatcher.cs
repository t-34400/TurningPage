#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class UpdateVerticesDispatcher
    {
        [SerializeField] private ComputeShader computeShader = default!;
        [SerializeField] private ConicalSurfaceParameterManager parameterManager = default!;

        private const float PARAM_THRESHOLD_APEX = 0.05f;
        private const float PARAM_THRESHOLD_ANGLE_DEG = 1f;
        private const float PARAM_THRESHOLD_AXISROLL_DEG = 1f;

        private Vector2Int gridCount;
        private int kernel;

        private ComputeShader? shaderInstance;

        private ConicalSurfaceParameters? _lastDispatchedParams;

        public ComputeShader? ComputeShader
        {
            get => computeShader;
            set
            {
                if (value != null)
                    computeShader = value;
            }
        }

        public ComputeShader ShaderInstance
        {
            get => shaderInstance ??= UnityEngine.Object.Instantiate(computeShader);
        }

        public ConicalSurfaceParameters LatestParameters => parameterManager.LatestParameters;

        public void Register(GraphicsBuffer vertexBuffer, Vector2 meshSize, Vector2Int gridCount)
        {
            this.gridCount = gridCount;

            kernel = ShaderInstance.FindKernel("CSMain");

            ShaderInstance.SetBuffer(kernel, "vertexBuffer", vertexBuffer);

            ShaderInstance.SetFloats("_MeshSize", meshSize.x, meshSize.y);
            ShaderInstance.SetInts("_GridCount", gridCount.x, gridCount.y);

            parameterManager.Initialize(meshSize);

            _lastDispatchedParams = null;
        }

        public bool Dispatch(float deltaTime)
        {
            var parameters = parameterManager.UpdateConicalSurfaceParameters(deltaTime);

            var snapshot = Snapshot(parameters);
            if (_lastDispatchedParams != null && ApproximatelyEqual(snapshot, _lastDispatchedParams))
                return false;

            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            var angleRad = snapshot.Angle * Mathf.Deg2Rad;
            var axis = snapshot.Axis;

            ShaderInstance.SetFloat("_ConeApex", snapshot.Apex);
            ShaderInstance.SetFloat("_ConeAngle", angleRad);
            ShaderInstance.SetFloats("_ConeAxis", axis.x, axis.y, axis.z);

            ShaderInstance.Dispatch(kernel, threadGroupX, threadGroupY, 1);

            _lastDispatchedParams = snapshot;
            return true;
        }

        public void ResetMeshCorners(bool isPageFlipped)
        {
            parameterManager.ResetMeshCorners(isPageFlipped);
            _lastDispatchedParams = null;
        }

        public void SetPinchPoint(Vector2Int vertexId)
        {
            var uv = new Vector2(
                (float)vertexId.x / gridCount.x,
                (float)vertexId.y / gridCount.y
            );

            parameterManager.PinchPointUv = uv;
            parameterManager.IsPinched = true;

            _lastDispatchedParams = null;
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            parameterManager.PinchPoint = pinchPoint;
            parameterManager.PinchRight = pinchRight;
            parameterManager.PinchForward = pinchForward;

            _lastDispatchedParams = null; // force one dispatch next time
        }

        public void Release()
        {
            parameterManager.IsPinched = false;
            _lastDispatchedParams = null; // force one dispatch next time
        }

        private static ConicalSurfaceParameters Snapshot(ConicalSurfaceParameters src)
            => new ConicalSurfaceParameters(src.Apex, src.Angle, src.AxisRoll);

        private static bool ApproximatelyEqual(ConicalSurfaceParameters a, ConicalSurfaceParameters b)
        {
            return Mathf.Abs(a.Apex - b.Apex) < PARAM_THRESHOLD_APEX
                   && Mathf.Abs(Mathf.DeltaAngle(a.Angle, b.Angle)) < PARAM_THRESHOLD_ANGLE_DEG
                   && Mathf.Abs(Mathf.DeltaAngle(a.AxisRoll, b.AxisRoll)) < PARAM_THRESHOLD_AXISROLL_DEG;
        }
    }
}
