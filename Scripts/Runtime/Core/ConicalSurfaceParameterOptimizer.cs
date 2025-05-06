#nullable enable

using System;
using UnityEngine;

namespace TurningPage
{
    [Serializable]
    public class ConicalSurfaceParameterOptimizer
    {
        private const float APEX_SATURATION_EXTENT = 2f;

        [SerializeField] private float learningRate = 0.01f;
        [SerializeField] private float diffStep = 1e-3f;
        [Min(1e-3f)]
        [SerializeField] private float bendCompliance = 4f;
        [Min(1e-3f)]
        [SerializeField] private float bendCenterCompliance = 10f;
        [Header("Pinched page parameters")]
        [SerializeField] private float positionWeight = 1f;
        [SerializeField] private float normalWeight = 0.2f;
        [Header("Unpinched page parameters")]
        [SerializeField] private float apexWeight = 0.4f;
        [SerializeField] private float angleWeight = 0.3f;
        [SerializeField] private float axisRollWeight = 0.3f;

        public ConicalSurfaceParameters UpdatePinchedPageParameters(
            ConicalSurfaceParameters parameters,
            Vector2 meshSize,
            Vector2 uv,
            Vector3 pinchPoint,
            Vector3 pinchNormal)
        {
            var apexPositiveLimit = meshSize.x;

            float CalculateLoss(float[] paramsArray)
            {
                if (paramsArray.Length < 3)
                {
                    Debug.LogError("Invalid parameters length.");
                    return 1e5f;
                }

                var newParameters = ConvertOptimzedValueToParams(paramsArray, apexPositiveLimit, bendCompliance, bendCenterCompliance);

                var (position, normal) = CalculatePositionAndNormal(newParameters, meshSize, uv);

                var sqrDistance = (position - pinchPoint).sqrMagnitude;
                var normalAngle = Vector3.Angle(normal, pinchNormal) * Mathf.Deg2Rad;

                var loss = positionWeight * sqrDistance + normalWeight * normalAngle;

                return loss;
            }

            var updated = UpdateParameters(
                parameters,
                CalculateLoss,
                apexPositiveLimit,
                bendCompliance,
                bendCenterCompliance,
                diffStep,
                learningRate
            );

            if (float.IsNaN(updated.Apex) || float.IsInfinity(updated.Apex)
                || float.IsNaN(updated.Angle) || float.IsInfinity(updated.Angle)
                || float.IsNaN(updated.AxisRoll) || float.IsInfinity(updated.AxisRoll))
            {
                Debug.LogWarning("Invalid updated parameters.");
                return parameters;
            }

            return updated;
        }

        public ConicalSurfaceParameters UpdateUnpinchedPageParameters(
            ConicalSurfaceParameters parameters,
            ConicalSurfaceParameters targetParameters,
            Vector2 meshSize)
        {
            if (parameters.ApploximatelyEqual(targetParameters))
                return targetParameters;

            var apexPositiveLimit = meshSize.x;

            var targetValues = ConvertParamsToOptimizedValue(targetParameters, apexPositiveLimit, bendCompliance, bendCenterCompliance);

            float CalculateLoss(float[] paramsArray)
            {
                if (paramsArray.Length < 3 || targetValues.Length < 3)
                {
                    Debug.LogError("Invalid parameters length.");
                    return 1e5f;
                }

                var newParameters = ConvertOptimzedValueToParams(paramsArray, apexPositiveLimit, bendCompliance, bendCenterCompliance);

                var angleDiff = Mathf.DeltaAngle(newParameters.Angle, targetParameters.Angle);
                var axisRollDiff = Mathf.DeltaAngle(newParameters.AxisRoll, targetParameters.AxisRoll);

                var loss = apexWeight * Mathf.Abs(paramsArray[0] - targetValues[0])
                    + angleWeight * Mathf.Abs(angleDiff) * Mathf.Deg2Rad
                    + axisRollWeight * Mathf.Abs(axisRollDiff) * Mathf.Deg2Rad;

                return loss;
            }

            var updated = UpdateParameters(
                parameters,
                CalculateLoss,
                apexPositiveLimit,
                bendCompliance,
                bendCenterCompliance,
                diffStep,
                learningRate
            );

            if (float.IsNaN(updated.Apex) || float.IsInfinity(updated.Apex)
                || float.IsNaN(updated.Angle) || float.IsInfinity(updated.Angle)
                || float.IsNaN(updated.AxisRoll) || float.IsInfinity(updated.AxisRoll))
            {
                Debug.LogWarning("Invalid updated parameters.");
                return parameters;
            }

            return updated;
        }

        private static (Vector3 position, Vector3 normal) CalculatePositionAndNormal(
            ConicalSurfaceParameters parameters,
            Vector2 meshSize,
            Vector2 uv)
        {
            var polarX = uv.x * meshSize.x - parameters.Apex;
            var polarY = uv.y * meshSize.y;

            var r = Mathf.Sqrt(polarX * polarX + polarY * polarY);
            var theta = Mathf.Atan2(polarY, Mathf.Abs(polarX));

            var angleRad = parameters.Angle * Mathf.Deg2Rad;
            var angleAroundAxis = theta / Mathf.Sin(angleRad);
            var axis = parameters.Axis;

            var rotation = Quaternion.AngleAxis(angleAroundAxis * Mathf.Rad2Deg, axis);

            var pointOnCone = rotation * Vector3.right * r;
            pointOnCone.x *= Mathf.Sign(polarX);

            var position = pointOnCone + Vector3.right * parameters.Apex;

            var tangent = Vector3.Cross(axis, pointOnCone).normalized;
            var normal = Vector3.Cross(pointOnCone, tangent).normalized;

            return (position, normal);
        }
        
        public static ConicalSurfaceParameters UpdateParameters(
            ConicalSurfaceParameters parameters,
            Func<float[], float> lossFunc,
            float apexPositiveLimit,
            float bendCompliance,
            float bendCenterCompliance,
            float diffStep,
            float learningRate)
        {
            var initialParameters = ConvertParamsToOptimizedValue(parameters, apexPositiveLimit, bendCompliance, bendCenterCompliance);

            var optimized = ParameterOptimizer.OptimizeStep(
                initialParameters,
                lossFunc,
                diffStep,
                learningRate
            );

            if (optimized.Length < 3)
            {
                Debug.LogError("Invalid updated parameters length.");
                return parameters;
            }

            for (int i = 0; i < optimized.Length; ++i)
            {
                if (float.IsNaN(optimized[i]) || float.IsInfinity(optimized[i]))
                {
                    Debug.LogWarning($"Invalid updated parameter {i}: {optimized[i]}.");
                    return parameters;
                }
            }

            return ConvertOptimzedValueToParams(optimized, apexPositiveLimit, bendCompliance, bendCenterCompliance);
        }

        private static float[] ConvertParamsToOptimizedValue(
            ConicalSurfaceParameters parameters, 
            float apexPositiveLimit,
            float bendCompliance,
            float bendCenterCompliance)
        {
            var apex = InverseMapFromSignedSaturatedRange(
                parameters.Apex,
                -0.1f,
                apexPositiveLimit + 0.1f,
                APEX_SATURATION_EXTENT
            ) / bendCenterCompliance;
            var angle = ATanh((parameters.Angle - 30) / 60f - 1f) / bendCompliance;
            var axisRoll = parameters.AxisRoll / 180f;

            return new float[] { apex, angle, axisRoll };
        }

        private static ConicalSurfaceParameters ConvertOptimzedValueToParams(
            float[] optimizedValues, 
            float apexPositiveLimit,
            float bendCompliance,
            float bendCenterCompliance)
        {
            if (optimizedValues.Length < 3)
            {
                Debug.LogError("Invalid parameters length.");
                return new ConicalSurfaceParameters(-1f);
            }

            var apex = MapToSignedSaturatedRange(
                optimizedValues[0] * bendCenterCompliance,
                -0.1f,
                apexPositiveLimit + 0.1f,
                APEX_SATURATION_EXTENT
            );
            var angle = (1 + Tanh(optimizedValues[1] * bendCompliance)) * 60f + 30f;
            var axisRoll = optimizedValues[2] * 180f;

            return new ConicalSurfaceParameters(apex, angle, axisRoll);
        }

        private static float MapToSignedSaturatedRange(float value, float negativeMax, float positiveMin, float halfRange)
        {
            var tanh = Tanh(value);

            if (tanh >= 0)
                return positiveMin + (1 - tanh) * halfRange;
            else
                return negativeMax - (1 + tanh) * halfRange;
        }

        private static float InverseMapFromSignedSaturatedRange(float mappedValue, float negativeMax, float positiveMin, float halfRange)
        {
            float tanh;

            if (mappedValue >= positiveMin)
                tanh = 1 - (mappedValue - positiveMin) / halfRange;
            else
                tanh = (negativeMax - mappedValue) / halfRange - 1;

            return ATanh(tanh);
        }

        private static float Tanh(float value)
        {
            var exp2 = Mathf.Exp(2f * value);
            return (exp2 - 1f) / (exp2 + 1f);
        }

        private static float ATanh(float value)
        {
            value = Mathf.Clamp(value, -0.999f, 0.999f);
            return 0.5f * Mathf.Log((1f + value) / (1f - value));
        }
    }

    [Serializable]
    public class ConicalSurfaceParameters
    {
        [SerializeField] private float apex = -1f;
        [Range(30f, 150f)]
        [SerializeField] private float angle = 90f;
        [Range(-180f, 180f)]
        [SerializeField] private float axisRoll = 0f;

        public float Apex => apex;
        public float Angle => angle;
        public float AxisRoll => axisRoll;

        public Vector3 Axis
        {
            get
            {
                var angleRad = Angle * Mathf.Deg2Rad;

                var axisYaw = new Vector3(
                    Mathf.Cos(angleRad),
                    Mathf.Sin(angleRad),
                    0
                );
                var axis = Quaternion.Euler(AxisRoll, 0, 0) * axisYaw;
                
                return axis.normalized;
            }
        }

        public ConicalSurfaceParameters(float apex, float angle = 90f, float axisRoll = 0f)
        {
            this.apex = apex;
            this.angle = angle;

            while (axisRoll < -180f)
                axisRoll += 360f;
            while (axisRoll > 180f)
                axisRoll -= 360f;

            this.axisRoll = axisRoll;
        }

        public bool ApploximatelyEqual(ConicalSurfaceParameters others)
        {
            if (others == this)
                return true;

            return Mathf.Abs(Apex - others.Apex) < 0.05f
                && Mathf.Abs(Mathf.DeltaAngle(Angle, others.Angle)) < 1f
                && Mathf.Abs(Mathf.DeltaAngle(AxisRoll, others.AxisRoll)) < 1f;
        }
    }
}