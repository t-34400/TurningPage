#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurningPage
{
    class TurningPageSimulation : MonoBehaviour
    {
        [SerializeField] private List<Material> renderMaterials = default!;
        [SerializeField] private int solveIter = 10;
        [SerializeField] private float timestep = 0.05f;
        [Header("Grid")]
        [SerializeField] private Vector2 meshSize = Vector2.one;
        [SerializeField] private Vector2Int gridCount = new Vector2Int(16, 17);
        [Header("Dispatchers")]
        [SerializeField] private InitializeVerticesDispatcher initializeVerticesDispatcher = default!;
        [SerializeField] private PredictPositionsDispatcher predictPositionsDispatcher = default!;
        [SerializeField] private SolveOnFineGridDispatcher solveOnFineGridDispatcher = default!;
        [SerializeField] private UpdateVerticesDispatcher updateVerticesDispatcher = default!;

        private MeshRenderer? meshRenderer;

        private GraphicsBuffer? velocityBuffer = null;
        private GraphicsBuffer? predictedPositionBuffer = null;

        public void SetPinchPoint(Vector3 pinchPoint)
        {
            var pinchId = GetNearestPointIndices(pinchPoint);
            solveOnFineGridDispatcher.SetPinchPoint(pinchId);
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

        private Vector2Int GetNearestPointIndices(Vector3 worldPoint)
        {
            var localPoint = transform.InverseTransformPoint(worldPoint);

            var u = localPoint.x / meshSize.x;
            var v = localPoint.z / meshSize.y;

            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            var indexX = Mathf.RoundToInt(u * (gridCount.x - 1));
            var indexY = Mathf.RoundToInt(v * (gridCount.y - 1));

            return new (indexX, indexY);
        }

        private void Start()
        {
            if (meshSize.x <= 0 || meshSize.y <= 0 || gridCount.x < 2 || gridCount.y < 2)
            {
                enabled = false;
                return;
            }

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.SetMaterials(renderMaterials);

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            var mesh = GenerateGridMesh(meshSize, gridCount);
            meshFilter.mesh = mesh;

            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
            var vertexBuffer = mesh.GetVertexBuffer(0);

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

            initializeVerticesDispatcher.Dispatch(true);
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
        }

        static Mesh GenerateGridMesh(Vector2 meshSize, Vector2Int gridCount)
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
                    uvs[index] = new Vector2(x / (float)(gridCount.x - 1), z / (float)(gridCount.y - 1));

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
                for (var x = 0; x < gridCount.x - 1; x++)
                {
                    var i = x + z * gridCount.x;

                    trianglesBack[bi++] = i;
                    trianglesBack[bi++] = i + 1;
                    trianglesBack[bi++] = i + gridCount.x;

                    trianglesBack[bi++] = i + 1;
                    trianglesBack[bi++] = i + gridCount.x + 1;
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
            const string COMPUTE_SHADER_DIR = "Assets/TurningPage/ComputeShaders/";
            const string INITIALIZER_SHADER_FILENAME = COMPUTE_SHADER_DIR + "InitialilzeVertices.compute";
            const string PREDICTER_SHADER_FILENAME = COMPUTE_SHADER_DIR + "PredictPositions.compute";
            const string SOLVER_SHADER_FILENAME = COMPUTE_SHADER_DIR + "SolveOnFineGrid.compute";
            const string UPDATER_SHADER_FILENAME = COMPUTE_SHADER_DIR + "UpdateVertices.compute";

            bool updated = false;

            if (initializeVerticesDispatcher.ComputeShader == null)
            {
                var initializerShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(INITIALIZER_SHADER_FILENAME);
                initializeVerticesDispatcher.ComputeShader = initializerShader;

                updated = updated || initializerShader != null;
            }
            if (predictPositionsDispatcher.ComputeShader == null)
            {
                var predictorShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(PREDICTER_SHADER_FILENAME);
                predictPositionsDispatcher.ComputeShader = predictorShader;

                updated = updated || predictorShader != null;
            }
            if (solveOnFineGridDispatcher.ComputeShader == null)
            {
                var solverShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(SOLVER_SHADER_FILENAME);
                solveOnFineGridDispatcher.ComputeShader = solverShader;

                updated = updated || solverShader != null;
            }
            if (updateVerticesDispatcher.ComputeShader == null)
            {
                var updaterShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(UPDATER_SHADER_FILENAME);
                updateVerticesDispatcher.ComputeShader = updaterShader;

                updated = updated || updaterShader != null;
            }

            if (updated)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}