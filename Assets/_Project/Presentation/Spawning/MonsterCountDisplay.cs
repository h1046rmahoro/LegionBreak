using LegionBreak.Application.Spawning;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace LegionBreak.Presentation.Spawning
{
    /// <summary>
    /// 프로파일링 측정용 디버그 HUD. 동시 몬스터 수를 화면에 표시해
    /// Before/After 측정 시 정확한 개체 수 구간(200~500마리)을 눈으로 확인할 수 있게 한다.
    /// </summary>
    public class MonsterCountDisplay : MonoBehaviour
    {
        [SerializeField] private Text _countText;
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;

        private IMonsterSpawner _spawner;
        private int _lastDisplayedCount = -1;
        private float _elapsed;

        [Inject]
        public void Construct(IMonsterSpawner spawner)
        {
            _spawner = spawner;
        }

        private void Update()
        {
            if (_spawner == null || _countText == null)
            {
                return;
            }

            // 스폰 스트레스 테스트(간격 0.02s)에서는 개체 수가 초당 수십 번 바뀌어
            // "값이 바뀔 때만 갱신"만으로는 할당 빈도가 충분히 줄지 않는다. 갱신 주기를
            // 던져서 이 디버그 HUD 자신이 측정 대상인 GC Alloc 0B/frame 목표를
            // 오염시키는 정도를 최소화한다. 실제 개체 수 확인용이라 초 단위 지연은 무해하다.
            _elapsed += Time.deltaTime;
            if (_elapsed < _refreshIntervalSeconds)
            {
                return;
            }

            _elapsed = 0f;

            var count = _spawner.ActiveCount;
            if (count == _lastDisplayedCount)
            {
                return;
            }

            _lastDisplayedCount = count;
            _countText.text = $"Monsters: {count}";
        }
    }
}
