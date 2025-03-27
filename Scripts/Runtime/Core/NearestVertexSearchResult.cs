#nullable enable

using UnityEngine;

namespace TurningPage
{
    public struct NearestVertexSearchResult
    {
        public Vector2Int VertexId { get; set; }
        public Vector3 Position { get; set; }
        public float Distance { get; set; }
    }
}