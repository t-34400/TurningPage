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

        public PinchResult TryPinch(Vector3 pinchPoint, float maxPinchDistance)
        {
            if (IsPinched)
                return PinchResult.None;

            if (simulation.TrySearchNearestVertex(pinchPoint, out var result)
                && result.Distance < maxPinchDistance
                && result.VertexId.y > 0)
            {
                simulation.SetPinchPoint(result.VertexId);

                IsPinched = true;
                return PinchResult.CurrentPage;
            }

            result = simulation.SearchNearestNextPageVertex(pinchPoint);

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

            result = simulation.SearchNearestPreviousPageVertex(pinchPoint);

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
    }


}