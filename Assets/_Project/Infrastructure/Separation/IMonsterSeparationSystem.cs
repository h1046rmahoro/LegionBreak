using LegionBreak.Infrastructure.Spawning;

namespace LegionBreak.Infrastructure.Separation
{
    /// <summary>
    /// 풀링된 몬스터를 겹침 회피(separation) 연산 대상으로 등록/해제하는 포트.
    /// IMonsterMovementSystem과 동일하게 Transform을 직접 다루는 순수 인프라 관심사라
    /// Application에는 두지 않는다.
    /// Before(BruteForceMonsterSeparationSystem, O(n²))/After(SpatialHashMonsterSeparationSystem,
    /// 공간 분할로 O(n) 근사) 두 구현체를 GameLifetimeScope에서 스왑해 성능을 비교한다.
    /// </summary>
    public interface IMonsterSeparationSystem
    {
        void Register(MonsterView view);
        void Unregister(MonsterView view);
    }
}
