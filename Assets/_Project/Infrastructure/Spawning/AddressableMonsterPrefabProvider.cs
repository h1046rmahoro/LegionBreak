using Cysharp.Threading.Tasks;
using LegionBreak.Application.Spawning;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// Addressables로 몬스터 프리팹을 비동기 로드한다. AssetReference 필드에 실제 에셋을
    /// 드래그해서 할당하면 Unity가 자동으로 Addressable 그룹에 등록해주므로, 문자열 키를
    /// 별도로 관리할 필요가 없다. 스폰 파이프라인이 살아있는 동안(= 씬 수명 동안) 항상 필요한
    /// 참조라 로드 후 캐시하고, 씬이 내려갈 때(OnDestroy) 해제한다 — 재시작(씬 리로드)마다 새
    /// 컴포넌트가 같은 에셋을 다시 로드하므로, 이전 핸들을 놓지 않으면 참조 카운트가 재시작
    /// 횟수만큼 쌓여 0으로 돌아가지 않는다.
    ///
    /// AsyncOperationHandle&lt;T&gt;를 await하면 암묵적으로 비제네릭 AsyncOperationHandle로
    /// 변환되면서 결과값 없는 GetAwaiter()가 선택되는 문제가 있었다(await 표현식이 void가
    /// 되어 컴파일 에러). 그래서 await의 반환값에 의존하지 않고, 완료까지 기다린 뒤
    /// 핸들의 Result를 직접 읽는 방식으로 우회한다.
    /// </summary>
    public sealed class AddressableMonsterPrefabProvider : MonoBehaviour, IMonsterPrefabProvider
    {
        [SerializeField] private AssetReferenceGameObject _monsterPrefabReference;

        private GameObject _cachedPrefab;

        public async UniTask<GameObject> LoadAsync()
        {
            if (_cachedPrefab != null)
            {
                return _cachedPrefab;
            }

            var handle = _monsterPrefabReference.LoadAssetAsync();
            await handle;
            _cachedPrefab = handle.Result;
            return _cachedPrefab;
        }

        private void OnDestroy()
        {
            // 로드 전이면 해제할 핸들이 없다(ReleaseAsset이 경고 로그를 남기므로 가드).
            if (_monsterPrefabReference != null && _monsterPrefabReference.OperationHandle.IsValid())
            {
                _monsterPrefabReference.ReleaseAsset();
            }

            _cachedPrefab = null;
        }
    }
}
