#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class SolveOnFineGridDispatcher
    {
        [SerializeField] private ComputeShader computeShader = default!;
        [Header("Stretch")]
        [SerializeField] private float stretchStiffness = 10000f;
        [SerializeField] private float stretchDampingFactor = 0.1f;
        [SerializeField] private float diagStretchStiffness = 10000f;
        [SerializeField] private float diagStretchDampingFactor = 0.1f;
        [Header("Bend")]
        [SerializeField] private float bendHorizontalStiffness = 3000f;
        [SerializeField] private float bendVerticalStiffness = 10000f;
        [SerializeField] private float bendDampingFactor = 0.05f;
        [SerializeField] private float bendSorFactor = 2f;
        [Header("Pinch")]
        [SerializeField] private float pinchStiffness = 100f;
        [SerializeField] private float pinchDampingFactor = 0.8f;
        [SerializeField] private float pinchRotationStiffness = 100f;
        [SerializeField] private float pinchRotationDampingFactor = 0.8f;

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
        
        public void Register(GraphicsBuffer vertexBuffer, GraphicsBuffer predictedPositionBuffer, Vector2 gridSize, Vector2Int gridCount)
        {
            this.gridCount = gridCount;

            kernel = computeShader.FindKernel("CSMain");

            computeShader.SetBuffer(kernel, "vertexBuffer", vertexBuffer);
            computeShader.SetBuffer(kernel, "predictedPositionBuffer", predictedPositionBuffer);

            computeShader.SetFloats("_GridSize", gridSize.x, gridSize.y);
            computeShader.SetInts("_GridCount", gridCount.x, gridCount.y);
            computeShader.SetBool("_IsPinched", false);
        }

        public void SetPinchPoint(Vector2Int pinchId)
        {
            computeShader.SetBool("_IsPinched", true);
            computeShader.SetInts("_PinchId", pinchId.x, pinchId.y - 1);
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            computeShader.SetFloats("_PinchPoint", pinchPoint.x, pinchPoint.y, pinchPoint.z);
            computeShader.SetFloats("_PinchRight", pinchRight.x, pinchRight.y, pinchRight.z);
            computeShader.SetFloats("_PinchForward", pinchForward.x, pinchForward.y, pinchForward.z);
        }

        public void Release()
        {
            computeShader.SetBool("_IsPinched", false);
        }

        public void Dispatch(float stepDeltaTime)
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            var stretchCompliance = 1 / (stretchStiffness * stepDeltaTime * stepDeltaTime);
            var stretchDampingEffect = stretchDampingFactor / (stretchStiffness * stepDeltaTime);
            computeShader.SetFloat("_StretchCompliance", stretchCompliance);
            computeShader.SetFloat("_StretchDampingEffect", stretchDampingEffect);
            
            var diagStretchCompliance = 1 / (diagStretchStiffness * stepDeltaTime * stepDeltaTime);
            var diagStretchDampingEffect = diagStretchDampingFactor / (diagStretchStiffness * stepDeltaTime);
            computeShader.SetFloat("_DiagStretchCompliance", diagStretchCompliance);
            computeShader.SetFloat("_DiagStretchDampingEffect", diagStretchDampingEffect);
            
            var bendHorizontalCompliance = 1 / (bendHorizontalStiffness * stepDeltaTime * stepDeltaTime);
            var bendHorizontalDampingEffect = bendDampingFactor / (bendHorizontalStiffness * stepDeltaTime);
            var bendVerticalCompliance = 1 / (bendVerticalStiffness * stepDeltaTime * stepDeltaTime);
            var bendVerticalDampingEffect = bendDampingFactor / (bendVerticalStiffness * stepDeltaTime);
            computeShader.SetFloat("_BendHorizontalCompliance", bendHorizontalCompliance);
            computeShader.SetFloat("_BendHorizontalDampingEffect", bendHorizontalDampingEffect);
            computeShader.SetFloat("_BendVerticalCompliance", bendVerticalCompliance);
            computeShader.SetFloat("_BendVerticalDampingEffect", bendVerticalDampingEffect);
            
            var pinchCompliance = 1 / (pinchStiffness * stepDeltaTime * stepDeltaTime);
            var pinchDampingEffect = pinchDampingFactor / (pinchStiffness * stepDeltaTime);
            computeShader.SetFloat("_BendCompliance", pinchCompliance);
            computeShader.SetFloat("_BendDampingEffect", pinchDampingEffect);
            computeShader.SetFloat("_BendSORFactor", bendSorFactor);

            var pinchRotationCompliance = 1 / (pinchRotationStiffness * stepDeltaTime * stepDeltaTime);
            var pinchRotationDampingEffect = pinchRotationDampingFactor / (pinchRotationStiffness * stepDeltaTime);
            computeShader.SetFloat("_PinchRotationCompliance", pinchRotationCompliance);
            computeShader.SetFloat("_PinchRotationDampingEffect", pinchRotationDampingEffect);

            computeShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }
    }
}