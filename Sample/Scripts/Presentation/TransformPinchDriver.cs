#nullable enable

using UnityEngine;

namespace TurningPage.Sample.Presentation
{
    public class TransformPinchDriver : BasePinchDriver
    {
        [SerializeField] private float maxCurrentPageDistance = 0.20f;
        [SerializeField] private float maxPinchDistance = 0.04f;

        private void Update()
        {
            if (useCase == null)
                return;

            if (!useCase.IsPinched)
            {
                if (useCase.TryPinch(transform.position, maxCurrentPageDistance, maxPinchDistance))
                {
                    useCase.UpdatePinchData(transform.position, transform.right, transform.forward);
                }
            }
            else
            {
                useCase.UpdatePinchData(transform.position, transform.right, transform.forward);
            }
        }

        private void OnDisable()
        {
            useCase?.Release();
        }
    }
}
