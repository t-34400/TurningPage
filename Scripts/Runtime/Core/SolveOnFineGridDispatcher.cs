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

        private int kernel;
        private GraphicsBuffer? vertexBuffer;

        private Vector2Int gridCount;
        private Vector2 gridSize;

        private Vector2Int pinchedVertexId;
        private Vector3 latestPinchPoint;

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
            this.gridSize = gridSize;
            this.vertexBuffer = vertexBuffer;

            kernel = computeShader.FindKernel("CSMain");

            computeShader.SetBuffer(kernel, "vertexBuffer", vertexBuffer);
            computeShader.SetBuffer(kernel, "predictedPositionBuffer", predictedPositionBuffer);

            computeShader.SetFloats("_GridSize", gridSize.x, gridSize.y);
            computeShader.SetInts("_GridCount", gridCount.x, gridCount.y);
            computeShader.SetBool("_IsPinched", false);
        }

        public void SetPinchPoint(Vector2Int pinchedVertexId)
        {
            if (!TryGetVertexData(pinchedVertexId, out var vertex))
            {
                return;
            }

            computeShader.SetBool("_IsPinched", true);
            computeShader.SetInts("_PinchId", pinchedVertexId.x, pinchedVertexId.y - 1);

            this.pinchedVertexId = pinchedVertexId;
            latestPinchPoint = vertex.position;
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            pinchPoint = ConstrainPinchPointFromSeam(pinchPoint);

            computeShader.SetFloats("_PinchPoint", pinchPoint.x, pinchPoint.y, pinchPoint.z);
            computeShader.SetFloats("_PinchRight", pinchRight.x, pinchRight.y, pinchRight.z);
            computeShader.SetFloats("_PinchForward", pinchForward.x, pinchForward.y, pinchForward.z);

            latestPinchPoint = pinchPoint;
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

        private Vector3 ConstrainPinchPointFromSeam(Vector3 pinchPoint)
        {
            const int MAX_ITER = 5;
            const float OFFSET = 0.01f;

            var seamStart = Vector3.zero;
            var seamEnd = Vector3.right * gridSize.x;

            var maxDistanceFromStart = 
                new Vector2(
                    gridSize.x * pinchedVertexId.x, 
                    gridSize.y * pinchedVertexId.y
                ).magnitude;
            var maxDistanceFromEnd = 
                new Vector2(
                    gridSize.x * (gridCount.x - pinchedVertexId.x - 1), 
                    gridSize.y * (gridCount.y - pinchedVertexId.y - 1)
                ).magnitude;

            var distanceFromStart = (pinchPoint - seamStart).magnitude;
            var distanceFromEnd = (pinchPoint - seamEnd).magnitude;

            for (var i = 0; i < MAX_ITER; ++i)
            {
                if (distanceFromStart > maxDistanceFromStart + OFFSET)
                {
                    pinchPoint += (seamStart - pinchPoint).normalized * (distanceFromStart - maxDistanceFromStart);
                }
                else if (distanceFromEnd < maxDistanceFromEnd + OFFSET)
                {
                    return pinchPoint;
                }

                distanceFromEnd = (pinchPoint - seamEnd).magnitude;

                if (distanceFromEnd > maxDistanceFromEnd + OFFSET)
                {
                    pinchPoint += (seamEnd - pinchPoint).normalized * (distanceFromEnd - maxDistanceFromEnd);
                }
                else if (distanceFromStart < maxDistanceFromStart + OFFSET)
                {
                    return pinchPoint;
                }

                distanceFromStart = (pinchPoint - seamStart).magnitude;
            }

            return latestPinchPoint;
        }

        private bool TryGetVertexData(Vector2Int vertexId, out Vertex vertex)
        {
            if (vertexBuffer == null)
            {
                vertex = default;
                return false;
            }

            var vertexIndex = vertexId.y * gridCount.x + vertexId.x;

            var vertexArray = new Vertex[1];
            vertexBuffer.GetData(vertexArray, 0, vertexIndex, 1);
            
            vertex = vertexArray[0];
            return true;
        }
    }
}