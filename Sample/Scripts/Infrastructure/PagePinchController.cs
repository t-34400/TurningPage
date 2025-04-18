#nullable enable

using System;
using TurningPage.MeshSync;
using TurningPage.Sample.Domain;
using UnityEngine;

namespace TurningPage.Sample
{
    [Serializable]
    public class PagePinchController : IPagePinchController
    {
        [SerializeField] private TurningPageSimulation simulation = default!;
        [SerializeField] private GpuMeshReader meshReader = default!;

        public bool IsPinched { get; private set; }

        public PinchResult TryPinch(Vector3 pinchPoint, float maxPinchDistance, bool hasPreviousPage, bool hasNextPage)
        {
            if (IsPinched)
                return PinchResult.None;

            if (TryPinchNearestVertex(pinchPoint, maxPinchDistance))
                return PinchResult.CurrentPage;

            if (hasNextPage)
            {
                var result = TryPinchNextPageVertex(pinchPoint, maxPinchDistance);

                if (result != null)
                    return result.Value;
            }

            if (hasPreviousPage)
            {
                var result = TryPinchPreviousPageVertex(pinchPoint, maxPinchDistance);

                if (result != null)
                    return result.Value;
            }

            return PinchResult.None;
        }

        public void UpdatePinchData(Vector3 pinchPoint, Vector3 pinchRight, Vector3 pinchForward)
        {
            simulation.UpdatePinchData(pinchPoint, pinchRight, pinchForward);
        }

        public void Release()
        {
            if (!IsPinched)
                return;

            IsPinched = false;
            simulation.Release();
        }

        public PageSide ResetAndInitialize()
        {
            var pageSide = GetCurrentPageSide();

            Release();
            simulation.InitializePage(false);

            return pageSide;
        }

        public PageSide GetCurrentPageSide()
        {
            var meshCenter = meshReader.Mesh.bounds.center;

            return meshCenter.z < 0 ? PageSide.Previous : PageSide.Next;
        }

        private bool TryPinchNearestVertex(Vector3 pinchPoint, float maxPinchDistance)
        {
            if (simulation.TrySearchNearestVertex(pinchPoint, out var result)
                && result.Distance < maxPinchDistance
                && result.VertexId.y > 0)
            {
                simulation.SetPinchPoint(result.VertexId);

                IsPinched = true;
                return true;
            }

            return false;
        }

        private PinchResult? TryPinchNextPageVertex(Vector3 pinchPoint, float maxPinchDistance)
        {
            var result = simulation.SearchNearestNextPageVertex(pinchPoint);

            if (result.Distance < maxPinchDistance
                && result.VertexId.y > 0)
            {
                simulation.SetPinchPoint(result.VertexId);
                IsPinched = true;

                if (GetCurrentPageSide() == PageSide.Previous)
                {
                    simulation.InitializePage(false);
                    return PinchResult.NextPage;
                }
                else
                {
                    return PinchResult.CurrentPage;
                }
            }

            return null;
        }

        private PinchResult? TryPinchPreviousPageVertex(Vector3 pinchPoint, float maxPinchDistance)
        {
            var result = simulation.SearchNearestPreviousPageVertex(pinchPoint);

            if (result.Distance < maxPinchDistance
                && result.VertexId.y > 0)
            {
                simulation.SetPinchPoint(result.VertexId);
                IsPinched = true;

                if (GetCurrentPageSide() == PageSide.Previous)
                {
                    return PinchResult.CurrentPage;
                }
                else
                {
                    simulation.InitializePage(true);
                    return PinchResult.PreviousPage;
                }
            }

            return null;
        }
    }
}