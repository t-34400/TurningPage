#nullable enable

using TurningPage.Sample.Domain;
using UnityEngine;

namespace TurningPage.Sample.UseCase
{
    public class PagePinchUseCase
    {
        private TurningPageManager pageManager;
        private IPagePinchController pagePinchController;

        public PagePinchUseCase(
            TurningPageManager pageManager,
            IPagePinchController pagePinchController)
        {
            this.pageManager = pageManager;
            this.pagePinchController = pagePinchController;
        }

        public bool IsPinched => pagePinchController.IsPinched;

        public void UpdatePageManager(TurningPageManager pageManager)
        {
            this.pageManager = pageManager;
        }

        public bool TryPinch(Vector3 pinchPoint, float maxPinchDistance)
        {
            if (pagePinchController.IsPinched)
                return false;

            var result = pagePinchController.TryPinch(pinchPoint, maxPinchDistance, pageManager.HasPreviousPage, pageManager.HasNextPage);
            
            switch (result)
            {
                case PinchResult.CurrentPage:
                    {
                        return true;
                    }
                case PinchResult.NextPage:
                    {
                        if (!pageManager.TryTurningNextPage())
                        {
                            pagePinchController.Release();
                            return true;
                        }

                        return false;
                    }
                case PinchResult.PreviousPage:
                    {
                        if (!pageManager.TryTurningPreviousPage())
                        {
                            pagePinchController.Release();
                            return true;
                        }

                        return false;
                    }
                default:
                    return false;
            }
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            if (!pagePinchController.IsPinched)
                return;

            pagePinchController.UpdatePinchData(pinchPoint, pinchRight, pinchForward);
        }

        public void Release()
        {
            if (!pagePinchController.IsPinched)
                return;

            pagePinchController.Release();
        }

        public void ResetAndInitialize() => pagePinchController.ResetAndInitialize();

        public PageSide GetCurrentPageSide() => pagePinchController.GetCurrentPageSide();
    }
}