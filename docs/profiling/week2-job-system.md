# 2주차: 몬스터 이동/타겟팅 Job 병렬화 Before/After 측정

## 측정 대상

- 풀링된 더미 몬스터의 이동/타겟팅(플레이어 위치로 Seek) 연산
- `Before`: [MonoMonsterMovementSystem](../../Assets/_Project/Infrastructure/Movement/MonoMonsterMovementSystem.cs) — 메인 스레드 `Update()` 단일 루프에서 순차 계산
- `After`: [JobMonsterMovementSystem](../../Assets/_Project/Infrastructure/Movement/JobMonsterMovementSystem.cs) + [MonsterSeekJob](../../Assets/_Project/Infrastructure/Movement/MonsterSeekJob.cs) — `IJobParallelForTransform` + `[BurstCompile]`로 병렬화
- 스폰 파이프라인: [PooledMonsterSpawner](../../Assets/_Project/Infrastructure/Spawning/PooledMonsterSpawner.cs), 트리거: [MonsterSpawnTester](../../Assets/_Project/Presentation/Spawning/MonsterSpawnTester.cs)
- 화면 내 동시 몬스터 수: 약 100마리 (최종 빌드 측정 기준)

## 측정 방법 및 시행착오

1주차와 마찬가지로 측정 방법 자체에서 두 번의 함정을 발견해 기록으로 남긴다.

1. **1차 시도 (Unity 에디터에 Profiler를 붙여 Play Mode로 측정)**: `EditorLoop`/`RenderLoop`가 프레임 타임의 대부분(약 19ms)을 차지해, 정작 비교 대상인 `Scripts` 항목(1.263ms vs 1.243ms)이 노이즈에 묻혀 버렸다. 에디터 자체의 Scene 뷰/Inspector 렌더링 비용까지 같이 잡히기 때문이다.
2. 같은 1차 측정에서 GC 할당이 Before/After 각각 415건, 343건으로 잡혔는데, **Development Build로 재측정하니 0~2건 수준으로 사실상 사라졌다.** 즉 이 GC 할당은 게임 코드 문제가 아니라 에디터에 Profiler를 붙여 측정할 때 생기는 계측 아티팩트(Deep Profile 오버헤드 등)였다. **GC Alloc은 반드시 Development Build에서 검증해야 신뢰할 수 있다는 교훈.**
3. 이후 Development Build로 재측정한 수치를 최종 결과로 채택한다.

## 측정 결과 (Development Build, 몬스터 약 100마리)

| 항목 | Before (Mono) | After (Job + Burst) |
|---|---|---|
| Scripts (mean) | 0.339ms | 0.344ms |
| VSync | 0.171ms | 0.198ms |
| Frame Time (Median) | 0.718ms | 0.765ms |
| Frame Time (Max) | 5.318ms | 5.359ms |
| GC Alloc 총량 (캡처 전체) | 0건 | 2건 (2.5KB) |

## 관찰 및 해석

- **Scripts 시간은 사실상 동일**하다(0.339ms vs 0.344ms, 약 1.5% 차이로 노이즈 범위). 몬스터 100마리 수준의 단순 벡터 Seek 연산은 총합이 마이크로초 단위라, Job으로 병렬화해도 전체 프레임 타임에서 이득이 드러나지 않는다.
- 오히려 **After 쪽이 Frame Time·GC 모두 미세하게 더 크다.** `JobMonsterMovementSystem`은 `TransformAccessArray`/`Dictionary`/`List`로 등록 상태를 관리하는데, GC 2건은 몬스터 수가 늘어나는 초반 램프업 구간에서 이 내부 컬렉션들이 용량을 한두 번 키우며 발생한 일회성 할당으로 추정된다(매 프레임 반복되는 할당이 아니라 캡처 전체에서 2번뿐이므로 `Update` 루프의 `GC Alloc 0B/frame` 목표에는 영향 없음).
- 결론: **현재 워크로드(몬스터 ~100~500마리, 단순 Seek)에서는 Job System 도입이 측정 가능한 성능 이득을 주지 않으며, TransformAccessArray 관리 오버헤드 때문에 오히려 미세하게 불리하다.** 이는 실패가 아니라 "병목을 정확히 진단해서 필요한 곳에만 기술을 적용한다"는 이 프로젝트의 트레이드오프 판단 기준(CLAUDE.md 하이브리드 구조 항목)을 그대로 보여주는 결과다 — 지금 시점의 실제 병목은 이동 연산이 아니라 렌더링(개별 Draw Call, 4주차 GPU Instancing 대상)이다.
- Job 기반 아키텍처는 몬스터 수가 훨씬 늘어나거나(수천 마리 스케일) 이동 로직이 복잡해질 때(타겟 우선순위, 회피 등) 이득이 드러날 구조로 미리 갖춰둔 것으로 판단한다.

## 다음 조치 (미착수, 필요 시 진행)

- 수천 마리 규모 스트레스 테스트로 Job이 유리해지는 크로스오버 지점 확인 (`_prewarmCount`를 5000 이상으로 올려 실제 빌드에서 재측정)
- 3주차(Spatial Hashing 충돌 판정) 작업 시 이동 Job과 함께 병목이 실제로 드러나는지 재확인
