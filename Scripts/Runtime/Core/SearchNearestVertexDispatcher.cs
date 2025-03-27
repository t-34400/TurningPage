#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    class SearchNearestVertexDispatcher
    {
        [SerializeField] private ComputeShader computeShader = default!;
        [Min(1)]
        [SerializeField] private int checkedVerticesPerThread = 4;

        private int checkedVerticesPerGroup;
        private Vector2Int gridCount;

        private int initialAggregationKernel;
        private int finalAggregationKernel;

        private GraphicsBuffer? vertexBuffer = null;
        private GraphicsBuffer? resultSqrDistanceBuffer = null;
        private GraphicsBuffer? resultIndexBuffer = null;

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

        public void Register(GraphicsBuffer vertexBuffer, Vector2Int gridCount)
        {
            this.vertexBuffer = vertexBuffer;

            this.gridCount = gridCount;
            var vertexCount = gridCount.x * (gridCount.y - 1);

            checkedVerticesPerGroup = 64 * checkedVerticesPerThread;
            int resultBufferCount = Mathf.CeilToInt((float)vertexCount / checkedVerticesPerGroup);

            resultSqrDistanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, resultBufferCount, sizeof(float));
            resultIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, resultBufferCount, sizeof(int));

            initialAggregationKernel = computeShader.FindKernel("InitialAggregationKernel");
            computeShader.SetBuffer(initialAggregationKernel, "vertexBuffer", vertexBuffer);
            computeShader.SetBuffer(initialAggregationKernel, "resultSqrDistanceBuffer", resultSqrDistanceBuffer);
            computeShader.SetBuffer(initialAggregationKernel, "resultIndexBuffer", resultIndexBuffer);

            finalAggregationKernel = computeShader.FindKernel("FinalAggregationKernel");
            computeShader.SetBuffer(finalAggregationKernel, "vertexBuffer", vertexBuffer);
            computeShader.SetBuffer(finalAggregationKernel, "resultSqrDistanceBuffer", resultSqrDistanceBuffer);
            computeShader.SetBuffer(finalAggregationKernel, "resultIndexBuffer", resultIndexBuffer);

            computeShader.SetInts("_GridCount", gridCount.x, gridCount.y);
            computeShader.SetInt("_VertexCount", vertexCount);
            computeShader.SetInt("_VerticesPerThread", checkedVerticesPerThread);
            computeShader.SetInt("_ResultBufferCount", resultBufferCount);
        }

        public NearestVertexSearchResult? SearchNearestVertex(Vector3 queryPoint)
        {
            if (vertexBuffer ==  null || resultSqrDistanceBuffer == null || resultIndexBuffer == null)
            {
                Debug.LogError("Result buffers are not initialized. Ensure that the Register method is called before invoking SearchNearestVertex.");
                return null;
            }

            var vertexCount = gridCount.x * (gridCount.y - 1);
            int resultBufferCount = Mathf.CeilToInt((float) vertexCount / checkedVerticesPerGroup);

            computeShader.SetFloats("_QueryPoint", queryPoint.x, queryPoint.y, queryPoint.z);

            computeShader.Dispatch(initialAggregationKernel, resultBufferCount, 1, 1);

            var interval = 1;
            var threadCount = Mathf.CeilToInt((float)resultBufferCount / checkedVerticesPerGroup);
            while (threadCount > 1)
            {
                computeShader.SetInt("_FinalAggregationInterval", interval);
                computeShader.SetInt("_FinalAggregationThreadCount", threadCount);                

                computeShader.Dispatch(finalAggregationKernel, resultBufferCount, 1, 1);

                interval *= 64 * checkedVerticesPerGroup;
                threadCount = Mathf.CeilToInt((float)threadCount / checkedVerticesPerGroup);
            }

            var indexArray = new int[1];
            resultIndexBuffer.GetData(indexArray, 0, 0, 1);
            var resultIndex = indexArray[0];

            if (resultIndex < 0 || resultIndex >= gridCount.x * gridCount.y)
            {
                return null;
            }

            var vertexIdX = resultIndex % gridCount.x;
            var vertexIdY = resultIndex / gridCount.x;
            var vertexId = new Vector2Int(vertexIdX, vertexIdY);

            var vertexArray = new Vertex[1];
            vertexBuffer.GetData(vertexArray, 0, resultIndex, 1);
            var position = vertexArray[0].position;

            var distance = Vector3.Distance(position, queryPoint);

            return new NearestVertexSearchResult()
                {
                    VertexId = vertexId,
                    Position = position,
                    Distance = distance,
                };
        }

        public void Dispose()
        {
            vertexBuffer = null;
            resultSqrDistanceBuffer?.Dispose();
            resultIndexBuffer?.Dispose();
        }

        [Serializable]
        public struct Vertex
        {
            public Vector3 position;
            public Vector2 uv;
            public Vector3 normals;
        }
    }
}