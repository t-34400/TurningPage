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
        }

        public void ResetMeshCorners(bool isPageFlipped)
        {
            LatestParameters = isPageFlipped ? flippedPageParams : unflippedPageParams;
        }

        public ConicalSurfaceParameters UpdateBezierCorrectedParaboloidParameters()
        {
            if (IsPinched)
            {
                LatestParameters = UpdatePinchedParameters();
            }
            else
            {
                LatestParameters = UpdateUnpinchedParameters();
            }

            return LatestParameters;
        }

        private ConicalSurfaceParameters UpdatePinchedParameters()
        {
            return optimizer.UpdatePinchedPageParameters(
                LatestParameters,
                MeshSize,
                PinchPointUv,
                PinchPoint,
                Vector3.Cross(PinchRight, PinchForward).normalized
            );
        }

        private ConicalSurfaceParameters UpdateUnpinchedParameters()
        {
            var latestAxisRoll = LatestParameters.AxisRoll;

            var targetParams = (Mathf.Abs(latestAxisRoll) < 90)
                ? flippedPageParams
                : unflippedPageParams;

            return optimizer.UpdateUnpinchedPageParameters(
                LatestParameters,
                targetParams,
                MeshSize
            );
        }
    }
}