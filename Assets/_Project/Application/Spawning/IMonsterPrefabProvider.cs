using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LegionBreak.Application.Spawning
{
    /// <summary>
    /// 몬스터 프리팹을 비동기로 제공하는 포트(port). 실제 구현은 Infrastructure에서
    /// Addressables로 로드하지만, 이 인터페이스만 보면 그 출처를 알 수 없다.
    /// </summary>
    public interface IMonsterPrefabProvider
    {
        UniTask<GameObject> LoadAsync();
    }
}
