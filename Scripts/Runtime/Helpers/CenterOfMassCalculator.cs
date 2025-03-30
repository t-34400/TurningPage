#nullable enable

using UnityEngine;

namespace TurningPage.Helpers
{
    public class CenterOfMassCalculator : MonoBehaviour
    {
        [SerializeField] private TurningPageSimulation turningPageSimulation = default!;
        [SerializeField] private ComputeShader computeShader = default!;

        private bool initialized = false;

        private GraphicsBuffer? sumBarycenterBuffer = null;

        private int initialAggregationKernel;
        private int finalAggregationKernel;

        public Vector3 CalculateCenterOfMass()
        {
            if (!initialized)
            {
                if (Initialize())
                    initialized = true;
                else
                    return Vector3.zero;
            }

            if (sumBarycenterBuffer == null)
                return Vector3.zero;

            var gridCount = turningPageSimulation.GridCount;

            var threadGroupX = Mathf.CeilToInt(gridCount.x / 16f);
            var threadGroupY = Mathf.CeilToInt(gridCount.y / 16f);

            var interval = 1;
            threadGroupX = Mathf.CeilToInt(threadGroupX / 16f);
            threadGroupY = Mathf.CeilToInt(threadGroupY / 16f);

            while (threadGroupX > 1 && threadGroupY > 1)
            {
                computeShader.SetInt("_FinalAggregationInterval", interval);
                //computeShader.Dispatch(finalAggregationKernel, threadGroupX, threadGroupY, 1);

                interval *= 2;
            }

            var vectorBuffer = new Vector3[1];
            sumBarycenterBuffer.GetData(vectorBuffer, 0, 0, 1);
            var position = vectorBuffer[0];

            return position;
        }

        private bool Initialize()
        {
            var vertexBuffer = turningPageSimulation.VertexBuffer;
            if (vertexBuffer == null)
            {
                return false;
            }

            var gridCount = turningPageSimulation.GridCount;

            var threadGroupX = Mathf.CeilToInt(gridCount.x / 16f);
            var threadGroupY = Mathf.CeilToInt(gridCount.y / 16f);

            computeShader.SetInts("_GridCount", gridCount.x, gridCount.y);
            computeShader.SetInts("_GroupCount", threadGroupX, threadGroupY);

            sumBarycenterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, threadGroupX * threadGroupX, sizeof(float) * 3);

            initialAggregationKernel = computeShader.FindKernel("InitialAggregationKernel");
            computeShader.SetBuffer(initialAggregationKernel, "vertexBuffer", vertexBuffer);
            computeShader.SetBuffer(initialAggregationKernel, "sumBarycenterBuffer", sumBarycenterBuffer);

            finalAggregationKernel = computeShader.FindKernel("FinalAggregationKernel");
            computeShader.SetBuffer(finalAggregationKernel, "vertexBuffer", vertexBuffer);
            computeShader.SetBuffer(finalAggregationKernel, "sumBarycenterBuffer", sumBarycenterBuffer);

            return true;
        }

        private void OnDestroy()
        {
            sumBarycenterBuffer?.Dispose();
            sumBarycenterBuffer = null;

            initialized = false;
        }

#if UNITY_EDITOR
        [ContextMenu("Calculate Center of Mass")]
        private void _CalculateCenterOfMass()
        {
            var com = CalculateCenterOfMass();
            Debug.Log($"Center of Mass: {com}");

            var position = transform.TransformPoint(com);
            Debug.DrawLine(position, position + Vector3.up * 0.1f, Color.red, 1);
        }

        private void OnValidate()
        {
            const string COMPUTE_SHADER_DIR = "Assets/TurningPage/ComputeShaders/Helpers/";
            const string SHADER_FILENAME = COMPUTE_SHADER_DIR + "ComputeCenterOfMass.compute";

            if (computeShader == null)
            {
                var initializerShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(SHADER_FILENAME);
                computeShader = initializerShader;

                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif        
    } 
}