using UnityEngine;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 가능한 오브젝트에 부착하는 컴포넌트
    /// CamouflageColorData를 참조하여 대표 색상 제공
    /// </summary>
    public class CamouflageTarget : MonoBehaviour
    {
        [Header("의태 색상 데이터")]
        [SerializeField] private CamouflageColorData colorData;

        /// <summary>
        /// 대표 색상 반환 (colorData가 없으면 흰색)
        /// </summary>
        public Color GetCamouflageColor()
        {
            if (colorData != null)
            {
                return colorData.TargetColor;
            }
            return Color.white;
        }

        /// <summary>
        /// 색상 데이터 설정
        /// </summary>
        public void SetColorData(CamouflageColorData data)
        {
            colorData = data;
        }
    }
}
