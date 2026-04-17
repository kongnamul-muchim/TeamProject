using UnityEngine;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 타겟의 대표 색상을 저장하는 ScriptableObject
    /// 각 Sprite/오브젝트별로 대표 색상을 미리 설정하여 런타임 오버헤드 없이 색상 참조
    /// </summary>
    [CreateAssetMenu(fileName = "CamouflageColorData", menuName = "HideAndInk/CamouflageColorData")]
    public class CamouflageColorData : ScriptableObject
    {
        [Header("대표 색상")]
        [SerializeField] private Color targetColor = Color.white;

        [Header("디버그")]
        [SerializeField] private string description;

        public Color TargetColor => targetColor;
        public string Description => description;
    }
}
