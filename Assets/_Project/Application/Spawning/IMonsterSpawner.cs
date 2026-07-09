using System.Numerics;

namespace LegionBreak.Application.Spawning
{
    /// <summary>
    /// 몬스터를 생성하는 포트(port). Infrastructure에서 Instantiate 방식과
    /// 풀링 방식 두 구현체로 제공하며, 구현체만 교체해 Before/After 성능을 비교한다.
    /// </summary>
    public interface IMonsterSpawner
    {
        void Spawn(Vector2 position);
    }
}
