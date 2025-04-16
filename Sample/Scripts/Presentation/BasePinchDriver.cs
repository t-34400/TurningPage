#nullable enable

using TurningPage.Sample.UseCase;
using UnityEngine;

namespace TurningPage.Sample.Presentation
{
    public abstract class BasePinchDriver : MonoBehaviour
    {
        protected PagePinchUseCase? useCase;

        public void SetUseCase(PagePinchUseCase useCase)
        {
            this.useCase = useCase;
        }
    }
}
