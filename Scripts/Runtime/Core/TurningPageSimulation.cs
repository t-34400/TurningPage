#nullable enable

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurningPage
{
    public class TurningPageSimulation : MonoBehaviour
    {
        [SerializeField] private Material frontMaterial = default!;
        [SerializeField] private Material backMaterial = default!;
        [Header("Simulation Settings")]
        [SerializeField] private int solveIter = 10;
        [SerializeField] private float timestep = 0.05f;
        [Header("Grid")]
        [SerializeField] private Vector2 meshSize = Vector2.one;
        [SerializeField] private Vector2Int gridCount = new Vector2Int(16, 17);
        [SerializeField] private CornerUvs cornerUvs = new();
        [Header("Dispatchers")]
        [SerializeField] private InitializeVerticesDispatcher initializeVerticesDispatcher = default!;
        [SerializeField] private PredictPositionsDispatcher predictPositionsDispatcher = default!;
        [SerializeField] private SolveOnFineGridDispatcher solveOnFineGridDispatcher = default!;
        [SerializeField] private UpdateVerticesDispatcher updateVerticesDispatcher = default!;
        [SerializeField] private SearchNearestVertexDispatcher searchNearestVertexDispatcher = default!;

        private MeshRenderer? meshRenderer;

        private GraphicsBuffer? vertexBuffer = null;
        private GraphicsBuffer? velocityBuffer = null;
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

        public bool AreBuffersRegistered { get; private set; } = false;

        public void InitializePage(bool isPageFlipped)
        {
            if (!AreBuffersRegistered)
                return;

            initializeVerticesDispatcher.Dispatch(isPageFlipped);
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

        public void SetPinchPoint(Vector2Int vertexId) => solveOnFineGridDispatcher.SetPinchPoint(vertexId);
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

            solveOnFineGridDispatcher.UpdatePinchData(localPinchPoint, localPinchRight, localPinchForward);
        }

        public void Release()
        {
            solveOnFineGridDispatcher.Release();
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

            meshRenderer.SetMaterials(new () { FrontMaterial, BackMaterial });

            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();

            var mesh = GenerateGridMesh(meshSize, gridCount, cornerUvs);
            meshFilter.mesh = mesh;

            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
            vertexBuffer = mesh.GetVertexBuffer(0);

            int vertexCount = mesh.vertexCount;
            int float3Size = sizeof(float) * 3;

            velocityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, float3Size);
            predictedPositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, float3Size);

            var dx = meshSize.x / (gridCount.x - 1);
            var dz = meshSize.y / (gridCount.y - 1);
            var gridSize = new Vector2(dx, dz);

            initializeVerticesDispatcher.Register(vertexBuffer, velocityBuffer, gridSize, gridCount);
            predictPositionsDispatcher.Register(vertexBuffer, velocityBuffer, predictedPositionBuffer, gridSize, gridCount);
            solveOnFineGridDispatcher.Register(vertexBuffer, predictedPositionBuffer, gridSize, gridCount);
            updateVerticesDispatcher.Register(vertexBuffer, velocityBuffer, predictedPositionBuffer, gridSize, gridCount);
            searchNearestVertexDispatcher.Register(vertexBuffer, gridCount, meshSize);

            AreBuffersRegistered = true;

            InitializePage(false);
        }

        private void FixedUpdate()
        {
            if (solveIter <= 0)
            {
                return;
            }

            var stepDeltaTime = timestep / solveIter;

            for (int iter = 0; iter < solveIter; ++iter)
            {
                predictPositionsDispatcher.Dispatch(transform, stepDeltaTime);
                solveOnFineGridDispatcher.Dispatch(stepDeltaTime);
                updateVerticesDispatcher.Dispatch(stepDeltaTime);                
            }
        }
    
        private void OnDestroy()
        {
            velocityBuffer?.Dispose();
            predictedPositionBuffer?.Dispose();
            searchNearestVertexDispatcher?.Dispose();

            vertexBuffer = null;
            velocityBuffer = null;
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

            if (initializeVerticesDispatcher.ComputeShader == null)
            {
                var initializerShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.INITIALIZER_SHADER_FILENAME);
                initializeVerticesDispatcher.ComputeShader = initializerShader;

                updated = updated || initializerShader != null;
            }
            if (predictPositionsDispatcher.ComputeShader == null)
            {
                var predictorShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.PREDICTER_SHADER_FILENAME);
                predictPositionsDispatcher.ComputeShader = predictorShader;

                updated = updated || predictorShader != null;
            }
            if (solveOnFineGridDispatcher.ComputeShader == null)
            {
                var solverShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.SOLVER_SHADER_FILENAME);
                solveOnFineGridDispatcher.ComputeShader = solverShader;

                updated = updated || solverShader != null;
            }
            if (updateVerticesDispatcher.ComputeShader == null)
            {
                var updaterShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.UPDATER_SHADER_FILENAME);
                updateVerticesDispatcher.ComputeShader = updaterShader;

                updated = updated || updaterShader != null;
            }
            if (searchNearestVertexDispatcher.ComputeShader == null)
            {
                var updaterShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(Constants.VERTEX_SEARCHER_SHADER_FILENAME);
                searchNearestVertexDispatcher.ComputeShader = updaterShader;

                updated = updated || updaterShader != null;
            }

            if (updated)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
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