#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    public class ConicalSurfaceParameterManager
    {
        [SerializeField] private ConicalSurfaceParameters unflippedPageParams =
            new ConicalSurfaceParameters(
                apex: -1f,
                angle: 90f,
                axisRoll: 180f
            );
        [SerializeField] private ConicalSurfaceParameters flippedPageParams = 
            new ConicalSurfaceParameters(
                apex: -1f,
                angle: 90f,
                axisRoll: 0f
            );
        [SerializeField] private ConicalSurfaceParameterOptimizer optimizer = default!;
        [SerializeField] private int stepCount = 5;

        private readonly float[] paramVelocities = new float[ConicalSurfaceParameters.PARAMETER_COUNT];

        public bool IsPinched { get; set; } = false;
        public Vector2 PinchPointUv { get; set; } = Vector2.zero;

        public Vector3 PinchPoint { get; set; }
        public Vector3 PinchRight { get; set; }
        public Vector3 PinchForward { get; set; }

        public ConicalSurfaceParameters LatestParameters { get; private set; } = 
            new ConicalSurfaceParameters(
                apex: -1f
            );

        private Vector2 MeshSize { get; set; } = Vector2.one;

        public void Initialize(Vector2 meshSize)
        {
            MeshSize = meshSize;

            LatestParameters = unflippedPageParams;
            for (int i = 0; i < paramVelocities.Length; ++i)
            {
                paramVelocities[i] = 0;
            }
        }

        public void ResetMeshCorners(bool isPageFlipped)
        {
            LatestParameters = isPageFlipped ? flippedPageParams : unflippedPageParams;
            for (int i = 0; i < paramVelocities.Length; ++i)
            {
                paramVelocities[i] = 0;
            }
        }

        public ConicalSurfaceParameters UpdateConicalSurfaceParameters(float totalTimeStap)
        {
            if (stepCount < 1)
                return LatestParameters;

            var timeStap = totalTimeStap / stepCount;

            if (IsPinched)
            {
                for (int i = 0; i < stepCount; i++)
                {
                    LatestParameters = UpdatePinchedParameters(timeStap);
                }
            }
            else
            {
                for (int i = 0; i < stepCount; i++)
                {
                    LatestParameters = UpdateUnpinchedParameters(timeStap);
                }
            }

            return LatestParameters;
        }

        private ConicalSurfaceParameters UpdatePinchedParameters(float timeStap)
        {
            return optimizer.UpdatePinchedPageParameters(
                LatestParameters,
                MeshSize,
                PinchPointUv,
                PinchPoint,
                Vector3.Cross(PinchRight, PinchForward).normalized,
                paramVelocities,
                timeStap
            );
        }

        private ConicalSurfaceParameters UpdateUnpinchedParameters(float timeStap)
        {
            var latestAxisRoll = LatestParameters.AxisRoll;

            var targetParams = (Mathf.Abs(latestAxisRoll) < 90)
                ? flippedPageParams
                : unflippedPageParams;

            return optimizer.UpdateUnpinchedPageParameters(
                LatestParameters,
                targetParams,
                MeshSize,
                paramVelocities,
                timeStap
            );
        }
    }
}