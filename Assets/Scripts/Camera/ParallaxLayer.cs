using UnityEngine;

namespace HideAndInk.Parallax
{
    public sealed class ParallaxLayer : MonoBehaviour, IParallaxLayer
    {
        [Header("패럴랙스 속도 비율")]
        [SerializeField] private float speedRatio = 0.5f;

        [Header("무한 스크롤 (Texture Offset)")]
        [SerializeField] private bool useInfiniteScroll;
        [SerializeField] private float textureScaleX = 1f;

        [Header("DI - 패럴랙스 컨트롤러")]
        [SerializeField] private ParallaxController parallaxController;

        private Material _material;
        private Vector2 _textureOffset;

        public float SpeedRatio => speedRatio;

        private void OnEnable()
        {
            if (parallaxController != null)
                parallaxController.OnCameraMoved += ApplyOffset;
        }

        private void OnDisable()
        {
            if (parallaxController != null)
                parallaxController.OnCameraMoved -= ApplyOffset;
        }

        private void Start()
        {
            if (useInfiniteScroll)
            {
                SpriteRenderer renderer = GetComponent<SpriteRenderer>();
                if (renderer != null)
                    _material = renderer.material;
            }
        }

        public void ApplyOffset(Vector3 delta)
        {
            Vector3 offset = new Vector3(delta.x * speedRatio, delta.y * speedRatio, 0f);

            if (useInfiniteScroll && _material != null)
            {
                _textureOffset.x += (delta.x * speedRatio) * textureScaleX;
                _textureOffset.y += (delta.y * speedRatio) * textureScaleX;
                _material.mainTextureOffset = _textureOffset;
            }
            else
            {
                transform.position += offset;
            }
        }
    }
}
