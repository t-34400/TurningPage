#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    public class InitializeVerticesDispatcher
    {
        [SerializeField] internal ComputeShader computeShader = default!;

        private ComputeShader? _computeShader;

        private Vector2Int gridCount;
        private int kernel;

        public ComputeShader ComputeShader  => _computeShader ??= UnityEngine.Object.Instantiate(computeShader);

        public void Register(GraphicsBuffer vertexBuffer, GraphicsBuffer velocityBuffer, Vector2 gridSize, Vector2Int gridCount)
        {
            this.gridCount = gridCount;

            kernel = ComputeShader.FindKernel("CSMain");

            ComputeShader.SetBuffer(kernel, "vertexBuffer", vertexBuffer);
            ComputeShader.SetBuffer(kernel, "velocityBuffer", velocityBuffer);

            ComputeShader.SetFloats("_GridSize", gridSize.x, gridSize.y);
            ComputeShader.SetInts("_GridCount", gridCount.x, gridCount.y);
        }

        public void Dispatch(bool isPageFlipped)
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt(gridCount.y / 8f);

            ComputeShader.SetBool("_IsPageFlipped", isPageFlipped);

            ComputeShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }

# if UNITY_EDITOR
        public void SetComputeShader_Editor(ComputeShader computeShader) => this.computeShader = computeShader;
# endif
    }
}