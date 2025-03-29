#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    public struct Vertex
    {
        public Vector3 position;
        public Vector2 uv;
        public Vector3 normals;
    }
}