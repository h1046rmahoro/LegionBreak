# 3주차: 몬스터 겹침 회피(Separation) O(n²) → Spatial Hashing Before/After 측정

## 측정 대상 선정 배경

로드맵 원안은 "기존 O(n²) 충돌 판정을 Spatial Hashing으로 교체"였으나, 3주차 착수 시점에 코드를 확인한 결과 프로젝트에 충돌 판정 로직 자체가 없었다(2주차까지 몬스터 이동은 플레이어 단일 좌표를 향한 Seek뿐이라 O(n)이었고, `Physics.OverlapSphere` 등도 미사용). 그래서 이번 주차는 핵앤슬래시 장르에서 실질적으로 필요한 "몬스터-몬스터 겹침 회피(separation)" 기능을 O(n²) 전수 비교로 신규 구현해 Before 베이스라인으로 삼고, 이를 Spatial Hashing으로 교체하는 방향으로 범위를 잡았다.

- `Before`: [BruteForceMonsterSeparationSystem](../../Assets/_Project/Infrastructure/Separation/BruteForceMonsterSeparationSystem.cs) — 등록된 몬스터를 (i, j) 쌍으로 전수 비교, n마리당 n(n-1)/2회 거리 계산
- `After`: [SpatialHashMonsterSeparationSystem](../../Assets/_Project/Infrastructure/Separation/SpatialHashMonsterSeparationSystem.cs) — 셀 크기 = 겹침 판정 반경*2인 균일 그리드에 매 프레임 재삽입, 자기 셀+주변 8칸(3x3)만 검사
- 겹침 판정 반경(`_separationRadius = 0.5`)과 밀어내는 공식은 Before/After 동일하게 맞춰 **알고리즘(O(n²) vs 공간 분할) 차이만** 비교되도록 했다. 2주차에서 이미 Job 병렬화를 다뤘으므로 이번 측정에는 Job/Burst를 섞지 않았다(변수를 하나로 유지).
- 이동/타겟팅은 2주차와 동일하게 [JobMonsterMovementSystem](../../Assets/_Project/Infrastructure/Movement/JobMonsterMovementSystem.cs)을 그대로 활성화한 상태에서 측정했다.
- 스폰 파이프라인: [PooledMonsterSpawner](../../Assets/_Project/Infrastructure/Spawning/PooledMonsterSpawner.cs), 트리거: [MonsterSpawnTester](../../Assets/_Project/Presentation/Spawning/MonsterSpawnTester.cs)
- 측정용으로 `_spawnIntervalSeconds = 0.02`, `_monsterLifetimeSeconds = 5`로 임시 조정해 동시 몬스터 수를 200~500마리 구간으로 맞췄다(커밋된 기본값은 게임플레이용 수치로 별도 유지).

## 측정 방법

2주차에서 확인한 교훈을 그대로 적용해, 처음부터 Development Build에 Profiler를 Attach하는 방식으로만 측정했다(Editor Profiler 부착 시 EditorLoop 오버헤드와 GC Alloc 계측 아티팩트가 섞이는 문제를 2주차에서 이미 확인했기 때문).

## 측정 결과 (Development Build, 몬스터 200~500마리 구간)

| 항목 | Before (O(n²)) | After (Spatial Hashing) |
|---|---|---|
| Scripts (mean) | 1.197ms | 0.528ms |
| Frame Time (Median) | 1.543ms | 0.914ms |
| Frame Time (Max) | 2.995ms | 2.461ms |
| Frame Time (Min) | 1.008ms | 0.702ms |
| GC Alloc 총량 (캡처 전체) | 8건 (최대 11.8KB) | 0건 |

## 관찰 및 해석

- **Scripts 시간이 1.197ms → 0.528ms로 약 55.9% 감소**했다. 2주차(이동 Job 단독, Scripts 0.339~0.344ms)와 비교하면 O(n²) separation이 Scripts 시간을 3배 이상 끌어올렸던 것이 확인되고, Spatial Hashing 교체로 그 증가분의 절반 이상을 상쇄했다.
- Frame Time도 Median 1.543ms → 0.914ms, Max 2.995ms → 2.461ms로 전반적으로 개선됐다.
- **GC Alloc이 8건(최대 11.8KB)에서 0건으로 완전히 사라졌다.** Before의 8건은 `Mono.JIT`/`Presentation.dll` 쪽으로 잡혀 있어 별도 확인이 필요하지만, 애초에 `Dictionary<cell, List<index>>` 대신 `int[]` 버킷 헤드 + 인덱스 연결 리스트로 그리드를 구성해 프레임마다 셀 소속이 바뀌어도 할당이 발생하지 않도록 설계한 의도가 그대로 반영된 결과로 보인다.
- After 측정에서는 Top self-time 마커가 스크립트가 아니라 `DXGI.WaitOnSwapChain`(VSync 대기)로 옮겨갔다. 즉 이 시점부터는 separation 연산이 더 이상 프레임 타임의 지배적 병목이 아니며, 병목이 렌더링/프레젠트 대기 쪽으로 이동했다 — 4주차 렌더링 최적화(GPU Instancing) 대상과 맞아떨어진다.
- 결론: 2주차(Job 이동)와 달리, **이번 3주차는 알고리즘 복잡도 자체를 O(n²)→O(n) 근사로 낮춘 경우라 몬스터 200~500마리 규모에서도 개선이 뚜렷하게 측정된다.** "병목을 정확히 진단해서 필요한 곳에만 기술을 적용한다"는 프로젝트 기준에서, 2주차는 "이 규모에서는 병렬화가 이득이 아니다"를, 3주차는 "이 규모에서는 알고리즘 복잡도 개선이 이득이다"를 각각 수치로 보여준다.

## 다음 조치 (미착수, 필요 시 진행)

- 2주차 백로그: 몬스터 수천 마리 규모로 늘렸을 때 Job 병렬화가 유리해지는 크로스오버 지점 재확인 — separation을 Spatial Hashing으로 교체한 지금이 함께 검증하기 좋은 시점
- 1주차 백로그: 풀링 프레임 타임 증가 원인(Collider 재삽입 비용 의심) 조사 — 아직 미착수
- `_bucketCount`(현재 1024)를 몬스터 수 대비 로드팩터 기준으로 튜닝할지 검토(현재는 고정값)
