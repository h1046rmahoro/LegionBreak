using LegionBreak.Infrastructure.Spawning;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// 풀링된 몬스터를 이동/타겟팅 연산 대상으로 등록/해제하는 포트.
    /// Transform을 직접 다루는 순수 인프라 관심사라 Application에는 두지 않는다.
    /// Before(MonoMonsterMovementSystem)/After(JobMonsterMovementSystem) 두 구현체를
    /// GameLifetimeScope에서 스왑해 성능을 비교한다.
    /// </summary>
    public interface IMonsterMovementSystem
    {
        void Register(MonsterView view);
        void Unregister(MonsterView view);
    }
}
