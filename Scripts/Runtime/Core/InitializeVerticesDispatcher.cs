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
        public void Register(GraphicsBuffer vertexBuffer, Vector2 gridSize, Vector2Int gridCount)
        {
            this.gridCount = gridCount;

            kernel = ShaderInstance.FindKernel("CSMain");

            ShaderInstance.SetBuffer(kernel, "vertexBuffer", vertexBuffer);

            ShaderInstance.SetFloats("_GridSize", gridSize.x, gridSize.y);
            ShaderInstance.SetInts("_GridCount", gridCount.x, gridCount.y);
        }

        public void Dispatch(bool isPageFlipped)
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt(gridCount.y / 8f);

            ShaderInstance.SetBool("_IsPageFlipped", isPageFlipped);

            ShaderInstance.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }

# if UNITY_EDITOR
        public void SetComputeShader_Editor(ComputeShader computeShader) => this.computeShader = computeShader;
# endif
    }
}