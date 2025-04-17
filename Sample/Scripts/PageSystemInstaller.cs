#nullable enable

using System;
using System.Linq;
using TurningPage.Sample.Domain;
using TurningPage.Sample.Presentation;
using TurningPage.Sample.UseCase;
using UnityEngine;

namespace TurningPage.Sample
{
    public class PageSystemInstaller : MonoBehaviour
    {
        [SerializeField] private TurningPageSimulation simulation = default!;
        [SerializeField] private ModelTextureManager modelTextureManager = default!;
        [SerializeField] private BasePinchDriver[] pinchDrivers = default!;
        [SerializeField] private PagePinchController pagePinchController = default!;
        [SerializeField] private bool fixLastPage = false;
        [SerializeField] private PageTexture[] pageTextures = default!;

        private TurningPageManager pageManager = new (1);
        private PagePinchUseCase? pagePinchUseCase;

        public int CurrentTurningPage => pageManager.CurrentTurningPage;
        public PageSide PageSide => pagePinchUseCase?.GetCurrentPageSide() ?? PageSide.Next;

        private IPagePinchController PinchController => pagePinchController;

        public event Action<int>? TurningPageUpdated;

        public bool TrySetPageTextures(PageTexture[] pageTextures)
        {
            if (pageTextures.Length <= 0)
                return false;

            this.pageTextures = pageTextures;
            var totalTurnablePages = pageTextures.Length;

            if (fixLastPage)
                --totalTurnablePages;

            if (pageManager != null)
            {
                pageManager.CurrentPageChanged -= InvokeTurningPageUpdated;
            }
            pageManager = new(totalTurnablePages);
            pageManager.CurrentPageChanged += InvokeTurningPageUpdated;

            pagePinchUseCase = new PagePinchUseCase(pageManager, PinchController);
            pagePinchUseCase.UpdatePageManager(pageManager);
            pagePinchUseCase.ResetAndInitialize();

            modelTextureManager.Initialize(pageManager, pageTextures);

            if (totalTurnablePages > 0)
            {
                foreach (var pinchDriver in pinchDrivers)
                {
                    pinchDriver.SetUseCase(pagePinchUseCase);
                }
            }
            else
            {
                foreach (var pinchDriver in pinchDrivers)
                {
                    pinchDriver.SetUseCase(null);
                }
            }

            InvokeTurningPageUpdated(0);

            Debug.Log($"Page Texture Set: Texture Count = {pageTextures.Length}, Turnable Pages: {totalTurnablePages}", this);

            return true;
        }

        private void InvokeTurningPageUpdated(int id) => TurningPageUpdated?.Invoke(id);

        private void Awake()
        {
            TrySetPageTextures(pageTextures);
        }
    }
}
