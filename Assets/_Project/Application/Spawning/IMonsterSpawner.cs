using UnityEngine;

namespace LegionBreak.Application.Spawning
{
    /// <summary>
    /// 몬스터를 생성하는 포트(port). Infrastructure에서 Instantiate 방식과
    /// 풀링 방식 두 구현체로 제공하며, 구현체만 교체해 Before/After 성능을 비교한다.
    /// </summary>
    public interface IMonsterSpawner
    {
        int ActiveCount { get; }

        void Spawn(Vector2 position);

        /// <summary>
        /// center를 중심으로 radius 반경 안의 몬스터 전부에게 damage를 적용한다.
        /// 맞은 몬스터 수를 반환한다(피드백/로그용).
        /// </summary>
        int ApplyDamageInRange(Vector2 center, float radius, float damage);
    }
}
