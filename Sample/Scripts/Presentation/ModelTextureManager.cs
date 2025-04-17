#nullable enable

using System;
using TurningPage.Sample.Domain;
using UnityEngine;

namespace TurningPage.Sample.Presentation
{
    public class ModelTextureManager : MonoBehaviour
    {
        [SerializeField] private TurningPageSimulation simulation = default!;
        [Header("Stacked Page (Optional)")]
        [SerializeField] private MeshRenderer previousPageMeshRenderer = default!;
        [SerializeField] private MeshRenderer nextPageMeshRenderer = default!;

        private TurningPageManager? pageManager;
        private PageTexture[] pageTextures = new PageTexture[0];
        
        public void Initialize(TurningPageManager manager, PageTexture[] textures)
        {
            pageManager = manager;
            pageTextures = textures;

            pageManager.CurrentPageChanged += OnPageChanged;
            OnPageChanged(pageManager.CurrentTurningPage);
        }

        private void OnPageChanged(int pageIndex)
        {
            if (TryGetPageTexture(pageIndex, out var pageTexture))
            {
                simulation.FrontMaterial.mainTexture = pageTexture.frontTexture;
                simulation.BackMaterial.mainTexture = pageTexture.backTexture;
            }

            SetPageTexture(previousPageMeshRenderer, 0, pageIndex - 1);
            SetPageTexture(nextPageMeshRenderer, pageIndex + 1, pageTextures.Length - 1);
        }

        private void SetPageTexture(MeshRenderer meshRenderer, int frontPageIndex, int backPageIndex)
        {
            if (TryGetPageTexture(frontPageIndex, out var frontPageTexture)
                && TryGetPageTexture(backPageIndex, out var backPageTexture))
            {
                meshRenderer.gameObject.SetActive(true);

                var materials = meshRenderer.sharedMaterials;

                if (materials.Length < 1)
                    return;

                materials[0].mainTexture = frontPageTexture.frontTexture;

                if (materials.Length < 2)
                    return;

                materials[1].mainTexture = backPageTexture.backTexture;
            }
            else
            {
                meshRenderer.gameObject.SetActive(false);
            }
        }

        private bool TryGetPageTexture(int pageIndex, out PageTexture pageTexture)
        {
            if (pageIndex < 0 || pageIndex >= pageTextures.Length)
            {
                pageTexture = default;
                return false;
            }

            pageTexture = pageTextures[pageIndex];
            return true;
        }
    }

    [Serializable]
    public struct PageTexture
    {
        public Texture frontTexture;
        public Texture backTexture;
    }
}