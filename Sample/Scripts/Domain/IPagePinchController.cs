#nullable enable

using UnityEngine;

namespace TurningPage.Sample.Domain
{
    public interface IPagePinchController
    {
        bool IsPinched { get; }

        PinchResult TryPinch(Vector3 pinchPoint, float maxPinchDistance);
        void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward);
        void Release();
        PageSide ResetAndInitialize();
        PageSide GetCurrentPageSide();
    }

    public enum PageSide
    {
        Previous,
        Next
    }

    public enum PinchResult
    {
        None,
        CurrentPage,
        PreviousPage,
        NextPage,
    }
}