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

        private Vector2Int gridCount;
        private int kernel;

        private ComputeShader? shaderInstance;

        public ComputeShader? ComputeShader
        {
            get => computeShader;
            set
            {
                if (value != null)
                {
                    computeShader = value;
                }
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
        }

        public void Dispatch()
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            var parameters = parameterManager.UpdateBezierCorrectedParaboloidParameters();
            var angleRad = parameters.Angle * Mathf.Deg2Rad;
            var axis = parameters.Axis;

            ShaderInstance.SetFloat("_ConeApex", parameters.Apex);
            ShaderInstance.SetFloat("_ConeAngle", angleRad);
            ShaderInstance.SetFloats("_ConeAxis", axis.x, axis.y, axis.z);

            ShaderInstance.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }

        public void ResetMeshCorners(bool isPageFlipped) => parameterManager.ResetMeshCorners(isPageFlipped);

        public void SetPinchPoint(Vector2Int vertexId)
        {
            var uv = new Vector2(
                (float)vertexId.x / gridCount.x,
                (float)vertexId.y / gridCount.y
            );

            parameterManager.PinchPointUv = uv;
            parameterManager.IsPinched = true;
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            parameterManager.PinchPoint = pinchPoint;
            parameterManager.PinchRight = pinchRight;
            parameterManager.PinchForward = pinchForward;
        }

        public void Release()
        {
            parameterManager.IsPinched = false;
        }
    }
}