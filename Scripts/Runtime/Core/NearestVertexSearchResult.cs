#nullable enable

using UnityEngine;

namespace TurningPage
{
    public readonly struct NearestVertexSearchResult
    {
        public Vector2Int VertexId { get; }
        public Vector3 Position { get; }
        public float Distance { get; }

        public NearestVertexSearchResult(Vector2Int vertexId, Vector3 position, float distance)
        {
            VertexId = vertexId;
            Position = position;
            Distance = distance;
        }

        public override string ToString()
        {
            return $"VertexId: {VertexId}, Pos: {Position}, Dist: {Distance:F3}";
        }
    }
}