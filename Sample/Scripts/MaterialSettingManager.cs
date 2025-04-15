#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurningPage.Sample
{
    public class MaterialSettingManager : MonoBehaviour
    {
        [SerializeField] private TurningPageSimulation turningPageSimulation = default!;
        [SerializeField] private List<FloatMaterialSettings> floatMaterialSettings = default!;

        private void Start()
        {
            SetMaterialProperties();
        }

# if UNITY_EDITOR
        [ContextMenu("Set Material Properties")]
# endif
        private void SetMaterialProperties()
        {
            var frontMaterial = turningPageSimulation.FrontMaterial;
            var backMaterial = turningPageSimulation.BackMaterial;

            foreach (var settings in floatMaterialSettings)
            {
                frontMaterial.SetFloat(settings.propertyName, settings.frontMaterialPropertyValue);
                backMaterial.SetFloat(settings.propertyName, settings.backMaterialPropertyValue);
            }
        }


        [Serializable]
        class FloatMaterialSettings
        {
            public string propertyName = "";
            public float frontMaterialPropertyValue = 0;
            public float backMaterialPropertyValue = 0;
        }
    }
}