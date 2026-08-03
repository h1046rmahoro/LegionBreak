using LegionBreak.Application.Waves;
using UnityEngine;
using VContainer;

namespace LegionBreak.Presentation.Spawning
{
    /// <summary>
    /// 실제 웨이브 시퀀스를 구동하는 진입점. MonsterSpawnTester(인터벌마다 무한 스폰하는
    /// 풀링 Before/After 측정용 하네스)와 달리 WaveData가 정의한 시간표대로 몬스터를 소환한다.
    /// 매 프레임 IWaveSpawnUseCase.Tick만 호출하는 얇은 어댑터 — 판정/스폰 로직은 전부
    /// Domain(WaveDirector)/Application(WaveSpawnUseCase)에 있다.
    /// </summary>
    public class WaveSpawnController : MonoBehaviour
    {
        private IWaveSpawnUseCase _waveSpawnUseCase;

        [Inject]
        public void Construct(IWaveSpawnUseCase waveSpawnUseCase)
        {
            _waveSpawnUseCase = waveSpawnUseCase;
        }

        private void Update()
        {
            _waveSpawnUseCase?.Tick(Time.deltaTime);
        }
    }
}
