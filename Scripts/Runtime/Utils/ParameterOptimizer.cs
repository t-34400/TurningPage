#nullable enable

using System;
using System.Linq;

namespace TurningPage
{
    public static class ParameterOptimizer
    {
        public static float[] OptimizeStep(
            float[] parameters, 
            Func<float[], float> lossFunc, 
            float diffStep, 
            float learningRate)
        {
            var grad = Gradient(parameters, lossFunc, diffStep);
            var newParameters = new float[parameters.Length];

            for (int i = 0; i < parameters.Length; ++i)
            {
                newParameters[i] = parameters[i] - learningRate * grad[i];
            }

            return newParameters;
        }

        public static float[] Gradient(
            float[] parameters, 
            Func<float[], float> lossFunc, 
            float diffStep)
        {
            var grad = new float[parameters.Length];

            for (int i = 0; i < parameters.Length; ++i)
            {
                var up = parameters.ToArray();
                up[i] += diffStep;

                var down = parameters.ToArray();
                down[i] -= diffStep;

                var lossUp = lossFunc(up);
                var lossDown = lossFunc(down);

                grad[i] = (lossUp - lossDown) / (2f * diffStep);
            }

            return grad;
        }
    }
}