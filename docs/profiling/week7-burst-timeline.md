# 7주차: Burst 벡터화 확인 & 메인 스레드 vs Job 스레드 Timeline 캡처

## 배경

7주차에 겹침 회피(`SpatialHashMonsterSeparationSystem`, 메인 스레드 C#)를 Job으로 감싸
`MonsterMovementResolver`+`MonsterSeparationJob`으로 통합하면서, `[BurstCompile]` 적용
여부는 코드에 어노테이션으로 확인했지만 실제 컴파일 결과(벡터화 여부)와 메인/Job 스레드
분산은 6주차 프로파일링 캡처와 함께 나중에 일괄 진행하기로 보류해뒀다. 6주차가 완료된
시점에 이어서 진행한다.

## 1. Burst Inspector — 벡터화 확인

대상: `FlowFieldSeekJob.Execute()`(5주차, 장애물 우회 이동), `MonsterSeparationJob.Execute()`
(7주차, 겹침 회피) — `MonsterMovementResolver`가 매 프레임 스케줄링하는 두 Job.

**FlowFieldSeekJob**: `cellCenter`/`toCell` 등 `float2` 연산이 `vmulps`/`vaddps`/`vsubps`
(packed 연산)로, `math.rsqrt`가 `vsqrtss`/`vdivss`(스칼라 역제곱근)로 컴파일됐다 — Burst가
`Unity.Mathematics` 연산을 SIMD 명령어로 실제 변환하고 있음을 확인.

**MonsterSeparationJob**: `delta = posA - posB`가 `vsubps`, `overlap` 계산과 `totalPush`
누적이 `vmulps`/`vdivps`/`vaddps`(packed 연산), `distance = math.sqrt(distanceSq)`가
`vsqrtss`로 컴파일됐다 — 3x3 버킷 순회 루프 내부의 반복 연산이 스칼라 float 루프가 아니라
SIMD 레지스터(xmm)를 사용하는 것으로 확인.

**결론**: 두 Job 모두 `[BurstCompile]` 적용이 실제 벡터화된 네이티브 코드로 이어졌다.
`IJobParallelForTransform`은 트랜스폼 하나당 한 번 `Execute`가 호출되는 구조라 "루프
전체를 여러 개체에 걸쳐 한 번에 처리하는" 전통적 auto-vectorization과는 다르지만, 각
호출 내부의 `float2`/`float3` 벡터 수학 연산 자체가 packed SIMD 명령어로 컴파일되어
스칼라 코드 대비 명령어 수가 줄어든다.

## 2. Timeline 프로파일링 — 메인 스레드 vs Job 스레드 분산

Development Build + Profiler Attach, CPU Usage 모듈 Timeline 뷰로 200~500마리 정상
상태(`MonsterSpawnTester` 자동 정지 후)에서 캡처했다(선택 프레임 CPU 1.01ms).

**Main Thread**: `MonsterMovementResolver.Update()`(`Resolver.Update(...)`) 호출 뒤
`WaitForJobGroupID (0.09ms)`가 관측됐다 — `MonsterMovementResolver.LateUpdate()`의
`handleB.Complete()`가 실제로 대기하는 비용이다. 프레임 전체(1.01ms) 대비 0.09ms로,
Job으로 넘긴 이동/겹침회피 연산이 메인 스레드를 길게 블로킹하지 않는다.

**Job Worker 스레드 — 최초 해석은 과장이었음(정정)**: Worker 0~20+ 행에 흩어진
`EngineJob`/`Consumer`/`lob.Sort`/`DrawSRPBatcher` 등은 이름상 Unity 내부 렌더링
관련 Job(SRP 배칭, 컬링 등)으로 보이며, 우리가 만든 `MonsterSeparationJob`/
`FlowFieldSeekJob`이라는 근거가 없다. 툴팁으로 확인된 `MonsterSeparationJob (Cleanup)
0.000ms`도 실제 연산이 아니라 Job Safety System의 의존성 정리 마커(0ms)다.

**`MonsterSeparationJob (Burst)`는 실제로 Main Thread에서 0.083ms 실행됐다**: 이름이
명확한 이 블록을 Main Thread 행에서 직접 클릭해 확인한 결과 0.083ms — 바로 옆의
`WaitForJobGroupID (0.09ms)`와 거의 일치한다. 즉 `Complete()` 대기 시간의 대부분이
"워커가 끝내길 기다리는 유휴 대기"가 아니라 **메인 스레드 자신이 Job을 직접 실행한
시간**이었다는 뜻이다. Unity의 Job 시스템은 워커 스레드가 이미 다른 작업(렌더링 관련
Job 등)으로 바쁘거나, 작업량이 작아 분배 오버헤드가 이득보다 클 때 `Complete()`를
호출한 스레드가 직접 실행을 떠맡는 경우가 있다 — 이번 500마리 규모, 3x3 버킷 순회
정도의 가벼운 연산량에서는 그 경로를 탄 것으로 판단된다.

## 관찰 및 해석

- Burst Inspector로는 `[BurstCompile]`이 실제 SIMD 벡터화 코드로 이어졌음을 확인했다
  (검증 완료).
- Timeline 뷰 + 실제 블록 클릭으로 확인한 결과, **이번 캡처의 이 프레임에서는
  `MonsterSeparationJob`이 별도 Worker 스레드로 분산되지 않고 메인 스레드에서
  0.083ms 동안 직접 실행됐다.** "Job으로 감쌌으니 항상 다른 스레드에서 병렬 실행된다"는
  가정이 이 규모에서는 성립하지 않음을 실측으로 확인한 것 — Job/Burst 최적화가
  "무조건 이득"이 아니라 워크로드 크기에 따라 실제 이득 여부가 갈린다는 걸 보여주는
  사례로, 이 프로젝트의 "병목을 정확히 진단해서 필요한 곳에만 기술 적용" 원칙과도
  맞닿아 있다(2주차에서 Job 병렬화가 이 규모(~100마리)에서 오히려 손해였던 것과
  같은 성격의 결론).
- 다만 `WaitForJobGroupID (0.09ms)` 자체는 프레임 전체(1.01ms) 대비 여전히 작아,
  이 경로(메인 스레드 직접 실행)를 타더라도 성능에 실질적 문제는 없다. `[BurstCompile]`이
  적용된 덕분에 메인 스레드에서 실행되더라도 SIMD 벡터화된 빠른 코드가 도는 것 —
  즉 이번 결과는 "병렬화 실패"가 아니라 "Burst 벡터화가 병렬화 여부와 무관하게
  그 자체로 유효했다"는 쪽에 더 가깝다.
- 이 규모(500마리)에서는 렌더링(`UniversalRenderTotal` 등)이 프레임 시간의 더 큰
  비중을 차지한다 — 3주차 문서에서 이미 확인했던 "병목이 렌더링/프레젠트 대기 쪽으로
  이동했다"는 결론과 일관된다.
- 7주차 로드맵의 "메인 스레드 vs Job 스레드 분산 프로파일링 캡처" 항목은 이걸로
  완료 처리한다 — 다만 결론은 로드맵이 암묵적으로 기대했을 "분산 병렬 실행 확인"이
  아니라 "이 규모에서는 메인 스레드 직접 실행 경로를 탔다"는 실측 결과였다.
