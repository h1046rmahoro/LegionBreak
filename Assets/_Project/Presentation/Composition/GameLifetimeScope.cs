using LegionBreak.Application.Events;
using LegionBreak.Application.Movement;
using LegionBreak.Application.Skills;
using LegionBreak.Application.Spawning;
using LegionBreak.Application.Waves;
using LegionBreak.Data;
using LegionBreak.Domain.Skills;
using LegionBreak.Domain.Waves;
using LegionBreak.Infrastructure.Movement;
using LegionBreak.Infrastructure.Player;
using LegionBreak.Infrastructure.Spawning;
using LegionBreak.Presentation.GameOver;
using LegionBreak.Presentation.Movement;
using LegionBreak.Presentation.Skills;
using LegionBreak.Presentation.Spawning;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LegionBreak.Presentation.Composition
{
    /// <summary>
    /// 게임의 최상위 Composition Root.
    /// Domain/Application에서 정의된 인터페이스에 Infrastructure 구현체를 바인딩하는
    /// 유일한 지점이다. 이 클래스 외에는 어떤 Presentation 코드도 Infrastructure를
    /// 직접 참조해서는 안 된다.
    ///
    /// 씬 설정:
    /// 1. 부트스트랩 씬에 빈 GameObject 생성 (예: "GameLifetimeScope")
    /// 2. 이 스크립트를 부착
    /// 3. Inspector에서 Parent를 비워두면 루트 스코프로 동작
    /// 4. autoRun은 기본 true (씬 로드 시 자동으로 Configure 호출)
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private SkillData _equippedSkillData;
        [SerializeField] private CombatBalanceData _combatBalanceData;
        [SerializeField] private WaveData _waveData;

        protected override void Configure(IContainerBuilder builder)
        {
            // Application: 유스케이스 (이동량 계산은 분기·밸런스 없는 범용 수학이라
            // Domain으로 분리하지 않고 유스케이스에 인라인되어 있음)
            builder.Register<IPlayerMoveUseCase, PlayerMoveUseCase>(Lifetime.Singleton);

            // Application: 이벤트 버스 (타입 기반 발행-구독, 발행자/구독자가 서로를 모름)
            builder.Register<IEventBus, EventBus>(Lifetime.Singleton);

            // Infrastructure: 씬에 존재하는 실제 구현체를 찾아 인터페이스에 바인딩
            builder.RegisterComponentInHierarchy<TransformPlayerMotor>().AsImplementedInterfaces();

            // 플레이어 체력. TransformPlayerMotor와 같은 패턴 — 씬의 Player GameObject에
            // 부착된 컴포넌트를 찾아 IPlayerHealth에 바인딩한다. 몬스터 Attack 상태의
            // TryConsumeAttack() 데미지를 실제로 받는 소비처.
            builder.RegisterComponentInHierarchy<PlayerHealthController>().AsImplementedInterfaces();

            // 이동(FlowFieldMonsterMovementSystem)/겹침회피(SpatialHashMonsterSeparationSystem)
            // Before/After 이력: 2주차(Job+Burst), 3주차(Spatial Hashing), 5주차(장애물 우회
            // FlowField)를 각각 별도 컴포넌트로 검증해왔다. 7주차부터는 두 시스템을
            // MonsterMovementResolver 하나로 합쳐 등록한다 — 겹침 회피를 별도 Job으로
            // 전환하면 이동 Job과 같은 몬스터 Transform 집합에 서로 모르는 채 동시에 쓰기
            // Job을 스케줄링하게 되어 레이스 컨디션 위험이 생기므로, 하나의
            // TransformAccessArray를 공유하고 JobHandle 의존성으로 순서를 체이닝하기 위함
            // (자세한 이유는 MonsterMovementResolver 클래스 주석 참고). 이전 단계 컴포넌트들은
            // 삭제하지 않고 씬에 비활성 상태로 남겨 Before 대조군으로 보존한다.
            builder.RegisterComponentInHierarchy<MonsterMovementResolver>().AsImplementedInterfaces();

            // 몬스터 프리팹을 Addressables로 비동기 로드하는 어댑터
            builder.RegisterComponentInHierarchy<AddressableMonsterPrefabProvider>().AsImplementedInterfaces();

            // 풀링 Before/After 비교용: 씬에는 아래 둘 중 하나의 컴포넌트만 부착하고,
            // 그에 맞는 한 줄만 활성화한다.
            // Before(베이스라인): InstantiateMonsterSpawner
            // After(풀링 적용, 현재 활성화): PooledMonsterSpawner
            builder.RegisterComponentInHierarchy<PooledMonsterSpawner>().AsImplementedInterfaces();

            // Application: 스킬 시전 유스케이스. Skill은 SkillData(ScriptableObject)를
            // SkillFactory로 변환한 인스턴스 하나를 그대로 주입해 쿨다운 상태를 들고 있게 한다
            // (다중 스킬 슬롯은 실제 소비처가 생기기 전까진 만들지 않음).
            builder.Register<ISkillDamageCalculator>(
                resolver => new SkillDamageCalculator(_combatBalanceData.CriticalDamageMultiplier),
                Lifetime.Singleton);
            builder.RegisterInstance(SkillFactory.Create(_equippedSkillData));
            builder.Register<ICriticalHitJudge, CriticalHitJudge>(Lifetime.Singleton);
            builder.Register<IRandomProvider, RandomProvider>(Lifetime.Singleton);
            builder.Register<IPlayerSkillCastUseCase>(
                resolver => new PlayerSkillCastUseCase(
                    resolver.Resolve<Skill>(),
                    resolver.Resolve<ISkillDamageCalculator>(),
                    resolver.Resolve<IMonsterSpawner>(),
                    resolver.Resolve<ICriticalHitJudge>(),
                    resolver.Resolve<IRandomProvider>(),
                    _combatBalanceData.CriticalChance),
                Lifetime.Singleton);

            // Application: 웨이브 스폰 유스케이스. WaveData(ScriptableObject)를
            // WaveDirectorFactory로 변환한 WaveDirector 인스턴스를 그대로 주입해 웨이브별
            // 스폰 타이머 상태를 들고 있게 한다(Skill/SkillFactory와 동일 패턴).
            builder.RegisterInstance(WaveDirectorFactory.Create(_waveData));
            builder.Register<IWaveSpawnUseCase>(
                resolver => new WaveSpawnUseCase(
                    resolver.Resolve<WaveDirector>(),
                    resolver.Resolve<IMonsterSpawner>(),
                    _waveData.SpawnRadius),
                Lifetime.Singleton);

            // Presentation: 씬에 존재하는 입력 처리기에 위 의존성을 주입
            builder.RegisterComponentInHierarchy<PlayerInputController>();
            builder.RegisterComponentInHierarchy<PlayerSkillInputController>();

            // 실제 웨이브 진행은 WaveSpawnController가 담당한다. MonsterSpawnTester는
            // 폐기하지 않고 남겨뒀다 — 웨이브 로직과 무관하게 순수 스폰 처리량만 보고 싶은
            // 풀링 Before/After 재측정(6주차) 등에서 다시 쓸 수 있는 별도 스트레스 테스트
            // 하네스이기 때문. 씬에는 WaveSpawnController와 MonsterSpawnTester 중
            // 하나의 GameObject만 활성화한다(둘 다 켜두면 중복 스폰됨).
            //builder.RegisterComponentInHierarchy<WaveSpawnController>();
            builder.RegisterComponentInHierarchy<MonsterSpawnTester>();

            // 프로파일링 측정용 디버그 HUD: 동시 몬스터 수 표시. MVP 패턴 첫 적용 사례로,
            // View(Presentation)와 Presenter(Application)를 분리했다. Presenter는 컨테이너가
            // 아니라 View가 직접 생성한다(이유는 MonsterCountView 주석 참고 — 순환 의존 회피).
            builder.RegisterComponentInHierarchy<MonsterCountView>().AsImplementedInterfaces();

            // 플레이어 사망 시 "GAME OVER" 텍스트만 노출하는 View. IPlayerHealth.IsDead를
            // 직접 폴링하므로(이유는 GameOverView 주석 참고) 별도 Presenter/등록 없이
            // 컴포넌트만 씬에서 찾아 의존성을 주입한다.
            builder.RegisterComponentInHierarchy<GameOverView>();
        }
    }
}
