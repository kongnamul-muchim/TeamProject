using UnityEngine;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의태 가능한 오브젝트에 부착하는 컴포넌트
    /// Inspector에서 직접 대표 색상을 설정
    /// </summary>
    public class CamouflageTarget : MonoBehaviour
    {
        [Header("의태 색상")]
        [SerializeField] private Color camouflageColor = Color.white;

        /// <summary>
        /// 대표 색상 반환
        /// </summary>
        public Color GetCamouflageColor()
        {
            return camouflageColor;
        }

        /// <summary>
        /// 색상 설정
        /// </summary>
        public void SetCamouflageColor(Color color)
        {
            camouflageColor = color;
        }
    }
}
