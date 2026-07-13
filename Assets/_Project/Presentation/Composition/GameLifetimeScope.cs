using LegionBreak.Application.Movement;
using LegionBreak.Infrastructure.Movement;
using LegionBreak.Infrastructure.Separation;
using LegionBreak.Infrastructure.Spawning;
using LegionBreak.Presentation.Movement;
using LegionBreak.Presentation.Spawning;
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
        protected override void Configure(IContainerBuilder builder)
        {
            // Application: 유스케이스 (이동량 계산은 분기·밸런스 없는 범용 수학이라
            // Domain으로 분리하지 않고 유스케이스에 인라인되어 있음)
            builder.Register<IPlayerMoveUseCase, PlayerMoveUseCase>(Lifetime.Singleton);

            // Infrastructure: 씬에 존재하는 실제 구현체를 찾아 인터페이스에 바인딩
            builder.RegisterComponentInHierarchy<TransformPlayerMotor>().AsImplementedInterfaces();

            // 이동/타겟팅 Job 병렬화 Before/After 비교용: 씬에는 아래 둘 중 하나의
            // 컴포넌트만 부착하고, 그에 맞는 한 줄만 활성화한다.
            // Before(베이스라인): MonoMonsterMovementSystem
            // After(Job+Burst 병렬화, 현재 활성화): JobMonsterMovementSystem
            builder.RegisterComponentInHierarchy<JobMonsterMovementSystem>().AsImplementedInterfaces();

            // 3주차 몬스터 겹침 회피(separation) O(n²) vs Spatial Hashing 비교용: 씬에는
            // 아래 둘 중 하나의 컴포넌트만 부착하고, 그에 맞는 한 줄만 활성화한다.
            // Before(베이스라인): BruteForceMonsterSeparationSystem
            // After(Spatial Hashing, 현재 활성화): SpatialHashMonsterSeparationSystem
            builder.RegisterComponentInHierarchy<SpatialHashMonsterSeparationSystem>().AsImplementedInterfaces();

            // 풀링 Before/After 비교용: 씬에는 아래 둘 중 하나의 컴포넌트만 부착하고,
            // 그에 맞는 한 줄만 활성화한다.
            // Before(베이스라인): InstantiateMonsterSpawner
            // After(풀링 적용, 현재 활성화): PooledMonsterSpawner
            builder.RegisterComponentInHierarchy<PooledMonsterSpawner>().AsImplementedInterfaces();

            // Presentation: 씬에 존재하는 입력 처리기에 위 의존성을 주입
            builder.RegisterComponentInHierarchy<PlayerInputController>();
            builder.RegisterComponentInHierarchy<MonsterSpawnTester>();
        }
    }
}
