using LegionBreak.Application.Spawning;
using UnityEngine;
using VContainer;

namespace LegionBreak.Presentation.Spawning
{
    /// <summary>
    /// 오브젝트 풀링 Before/After 측정을 위한 테스트 하네스.
    /// 실제 웨이브/스폰 기획이 아니라 인터벌마다 랜덤 위치에 더미 몬스터를 요청하는
    /// 프로파일링 검증용 스크립트다.
    /// </summary>
    public class MonsterSpawnTester : MonoBehaviour
    {
        [SerializeField] private float _spawnIntervalSeconds = 0.1f;
        [SerializeField] private float _spawnRadius = 20f;
        [SerializeField] private int _targetCount = 500;

        private IMonsterSpawner _spawner;
        private float _elapsed;

        [Inject]
        public void Construct(IMonsterSpawner spawner)
        {
            _spawner = spawner;
        }

        private void Update()
        {
            if (_spawner == null)
            {
                return;
            }

            // 5주차에 몬스터 수명 타이머가 제거되어 디스폰 수단이 없어졌다 — 목표 개체 수에
            // 도달하면 스스로 멈춰서, 정상 상태(Steady State) 프로파일링 캡처 때마다 수동으로
            // 꺼야 했던 절차를 없앤다.
            if (_spawner.ActiveCount >= _targetCount)
            {
                enabled = false;
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed < _spawnIntervalSeconds)
            {
                return;
            }

            _elapsed = 0f;

            var angle = Random.Range(0f, Mathf.PI * 2f);
            var distance = Random.Range(0f, _spawnRadius);
            var position = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
            _spawner.Spawn(position);
        }
    }
}
