#nullable enable

using UnityEngine;

namespace TurningPage
{
    public interface IVertexUpdaterOverride
    {
        void Initialize(GraphicsBuffer vertexBuffer, Vector2 meshSize, Vector2Int gridCount);
        bool TryUpdateVertex(float deltaTime);
    }
}