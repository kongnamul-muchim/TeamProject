using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndInk.UI
{
    public class SceneFadeIn : MonoBehaviour
    {
        [Tooltip("화면을 가리고 있는 검은색 UI 이미지 (알아서 투명해집니다)")]
        public Image fadeImage;
        public float fadeDuration = 1.5f;

        private void Awake()
        {
            // 씬이 로드되자마자 눈에 보이기 전(가장 첫 프레임)에 미리 화면을 까맣게 덮습니다.
            if (fadeImage != null)
            {
                fadeImage.gameObject.SetActive(true);
                fadeImage.color = new Color(0, 0, 0, 1);
            }
        }

        private void Start()
        {
            if (fadeImage != null)
            {
                StartCoroutine(FadeInRoutine());
            }
        }

        private IEnumerator FadeInRoutine()
        {
            // 유니티 에디터에서 ▶ 버튼을 누를 때 첫 프레임의 렉(DeltaTime 폭주) 때문에 
            // 1.5초가 순식간에 지나가버리는 현상을 방지하기 위해 한 프레임 대기합니다.
            yield return null; 

            fadeImage.gameObject.SetActive(true);
            fadeImage.color = new Color(0, 0, 0, 1); // 완전 까만색에서 시작
            
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }
            
            fadeImage.color = new Color(0, 0, 0, 0); // 완전 투명
            fadeImage.gameObject.SetActive(false); // 버튼 클릭을 방해하지 않게 꺼줌
        }
    }
}
