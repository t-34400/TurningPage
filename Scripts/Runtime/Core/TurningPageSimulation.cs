#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurningPage
{
    public class TurningPageSimulation : MonoBehaviour
    {
        [SerializeField] private Material frontMaterial = default!;
        [SerializeField] private Material backMaterial = default!;
        [Header("Grid")]
        [SerializeField] private Vector2 meshSize = Vector2.one;
        [SerializeField] private Vector2Int gridCount = new Vector2Int(16, 17);
        [SerializeField] private CornerUvs cornerUvs = new();
        [Header("Dispatchers")]
        [SerializeField] private InitializeVerticesDispatcher initializeVerticesDispatcher = default!;
        [SerializeField] private UpdateVerticesDispatcher updateVerticesDispatcher = default!;
        [SerializeField] private SearchNearestVertexDispatcher searchNearestVertexDispatcher = default!;

        private HashSet<IVertexUpdaterOverride> vertexUpdaterOverrides = new();

        private MeshRenderer? meshRenderer;

        private GraphicsBuffer? vertexBuffer = null;
        private GraphicsBuffer? predictedPositionBuffer = null;

        private Material? _frontMaterial;
        private Material? _backMaterial;

        public Material FrontMaterial
        {
            get => _frontMaterial ??= Instantiate(frontMaterial);
            set
            {
                _frontMaterial = value;
                meshRenderer?.SetMaterials(new () { FrontMaterial, BackMaterial });
            }
        }
        public Material BackMaterial
        {
            get => _backMaterial ??= Instantiate(backMaterial);
            set
            {
                _backMaterial = value;
                meshRenderer?.SetMaterials(new () { FrontMaterial, BackMaterial });
            }
        }

        public GraphicsBuffer? VertexBuffer => vertexBuffer;
        public Vector2 MeshSize => meshSize;
        public Vector2Int GridCount => gridCount;
        public Vector2 GridSize => new (meshSize.x / (gridCount.x - 1), meshSize.y / (gridCount.y - 1));

        public bool IsRunning { get; set; } = true;

        public bool AreBuffersRegistered { get; private set; } = false;

        public void InitializePage(bool isPageFlipped)
        {
            if (!AreBuffersRegistered)
                return;

            initializeVerticesDispatcher.Dispatch(isPageFlipped);
            updateVerticesDispatcher.ResetMeshCorners(isPageFlipped);
        }

        public bool TrySearchNearestVertex(Vector3 queryPoint, out NearestVertexSearchResult result)
        {
            if (!AreBuffersRegistered)
            {
                result = default;
                return false;
            }

            var localQueryPoint = transform.InverseTransformPoint(queryPoint);
            var _result = searchNearestVertexDispatcher.SearchNearestVertex(localQueryPoint);

            result = _result ?? default;

            return _result != null;
        }

        public NearestVertexSearchResult SearchNearestNextPageVertex(Vector3 queryPoint)
        {
            var localQueryPoint = transform.InverseTransformPoint(queryPoint);
            var result = searchNearestVertexDispatcher.SearchNearestNextPageVertex(localQueryPoint);

            return result;
        }

        public NearestVertexSearchResult SearchNearestPreviousPageVertex(Vector3 queryPoint)
        {
            var localQueryPoint = transform.InverseTransformPoint(queryPoint);
            var result = searchNearestVertexDispatcher.SearchNearestPreviousPageVertex(localQueryPoint);

            return result;
        }

        public void SetPinchPoint(Vector2Int vertexId) => updateVerticesDispatcher.SetPinchPoint(vertexId);
        public bool TrySetPinchPoint(Vector3 pinchPoint)
        {
            if (!TrySearchNearestVertex(pinchPoint, out var result))
            {
                return false;
            }

            SetPinchPoint(result.VertexId);
            return true;
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            var localPinchPoint = transform.InverseTransformPoint(pinchPoint);
            var localPinchRight = transform.InverseTransformDirection(pinchRight);
            var localPinchForward = transform.InverseTransformDirection(pinchForward);

            updateVerticesDispatcher.UpdatePinchData(localPinchPoint, localPinchRight, localPinchForward);
        }

        public void Release() => updateVerticesDispatcher.Release();

        public void RegisterVertexUpdaterOverride(IVertexUpdaterOverride vertexUpdaterOverride)
        {
            vertexUpdaterOverrides.Add(vertexUpdaterOverride);
        }

        public void UnregisterVertexUpdaterOverride(IVertexUpdaterOverride vertexUpdaterOverride)
        {
            vertexUpdaterOverrides.Remove(vertexUpdaterOverride);
        }

        private void Start()
        {
            if (meshSize.x <= 0 || meshSize.y <= 0 || gridCount.x < 2 || gridCount.y < 2)
            {
                enabled = false;
                return;
            }

            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshRenderer.SetMaterials(new() { FrontMaterial, BackMaterial });

            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();

            var mesh = GenerateGridMesh(meshSize, gridCount, cornerUvs);
            meshFilter.mesh = mesh;

            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
            vertexBuffer = mesh.GetVertexBuffer(0);

            int vertexCount = mesh.vertexCount;
            int float3Size = sizeof(float) * 3;

            predictedPositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, float3Size);

            var dx = meshSize.x / (gridCount.x - 1);
            var dz = meshSize.y / (gridCount.y - 1);
            var gridSize = new Vector2(dx, dz);

            initializeVerticesDispatcher.Register(vertexBuffer, gridSize, gridCount);
            updateVerticesDispatcher.Register(vertexBuffer, meshSize, gridCount);
            searchNearestVertexDispatcher.Register(vertexBuffer, gridCount, meshSize);

            AreBuffersRegistered = true;

            InitializePage(false);
        }

        private void FixedUpdate()
        {
            if (IsRunning)
            {
                var deltaTime = Time.fixedDeltaTime;

                foreach (IVertexUpdaterOverride vertexUpdaterOverride in vertexUpdaterOverrides)
                {
                    var overridden = vertexUpdaterOverride.TryUpdateVertex(deltaTime);

                    if (overridden)
                    {
                        return;
                    }
                }

                updateVerticesDispatcher.Dispatch(deltaTime);                
            }
        }
    
        private void OnDestroy()
        {
            vertexBuffer?.Dispose();
            predictedPositionBuffer?.Dispose();
            searchNearestVertexDispatcher?.Dispose();

            vertexBuffer = null;
            predictedPositionBuffer = null;
        }

        public Mesh GenerateGridMesh()
        {
            return GenerateGridMesh(MeshSize, GridCount, cornerUvs);
        }

        static Mesh GenerateGridMesh(Vector2 meshSize, Vector2Int gridCount, CornerUvs cornerUvs)
        {
            var mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;

            var vertexCount = gridCount.x * gridCount.y;
            var cellCount = (gridCount.x - 1) * (gridCount.y - 1);
            var triangleCount = cellCount * 6;

            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var normals = new Vector3[vertexCount];

            var trianglesFront = new int[triangleCount];
            var trianglesBack = new int[triangleCount];

            var dx = meshSize.x / (gridCount.x - 1);
            var dz = meshSize.y / (gridCount.y - 1);

            for (var z = 0; z < gridCount.y; z++)
            {
                for (var x = 0; x < gridCount.x; x++)
                {
                    var index = x + z * gridCount.x;
                    var posX = x * dx;
                    var posZ = z * dz;
                    vertices[index] = new Vector3(posX, 0, posZ);
                    uvs[index] = cornerUvs.GetUvCoordinates(
                        new Vector2(
                            (float) x / (gridCount.x - 1), 
                            (float) z / (gridCount.y - 1)
                        ));

                    normals[index] = Vector3.up;
                }
            }

            var ti = 0;
            var bi = 0;

            for (var z = 0; z < gridCount.y - 1; z++)
            {
                for (var x = 0; x < gridCount.x - 1; x++)
                {
                    var i = x + z * gridCount.x;

                    trianglesFront[ti++] = i;
                    trianglesFront[ti++] = i + gridCount.x;
                    trianglesFront[ti++] = i + 1;

                    trianglesFront[ti++] = i + 1;
                    trianglesFront[ti++] = i + gridCount.x;
                    trianglesFront[ti++] = i + gridCount.x + 1;
                }
            }
            for (var z = 0; z < gridCount.y - 1; z++)
            {
                for (var x = gridCount.x - 1; x > 0; x--)
                {
                    var i = x + z * gridCount.x;

                    trianglesBack[bi++] = i;
                    trianglesBack[bi++] = i - 1;
                    trianglesBack[bi++] = i + gridCount.x;

                    trianglesBack[bi++] = i - 1;
                    trianglesBack[bi++] = i + gridCount.x - 1;
                    trianglesBack[bi++] = i + gridCount.x;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;

            mesh.subMeshCount = 2;
            mesh.SetTriangles(trianglesFront, 0);
            mesh.SetTriangles(trianglesBack, 1);

            mesh.RecalculateBounds();

            return mesh;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            bool updated = false;

            if (initializeVerticesDispatcher.computeShader == null)
            {
                var initializerShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.INITIALIZER_SHADER_FILENAME);
                initializeVerticesDispatcher.computeShader = initializerShader;

                updated = updated || initializerShader != null;
            }
            if (updateVerticesDispatcher.ComputeShader == null)
            {
                var updaterShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.UPDATER_SHADER_FILENAME);
                updateVerticesDispatcher.ComputeShader = updaterShader;

                updated = updated || updaterShader != null;
            }
            if (searchNearestVertexDispatcher.computeShader == null)
            {
                var updaterShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.VERTEX_SEARCHER_SHADER_FILENAME);
                searchNearestVertexDispatcher.computeShader = updaterShader;

                updated = updated || updaterShader != null;
            }

            if (updated)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            const int NUM_SEGMENTS = 16;

            var originalMatrix = Gizmos.matrix;
            var originalColor = Gizmos.color;

            Gizmos.matrix = transform.localToWorldMatrix;

            var parameters = updateVerticesDispatcher.LatestParameters;

            var apex = parameters.Apex;
            var angle = parameters.Angle;
            var axisRoll = parameters.AxisRoll;

            var axis = parameters.Axis;
            var apexPoint = apex * Vector3.right;

            Gizmos.color = Color.red;
            Gizmos.DrawRay(apexPoint, axis);

            var azimuthRot = Quaternion.Euler(axisRoll, 0, 0) * Quaternion.Euler(0, 0, angle);
            var vertices = Enumerable.Range(0, NUM_SEGMENTS)
                .Select(i => i * 2 * Mathf.PI / NUM_SEGMENTS)
                .Select(theta => 
                    {
                        var angleRad = angle * Mathf.Deg2Rad;
                        var sinAngle = Mathf.Sin(angleRad);

                        return new Vector3(
                            Mathf.Cos(angleRad),
                            sinAngle * Mathf.Cos(theta),
                            sinAngle * Mathf.Sin(theta)
                        );
                    })
                .Select(v => azimuthRot * v + apexPoint)
                .ToArray();

            Gizmos.color = Color.cyan;
            for (int i = 0; i < vertices.Length; i++)
            {
                var nextIndex = (i + 1) % vertices.Length;
                Gizmos.DrawLine(vertices[i], apexPoint);
                Gizmos.DrawLine(vertices[i], vertices[nextIndex]);
            }

            Gizmos.matrix = originalMatrix;
            Gizmos.color = originalColor;
        }
#endif

        [Serializable]
        class CornerUvs
        {
            [SerializeField] private Vector2 leftBackwardCornerUv = new Vector2(1, 1);
            [SerializeField] private Vector2 rightBackwardCornerUv = new Vector2(1, 0);
            [SerializeField] private Vector2 leftForwardCornerUv = new Vector2(0, 1);
            [SerializeField] private Vector2 rightForwardCornerUv = new Vector2(0, 0);

            public Vector2 LeftBackwardCornerUv => leftBackwardCornerUv;
            public Vector2 RightBackwardCornerUv => rightBackwardCornerUv;
            public Vector2 LeftForwardCornerUv => leftForwardCornerUv;
            public Vector2 RightForwardCornerUv => rightForwardCornerUv;

            public Vector2 GetUvCoordinates(Vector2 normalizedPoint)
            {
                var top = Vector2.Lerp(LeftBackwardCornerUv, RightBackwardCornerUv, normalizedPoint.x);
                var bottom = Vector2.Lerp(LeftForwardCornerUv, RightForwardCornerUv, normalizedPoint.x);
                return Vector2.Lerp(bottom, top, normalizedPoint.y);
            }
        }
    }
}