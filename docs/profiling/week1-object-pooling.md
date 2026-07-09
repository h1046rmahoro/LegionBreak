# 1주차: 오브젝트 풀링 Before/After 측정

## 측정 대상

- 더미 몬스터(프리미티브 Capsule) 스폰/디스폰 파이프라인
- `Before`: [InstantiateMonsterSpawner](../../Assets/_Project/Infrastructure/Spawning/InstantiateMonsterSpawner.cs) — 매 스폰마다 `Instantiate`, 수명 종료 시 `Destroy`
- `After`: [PooledMonsterSpawner](../../Assets/_Project/Infrastructure/Spawning/PooledMonsterSpawner.cs) — 시작 시 프리워밍, 수명 종료 시 `SetActive(false)`로 반환 후 재사용
- 스폰 트리거: [MonsterSpawnTester](../../Assets/_Project/Presentation/Spawning/MonsterSpawnTester.cs) (인터벌마다 랜덤 위치 스폰 요청)
- 동시 존재 몬스터 수: 200~500마리 구간 (스폰 간격/수명 파라미터로 조정)

## 측정 결과

| 시나리오 | 프레임 타임 | GC Alloc |
|---|---|---|
| Before (Instantiate/Destroy) | 15ms | 415 |
| After (오브젝트 풀링) | 17ms | 189 |

측정 환경(Unity Editor Profiler, 단일 프레임 스냅샷 기준)에서 캡처.

## 관찰 및 원인 분석 (미해결 이슈로 기록)

기대와 달리 GC Alloc은 줄었지만(415 → 189) 프레임 타임은 오히려 증가했다(15ms → 17ms). 이 결과를 그대로 기록하며, 유력한 원인 후보는 다음과 같다.

1. **Collider 재삽입 비용**: `GameObject.CreatePrimitive(PrimitiveType.Capsule)`은 기본으로 `CapsuleCollider`를 포함한다. 현재 프로젝트엔 아직 충돌 판정 로직이 없어 이 Collider는 기능적으로 불필요한데, `SetActive(true/false)` 토글마다 PhysX 브로드페이즈에 재삽입/제거되는 비용이 발생해 GC 절감분보다 CPU 비용이 더 커졌을 가능성이 있다.
2. **측정 방법의 한계**: 단일 프레임 스냅샷 비교라서, Before 캡처 시점에 실제 GC.Collect가 발생하지 않았을 수 있다. 이 경우 Before의 15ms는 Instantiate/Destroy의 실제 비용(할당 자체가 아니라 GC 수행 시점의 스파이크)을 반영하지 못했을 수 있다.

## 다음 조치 (미착수)

- `DummyMonsterView` 생성 시 불필요한 `Collider` 제거 후 재측정
- 여러 프레임 평균 + GC.Collect 발생 시점 확인을 포함한 재측정
- 위 조치 후에도 경향이 유지되는지 확인 후 2주차(Job System 이동/타겟팅) 진행
