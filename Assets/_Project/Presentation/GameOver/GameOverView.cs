using LegionBreak.Application.Player;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace LegionBreak.Presentation.GameOver
{
    /// <summary>
    /// 플레이어 사망 시 "GAME OVER" 텍스트만 노출한다. MonsterCountView와 달리 Presenter를
    /// 두지 않았다 — IsDead는 생존→사망으로만 전이되는 1회성 상태라 스로틀링이나 변경 감지처럼
    /// 테스트할 가치가 있는 로직이 없다(MVP 분리가 여기선 오버엔지니어링이 된다).
    /// 이 컴포넌트가 붙은 GameObject 자체는 항상 활성 상태여야 한다 — VContainer의
    /// RegisterComponentInHierarchy는 씬 로드 시점에 이미 비활성인 GameObject를 스캔 대상에서
    /// 제외해 Construct가 호출되지 않고, 그러면 Update()도 애초에 돌지 않아 스스로를 다시
    /// 켤 방법이 없어진다(닭-달걀 문제). 그래서 표시/숨김은 gameObject.SetActive가 아니라
    /// Text(Graphic)의 enabled만 토글한다 — 렌더링만 꺼질 뿐 스크립트는 계속 살아있다.
    /// </summary>
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private Text _gameOverText;

        private IPlayerHealth _playerHealth;
        private bool _shown;

        [Inject]
        public void Construct(IPlayerHealth playerHealth)
        {
            _playerHealth = playerHealth;
        }

        private void Awake()
        {
            if (_gameOverText != null)
            {
                _gameOverText.enabled = false;
            }
        }

        private void Update()
        {
            if (_shown || _playerHealth == null || !_playerHealth.IsDead)
            {
                return;
            }

            _shown = true;
            if (_gameOverText != null)
            {
                _gameOverText.enabled = true;
            }
        }
    }
}
