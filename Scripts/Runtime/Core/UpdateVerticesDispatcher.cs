#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class UpdateVerticesDispatcher
    {
        [SerializeField] internal ComputeShader computeShader = default!;

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

        public void Dispatch(float deltaTime)
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            ComputeShader.SetFloat("_DeltaTime", deltaTime);

            ComputeShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }
    }
}