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
