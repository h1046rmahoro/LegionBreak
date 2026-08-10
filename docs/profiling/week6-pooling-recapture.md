# 6주차: 전투 시스템 완성 후 풀링 재측정

## 배경

1주차 풀링 Before/After 측정은 몬스터가 수명 타이머 + 단순 Seek 이동만 있는 스켈레톤
상태(`docs/profiling/week1-object-pooling.md`)에서 진행한 1차 패스였다. 5주차에 전투
시스템(FSM, 스킬, HP, 웨이브, FlowField 장애물 회피)이 실제로 "동작하는 상태"로
완성됐으므로, 6주차는 그 위에서 CLAUDE.md 목표 수치(몬스터 200~500마리, 60fps,
GC Alloc 0B/frame)를 실측으로 재확인하는 2차 패스다.

Before(`InstantiateMonsterSpawner`) 캡처는 생략했다 — 1주차에 이미 Instantiate/Destroy
기준선(GC 415건, Frame Time 15ms)이 있고, 이번 목표는 "풀링이 유효한가" 재확인이 아니라
"전투 시스템이 붙은 지금도 목표 수치를 실제로 달성하는가"였기 때문이다(2026-08-11 결정).

## 측정 대상

- [PooledMonsterSpawner](../../Assets/_Project/Infrastructure/Spawning/PooledMonsterSpawner.cs) (prewarm 500)
- 스폰 트리거: [MonsterSpawnTester](../../Assets/_Project/Presentation/Spawning/MonsterSpawnTester.cs)
  — `_targetCount`(기본값 500) 도달 시 `enabled = false`로 자동 정지하는 로직을 이번에
  추가해, 5주차에 제거된 수명 타이머 대신 정상 상태 캡처를 위한 정지 지점을 만들었다
- 이동/겹침회피: [MonsterMovementResolver](../../Assets/_Project/Infrastructure/Movement/MonsterMovementResolver.cs) (7주차 통합본)
- Development Build + Profiler Attach로 측정 (2주차 교훈: Editor Profiler는 EditorLoop
  오버헤드와 GC Alloc 계측 아티팩트가 섞여 신뢰 불가)

## 캡처 도중 발견한 버그: 풀링이 도메인 상태까지는 재사용하지 않고 있었음

램프업(0→500마리) 구간 캡처에서 GC 상위 기여자가 `MonsterSpawnTester.Update()` 호출
체인으로 잡혔다(Deep Profile 없이는 호출 체인 상위 마커로 뭉쳐 잡힘). 코드를 추적한
결과 두 가지 원인이 확인됐다.

1. **`Health`/`MonsterAI` 매 스폰 재할당**: [MonsterView.Initialize()](../../Assets/_Project/Infrastructure/Spawning/MonsterView.cs)가
   스폰될 때마다 `_health = new Health(...)`, `_ai = new MonsterAI(...)`로 도메인 상태를
   새로 할당하고 있었다 — 오브젝트 풀링은 `GameObject`/`MonsterView` 컴포넌트만 재사용하고
   그 안의 도메인 객체는 매번 새로 만드는 사각지대였다(6주차 로드맵의 "Instantiate/Destroy
   직접 호출 코드 전수 검색"이 GameObject 레벨만 봤을 뿐, 도메인 객체 레벨 재할당까지는
   잡지 못함). `Health`/`MonsterAI`에 `Reset(...)`을 추가하고(생성자가 내부적으로 Reset을
   호출해 기존 테스트/API는 유지), `MonsterView.Awake()`에서 1회만 생성한 뒤
   `Initialize()`에서는 `Reset()`으로 값만 되돌리도록 수정했다.
2. **컬렉션 미사전할당**: `PooledMonsterSpawner`의 `_pool`/`_activeViews`/`_activeIndexByView`가
   용량 지정 없이 빈 상태로 시작해, 몬스터 수가 늘어날 때마다 내부 배열이 2배씩
   재할당되고 있었다. `_prewarmCount`를 이미 알고 있으므로 `Awake()`에서 그 용량으로
   미리 할당하도록 수정했다(필드 초기화 시점엔 Inspector로 설정된 `_prewarmCount`가 아직
   적용되기 전이라 Awake로 옮겨야 했다).

**수정 전/후 (램프업 0→524마리 전체 구간 캡처, 몬스터 수는 목표 상단 500을 살짝 넘긴 채 종료)**

| 항목 | 수정 전 | Health/MonsterAI Reset 수정 후 | 컬렉션 사전할당 수정 후 |
|---|---|---|---|
| GC Alloc 총량 | 411건 (최대 크기 50.5KB) | 233건 (최대 크기 50.4KB) | 213건 (최대 크기 25.2KB) |
| GC Collect | 캡처 중 1회 발생 | 미발생 | 미발생 |
| Frame Time (Median) | 1.082ms | 1.006ms | 1.000ms |
| Scripts (mean) | 0.597ms | 0.546ms | _(미기록)_ |

두 수정 모두 GC Alloc을 줄였지만 램프업 구간에서 완전히 0으로 만들지는 못했다. 남은
건수는 `Instantiate.Produce`(524마리가 프리워밍 500을 넘겨 풀이 소진된 정상 경로, 버그
아님)와, 컬렉션을 정확히 500으로 사전할당해도 500을 넘기면 여전히 발생하는 리사이즈
잔여분으로 판단된다 — 이는 설계 목표(200~500) 범위를 살짝 벗어난 스트레스 테스트의
경계 케이스라 추가 대응은 하지 않았다.

## 핵심 결론: 정상 상태(Steady State)는 이미 GC Alloc 0건

램프업 구간에 남은 GC Alloc은 전부 "스폰되는 그 프레임"에만 발생하는 1회성 비용이다.
CLAUDE.md의 목표("Update 루프 GC Alloc 0B/frame")는 스폰이 아니라 매 프레임 반복되는
정상 상태 비용을 가리키므로, `MonsterSpawnTester`가 500마리에서 자동 정지한 뒤 몬스터들이
추격/공격/스킬 피격으로 죽는 것까지 섞인 구간만 별도로 캡처했다.

| 항목 | 값 |
|---|---|
| Frame Time (Median) | 1.024ms |
| Frame Time (Max) | 2.133ms |
| Frame Time (Min) | 0.845ms |
| Scripts (mean) | 0.550ms |
| GC Alloc | **No markers found (0건, Mono.JIT 아티팩트조차 없음)** |
| GC Collect | No markers found |

## 목표 수치 대조 (CLAUDE.md 기준)

- [x] 몬스터 200~500마리 동시 스폰 시 60fps 안정 유지 — Frame Time Median 1.024ms /
      Max 2.133ms, 예산(16.6ms) 대비 약 8~16배 여유
- [x] Update 루프 GC Alloc 0B/frame — 정상 상태 캡처에서 Allocations 패널 전체가
      "No markers found"로 완전한 0건 확인

## 관찰 및 해석

- 이번에 발견/수정한 두 GC Alloc 버그(Health/MonsterAI 재할당, 컬렉션 미사전할당)는
  "몬스터가 스폰되는 순간"에만 영향을 주는 문제였고, 정상 게임플레이(추격/공격/스킬/사망)
  중에는 애초에 GC Alloc이 없었다 — 다만 램프업 구간도 실제 플레이 중 발생 가능한
  상황(웨이브가 겹쳐 스폰될 때)이므로 수정 자체는 유효하다.
- 정상 상태 Frame Time 상위 마커는 `MonsterMovementResolver`의 메인 스레드 O(n)
  위치 스냅샷+버킷 구성 패스로 추정되지만, Scripts mean이 0.550ms에 불과해 60fps
  예산 대비 병목으로 볼 수준이 아니다.
- 목표 수치(200~500마리, 60fps, GC 0B/frame)를 모두 실측으로 달성 확인했다. 8주차
  로드맵의 "최종 목표 수치 검증" 항목의 근거 데이터로 이 캡처를 재사용할 수 있다.
- 1주차(스켈레톤 상태)는 GC Alloc이 줄었음에도(415→189) 원인 불명의 프레임 타임 증가를
  미해결로 남긴 채 종료됐었다. 그 미해결 이슈(Collider 재삽입 비용 의심)는 4주차
  CapsuleCollider 제거로 이미 해소됐고, 전투 시스템 추가로 새로 생긴 GC 원인
  (Health/MonsterAI 재할당, 컬렉션 미사전할당)은 이번 6주차에서 발견해 모두 해결했다.
