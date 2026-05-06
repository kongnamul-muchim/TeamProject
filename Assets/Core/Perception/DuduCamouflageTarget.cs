using UnityEngine;

namespace HideAndInk.Siyeon1
{
    [DisallowMultipleComponent]
    public sealed class DuduCamouflageTarget : HideAndInk.Core.Perception.CamouflageTarget
    {
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Sprite camouflageSprite;
        [SerializeField] private Material camouflageMaterial;

        public Sprite CamouflageSprite => camouflageSprite != null ? camouflageSprite : sourceRenderer != null ? sourceRenderer.sprite : null;
        public Material CamouflageMaterial => camouflageMaterial != null ? camouflageMaterial : sourceRenderer != null ? sourceRenderer.sharedMaterial : null;

        private void Awake()
        {
            CacheRendererValues();
        }

        private void Reset()
        {
            CacheRendererValues();
        }

        private void OnValidate()
        {
            CacheRendererValues();
        }

        private void CacheRendererValues()
        {
            if (sourceRenderer == null)
            {
                sourceRenderer = GetComponent<SpriteRenderer>();
            }

            if (sourceRenderer == null)
            {
                return;
            }

            if (camouflageSprite == null)
            {
                camouflageSprite = sourceRenderer.sprite;
            }

            if (camouflageMaterial == null)
            {
                camouflageMaterial = sourceRenderer.sharedMaterial;
            }
        }
    }
}
