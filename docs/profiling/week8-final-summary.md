# 8주차: 2개월차 최적화 파이프라인 최종 요약 & 목표 수치 검증

## 목적

CLAUDE.md 로드맵의 8주차 원안("Spatial Hashing 충돌 판정 & 렌더링 최적화")은 실제 진행
과정에서 상당 부분이 3~7주차에 선행 처리되었다. 이 문서는 (1) 8주차에 남은 항목이
실제로 무엇이었는지 판단 근거를 남기고, (2) 1~7주차 전체를 시계열로 묶어 "왜 이
순서로 적용했는지"를 정리하며, (3) `MonsterMovementResolver`(7주차 통합본) 기준
최종 목표 수치 달성 여부를 확정하는 월말 체크포인트 문서다.

## 1. 8주차 원안 항목별 처리 현황

| 원안 항목 | 처리 현황 |
|---|---|
| Spatial Hash 그리드 직접 구현 | **3주차에 이미 완료.** `SpatialHashMonsterSeparationSystem`(균일 그리드, 셀 크기 = 겹침 판정 반경×2, 자기 셀+3x3 이웃만 검사)으로 구현·측정 완료([week3-spatial-hashing.md](week3-spatial-hashing.md)). 7주차에 `MonsterSeparationJob`으로 Burst 병렬화까지 완료. |
| `ITargetQueryService` Physics→Spatial Hash 교체 | **해당 없음으로 판단, 스코프 아웃.** 프로젝트에 `ITargetQueryService`라는 서비스 자체가 존재하지 않는다. 스킬 타겟팅(`PlayerSkillCastUseCase`→`PooledMonsterSpawner.ApplyDamageInRange`)은 클릭 지점 기준 저빈도(쿨다운 단위) 이벤트라, 5주차에 "몬스터 200~500마리 규모에서도 1회성 선형 순회가 이미 충분히 싸고, 조회 빈도·기준(몬스터 vs 임의 좌표) 자체가 겹침 회피와 달라 공간 분할을 끌어올 근거가 없다"고 이미 판단해 선형 순회를 의도적으로 유지 중이다(CLAUDE.md 해당 항목 참고). Physics 기반 구현 자체가 애초에 없었으므로 "교체"할 대상이 없다. |
| O(n²) 대비 처리 시간 비교 측정 | **3주차에 이미 완료.** Scripts mean 1.197ms → 0.528ms(약 55.9% 감소), GC Alloc 8건 → 0건. |
| GPU Instancing 적용 | **4주차에 이미 처리 후 철회/대체 완료.** URP GPU Resident Drawer(진짜 자동 인스턴싱 경로)를 시도했으나 `DOTS_INSTANCING_ON` 셰이더 변형 누락 런타임 에러 발생 — DOTS(Entities Graphics) 계열 기능이라 "순수 ECS 전체 전환은 하지 않는다"는 프로젝트 원칙에 따라 철회했다. 대신 몬스터 전용 공유 머티리얼(`Monster.mat`, GPU Instancing 플래그 on)로 SRP Batcher 수준 개선(Draw Calls 462→436, SetPass Calls 16→12)을 확보했다([week4-rendering.md](week4-rendering.md)). 8주차에 다시 시도할 근거(병목 재진단)가 없어 스코프 아웃. |
| 애니메이션 LOD | **스코프 아웃.** 코드베이스 전수 검색 결과 `Animator`/`Animation` 컴포넌트가 프로젝트에 존재하지 않는다 — 몬스터가 정적 Capsule 프리미티브(`Monster.prefab`)라 애초에 LOD를 걸 대상(애니메이션 자체)이 없다. 몬스터에 실제 애니메이션을 붙이는 건 이번 최적화 파이프라인의 범위를 넘는 별도 작업(리깅/애니메이터 컨트롤러 설계)이라, 2개월차 범위에서는 진행하지 않는다. |
| 최종 목표 수치 검증 & 리포트 정리 | **본 문서에서 수행 (아래 2~3절).** |

## 2. 전체 최적화 파이프라인 시계열

| 주차 | 적용 기법 | 측정 대상 시스템 | Frame Time (Median) | GC Alloc | 비고 |
|---|---|---|---|---|---|
| 1주차 | 오브젝트 풀링 | `InstantiateMonsterSpawner`→`PooledMonsterSpawner` | 15ms → 17ms | 415 → 189건 | GC는 감소, 프레임 타임은 오히려 증가(원인 미해결로 보류) |
| 2주차 | Job System(이동) | `MonoMonsterMovementSystem`→`JobMonsterMovementSystem` | (~100마리 규모) 유의미한 차이 없음 | - | Job 병렬화가 이 규모에서 이득 없음 — TransformAccessArray 관리 오버헤드가 병렬화 이득을 상쇄 |
| 3주차 | Spatial Hashing(겹침 회피) | `BruteForceMonsterSeparationSystem`→`SpatialHashMonsterSeparationSystem` | 1.543ms → 0.914ms | 8건 → 0건 | Scripts mean 1.197ms→0.528ms(-55.9%), 병목이 렌더링(VSync 대기)로 이동 |
| 4주차 | 렌더링(머티리얼 공유+그림자 Off) | `Monster.mat` 공유 할당 | 0.913ms → 0.869ms | 12건 → 0건 | GPU Resident Drawer는 DOTS 이슈로 철회, SRP Batcher 수준 개선으로 대체. 통합 검증 0.854ms |
| 4주차(후속) | Collider 제거 | `PooledMonsterSpawner` | 0.868~0.877ms 범위 내 변화 없음 | - | 245~450마리 규모에서 측정 가능한 개선 없음(2주차와 같은 성격) |
| 6주차 | 전투 시스템 완성 후 풀링 재점검 | `MonsterView.Awake()` 1회 생성+`Reset()`, 컬렉션 사전할당 | 1.082ms → 1.000ms(램프업 구간) | 411건 → 213건(램프업) | 풀링이 GameObject만 재사용하고 도메인 상태(`Health`/`MonsterAI`)는 매번 재할당하던 사각지대 발견·수정 |
| 6주차 | 정상 상태(Steady State) 최종 캡처 | `MonsterMovementResolver`(7주차 통합본) 포함 전체 파이프라인 | **1.024ms** | **0건** | 목표 수치 최초 달성 확인(아래 3절에서 8주차 최종 검증으로 재사용) |
| 7주차 | Job System(겹침 회피 통합) | `SpatialHashMonsterSeparationSystem`→`MonsterMovementResolver`+`MonsterSeparationJob` | Timeline 캡처(프레임 1.01ms) | - | Burst 벡터화 확인(SIMD 명령어), 500마리 규모에서는 메인 스레드 직접 실행 경로("Job 감싸도 항상 병렬 실행되진 않는다") |

## 3. 적용 순서의 논리: 왜 이 순서였는가

1. **오브젝트 풀링 (1주차) — GC 임팩트가 가장 큰 것부터.** `Instantiate`/`Destroy` 반복 호출은 프레임마다 GC Alloc을 직접 발생시키는 가장 명백한 원인이라 최우선으로 제거했다. (측정 방법론 자체의 한계 — Editor Profiler 단일 스냅샷 — 는 이후 2주차에서 Development Build 방식으로 교정됨.)
2. **Job System 이동 (2주차) — 연산 병목 후보 검증.** 로드맵상 다음 순서였던 "이동/타겟팅 병렬화"를 시도했으나, 이 시점 규모(~100마리)에서는 병목이 아니었음을 실측으로 확인 — "정확한 진단 없이 기술을 먼저 적용하지 않는다"는 원칙에 따라 결과를 그대로 기록하고 다음 단계로 넘어갔다.
3. **Spatial Hashing 겹침 회피 (3주차) — 판정 알고리즘 복잡도 개선.** 로드맵 원안이 가리키던 "충돌 판정"이 실제로는 존재하지 않아, 그 자리를 대체할 실질적 O(n²) 로직(몬스터-몬스터 겹침 회피)을 신규 구현한 뒤 공간 분할로 교체했다. 이 시점에서 처음으로 뚜렷한 개선(Scripts -55.9%)이 측정됐다 — 알고리즘 복잡도 개선이 이 규모에서 실제로 유효한 병목이었음을 보여준다.
4. **렌더링 최적화 (4주차) — 3주차가 가리킨 다음 병목을 따라감.** 3주차 캡처에서 Top self-time 마커가 스크립트에서 `DXGI.WaitOnSwapChain`(VSync/프레젠트 대기)으로 이동한 것을 확인하고, 그 신호를 따라 렌더링 쪽(머티리얼 배칭)으로 이동했다. GPU Resident Drawer라는 "정답에 가까운" 선택지가 있었지만 DOTS 종속성이 확인되자 프로젝트 원칙(순수 ECS 지양)에 따라 철회하고 대안(SRP Batcher 수준 개선)으로 목표를 달성했다.
5. **전투 시스템 완성 후 재점검 (5~6주차) — "동작하는 상태"에서 재검증.** 1~4주차는 몬스터가 수명 타이머+단순 Seek뿐인 스켈레톤 상태에서의 1차 패스였다. 실제 게임플레이(FSM, 스킬, HP, 웨이브, FlowField)가 완성된 뒤에야 최적화가 실전 조건에서도 유효한지 확인할 수 있어, 6주차에 전체 파이프라인을 다시 프로파일링했다. 이 과정에서 1차 패스가 놓쳤던 새로운 GC Alloc 원인(도메인 객체 재할당, 컬렉션 미사전할당)을 실측으로 발견해 수정했다 — "코드 리뷰만으로는 못 잡는 문제를 실측이 잡아낸" 사례.
6. **Job System 통합 (7주차) — 안전성 문제 해결이 우선, 성능은 부차.** 겹침 회피를 Job으로 감싼 이유는 성능(이미 3주차에 충분히 빨랐음)이 아니라, 이동 시스템과 겹침 회피 시스템이 각자 독립된 `TransformAccessArray`로 같은 Transform 집합에 동시에 쓰기 Job을 스케줄링하면 레이스 컨디션 위험이 있었기 때문이다. `MonsterMovementResolver`로 통합해 Job 의존성 체인(`handleB = job.Schedule(..., handleA)`)으로 순서를 보장했다.

이 순서를 요약하면: **GC 임팩트가 명백한 것(풀링) → 가설 기반 연산 병목 검증(Job, 결과는 부정적) → 실측이 가리킨 진짜 병목(알고리즘 복잡도) → 그 다음 병목(렌더링) → 실전 조건 재검증(전투 시스템 완성 후) → 안전성 이슈 해결(Job 통합)** 이다. 각 단계가 "다음에 뭘 할지"를 이전 단계의 프로파일러 실측 결과로부터 얻었다는 점이 일관된 흐름이다.

## 4. 최종 목표 수치 검증 (CLAUDE.md 기준)

측정은 [week6-pooling-recapture.md](week6-pooling-recapture.md)의 정상 상태(Steady State) 캡처를 그대로 재사용한다 — 해당 캡처는 이동/겹침회피 시스템으로 이미 `MonsterMovementResolver`(7주차 통합본)를 사용하고 있어(문서 상단 "측정 대상" 절 참고), 사실상 7주차 완료 이후 시점의 최종 통합 상태를 반영하고 있다. 별도 재캡처 없이 이 수치를 8주차 최종 검증 근거로 채택한다.

| 목표 (CLAUDE.md) | 실측 | 판정 |
|---|---|---|
| 몬스터 200~500마리 동시 스폰 시 60fps 안정 유지 | Frame Time Median **1.024ms** / Max **2.133ms** (60fps 예산 16.6ms 대비 약 8~16배 여유) | **달성** |
| Update 루프 GC Alloc 0B/frame | Allocations 패널 "No markers found" — **완전한 0건** | **달성** |
| O(n²) 전수 검사 → 공간 분할로 O(n) 근사 | 3주차 Spatial Hashing 적용, Scripts mean -55.9% | **달성** |

두 핵심 목표(60fps 안정, GC Alloc 0B/frame) 모두 실측으로 확정됐다. 8주차 로드맵의 "목표 수치 미달성 시 보완 계획" 절차는 필요하지 않다.

## 5. 결론

- 8주차 원안의 6개 세부 항목 중 3개(Spatial Hash 구현, O(n²) 비교 측정, GPU Instancing)는 3~4주차에 이미 완료됐고, 1개(`ITargetQueryService` 교체)는 대상 자체가 없어 스코프 아웃, 1개(애니메이션 LOD)는 몬스터에 애니메이션 시스템 자체가 없어 스코프 아웃했다. 실질적으로 8주차에 남아있던 것은 "최종 목표 수치 검증과 전체 리포트 정리"뿐이었다.
- 이는 로드맵을 문자 그대로 따르지 않은 게 아니라, 매 주차 착수 시점마다 "로드맵 원안의 전제가 실제 코드 상태와 맞는지"를 먼저 확인하고 범위를 재조정해 온 이 프로젝트의 반복된 패턴(3주차 "충돌 판정 로직 자체가 없었음", 4주차 "GPU Resident Drawer는 DOTS 종속", 5주차 "SkillRangeChecker 분리 불필요" 등)의 연장선이다.
- 2개월차 최적화 파이프라인(오브젝트 풀링 → Job System → Spatial Hashing → 렌더링 → 실전 재검증 → Job 통합)은 CLAUDE.md의 정량 목표(200~500마리, 60fps, GC 0B/frame)를 모두 실측으로 달성한 상태로 종료한다. 3개월차(커스텀 에디터 툴, README/아키텍처 다이어그램, 플레이 영상, 프로파일링 리포트 정리)로 이관한다.
