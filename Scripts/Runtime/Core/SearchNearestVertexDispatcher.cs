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
        private Vector2 meshSize;

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

        public void Register(GraphicsBuffer vertexBuffer, Vector2Int gridCount, Vector2 meshSize)
        {
            this.vertexBuffer = vertexBuffer;

            this.gridCount = gridCount;
            this.meshSize = meshSize;

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

            return new NearestVertexSearchResult(
                vertexId,
                position,
                distance
            );
        }

        public NearestVertexSearchResult SearchNearestPreviousPageVertex(Vector3 queryPoint)
        {
            var flippedQueryPoint = new Vector3(queryPoint.x, queryPoint.y, -queryPoint.z);

            var result = SearchNearestNextPageVertex(flippedQueryPoint);

            var position = new Vector3(result.Position.x, result.Position.y, result.Position.z);

            return new NearestVertexSearchResult(
                result.VertexId, 
                position, 
                result.Distance
            );
        }

        public NearestVertexSearchResult SearchNearestNextPageVertex(Vector3 queryPoint)
        {
            var cellSize = new Vector2(
                meshSize.x / (gridCount.x - 1),
                meshSize.y / (gridCount.y - 1)
            );

            int ix = Mathf.RoundToInt(queryPoint.x / cellSize.x);
            int iy = Mathf.RoundToInt(queryPoint.z / cellSize.y);

            ix = Mathf.Clamp(ix, 0, gridCount.x - 1);
            iy = Mathf.Clamp(iy, 0, gridCount.y - 1);

            Vector3 vertexLocalPos = new Vector3(
                ix * cellSize.x,
                0f,
                iy * cellSize.y
            );

            float distance = Vector3.Distance(queryPoint, vertexLocalPos);

            return new NearestVertexSearchResult(
                new Vector2Int(ix, iy),
                vertexLocalPos,
                distance
            );
        }

        public void Dispose()
        {
            vertexBuffer = null;
            resultSqrDistanceBuffer?.Dispose();
            resultIndexBuffer?.Dispose();
        }
    }
}