#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class UpdateVerticesDispatcher
    {
        [SerializeField] private ComputeShader computeShader = default!;

        private Vector2Int gridCount;
        private int kernel;

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

        public void Register(GraphicsBuffer vertexBuffer, GraphicsBuffer velocityBuffer, GraphicsBuffer predictedPositionBuffer, Vector2 gridSize, Vector2Int gridCount)
        {
            this.gridCount = gridCount;

            kernel = computeShader.FindKernel("CSMain");

            computeShader.SetBuffer(kernel, "vertexBuffer", vertexBuffer);
            computeShader.SetBuffer(kernel, "velocityBuffer", velocityBuffer);
            computeShader.SetBuffer(kernel, "predictedPositionBuffer", predictedPositionBuffer);

            computeShader.SetFloats("_GridSize", gridSize.x, gridSize.y);
            computeShader.SetInts("_GridCount", gridCount.x, gridCount.y);
        }

        public void Dispatch(float deltaTime)
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            computeShader.SetFloat("_DeltaTime", deltaTime);

            computeShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }
    }
}