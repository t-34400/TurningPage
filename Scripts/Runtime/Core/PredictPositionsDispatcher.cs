#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class PredictPositionsDispatcher
    {
        [SerializeField] internal ComputeShader computeShader = default!;
        [SerializeField] private Vector3 gravity = Vector3.down * 100f;
        [SerializeField] private float resistance = 1f;

        private ComputeShader? _computeShader;

        private Vector2Int gridCount;
        private int kernel;

        public ComputeShader ComputeShader  => _computeShader ??= UnityEngine.Object.Instantiate(computeShader);

        public void Register(GraphicsBuffer vertexBuffer, GraphicsBuffer velocityBuffer, GraphicsBuffer predictedPositionBuffer, Vector2 gridSize, Vector2Int gridCount)
        {
            this.gridCount = gridCount;

            kernel = ComputeShader.FindKernel("CSMain");

            ComputeShader.SetBuffer(kernel, "vertexBuffer", vertexBuffer);
            ComputeShader.SetBuffer(kernel, "velocityBuffer", velocityBuffer);
            ComputeShader.SetBuffer(kernel, "predictedPositionBuffer", predictedPositionBuffer);

            ComputeShader.SetFloats("_GridSize", gridSize.x, gridSize.y);
            ComputeShader.SetInts("_GridCount", gridCount.x, gridCount.y);
        }

        public void Dispatch(Transform transform, float deltaTime)
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            var stepGravity = transform.InverseTransformDirection(gravity * deltaTime * deltaTime);

            ComputeShader.SetFloats("_Gravity", stepGravity.x, stepGravity.y, stepGravity.z);
            ComputeShader.SetFloat("_Resistance", resistance);
            ComputeShader.SetFloat("_DeltaTime", deltaTime);

            ComputeShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }
    }
}