#nullable enable

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

        private IPagePinchController PinchController => pagePinchController;

        public bool TrySetPageTextures(PageTexture[] pageTextures)
        {
            if (pageTextures.Length <= 0)
                return false;

            this.pageTextures = pageTextures;

            var totalTurnablePages = pageTextures.Length;

            if (fixLastPage)
                --totalTurnablePages;

            pageManager = new(totalTurnablePages);

            pagePinchUseCase ??= new PagePinchUseCase(pageManager, PinchController);
            pagePinchUseCase.UpdatePageManager(pageManager);

            modelTextureManager.Initialize(pageManager, pageTextures);
            foreach (var pinchDriver in pinchDrivers)
            {
                pinchDriver.SetUseCase(pagePinchUseCase);
            }

            return true;
        }

        private void Awake()
        {
            TrySetPageTextures(pageTextures);
        }
    }
}
