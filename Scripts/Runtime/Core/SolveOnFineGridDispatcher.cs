#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class SolveOnFineGridDispatcher
    {
        [SerializeField] internal ComputeShader computeShader = default!;
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

        private ComputeShader? _computeShader;

        private int kernel;
        private GraphicsBuffer? vertexBuffer;

        private Vector2Int gridCount;
        private Vector2 gridSize;

        private Vector2Int pinchedVertexId;
        private Vector3 latestPinchPoint;

        public ComputeShader ComputeShader  => _computeShader ??= UnityEngine.Object.Instantiate(computeShader);
        
        public void Register(GraphicsBuffer vertexBuffer, GraphicsBuffer predictedPositionBuffer, Vector2 gridSize, Vector2Int gridCount)
        {
            this.gridCount = gridCount;
            this.gridSize = gridSize;
            this.vertexBuffer = vertexBuffer;

            kernel = ComputeShader.FindKernel("CSMain");

            ComputeShader.SetBuffer(kernel, "vertexBuffer", vertexBuffer);
            ComputeShader.SetBuffer(kernel, "predictedPositionBuffer", predictedPositionBuffer);

            ComputeShader.SetFloats("_GridSize", gridSize.x, gridSize.y);
            ComputeShader.SetInts("_GridCount", gridCount.x, gridCount.y);
            ComputeShader.SetBool("_IsPinched", false);
        }

        public void SetPinchPoint(Vector2Int pinchedVertexId)
        {
            Debug.Log($"Set Pinch Point: ID={pinchedVertexId}");

            if (!TryGetVertexData(pinchedVertexId, out var vertex))
            {
                return;
            }

            ComputeShader.SetBool("_IsPinched", true);
            ComputeShader.SetInts("_PinchId", pinchedVertexId.x, pinchedVertexId.y - 1);

            this.pinchedVertexId = pinchedVertexId;
            latestPinchPoint = vertex.position;
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            pinchPoint = ConstrainPinchPointFromSeam(pinchPoint);

            ComputeShader.SetFloats("_PinchPoint", pinchPoint.x, pinchPoint.y, pinchPoint.z);
            ComputeShader.SetFloats("_PinchRight", pinchRight.x, pinchRight.y, pinchRight.z);
            ComputeShader.SetFloats("_PinchForward", pinchForward.x, pinchForward.y, pinchForward.z);

            latestPinchPoint = pinchPoint;
        }

        public void Release()
        {
            ComputeShader.SetBool("_IsPinched", false);
        }

        public void Dispatch(float stepDeltaTime)
        {
            var threadGroupX = Mathf.CeilToInt(gridCount.x / 8f);
            var threadGroupY = Mathf.CeilToInt((gridCount.y - 1) / 8f);

            var stretchCompliance = 1 / (stretchStiffness * stepDeltaTime * stepDeltaTime);
            var stretchDampingEffect = stretchDampingFactor / (stretchStiffness * stepDeltaTime);
            ComputeShader.SetFloat("_StretchCompliance", stretchCompliance);
            ComputeShader.SetFloat("_StretchDampingEffect", stretchDampingEffect);
            
            var diagStretchCompliance = 1 / (diagStretchStiffness * stepDeltaTime * stepDeltaTime);
            var diagStretchDampingEffect = diagStretchDampingFactor / (diagStretchStiffness * stepDeltaTime);
            ComputeShader.SetFloat("_DiagStretchCompliance", diagStretchCompliance);
            ComputeShader.SetFloat("_DiagStretchDampingEffect", diagStretchDampingEffect);
            
            var bendHorizontalCompliance = 1 / (bendHorizontalStiffness * stepDeltaTime * stepDeltaTime);
            var bendHorizontalDampingEffect = bendDampingFactor / (bendHorizontalStiffness * stepDeltaTime);
            var bendVerticalCompliance = 1 / (bendVerticalStiffness * stepDeltaTime * stepDeltaTime);
            var bendVerticalDampingEffect = bendDampingFactor / (bendVerticalStiffness * stepDeltaTime);
            ComputeShader.SetFloat("_BendHorizontalCompliance", bendHorizontalCompliance);
            ComputeShader.SetFloat("_BendHorizontalDampingEffect", bendHorizontalDampingEffect);
            ComputeShader.SetFloat("_BendVerticalCompliance", bendVerticalCompliance);
            ComputeShader.SetFloat("_BendVerticalDampingEffect", bendVerticalDampingEffect);
            
            var pinchCompliance = 1 / (pinchStiffness * stepDeltaTime * stepDeltaTime);
            var pinchDampingEffect = pinchDampingFactor / (pinchStiffness * stepDeltaTime);
            ComputeShader.SetFloat("_BendCompliance", pinchCompliance);
            ComputeShader.SetFloat("_BendDampingEffect", pinchDampingEffect);
            ComputeShader.SetFloat("_BendSORFactor", bendSorFactor);

            var pinchRotationCompliance = 1 / (pinchRotationStiffness * stepDeltaTime * stepDeltaTime);
            var pinchRotationDampingEffect = pinchRotationDampingFactor / (pinchRotationStiffness * stepDeltaTime);
            ComputeShader.SetFloat("_PinchRotationCompliance", pinchRotationCompliance);
            ComputeShader.SetFloat("_PinchRotationDampingEffect", pinchRotationDampingEffect);

            ComputeShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        }

        private Vector3 ConstrainPinchPointFromSeam(Vector3 pinchPoint)
        {
            const int MAX_ITER = 5;
            const float OFFSET = 0.01f;

            var seamStart = Vector3.zero;
            var seamEnd = Vector3.right * gridSize.x * gridCount.x;

            var maxDistanceFromStart = 
                new Vector2(
                    gridSize.x * pinchedVertexId.x, 
                    gridSize.y * pinchedVertexId.y
                ).magnitude;
            var maxDistanceFromEnd = 
                new Vector2(
                    gridSize.x * (gridCount.x - pinchedVertexId.x - 1), 
                    gridSize.y * pinchedVertexId.y
                ).magnitude;

            var distanceFromStart = (pinchPoint - seamStart).magnitude;
            var distanceFromEnd = (pinchPoint - seamEnd).magnitude;

            for (var i = 0; i < MAX_ITER; ++i)
            {
                if (distanceFromStart > maxDistanceFromStart + OFFSET)
                {
                    var delta = (seamStart - pinchPoint).normalized * (distanceFromStart - maxDistanceFromStart);
                    pinchPoint += delta;

                    distanceFromStart = (pinchPoint - seamStart).magnitude;
                    distanceFromEnd = (pinchPoint - seamEnd).magnitude;
                }
                else if (distanceFromEnd < maxDistanceFromEnd + OFFSET)
                {
                    return pinchPoint;
                }

                if (distanceFromEnd > maxDistanceFromEnd + OFFSET)
                {
                    var delta = (seamEnd - pinchPoint).normalized * (distanceFromEnd - maxDistanceFromEnd);
                    pinchPoint += delta;

                    distanceFromStart = (pinchPoint - seamStart).magnitude;
                    distanceFromEnd = (pinchPoint - seamEnd).magnitude;
                }
                else if (distanceFromStart < maxDistanceFromStart + OFFSET)
                {
                    return pinchPoint;
                }
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