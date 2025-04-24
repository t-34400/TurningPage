#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class UpdateVerticesDispatcher
    {
        [SerializeField] private ComputeShader computeShader = default!;
        [SerializeField] private BezierCorrectedParaboloidCornerCalculator bezierCalculator = default!;

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

        public void Register(GraphicsBuffer vertexBuffer, Vector2 meshSize, Vector2Int gridCount)
        {
            this.gridCount = gridCount;

            kernel = computeShader.FindKernel("CSMain");

            computeShader.SetBuffer(kernel, "vertexBuffer", vertexBuffer);

            computeShader.SetFloats("_MeshSize", meshSize.x, meshSize.y);
            computeShader.SetInts("_GridCount", gridCount.x, gridCount.y);

            bezierCalculator.Initialize(meshSize);
        }

        public void Dispatch()
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            var parameters = bezierCalculator.UpdateBezierCorrectedParaboloidParameters();

            var corner1 = parameters.CornerPoint1;
            var corner2 = parameters.CornerPoint2;

            computeShader.SetFloats("_Corner1", corner1.x, corner1.y, corner1.z);
            computeShader.SetFloats("_Corner2", corner2.x, corner2.y, corner2.z);

            computeShader.SetFloat("_BezierHeight1", parameters.BezierHeight1);
            computeShader.SetFloat("_BezierHeight2", parameters.BezierHeight2);

            computeShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }

        public void ResetMeshCorners(bool isPageFlipped) => bezierCalculator.ResetMeshCorners(isPageFlipped);

        public void SetPinchPoint(Vector2Int vertexId)
        {
            var uv = new Vector2(
                (float)vertexId.x / gridCount.x,
                (float)vertexId.y / gridCount.y
            );

            bezierCalculator.PinchPointUv = uv;
            bezierCalculator.IsPinched = true;
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            bezierCalculator.PinchPoint = pinchPoint;
            bezierCalculator.PinchRight = pinchRight;
            bezierCalculator.PinchForward = pinchForward;
        }

        public void Release()
        {
            bezierCalculator.IsPinched = false;
        }
    }
}