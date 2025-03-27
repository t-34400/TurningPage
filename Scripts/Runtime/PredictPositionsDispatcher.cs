#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class PredictPositionsDispatcher
    {
        [SerializeField] private ComputeShader computeShader = default!;
        [SerializeField] private Vector3 gravity = Vector3.down * 100f;
        [SerializeField] private float resistance = 1f;

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

        public void Dispatch(Transform transform, float deltaTime)
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            var stepGravity = transform.InverseTransformDirection(gravity * deltaTime * deltaTime);

            computeShader.SetFloats("_Gravity", stepGravity.x, stepGravity.y, stepGravity.z);
            computeShader.SetFloat("_Resistance", resistance);
            computeShader.SetFloat("_DeltaTime", deltaTime);

            computeShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }
    }
}