namespace LegionBreak.Infrastructure.Pathfinding
{
    /// <summary>
    /// WalkableGrid를 베이크해 소유한 시스템(FlowFieldMonsterMovementSystem)이, 같은 그리드를
    /// 필요로 하는 다른 시스템(SpatialHashMonsterSeparationSystem)에 읽기 전용으로 노출하는
    /// 포트. 그리드 베이크/해제 소유권은 계속 FlowFieldMonsterMovementSystem 한 곳에 두고,
    /// 이 인터페이스로는 참조만 공유한다 — 별도 DI 싱글턴으로 분리하면 GameLifetimeScope의
    /// Configure() 시점에 씬 장애물 Collider가 아직 Awake되지 않았을 수 있어 베이크 타이밍이
    /// 불안정해지므로, 검증된 기존 베이크 지점(Awake)을 그대로 유지한 채 참조만 공유한다.
    /// </summary>
    public interface IWalkableGridProvider
    {
        WalkableGrid Grid { get; }
    }
}
