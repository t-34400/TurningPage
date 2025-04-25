#nullable enable

using System;
using System.Linq;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    public class BezierCorrectedParaboloidCornerCalculator
    {
        [SerializeField] private float maxCornerVelocity = 5f;
        [SerializeField] private float maxHeightVelocity = 10f;

        [SerializeField] private float unpinchedCornerVelocity = 1f;

        public bool IsPinched { get; set; } = false;
        public Vector2 PinchPointUv { get; set; } = Vector2.zero;

        public Vector3 PinchPoint { get; set; }
        public Vector3 PinchRight { get; set; }
        public Vector3 PinchForward { get; set; }

        private BezierCorrectedParaboloidParameters LatestParameters { get; set; } = new (Vector3.zero, Vector3.right);

        private Vector2 MeshSize { get; set; } = Vector2.one;

        public void Initialize(Vector2 meshSize)
        {
            MeshSize = meshSize;

            LatestParameters = new BezierCorrectedParaboloidParameters(
                new Vector3(0, 0, meshSize.y),
                new Vector3(meshSize.x, 0, meshSize.y)
            );
        }

        public void ResetMeshCorners(bool isPageFlipped)
        {
            var z = isPageFlipped ? -MeshSize.y : MeshSize.y;

            LatestParameters = new BezierCorrectedParaboloidParameters(
                new Vector3(0, 0, z),
                new Vector3(MeshSize.x, 0, z)
            );
        }

        public BezierCorrectedParaboloidParameters UpdateBezierCorrectedParaboloidParameters()
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

        private BezierCorrectedParaboloidParameters UpdatePinchedParameters()
        {
            // TODO
            // var targetCorner1 = PinchPoint + PinchRight * (-MeshSize.x * PinchPointUv.x) + PinchForward * (MeshSize.y * (1 - PinchPointUv.y));
            // var targetCorner2 = PinchPoint + PinchRight * (MeshSize.x * (1 - PinchPointUv.x)) + PinchForward * (MeshSize.y * (1 - PinchPointUv.y));
            var targetCorner1 = PinchPoint + Vector3.right * (-MeshSize.x * PinchPointUv.x) + PinchForward * (MeshSize.y * (1 - PinchPointUv.y));
            var targetCorner2 = PinchPoint + Vector3.right * (MeshSize.x * (1 - PinchPointUv.x)) + PinchForward * (MeshSize.y * (1 - PinchPointUv.y));

            if (!ConstrainToSeamEnd(targetCorner1, targetCorner2, out targetCorner1, out targetCorner2))
            {
                Debug.LogWarning($"Failed to constrain to seam ends: {targetCorner1}, {targetCorner2}");
                return LatestParameters;
            }

            var maxCornerDelta = maxCornerVelocity * Time.fixedDeltaTime;

            var latestCorner1 = LatestParameters.CornerPoint1;
            var latestCorner2 = LatestParameters.CornerPoint2;

            targetCorner1 = ConstrainDistance(targetCorner1, latestCorner1, maxCornerDelta, out var _);
            targetCorner2 = ConstrainDistance(targetCorner2, latestCorner2, maxCornerDelta, out var _);

            var seamEnd1 = Vector3.zero;
            var seamEnd2 = Vector3.right * MeshSize.x;

            var seamEndDistance1 = Vector3.Distance(seamEnd1, targetCorner1);
            var seamEndDistance2 = Vector3.Distance(seamEnd2, targetCorner2);

            var bezierHeight1 = ComputeBezierHeight(MeshSize.y, seamEndDistance1);
            var bezierHeight2 = ComputeBezierHeight(MeshSize.y, seamEndDistance2);

            var maxHeightDelta = maxHeightVelocity * Time.fixedDeltaTime;

            var heightDelta1 = bezierHeight1 - LatestParameters.BezierHeight1;
            var heightDelta2 = bezierHeight2 - LatestParameters.BezierHeight2;

            if (Mathf.Abs(heightDelta1) > maxHeightDelta)
            {
                bezierHeight1 = LatestParameters.BezierHeight1 + Mathf.Sign(heightDelta1) * maxHeightDelta;
            }
            if (Mathf.Abs(heightDelta2) > maxHeightDelta)
            {
                bezierHeight2 = LatestParameters.BezierHeight2 + Mathf.Sign(heightDelta2) * maxHeightDelta;
            }

            return new BezierCorrectedParaboloidParameters(
                targetCorner1,
                targetCorner2,
                bezierHeight1,
                bezierHeight2);
        }

        private BezierCorrectedParaboloidParameters UpdateUnpinchedParameters()
        {
            var latestCorner1 = LatestParameters.CornerPoint1;
            var latestCorner2 = LatestParameters.CornerPoint2;

            var unflippedStackedCorner1 = new Vector3(0, 0, MeshSize.y);
            var unflippedStackedCorner2 = new Vector3(MeshSize.x, 0, MeshSize.y);

            var flippedStackedCorner1 = new Vector3(0, 0, -MeshSize.y);
            var flippedStackedCorner2 = new Vector3(MeshSize.x, 0, -MeshSize.y);

            var isPageFlipped = 
                Vector3.Distance(latestCorner1, flippedStackedCorner1) + Vector3.Distance(latestCorner2, flippedStackedCorner2)
                    < Vector3.Distance(latestCorner1, unflippedStackedCorner1) + Vector3.Distance(latestCorner2, unflippedStackedCorner2);

            var targetStackedCorner1 = isPageFlipped ? flippedStackedCorner1 : unflippedStackedCorner1;
            var targetStackedCorner2 = isPageFlipped ? flippedStackedCorner2 : unflippedStackedCorner2;

            var unpinchedCornerDelta = unpinchedCornerVelocity * Time.fixedDeltaTime;
            
            var corner1 = ConstrainDistance(targetStackedCorner1, latestCorner1, unpinchedCornerDelta, out var progressRatio1);
            var corner2 = ConstrainDistance(targetStackedCorner2, latestCorner2, unpinchedCornerDelta, out var progressRatio2);

            var height1 = LatestParameters.BezierHeight1 * (1 - progressRatio1);
            var height2 = LatestParameters.BezierHeight2 * (1 - progressRatio2);

            return new BezierCorrectedParaboloidParameters(
                corner1,
                corner2,
                height1,
                height2);
        }

        private float ComputeBezierHeight(float targetLength, float distance)
        {
            if (distance > targetLength)
                return 0;

            Vector2 Bezier(float t, Vector2 a, Vector2 b, Vector2 p)
            {
                return (1 - t) * (1 - t) * a + 2 * (1 - t) * t * p + t * t * b;
            }

            float ComputeBezierLength(float height)
            {
                const int SAMPLING_POINT = 16;

                var points = Enumerable.Range(0, SAMPLING_POINT)
                    .Select(i => (float) i / (SAMPLING_POINT - 1))
                    .Select(t => 
                        Bezier(
                            t, 
                            Vector2.zero, 
                            Vector2.right * distance, 
                            0.5f * Vector2.right * distance + Vector2.down * height));
                var length = 
                    points.Skip(1)
                    .Zip(points, (a, b) => Vector2.Distance(a, b))
                    .Sum();

                return length;
            }

            return SolveRoot(
                t => ComputeBezierLength(t) - targetLength,
                0, MeshSize.y);
        }

        bool ConstrainToSeamEnd(Vector3 corner1, Vector3 corner2, out Vector3 constrainedCorner1, out Vector3 constrainedCorner2)
        {
            const int MAX_ITER = 20;
            const float OFFSET = 0.01f;

            constrainedCorner1 = corner1;
            constrainedCorner2 = corner2;

            var seamEnd1 = Vector3.zero;
            var seamEnd2 = Vector3.right * MeshSize.x;

            var currentDistance1 = Vector3.Distance(seamEnd1, constrainedCorner1);
            var currentDistance2 = Vector3.Distance(seamEnd2, constrainedCorner2);

            if (currentDistance1 <= MeshSize.y + OFFSET && currentDistance2 <= MeshSize.y + OFFSET)
                return true;

            for (var i = 0; i < MAX_ITER; ++i)
            {
                constrainedCorner1 = ConstrainDistance(constrainedCorner1, seamEnd1, MeshSize.y, out var _);

                var delta = constrainedCorner2 - constrainedCorner1;
                var deltaMagnitude = delta.magnitude;

                constrainedCorner1 += delta.normalized * (MeshSize.x - deltaMagnitude) / 2;
                constrainedCorner2 -= delta.normalized * (MeshSize.x - deltaMagnitude) / 2;

                constrainedCorner2 = ConstrainDistance(constrainedCorner2, seamEnd2, MeshSize.y, out var _);

                delta = constrainedCorner2 - constrainedCorner1;
                deltaMagnitude = delta.magnitude;

                constrainedCorner1 += delta.normalized * (MeshSize.x - deltaMagnitude) / 2;
                constrainedCorner2 -= delta.normalized * (MeshSize.x - deltaMagnitude) / 2;

                currentDistance1 = Vector3.Distance(seamEnd1, constrainedCorner1);
                currentDistance2 = Vector3.Distance(seamEnd2, constrainedCorner2);

                if (currentDistance1 <= MeshSize.y + OFFSET && currentDistance2 <= MeshSize.y + OFFSET)
                    return true;
            }

            return false;
        }

        private static float SolveRoot(Func<float, float> func, float min, float max)
        {
            const int MAX_ITER = 30;
            const float EPSILON = 1e-4f;

            var fMin = func(min);
            if (Mathf.Abs(fMin) < EPSILON)
                return min;

            for (var i = 0; i < MAX_ITER; ++i)
            {
                var mid = (min + max) / 2;
                var fMid = func(mid);

                if (Math.Abs(fMid) < EPSILON)
                    return mid;

                if (fMin * fMid < 0)
                    max = mid;
                else
                {
                    min = mid;
                    fMin = fMid;
                }
            }

            return (min + max) / 2;
        }

        private static Vector3 ConstrainDistance(Vector3 value, Vector3 target, float maxDistance, out float progressRatio)
        {
            var delta = value - target;
            var deltaMagnitude = delta.magnitude;

            if (deltaMagnitude > maxDistance)
            {
                progressRatio = maxDistance / deltaMagnitude;
                return target + delta.normalized * maxDistance;
            }

            progressRatio = 1;
            return value;
        }
    }

    public class BezierCorrectedParaboloidParameters
    {
        public Vector3 CornerPoint1 { get; }
        public Vector3 CornerPoint2 { get; }

        public float BezierHeight1 { get; }
        public float BezierHeight2 { get; }

        public BezierCorrectedParaboloidParameters(Vector3 cornerPoint1, Vector3 cornerPoint2, float bezierHeight1 = 0, float bezierHeight2 = 0)
        {
            CornerPoint1 = cornerPoint1;
            CornerPoint2 = cornerPoint2;
            BezierHeight1 = bezierHeight1;
            BezierHeight2 = bezierHeight2;
        }
    }
}