# CLAUDE.md

이 파일은 Claude Code가 이 프로젝트에서 작업할 때 참조하는 컨텍스트 문서입니다.

## 프로젝트 개요

- **프로젝트명**: Legion Break
- **목적**: 이직/취업용 포트폴리오 (Unity 클라이언트 개발자 포지션)
- **장르**: 탑다운 핵앤슬래시 로그라이트
- **개발 기간**: 2~3개월
- **핵심 어필 포인트**: 대규모 몬스터 스폰 상황에서의 성능 최적화 + 계층 분리 아키텍처 설계 역량

이 프로젝트는 완성도 높은 게임 자체보다 **"왜 이렇게 설계했는가"를 코드와 문서로 증명하는 것**이 우선순위입니다. 모든 기능 구현 시 구현 이유를 커밋 메시지나 코드 주석에 남길 것.

## 기술 스택

- **엔진**: Unity 6.3 LTS
- **렌더 파이프라인**: URP
- **DI**: VContainer
- **비동기 처리**: UniTask (코루틴 대신 async/await 우선 사용)
- **이벤트 스트림**: R3 (선택적 사용)
- **리소스 관리**: Addressables
- **성능 최적화**: Unity Job System + Burst Compiler + Collections + Mathematics
- **입력**: Input System (레거시 Input 클래스 사용 금지)
- **버전 관리**: Git + Git LFS

## 아키텍처 방향

### 하이브리드 구조 (DOTS/ECS 풀 전환 아님)

- 게임 로직(스킬, 상호작용, 상태 관리)은 **기존 MonoBehaviour/DI 기반 아키텍처** 유지
- 연산량이 큰 부분(이동, 타겟팅, 충돌 판정)만 **Job System + Burst로 병렬화**
- 이유: 리스크 관리 + "병목을 정확히 진단해서 필요한 곳에만 기술 적용"이라는 트레이드오프 판단을 보여주기 위함
- 순수 ECS 전체 전환은 하지 않는다. 이 프로젝트 범위에서 과도한 엔지니어링으로 간주.

### 계층 분리 원칙 (Layer-based, Unity 관례와 다름)

Unity의 일반적인 타입 기준(`Scripts/`, `Prefabs/` 등) 폴더 구조 대신, **아키텍처 계층 기준**으로 분리한다.

```
Assets/
  _Project/
    Presentation/   (View, UI, MonoBehaviour, Prefab)
    Application/    (Presenter, Service, UseCase, 이벤트 버스)
    Domain/         (순수 C# 로직, 전투 모델 — UnityEngine 참조 절대 금지)
    Infrastructure/ (Addressables 로더, 저장소, 외부 IO)
    Data/           (ScriptableObject 에셋)
    Editor/         (커스텀 에디터 툴)
    Tests/          (EditMode / PlayMode 테스트)
  ThirdParty/       (외부 라이브러리 — 수정 금지 영역)
```

### asmdef 의존성 규칙 (컴파일 타임 강제)

각 계층 폴더에는 `.asmdef`가 있으며, 다음 참조 규칙을 반드시 지킨다:

- `Domain.asmdef`: 어떤 계층도 참조하지 않음. **UnityEngine 참조 절대 금지.** 순수 C#만 허용.
- `Application.asmdef`: `Domain`, `Data` 참조 가능.
- `Infrastructure.asmdef`: `Domain`, `Application`, `Data` 참조 가능. `Presentation`을 참조해서는 안 됨.
- `Presentation.asmdef`(일반 View/Controller): `Application`, `Domain` 참조 가능. `Data`는 참조하지 않음 — ScriptableObject 에셋을 직접 드는 코드가 없기 때문.
- `Presentation.Composition.asmdef`(`GameLifetimeScope` 등 Composition Root 전용, `Presentation`과 별도 어셈블리): `Domain`, `Application`, `Infrastructure`, `Presentation`, `Data` 전부 참조 가능. Composition Root는 DI 등록을 위해 모든 계층과 ScriptableObject 에셋(`Data`)을 동시에 알아야 하는 유일한 지점이라 예외적으로 폭넓게 허용한다.
- `Data`(ScriptableObject 정의)는 계층이 아니라 순수 데이터 취급이라, 실제로 에셋을 필드로 들어야 하는 곳(Application의 팩토리, Infrastructure의 어댑터, Presentation.Composition의 `GameLifetimeScope`)만 참조한다. (2026-07-22: `GameLifetimeScope`가 `SkillData`/`CombatBalanceData`를 Inspector 직렬화 필드로 들어야 해서 `Presentation.Composition.asmdef`에 `Data` 참조를 추가. 처음엔 `GameLifetimeScope`가 일반 `Presentation.asmdef`에 속하는 줄 알고 그쪽에 추가했다가, `Composition` 폴더가 별도 asmdef로 분리되어 있다는 걸 뒤늦게 확인하고 원복 후 올바른 위치로 옮김)
- 역방향 참조(예: Domain이 Presentation을 참조)는 발생 시 즉시 리팩토링 대상.

Claude Code는 새 스크립트를 생성할 때 반드시 이 규칙에 맞는 폴더에 배치하고, Domain 계층 파일에는 `using UnityEngine`을 포함하지 않는다.

### Domain 계층 분리 기준 (2026-07-09 결정)

Domain 계층은 **실제 도메인 규칙**(데미지 공식, 크리티컬/속성 상성, 스킬 쿨다운 판정 등 분기·밸런스가 있는 로직)에만 적용한다. 분기나 밸런스 수치 없이 결과가 항상 같은 범용 수학·유틸리티 연산은 Domain으로 분리하지 않고 Application 계층에 인라인한다.

- 배경: 초기 플레이어 이동량 계산(`normalize(input) * speed * deltaTime`)을 `Domain.Movement.IMovementCalculator`로 분리했었으나, 이는 도메인 지식이 아닌 범용 벡터 연산이라 계층 분리의 가치를 증명하지 못하는 오버엔지니어링으로 판단하여 제거했다. 계산 로직은 `Application/Movement/PlayerMoveUseCase.cs`에 인라인되어 있다.
- 앞으로 Domain 계층은 스킬 데미지 공식, 몬스터 AI 판단 로직처럼 실제 밸런스·분기가 있는 로직부터 사용한다.
- 판단 기준: (1) Domain 순수성이 실질적으로 필요한가(테스트 대상이 되는 밸런스 로직인가), (2) 실제로 교체 가능한 지점을 보여주려는 것인가. 둘 다 아니면 인터페이스·계층을 추가하지 않는다.

### Application 계층의 UnityEngine 타입 사용 (2026-07-09 결정)

Application 계층은 `UnityEngine.Vector2` 등 UnityEngine 타입을 자유롭게 사용한다. `System.Numerics.Vector2`로 감쌀 필요 없다.

- 배경: `IPlayerMotor`, `IMonsterSpawner` 등 Application 포트가 `System.Numerics.Vector2`를 파라미터로 쓰고 있었는데, 이는 위에서 제거한 `Domain.Movement.IMovementCalculator`(Domain은 UnityEngine 참조 절대 금지) 설계 당시의 컨벤션이 관성적으로 남은 것이었다. Domain 계층 결정과 함께 재검토 없이 방치되어 있던 것을 확인하고 전부 `UnityEngine.Vector2`로 치환했다.
- asmdef 레벨에서도 `Application.asmdef`는 `noEngineReferences: false`라 UnityEngine 참조가 컴파일러로 막혀있지 않다. UnityEngine 비참조는 Domain에만 강제되는 규칙이며 Application까지 확장 적용할 근거가 없다.
- 앞으로 Application/Infrastructure/Presentation 계층 간 벡터·수치 타입 전달에는 `UnityEngine.Vector2`/`Vector3`를 그대로 쓴다.

## 성능 최적화 목표 (수치 기반)

- 몬스터 **200~500마리** 동시 스폰 시 **60fps 안정 유지**
- Update 루프 **GC Alloc 0B/frame**
- 충돌 판정: O(n²) 전수 검사 → 공간 분할(Spatial Hashing)로 O(n) 근사

모든 최적화 작업은 적용 전/후 프로파일러 수치를 캡처하여 `docs/profiling/`에 기록한다. (Before/After 비교가 없는 최적화 커밋은 미완성으로 간주)

## 3개월 로드맵

| 시기 | 내용 |
|---|---|
| 1개월차 | DI 컨테이너(VContainer), MVP UI 프레임워크, ScriptableObject 데이터 설계, 이벤트 버스, Addressables 세팅 |
| 2개월차 | 전투/스킬 시스템 + 최적화 파이프라인 (아래 상세) |
| 3개월차 | 커스텀 에디터 툴, README/아키텍처 다이어그램, 플레이 영상, 프로파일링 리포트 |

### 2개월차 최적화 파이프라인 (주차별)

1. **1주차**: 오브젝트 풀링 도입 + Before/After 베이스라인 측정
2. **2주차**: Job System으로 이동/타겟팅 연산 병렬화 (`IJobParallelFor` + Burst)
3. **3주차**: Spatial Hashing 직접 구현으로 충돌 판정 최적화
4. **4주차**: GPU Instancing, 애니메이션 LOD 등 렌더링 최적화 + 목표 수치 검증

1~4주차는 실제 몬스터 AI/스킬 발동 흐름 없이(수명 타이머 + 단순 Seek 이동만 있는 스켈레톤 상태로) 각 최적화 기법 자체를 검증한 1차 패스였다(`docs/profiling/week1~4`에 기록). 5주차부터는 전투 시스템을 실제로 "동작하는 상태"로 완성한 뒤, 그 위에서 동일한 최적화 기법들을 다시 적용·재측정하는 2차 패스로 로드맵을 확장한다.

5. **5주차 — 전투/스킬 시스템 완성 & 베이스라인 확보**: 전투 시스템 자체를 먼저 "동작하는 상태"로 완성해야 최적화 대상이 생긴다.
   - 몬스터 AI 기초 (FSM: Idle → Chase → Attack → Dead)
   - `MovementResolver`, `WalkableGrid`, `FlowFieldGenerator`를 실제 몬스터 이동에 연결
   - 스킬 발동 → `SkillRangeChecker` → 데미지 적용까지 전체 흐름 연결
   - 몬스터 스폰 시스템 (웨이브 단위, 스폰 포인트 관리)
   - 최적화 없이 몬스터 200~500마리 스폰 테스트 → 프로파일러로 베이스라인 캡처
     - Frame Debugger, CPU Profiler, Memory Profiler 스크린샷 저장 (`docs/profiling/baseline/`)
     - 이 시점의 fps, GC Alloc/frame, 드로우콜 수치를 표로 기록 (Before 수치 확정)

6. **6주차 — 오브젝트 풀링 & GC 최소화**
   - `MonsterPool`, `EffectPool`, `ProjectilePool` 등 풀링 인프라 구축
   - `Instantiate`/`Destroy` 직접 호출 코드 전수 검색 후 풀링으로 교체
   - UniTask 기반 비동기 로직에서 불필요한 클로저/박싱으로 인한 GC Alloc 점검
   - `foreach` 대신 `for`, `List` 대신 `NativeArray`/배열 사용 등 Update 루프 GC 원인 제거
   - 재측정 → 풀링 적용 후 수치를 베이스라인과 비교해 표로 정리 (`docs/profiling/after-pooling/`)
   - `perf:` 커밋 메시지에 Before/After 수치 명시

7. **7주차 — Job System + Burst 병렬화**
   - 몬스터 이동/타겟팅 연산을 `IJobParallelFor`로 이전
   - `MovementResolver.ResolveOverlap`을 Job으로 감싸기 (NativeArray 입출력 구조로 변환)
   - `FlowFieldGenerator`의 셀 단위 계산 병렬화 검토 (BFS 특성상 완전 병렬화가 까다로운 지점 — 레이어별로 나눠 처리하는 방식 조사)
   - `[BurstCompile]` 적용 및 Burst Inspector로 컴파일 결과 확인 (벡터화 여부 등)
   - 메인 스레드 vs Job 스레드 분산 프로파일링 캡처 (Timeline 뷰)
   - 재측정 → Job System 적용 후 수치 기록

8. **8주차 — Spatial Hashing 충돌 판정 & 렌더링 최적화**
   - Spatial Hash 그리드 직접 구현 (셀 크기 결정 근거도 문서화 — 몬스터 평균 크기/밀도 기준)
   - `ITargetQueryService` 구현체를 Physics 기반 → Spatial Hash 기반으로 교체 (Domain/Application 코드 무변경 확인 — 인터페이스 분리 효과 증명)
   - O(n²) 전수 검사 대비 처리 시간 비교 측정
   - GPU Instancing 적용 (동일 몬스터 메쉬 배칭)
   - 애니메이션 LOD (카메라 거리 기반 프레임 스킵 또는 애니메이션 컬링)
   - 최종 목표 수치(200~500마리, 60fps, GC 0B) 달성 여부 검증 및 최종 프로파일링 리포트 정리

### 월말 체크포인트

- `docs/profiling/`에 베이스라인 → 풀링 → Job System → Spatial Hash 단계별 Before/After 수치가 시계열로 정리되어 있는지 확인
- 각 최적화 단계별로 "왜 이 순서로 적용했는지"(가장 GC 임팩트 큰 것부터 → 연산 병목 → 판정 알고리즘 순) 논리를 README에 문단으로 정리
- 목표 수치 미달성 시, 어느 병목이 남았는지 프로파일러로 진단해 3개월차 초반에 보완할지 결정

## Git 컨벤션

- 브랜치: `main`(항상 빌드 가능 상태) + `feature/*`
- 커밋 메시지: `feat:`, `fix:`, `chore:`, `refactor:`, `perf:`, `docs:` 접두어 사용
- 성능 관련 커밋은 `perf:` 사용하고 커밋 메시지 본문에 Before/After 수치 명시

## 코딩 컨벤션

- 코루틴 대신 UniTask 우선 사용 (GC 압박 최소화 목적과 일치)
- Domain 계층 클래스는 인터페이스로 외부에 노출하고, Presentation은 구체 클래스를 직접 참조하지 않음
- ScriptableObject로 관리 가능한 값(스킬 스탯, 밸런스 수치)은 하드코딩 금지
- Job System 관련 구조체는 `[BurstCompile]` 적용 여부를 항상 확인

## 현재 상태 / 다음 작업

- [x] 장르, 기술 방향(하이브리드 최적화), 폴더 구조 결정
- [x] Unity 6.3 LTS 프로젝트 생성, 패키지/외부 라이브러리 설치 계획 수립
- [x] asmdef 파일별 의존성 실제 설정
- [x] VContainer LifetimeScope 배치 (부트스트랩 씬 분리 여부 결정)
- [x] 더미 몬스터 스폰 + 오브젝트 풀링 파이프라인 스켈레톤 구현 (`InstantiateMonsterSpawner`/`PooledMonsterSpawner`)
- [x] Before/After 프로파일링 1차 측정, `docs/profiling/week1-object-pooling.md` 기록 (GC는 감소했으나 프레임 타임 증가하는 이상 현상 발견, 원인은 백로그로 보류하고 1주차는 현재 수치로 종료)
- [x] (백로그) 풀링 프레임 타임 증가 원인 조사 (Collider 재삽입 비용 의심) — 4주차에 `PooledMonsterSpawner`의 미사용 `CapsuleCollider`를 제거하고 재측정. 다만 몬스터 245~450마리 규모에서는 Frame Time Median이 0.868~0.877ms 범위 안에서 그대로라 측정 가능한 개선으로는 이어지지 않음(2주차 Job System과 같은 성격의 결과). Collider 제거 자체는 불필요한 PhysX 오버헤드 요소 정리로 유효해 그대로 반영.
- [x] 스킬 기본 데이터 구조 설계. Domain 계층에 실제 코드가 생긴 첫 사례로, `Domain/Skills`(`Skill`, `ISkillDamageCalculator`, `SkillDamageCalculator` — 기본 데미지 × 크리티컬 배율 공식)와 이를 뒷받침하는 `Data`(`SkillData`, `CombatBalanceData` ScriptableObject) + `Application/Skills`(`SkillFactory`)로 계층 전체를 관통하는 구조로 설계했다. `Tests/Domain.Tests/SkillDamageCalculatorTests.cs`, `Tests/Application.Tests/SkillFactoryTests.cs`로 Domain 순수성/테스트 가능성도 실증.
  - 최초에는 속성 조합(`SkillElement`, `ElementCombination`, `ISkillCombinationTable`, `SkillCombinationTableAdapter`, `ElementComboTableData`)까지 함께 설계했으나, 실제 소비처(스킬 시전 UseCase)가 없는 상태에서 조합 규칙까지 먼저 만드는 건 오버엔지니어링으로 판단해 되돌렸다. 조합 기능은 필요해지는 시점(실제 스킬을 플레이 가능하게 연결할 때)에 다시 추가한다.
  - 실제 스킬 시전 UseCase/Presenter 연결과 VContainer 등록은 2개월차 전투/스킬 시스템 구현 시점으로 보류.
- [x] Job System 이동/타겟팅 코드 구현 (`MonsterSeekJob` + `JobMonsterMovementSystem`, Before 대조군 `MonoMonsterMovementSystem` 포함). 몬스터는 이전까지 이동/타겟팅 로직이 전혀 없었으므로(수명 타이머만 있는 스텁) 이번에 플레이어를 향한 Seek 이동을 처음 설계해 추가했다.
- [x] 2주차 Before/After 프로파일링 측정 및 `docs/profiling/week2-job-system.md` 완성. 몬스터 ~100마리 규모에서는 Job 병렬화가 이득을 주지 못하고 TransformAccessArray 관리 오버헤드로 오히려 미세하게 불리함을 확인(Scripts 0.339ms→0.344ms). 1차 측정을 에디터에 Profiler를 붙여 진행했다가 EditorLoop 오버헤드와 GC Alloc 계측 아티팩트(415건→실제 빌드에서는 0~2건)를 발견해 Development Build로 재측정함 — "GC Alloc은 반드시 빌드에서 검증" 교훈을 문서에 남김
- [x] 3주차: 로드맵 원안(기존 O(n²) 충돌 판정 → Spatial Hashing)의 전제와 달리 실제로는 충돌 판정 로직 자체가 없었음을 확인(2주차까지 몬스터 이동은 플레이어 단일 좌표를 향한 Seek뿐이라 O(n)). 몬스터-몬스터 겹침 회피(separation)를 신규 구현해 Before(`BruteForceMonsterSeparationSystem`, O(n²))/After(`SpatialHashMonsterSeparationSystem`, 셀 그리드 O(n) 근사)로 비교. Job/Burst는 2주차에서 이미 다뤘으므로 이번엔 섞지 않고 알고리즘 복잡도 변화만 단독으로 측정. 몬스터 200~500마리 구간에서 Scripts 1.197ms→0.528ms(약 55.9% 감소), GC Alloc 8건→0건 확인, `docs/profiling/week3-spatial-hashing.md`에 기록
- [x] 4주차: 로드맵 원안(GPU Instancing)을 문자 그대로 적용하려면 Unity 6 URP의 GPU Resident Drawer(파이프라인 에셋 레벨)가 필요하다는 걸 확인했으나, 활성화 시 `BatchRendererGroup`이 DOTS(Entities Graphics) 셰이더 변형(`DOTS_INSTANCING_ON`)을 요구해 런타임 크래시가 발생. "순수 ECS 전체 전환은 하지 않는다"는 원칙에 따라 DOTS 계열인 GPU Resident Drawer는 철회하고 설정을 원복. 대신 몬스터 전용 `Monster.mat`(URP/Lit, GPU Instancing 플래그 on)을 `sharedMaterial`로 공유 할당해 SRP Batcher 수준의 개선(Draw Calls 462→436, SetPass Calls 16→12)을 확보. 이 과정에서 머티리얼 교체 중간 단계의 의도치 않은 그림자 회귀(Shadow Casters 2→461, Before의 Unity 내장 Default-Material이 URP 비호환이라 애초에 그림자를 못 만들고 있었음)를 발견해 `shadowCastingMode = Off`로 통제. 이어서 1주차 백로그(미사용 CapsuleCollider 제거)를 처리하고, 정확한 동시 개체 수 확인을 위해 `MonsterCountDisplay` 디버그 HUD를 추가했다가 HUD 자신의 GC 기여(332건)를 발견해 갱신 주기 스로틀(0.25s)로 22건까지 줄임. 최종 통합 검증(풀링+Job+Spatial Hashing+렌더링, HUD는 비활성화)에서 Frame Time Median 0.854ms(60fps 예산 대비 약 19배 여유), GC Alloc 12건 전량 `Mono.JIT` 계측 아티팩트(Update 루프 실질 0B/frame)로 CLAUDE.md 목표 수치 두 가지 모두 달성 확인. `docs/profiling/week4-rendering.md`에 기록
- [x] MVP UI 프레임워크 첫 적용. 4주차에 만든 `MonsterCountDisplay`(View와 Presenter 로직이 한 MonoBehaviour에 섞여있던 디버그 HUD)를 계층 분리 원칙("Presentation: View", "Application: Presenter")에 맞게 `Presentation/Spawning/MonsterCountView`(그리기만 담당)와 `Application/UI/MonsterCountPresenter`(갱신 주기·변경 감지 판단, UnityEngine.Object 의존 없는 순수 C#)로 분리했다. `Tests/Application.Tests/MonsterCountPresenterTests.cs`(4개)로 Presenter가 View/Spawner 없이도 완전히 단위 테스트 가능함을 실증 — MVP 분리가 실제로 테스트 용이성을 높인다는 걸 보여주는 사례.
  - 최초 구현에서는 View와 Presenter를 둘 다 VContainer가 생성하게 했다가 런타임에 `InvalidOperationException`(`Lazy<T>`가 자기 자신을 참조)이 발생했다. Presenter 생성자가 `IMonsterCountView`(=View 자신)를 요구하는데 View도 Presenter를 주입받아야 해서 순환 의존이 된 것. View가 `IMonsterSpawner`만 DI로 받고 Presenter는 `new MonsterCountPresenter(spawner, this)`로 직접 생성하도록 바꿔 순환을 끊었다. Presenter는 Unity 생명주기가 없어 View의 Update에서 매 프레임 `Tick`을 호출해 구동한다.
- [x] Addressables 세팅. `PooledMonsterSpawner`가 `GameObject.CreatePrimitive`로 만들던 몬스터를 실제 프리팹(`Presentation/Spawning/Monster.prefab` — Capsule + `Monster.mat` + `DummyMonsterView`, Collider 없음, 그림자 Off)으로 교체하고 Addressables로 로드하도록 바꿨다. `Application/Spawning/IMonsterPrefabProvider`(포트) + `Infrastructure/Spawning/AddressableMonsterPrefabProvider`(`AssetReferenceGameObject` 기반 구현, 문자열 키 없이 Inspector에 에셋을 할당하면 자동으로 Addressable 그룹에 등록됨)로 계층을 나눴고, `PooledMonsterSpawner`의 프리워밍을 `UniTaskVoid`로 비동기화했다 — 이 프로젝트에서 UniTask 첫 실사용 사례.
  - `AsyncOperationHandle<GameObject>`를 곧바로 `await`하면 암묵적으로 비제네릭 `AsyncOperationHandle`로 변환되며 결과값 없는 `GetAwaiter()`가 선택되어 `await` 표현식이 `void`가 되는 컴파일 에러(CS0029)가 발생했다. UniTask의 선택적 Addressables 연동(`ToUniTask()`)도 같은 이유로 기대한 제네릭 오버로드로 해석되지 않았다. 최종적으로 핸들을 변수로 들고 있다가 `await handle;`로 완료만 기다린 뒤 `handle.Result`를 직접 읽는 방식으로 우회해 해결했다.
  - 4주차 렌더링 최적화 결과(머티리얼 공유, 그림자 Off, Collider 없음)를 스폰 코드가 아니라 프리팹 자체에 미리 구성해뒀다 — `PooledMonsterSpawner`의 `CreatePooledInstance`가 매번 다시 설정할 필요가 없어져 코드가 더 단순해졌다.
- [x] 이벤트 버스 도입. `R3.Unity` 패키지가 asmdef에 참조만 걸려있고 실제 사용처가 없던 상태였어서, 이미 동작 중이던 `MonsterCountPresenter`의 폴링(`IMonsterSpawner.ActiveCount`를 매 Tick 직접 읽는 방식)을 이벤트 구독 방식으로 바꿔 바로 연결했다(실제 소비처 없이 버스만 먼저 만드는 건 스킬 조합 때와 같은 실수라 지양). `Application/Events/IEventBus`(타입 기반 발행-구독 포트) + `EventBus`(타입별 `R3.Subject`를 지연 생성해 보관하는 기본 구현, Domain/Infrastructure 어디에도 속하지 않는 범용 Application 서비스라 별도 어댑터 없이 Application에 바로 구현) + `MonsterCountChangedEvent`. `PooledMonsterSpawner`가 스폰/디스폰 시 `IEventBus.Publish`로 개수를 발행하고, `MonsterCountPresenter`는 `IEventBus.Receive<T>().Subscribe(...)`로 구독해 최신값만 필드에 저장한다.
  - 이벤트가 올 때마다 View를 바로 갱신하면 4주차에 실측했던 것과 같은 문제(스폰 스트레스 테스트에서 초당 수십 회 갱신 → GC Alloc 0B/frame 오염)가 재발하므로, View 갱신 자체는 기존 Tick 기반 0.25초 스로틀을 그대로 유지했다 — 이벤트 구독은 "값을 받는 시점"만 바꿨고 "실제로 그리는 시점"의 스로틀 로직은 그대로 재사용했다.
  - Presenter가 `IDisposable`을 구현해 구독을 해제할 수 있게 하고, View의 `OnDestroy`에서 `Dispose`를 호출하도록 했다. `Tests/Application.Tests/MonsterCountPresenterTests.cs`에 구독 해제 테스트를 추가하고, 나머지 테스트도 `IMonsterSpawner` 폴백 대신 `EventBus`에 직접 이벤트를 발행하는 방식으로 다시 작성했다.
  - `Application.Tests.asmdef`에 `R3.Unity` 참조가 빠져있어 `IEventBus`(내부적으로 `R3.Observable<T>` 사용)를 참조하자마자 컴파일 에러가 날 수 있었던 것을 미리 추가해 방지했다.
  - `MonsterCountPresenter.cs`에 `using R3;`를 빠뜨려 `CS1660`(람다를 `Observer<T>`로 변환할 수 없음) 에러가 실제로 발생했다. R3의 `Subscribe(Action<T>)`는 확장 메서드라 네임스페이스를 열어야 보이는데, 이게 없으면 컴파일러가 `Observable<T>`의 기본 멤버인 `Subscribe(Observer<T>)`만 찾아 람다를 거부한다. `using R3;` 추가로 해결.
- [x] 플레이어 스킬 시전 UseCase 구현. 1개월차에 만든 스킬 데이터 구조(`Skill`, `SkillDamageCalculator`, `SkillFactory`)의 첫 실제 소비처로, `Skill`에 쿨다운 상태(`RemainingCooldown`/`IsReady`/`Tick`/`ConsumeCooldown`)를 추가하고 `Application/Skills/IPlayerSkillCastUseCase`+`PlayerSkillCastUseCase`(쿨다운 게이트 → 데미지 계산 → 쿨다운 소모)를 신규 작성했다. 쿨다운 판정을 Domain에 둔 건 "Domain 계층 분리 기준" 문서가 "스킬 쿨다운 판정"을 도메인 예시로 명시했기 때문. 입력은 `Presentation/Skills/PlayerSkillInputController`(마우스 좌클릭, Domain/Application에 이미 있던 `Skills` 폴더 구조를 Presentation에도 대응)가 담당하고, `GameLifetimeScope`에 스킬 관련 등록이 이번에 처음 생겼다(`SkillData`/`CombatBalanceData`를 Inspector 직렬화 필드로 받아 `SkillFactory.Create`로 변환한 `Skill` 인스턴스 하나를 등록 — 다중 스킬 슬롯은 실제 소비처가 생기기 전까진 만들지 않음, 속성 조합 되돌린 것과 같은 이유). `Tests/Domain.Tests/SkillTests.cs`, `Tests/Application.Tests/PlayerSkillCastUseCaseTests.cs`로 쿨다운 게이트를 검증.
  - 크리티컬 판정(`SkillDamageCalculator.Calculate`의 `isCritical` 파라미터)은 이번 범위에서 의도적으로 제외하고 `false` 고정으로 넘긴다 — 사용자 결정: 시전 흐름부터 완성하고 확률/난수 판정 시스템은 별도 작업으로 분리. 몬스터에게 데미지를 실제로 적용하는 것도 몬스터 쪽에 개별 인스턴스 참조/HP 개념이 아예 없어 함께 보류했다. `IEventBus`로 발행하지 않은 이유도 같다 — "실제 소비처 없이 버스부터 만들지 않는다"를 이미 두 번(스킬 속성 조합, 이벤트 버스 자체) 지킨 것과 같은 원칙이라, `Execute()` 결과를 Presentation이 직접 받아 로그로만 확인한다.
- [x] 몬스터 피격/HP 시스템 구현. 위 백로그의 "몬스터 피격/HP 시스템과의 연결"을 실제로 채웠다. `Domain/Monsters/MonsterHealth`(체력 판정 — `Skill`의 쿨다운 판정과 같은 이유로 Domain에 상태로 둠)를 신규 작성하고, `Data/MonsterData`(체력 밸런스 수치, SkillData/CombatBalanceData와 동일 패턴)와 `SkillData.Range`(스킬 사거리)를 추가했다. `IMonsterSpawner`에 `ApplyDamageInRange(center, radius, damage)`를 추가해 `PlayerSkillCastUseCase.Execute(Vector2 targetPoint)`가 실제로 데미지를 적용하도록 연결했다.
  - 타겟팅 방식은 사용자 결정으로 **마우스 클릭 지점 기준 스플래시**(클릭 지점 중심으로 스킬 Range 반경 안의 몬스터 전부가 피격)로 정했다. 클릭한 화면 좌표를 `Camera.ScreenPointToRay` + `Plane.Raycast`로 Y=0 지면 평면과 수학적으로 교차시켜 월드 좌표를 구한다 — `Physics.Raycast`/`OverlapSphere`가 아니라 순수 평면 수학이라 몬스터에 Collider가 필요 없다. 4주차에 미사용 `CapsuleCollider`를 의도적으로 제거한 최적화를 그대로 유지하면서 커서 조준을 만족시키는 선택.
  - `PooledMonsterSpawner`는 지금까지 `Stack<DummyMonsterView>`(풀)만 있고 활성 몬스터 목록이 없었다. `JobMonsterMovementSystem`이 이미 쓰던 **스왑 제거 + 인덱스 Dictionary** 패턴을 그대로 재사용해 활성 목록(`_activeViews`/`_activeIndexByView`)을 추가했다 — 새 패턴을 만들지 않고 이미 검증된 것을 재사용.
  - `ApplyDamageInRange`는 수집(반경 판정) → 적용(`TakeDamage` 호출) 2단계로 나눴다. `TakeDamage`가 죽음 판정 시 즉시 활성 목록을 스왑 제거하는데, 그 목록을 순회하면서 동시에 제거하면 인덱스가 꼬이기 때문 — 재사용 버퍼(`_damageQueryBuffer`, `Clear()`만 하고 매번 재할당하지 않음)에 먼저 모아둔 뒤 적용해 이 문제를 원천적으로 없앴다.
  - `ApplyDamageInRange`의 반경 판정은 3주차에 만든 `SpatialHashMonsterSeparationSystem`을 재사용하지 않고 단순 선형 순회로 구현했다. 그 그리드는 "몬스터 기준" 이웃 조회를 매 프레임 반복하는 걸 전제로 비용을 상각하는 구조인데, 스킬 시전은 쿨다운으로 제한된 이벤트성 호출(초당 1회 수준)이라 조회 빈도 자체가 다르고, 조회 기준도 몬스터가 아니라 임의의 클릭 지점이라 쿼리 형태도 다르다. 몬스터 200~500마리 규모에서 1회성 선형 순회는 이미 충분히 싸므로, 병목이 확인되지 않은 곳에 공간 분할을 끌어오는 건 "병목을 정확히 진단해서 필요한 곳에만 기술 적용"이라는 이 프로젝트의 트레이드오프 원칙과 어긋난다고 판단했다.
  - `DummyMonsterView`의 사망 콜백은 수명 만료(`_onLifetimeEnded`)와 피격 사망이 동일한 반환 흐름(Unregister → 비활성화 → 풀 반환)을 타야 해서, 필드명을 `_onDeactivated`로 일반화하고 두 경로가 같은 콜백을 공유하게 했다. 새 사망 경로를 따로 만들지 않음.
  - (백로그) 몬스터 사망 시 이펙트/보상(경험치, 드롭 등)은 아직 없음 — 필요해지는 시점에 `MonsterHealth.IsDead` 판정 지점을 소비처로 연결.
- [x] `DummyMonsterView` → `MonsterView` 네이밍 정리. 위 백로그로 남겨뒀던 항목을 처리했다. HP 상태를 실제로 갖게 된 이후 "더미"라는 이름이 더는 정확하지 않아 개명했고, 참조하는 8개 파일(`IMonsterMovementSystem`/`IMonsterSeparationSystem`/`JobMonsterMovementSystem`/`MonoMonsterMovementSystem`/`SpatialHashMonsterSeparationSystem`/`BruteForceMonsterSeparationSystem`/`PooledMonsterSpawner`/`InstantiateMonsterSpawner`)의 심볼을 함께 교체했다. `docs/profiling/week1-object-pooling.md`처럼 그 시점 코드 상태를 기록한 과거 문서는 의도적으로 그대로 뒀다(역사적 사실을 소급 수정하지 않음).
  - 파일 리네임은 `Write`로 새로 만들고 기존 파일을 지우는 대신 `git mv`로 `.cs`와 `.cs.meta`를 함께 옮겨 GUID를 보존했다 — GUID가 바뀌면 `Monster.prefab`의 스크립트 바인딩이 끊어져 "Missing Script"가 뜬다.
  - GUID만 보존한다고 끝이 아니었다: 이 프로젝트는 계층마다 커스텀 asmdef를 쓰기 때문에, Unity가 프리팹의 MonoBehaviour 컴포넌트에 `m_EditorClassIdentifier: LegionBreak.Infrastructure::LegionBreak.Infrastructure.Spawning.DummyMonsterView`라는 어셈블리:네임스페이스.클래스명 문자열을 추가로 저장하고 있었다(기본 Assembly-CSharp만 쓰는 프로젝트라면 잘 안 보이는 필드). GUID는 그대로라도 이 문자열이 옛 클래스명을 가리키면 깨질 수 있어, `Monster.prefab`의 해당 줄을 텍스트로 직접 `MonsterView`로 교체했다 — "커스텀 asmdef 프로젝트에서 스크립트 리네임은 GUID 보존만으로 끝나지 않는다"는 게 이번에 새로 확인한 사실.
- [x] 크리티컬 확률 판정 시스템 구현. 위 백로그로 남겨뒀던 `SkillDamageCalculator.Calculate`의 `isCritical: false` 고정을 실제 판정으로 교체했다. `Domain/Skills/ICriticalHitJudge`+`CriticalHitJudge`(확률과 난수값을 비교해 크리티컬인지만 판정하는 순수 도메인 규칙 — "크리티컬... 판정"이 Domain 분리 기준의 명시적 예시라서)와 `Application/Skills/IRandomProvider`+`RandomProvider`(`System.Random` 기반, 엔트로피 생성은 도메인 지식이 아니므로 Application이 담당)로 역할을 나눴다. `RandomProvider`는 `EventBus` 때와 같은 이유로 별도 Infrastructure 어댑터 없이 Application에 바로 구현했다. `Data/CombatBalanceData.CriticalChance` 필드를 추가하고, `PlayerSkillCastUseCase`가 매 시전마다 `_randomProvider.NextFloat01()`을 `_criticalHitJudge.Judge(chance, roll)`에 넘겨 판정한 뒤 `SkillDamageCalculator.Calculate`에 그대로 전달하도록 연결했다. `SkillCastResult.IsCritical`을 추가해 `PlayerSkillInputController`의 로그에도 크리티컬 여부(`(CRIT)`)가 표시된다.
  - `Tests/Application.Tests/PlayerSkillCastUseCaseTests.cs`에서 크리티컬 분기를 결정적으로 검증하기 위해 `IRandomProvider`만 고정값 반환 페이크로 교체하고, `ICriticalHitJudge`는 순수 함수라 페이크 없이 실제 `CriticalHitJudge`를 그대로 사용했다 — 판정 로직 자체도 함께 검증되는 효과.
- [x] 5주차: 몬스터 AI FSM(Idle → Chase → Attack → Dead) 구현. `Domain/Monsters/MonsterAIState`(4상태 enum) + `Domain/Monsters/MonsterAI`(`Tick(distanceToPlayer, isDead, deltaTime)` — Idle→Chase는 ChaseRange 이내, Chase→Attack은 AttackRange 이내, Chase→Idle은 ChaseRange 밖, Attack→Chase는 AttackRange 밖, 모든 상태→Dead는 isDead=true인 터미널 전이)로 Domain 계층에 순수 C#으로 작성했다 — "Domain 계층 분리 기준"이 이미 몬스터 AI 판단 로직을 예시로 명시해뒀던 대상이라 그 기준을 그대로 따름. `Infrastructure/Spawning/MonsterView`가 `Update()`마다 `_ai.Tick(...)`을 호출하는 소비처이며, 상태 전이 엣지에서 `IMonsterMovementSystem.Register`/`Unregister`를 함께 호출해 이동 시스템과 연결했다. `Tests/Domain.Tests/MonsterAITests.cs`에 전이 케이스 전부(쿨다운 게이트 `TryConsumeAttack` 포함) 12개 테스트로 검증.
  - (백로그, 아래 플레이어 HP 시스템 항목에서 해소) `TryConsumeAttack()`은 이 시점엔 아직 어디서도 호출되지 않았다 — 플레이어 HP 시스템 자체가 없어 Attack 상태가 실제로 데미지를 주는 소비처가 없었기 때문.
  - (백로그) `MonsterAIState.Dead`는 `MonsterAITests`로 전이 자체는 검증되지만, 실제 배선에서는 `TakeDamage`가 사망 즉시 동기적으로 `_onDeactivated`를 호출해 풀에 반환하므로 FSM의 Dead 상태를 경유하는 것으로 관측되지 않는다. 사망 연출(이펙트 등)을 붙일 때 이 경로를 재검토해야 한다.
  - 5주차 로드맵의 나머지 항목(`SkillRangeChecker` 분리, 200~500마리 베이스라인 프로파일링 캡처)은 아직 미착수 상태다. 스킬 사거리 판정은 전용 `SkillRangeChecker` 없이 `PlayerSkillCastUseCase` → `PooledMonsterSpawner.ApplyDamageInRange`로 이미 기능은 동작 중이라(몬스터 피격/HP 시스템 항목 참고), 로드맵 문서 그대로 이름을 맞춰 분리할지 현재 구조를 유지할지는 다음 작업 시점에 판단. (웨이브 단위 스폰 시스템, `WalkableGrid`/`FlowFieldGenerator` 연결은 아래 항목에서 각각 해소)
- [x] 플레이어 HP 시스템 구현. 위 FSM 항목의 백로그(`TryConsumeAttack()` 미연결)를 채웠다. `Domain/Player/PlayerHealth`(`MonsterHealth`와 완전히 동일한 형태 — MaxHp/CurrentHp/IsDead/TakeDamage — 지만 별도 클래스로 뒀다. 현재는 몬스터/플레이어 체력의 판정 규칙이 우연히 같을 뿐 서로 다른 도메인 엔티티라, 추후 플레이어 쪽에 리젠·부활 같은 분기가 생겨도 몬스터 쪽 규칙에 영향을 주지 않게 분리한 것 — `Skill`과 `MonsterAI`가 유사한 쿨다운 게이트를 공통 추상화 없이 각자 들고 있는 것과 같은 이유)와 `Data/PlayerData`(체력 밸런스 수치, `MonsterData`와 동일 패턴)를 신규 작성하고, `Application/Player/IPlayerHealth`(포트) + `Infrastructure/Player/PlayerHealthController`(`TransformPlayerMotor`와 동일 패턴 — Domain 객체를 감싸기만 하는 얇은 MonoBehaviour 어댑터, 씬의 Player GameObject에 부착)로 계층을 관통했다. `Data/MonsterData.AttackDamage` 필드를 추가해 몬스터 공격력 수치도 처음으로 정의했다(이전까지는 `TryConsumeAttack()`이 게이트만 판정하고 실제 데미지 양은 정의된 곳이 없었다).
  - `Infrastructure/Spawning/MonsterView.Update()`가 `MonsterAI.cs` 주석이 미리 지정해둔 대로 `if (currentState == MonsterAIState.Attack && _ai.TryConsumeAttack()) { _playerHealth.TakeDamage(_attackDamage); }` 형태로 연결됐다. `TryConsumeAttack()`이 쿨다운 게이트를 이미 들고 있어 매 프레임 호출해도 데미지가 중복 적용되지 않으므로, 상태 전이 여부를 따로 확인할 필요가 없다.
  - `MonsterView.Initialize(...)`의 파라미터가 늘어나 `PooledMonsterSpawner`/`InstantiateMonsterSpawner` 양쪽의 `[Inject] Construct(...)`와 호출부를 함께 갱신했다 — 두 스포너가 Before/After 비교용으로 항상 쌍으로 유지되는 구조라(1주차 결정) 한쪽만 고치면 컴파일이 깨진다.
  - (백로그, 아래 게임오버 처리 항목에서 해소) 플레이어 사망(`IPlayerHealth.IsDead`) 시의 게임오버 처리, UI 표시는 이 시점엔 아직 없었다.
  - `Tests/Domain.Tests/PlayerHealthTests.cs`(`MonsterHealthTests`와 동일한 4개 케이스)로 검증.
  - (2026-08-04 후속 결정) 위 문단에서 "서로 다른 도메인 엔티티라 분리"했던 `PlayerHealth`/`MonsterHealth`를 다시 하나로 합쳤다 — 사용자 판단: 지금은 플레이어/몬스터 체력을 갈라놓을 실제 기능(리젠, 실드 등)이 하나도 없어서, 분리를 정당화할 근거가 사실은 가정뿐이었다. `Domain/Combat/Health`(MaxHp/CurrentHp/IsDead/TakeDamage, 로직은 기존 두 클래스와 동일)로 통합하고 `MonsterView`/`PlayerHealthController` 양쪽이 이 클래스를 그대로 쓰도록 바꿨다. `Domain/Player` 폴더는 내용물이 없어져 삭제했고, `Domain/Monsters/MonsterHealth.cs`/`Tests/Domain.Tests/{Monster,Player}HealthTests.cs`도 제거해 `Tests/Domain.Tests/HealthTests.cs` 하나로 합쳤다. `Application/Player/IPlayerHealth`(포트)와 `Infrastructure/Player/PlayerHealthController`(어댑터)는 그대로 유지했다 — 합친 대상은 Domain의 "판정 로직"이지, 플레이어와 몬스터가 서로 다른 소비 경로(포트/DI/씬 배선)를 갖는다는 사실 자체는 바뀌지 않았기 때문. 이후 플레이어 전용 기능(리젠 등)이 실제로 필요해지면 그 시점에 다시 갈라진다.
- [x] 플레이어 사망 처리(게임오버) 구현. `Presentation/GameOver/GameOverView`가 `IPlayerHealth.IsDead`를 `Update()`에서 직접 폴링하다가 처음 true가 되는 프레임에 미리 숨겨둔 "GAME OVER" UI 텍스트(Canvas 하위, 화면 중앙, 빨간색)를 켠다. `MonsterCountView`와 달리 Presenter를 두지 않았다 — `IsDead`는 생존→사망 한 방향으로만 전이되는 상태라 스로틀링이나 변경 감지 같은 테스트 가치가 있는 로직이 없어서, MVP 분리가 여기선 오버엔지니어링이 된다고 판단했다(`GameOverView` 주석 참고).
  - 사망 이후에도 이동/스킬 입력이 계속 먹히는 문제를 함께 막았다: `PlayerInputController`/`PlayerSkillInputController`가 각각 `IPlayerHealth`를 주입받아 `Update()`/`OnCastPerformed()` 진입 조건에 `_playerHealth.IsDead` 분기를 추가했다. 별도 "게임 정지" 시스템을 만들지 않고, 이미 있던 null 체크 가드에 조건 하나를 얹는 방식으로 최소 변경했다.
  - 몬스터 스폰/이동은 의도적으로 멈추지 않았다 — 사망 후에도 화면에서 몬스터가 계속 움직이는 것 자체는 요구사항이 아니었고, 스포너를 멈추는 로직을 추가하면 "게임오버 이후 상태를 어떻게 관리할 것인가"라는 별도 스코프(재시작, 씬 리로드 등)를 건드리게 되어 이번 범위에서는 뺐다. 필요해지면 별도 작업으로 분리.
  - (후속) `GameOverView`를 별도의 항상-활성 `GameOverDisplay` 오브젝트 대신 "GameOverText" 오브젝트 자신에 부착하도록 합쳤다가, VContainer의 `RegisterComponentInHierarchy`가 **씬 로드 시점에 이미 비활성인 GameObject는 스캔에서 제외한다**는 걸 확인했다 — 텍스트를 처음부터 꺼둔 오브젝트에 스크립트를 얹으면 `Construct`가 호출되지 않고, `Update()` 자체도 안 돌아서 스스로를 다시 켤 방법이 없어지는 닭-달걀 문제였다. 해결책은 GameObject는 항상 활성 상태로 두고, 표시/숨김을 `gameObject.SetActive` 대신 `Text`(Graphic)의 `enabled`만 토글하는 것 — 렌더링만 꺼질 뿐 컴포넌트는 계속 살아있어 DI도 정상 동작하고 폴링도 끊기지 않는다. 이제 `GameOverText` 오브젝트 하나가 Text + `GameOverView`를 모두 들고, 별도 `GameOverDisplay` 오브젝트는 삭제했다.
- [x] 웨이브 단위 스폰 시스템 구현. 위 5주차 백로그 항목을 채웠다. 진행 방식은 사용자 결정으로 **시간 기반 연속 웨이브**(이전 웨이브가 다 죽기를 기다리지 않고, 웨이브별 절대 시작 시각이 되면 다음 웨이브가 겹쳐서 시작)로 정했다 — 클리어 기반 순차 웨이브는 로그라이트 장르엔 흔하지만, 동시 개체 수를 200~500마리까지 자연스럽게 누적/램프업시키기엔 웨이브 하나에 몰아넣어야 해서 CLAUDE.md 목표 프로파일링 시나리오와 맞지 않는다고 판단했다.
  - `Domain/Waves/WaveDefinition`(StartTimeSeconds/MonsterCount/SpawnIntervalSeconds 값 객체) + `Domain/Waves/WaveDirector`(웨이브별 독립 스폰 타이머를 배열로 추적하는 순수 도메인 규칙)로 설계했다. "시간 경과 + 임계값 비교에 따른 분기"라는 점에서 `Skill`의 쿨다운 게이트, `MonsterAI`의 상태 전이와 같은 패턴이라 Domain에 두는 것이 기존 기준과 일관적이라고 판단했다. `Tick(deltaTime)`은 그 프레임에 스폰해야 할 웨이브 인덱스 목록(`WaveTickResult.WaveIndexesToSpawn`)을 반환한다 — 웨이브가 겹치면 한 프레임에 여러 웨이브가 동시에 스폰을 요청할 수 있어 단일 bool이 아니라 리스트로 설계했다. 반환 리스트는 `PooledMonsterSpawner.ApplyDamageInRange`의 `_damageQueryBuffer`와 같은 이유로 매 Tick 재할당하지 않고 재사용한다.
  - `Data/WaveData`(+ `WaveEntry` 직렬화 구조체) ScriptableObject를 `SkillData`/`MonsterData`와 동일 패턴으로 추가하고, `Application/Waves/WaveDirectorFactory.Create(WaveData)`가 `SkillFactory.Create`와 같은 방식으로 Data → Domain 변환을 담당한다. 스폰 위치 반경(랜덤 원 좌표) 계산은 `MonsterSpawnTester`가 쓰던 것과 동일한 범용 수학이라 Domain으로 분리하지 않고 `Application/Waves/WaveSpawnUseCase`에 인라인했다(이동량 계산을 Domain에서 뺀 것과 같은 근거).
  - `Presentation/Spawning/WaveSpawnController`(매 프레임 `IWaveSpawnUseCase.Tick(Time.deltaTime)`만 호출하는 얇은 어댑터)를 신규 작성했다. 기존 `MonsterSpawnTester`(인터벌마다 무한 스폰하는 풀링 Before/After 측정용 하네스)는 폐기하지 않고 그대로 남겨뒀다 — 웨이브 로직과 무관하게 순수 스폰 처리량만 보고 싶은 6~8주차 재측정에서 여전히 쓸모가 있는 별도의 스트레스 테스트 도구이기 때문. `GameLifetimeScope`에 `WaveDirectorFactory.Create(_waveData)` 결과를 `RegisterInstance`하고 `IWaveSpawnUseCase`/`WaveSpawnController`를 등록했다.
  - `Tests/Domain.Tests/WaveDirectorTests.cs`(9개 — 시작 전/후 스폰, 간격 준수, 개수 도달 후 정지, 웨이브 겹침, 시퀀스 완료 등)와 `Tests/Application.Tests/WaveSpawnUseCaseTests.cs`(3개, 페이크 `IMonsterSpawner`로 겹치는 웨이브가 각각 한 번씩 `Spawn`을 호출하는지 검증)로 검증.
  - `WaveData.asset`은 예시 값(5개 웨이브, 0~32초에 걸쳐 겹치며 시작, 마릿수 10→150·간격 1s→0.08s로 점점 촘촘해짐)을 미리 채워 커밋했다. 다만 이 값은 게임플레이용 러프 스케치일 뿐, 3~4주차 프로파일링 때처럼 실제 200~500마리 구간을 캡처하려면 간격을 훨씬 더 좁히는 임시 조정이 별도로 필요하다(5주차 남은 백로그인 "베이스라인 프로파일링 캡처" 항목에서 처리).
  - **(완료, 2026-08-04)** Unity 에디터 수동 작업: 씬(`Bootstrap.unity`)에 `WaveSpawnController` 부착 GameObject 추가, `GameLifetimeScope`의 `Wave Data` 필드에 `WaveData.asset` 연결, 기존 `MonsterSpawnTester` 비활성화, Test Runner(EditMode)에서 `WaveDirectorTests`/`WaveSpawnUseCaseTests` 통과까지 사용자가 직접 완료하고 정상 작동 확인함.
- [x] `WalkableGrid`/`FlowFieldGenerator`를 실제 몬스터 이동에 연결(5주차 남은 백로그 중 하나). 착수 전 확인 결과 씬에 장애물이 전혀 없어서(몬스터는 열린 평면에서 플레이어를 직선으로 쫓아가는 Seek뿐), 장애물 없이 FlowField를 만들면 결과가 기존 Seek와 완전히 동일해 Before/After 비교가 성립하지 않는 문제가 있었다. 사용자 결정으로 **씬에 간단한 정적 장애물을 추가**하는 방향으로 진행했다(장애물 없이 인프라만 먼저 만드는 대안은 이벤트 버스/속성 조합 때와 같은 이유로 기각).
  - `Infrastructure/Pathfinding/WalkableGrid`(정적 장애물 Collider를 셀 단위로 1회 베이크하는 순수 C# 클래스, `Physics.CheckBox` 기반)와 `Infrastructure/Pathfinding/FlowFieldGenerator`(목표 셀 기준 BFS 거리장 계산 → 최급하강 방향으로 셀별 이동 방향 산출)를 새 폴더로 분리했다. 둘 다 분기·밸런스 없는 범용 알고리즘이라 Domain에 두지 않았다(이동량 계산을 Domain에서 뺀 것과 같은 근거) — `NativeArray`/`Unity.Collections`도 필요해 애초에 Domain(UnityEngine 참조 절대 금지) asmdef로는 불가능하기도 했다. `JobMonsterMovementSystem`/`MonsterSeekJob`과 같은 이유로 Infrastructure에 위치.
  - `Infrastructure/Movement/FlowFieldSeekJob`(`MonsterSeekJob`과 동일한 `IJobParallelForTransform`+Burst 구조, 다만 단일 직선 타겟 대신 방향장을 샘플링) + `Infrastructure/Movement/FlowFieldMonsterMovementSystem`(`IMonsterMovementSystem`의 새 After 구현체, `JobMonsterMovementSystem`과 동일한 Register/Unregister·TransformAccessArray 패턴을 그대로 재사용)로 실제 소비처를 만들었다. `JobMonsterMovementSystem`/`MonsterSeekJob`(직선 Seek)은 삭제하지 않고 Before 참조로 남겼다 — 이번 비교 대상은 "직선 Seek vs 장애물 우회 경로"이지 Job/Burst 병렬화 여부가 아니며(그건 2주차에서 이미 검증), 기존 Before/After 보존 관례와 일치.
  - FlowField는 매 프레임 재계산하지 않고 `_regenerateInterval`(기본 0.2초)마다만 갱신한다 — 그리드 규모(기본 80×80)에서 BFS 자체는 이미 충분히 싸지만, 플레이어가 한두 프레임 사이 이동한 정도로 최단 경로가 바뀔 일이 거의 없어서다(`MonsterCountView`의 0.25초 스로틀과 같은 이유). BFS 큐/거리 버퍼는 생성자에서 1회만 할당해 재사용해 GC Alloc 0B/frame 원칙을 지켰다.
  - BFS는 8방향 연결에 이동 비용을 전부 1로 단순화했다(대각선도 직선과 동일 비용) — 실제 최단 거리가 아니라 "장애물을 우회하는 방향" 자체가 목적이라 이 근사가 충분하다고 판단했다. 7주차 로드맵 메모("BFS 특성상 완전 병렬화가 까다로운 지점 — 레이어별로 나눠 처리하는 방식 조사")와 맞물려, BFS 본체는 지금 메인 스레드 C#으로 남겨두고 Job이 읽는 결과물(`Directions`, `NativeArray<float2>`)만 Burst 호환 타입으로 만들어 뒀다 — 7주차에 BFS 자체를 병렬화할 때 이 결과물 구조를 바꿀 필요가 없도록.
  - `_obstacleLayerMask` 기본값(Nothing)일 때는 전 셀이 walkable로 베이크되어 기존 직선 Seek와 동일하게 동작한다 — 씬에 장애물 레이어를 아직 구성하지 않은 상태에서도 안전한 기본값.
  - 로드맵이 언급한 `MovementResolver`(7주차 메모의 `MovementResolver.ResolveOverlap`)로의 개명·통합은 이번 범위에서 하지 않았다 — 그 이름은 이동(Seek)과 겹침 회피(separation, 3주차 `SpatialHashMonsterSeparationSystem`)를 하나로 합치는 걸 암시하는데, 지금은 두 시스템이 각자 독립적으로 이미 검증된 상태라 병합할 실제 필요가 없다. 사용자에게 확인하지 않고 판단한 스코프 결정이므로, 이견이 있으면 7주차 착수 시점에 다시 논의.
  - **미완료(Unity 에디터 수동 작업 필요)**: (1) Project Settings > Tags and Layers에서 "Obstacle" 레이어 생성, (2) BoxCollider를 가진 정적 장애물 GameObject(또는 프리팹) 몇 개를 `Bootstrap.unity`에 배치하고 레이어를 Obstacle로 설정, (3) 씬에서 기존 `JobMonsterMovementSystem` 컴포넌트를 `FlowFieldMonsterMovementSystem`으로 교체(또는 새로 부착)하고 `Obstacle Layer Mask` 필드에 방금 만든 레이어 지정, (4) Play 모드에서 몬스터가 장애물을 우회해 플레이어에게 접근하는지 육안 확인. 장애물 배치는 스폰 반경(20)·추격 범위(25) 안, 원점 근처에 두어야 확인이 쉽다.
- [x] 몬스터 수명 타이머 제거. `MonsterView`는 원래(HP 시스템이 붙기 전) 스폰 후 `_lifetimeSeconds`(스포너가 넘겨준 값, 기본 3초)가 지나면 HP와 무관하게 무조건 비활성화됐다 — 풀링 파이프라인 검증용 테스트 하네스 스텁 시절의 잔재(`MonsterView.cs` 상단 주석에도 명시돼 있었음). 이제 `Health`/`TakeDamage`로 실제 사망 판정이 있으므로, 몬스터가 살아서 플레이어를 추격/공격 중인데도 몇 초 뒤 그냥 사라지는 건 실제 게임플레이로는 맞지 않아 제거했다. `MonsterView.Initialize`에서 `lifetimeSeconds` 파라미터를 없애고 `Update()`의 만료 체크 블록을 삭제했으며, `PooledMonsterSpawner`/`InstantiateMonsterSpawner`(Before/After 쌍이라 항상 같이 수정) 양쪽에서 `_monsterLifetimeSeconds` 필드와 호출부 인자를 함께 제거했다. 이제 몬스터는 오직 `TakeDamage`로 HP가 0이 될 때만 죽는다.
  - 파급 효과: `MonsterSpawnTester`(풀링 처리량 측정용 하네스, 인터벌마다 무한 스폰)는 애초에 몬스터의 수명 만료를 자신의 유일한 디스폰 수단으로 삼고 있었다. 수명 타이머가 사라지면 `MonsterSpawnTester`를 계속 켜둘 경우 몬스터 수가 무한정 늘어난다(플레이어 스킬로 죽이는 것 외엔 디스폰 경로가 없음). 다만 6~8주차 재측정에서 이 하네스가 필요한 건 "200~500마리 도달"이지 "무한 실행"이 아니므로, 목표 개체 수 부근에서 수동으로 비활성화하는 방식으로 계속 쓸 수 있다 — 별도 캡/디스폰 로직을 지금 추가하지는 않았다(실제로 문제가 되는 시점, 즉 6주차 재측정 착수 시점에 필요하면 그때 대응).
- [x] 몬스터가 많아지면 장애물을 뚫고 지나가는 버그 수정. 원인은 두 가지가 겹친 것이었다: (1) `SpatialHashMonsterSeparationSystem`(겹침 회피)이 애초에 `WalkableGrid`를 전혀 몰라, 밀도가 높아지면 벽 앞에 낀 몬스터를 순수 거리 계산만으로 장애물 칸 안쪽으로 밀어버릴 수 있었다. (2) 한번 장애물 칸 안에 들어가면 `FlowFieldSeekJob`이 그 칸의 방향장 값이 0(비어있음)임을 확인하고 플레이어를 향한 직선 fallback으로 전환하는데, 이 직선은 장애물을 그대로 뚫고 지나갔다.
  - `Infrastructure/Pathfinding/IWalkableGridProvider`(`FlowFieldMonsterMovementSystem`이 베이크한 `WalkableGrid`를 참조로만 공유하는 포트)를 추가해 `SpatialHashMonsterSeparationSystem`이 push 결과 위치가 walkable이 아니면 그 push를 적용하지 않도록 막았다(반대쪽 몬스터는 그대로 밀림 — 벽에 눌려 멈추는 결과로 겹침이 완전히 해소되지 않을 순 있지만 장애물을 뚫는 것보단 낫다). `FlowFieldSeekJob`은 현재 칸이 walkable이 아니면 플레이어 직선 대신 반경 3칸 안에서 가장 가까운 walkable 칸으로 빠져나가는 방향을 먼저 찾도록 바꿨다(그래도 못 찾으면 기존처럼 직선 fallback — 완전한 예외 상황에서만 발생).
  - `IWalkableGridProvider`는 그리드 베이크/해제 소유권을 `FlowFieldMonsterMovementSystem`에 그대로 두고 참조만 공유한다. 별도 DI 싱글턴으로 분리해 `GameLifetimeScope.Configure()`에서 직접 베이크하는 방안도 검토했으나, 그 시점엔 씬의 장애물 Collider가 아직 Awake되지 않았을 수 있어 베이크 타이밍이 오히려 불안정해질 위험이 있었다(현재처럼 `FlowFieldMonsterMovementSystem.Awake()`에서 베이크하는 건 이미 검증된 지점이라 그대로 유지).
  - `SpatialHashMonsterSeparationSystem.Construct(IWalkableGridProvider)`는 그리드 참조를 생성자 시점에 캐시하지 않고 매 `Update()`마다 새로 읽는다 — Unity의 컴포넌트 간 Awake 호출 순서는 보장되지 않아 이 시스템의 Construct가 먼저 실행되면 그리드가 아직 베이크되지 않았을 수 있지만, Update는 씬의 모든 Awake가 끝난 뒤에만 시작되므로 항상 안전하다.
  - **주의(향후 작업 시 참고)**: 이 변경으로 `SpatialHashMonsterSeparationSystem`은 `IWalkableGridProvider`가 컨테이너에 등록되어 있어야만 빌드된다(`FlowFieldMonsterMovementSystem`이 그 인터페이스를 구현). 만약 7~8주차 재측정 등에서 이동 시스템을 다시 `JobMonsterMovementSystem`(장애물 개념이 없는 직선 Seek)으로 되돌리면, `IWalkableGridProvider` 등록이 사라져 컨테이너 빌드 자체가 예외를 던진다 — 그 시점엔 `JobMonsterMovementSystem`에도 `IWalkableGridProvider`를 구현시키거나(항상 walkable인 널 그리드 반환) 이 커플링을 다시 풀어야 한다.
- [x] **5주차 완료 처리.** 로드맵상 남아있던 두 항목을 다음과 같이 정리했다.
  - `SkillRangeChecker` 분리는 하지 않기로 결정. 실제 소비처가 `PlayerSkillCastUseCase` → `PooledMonsterSpawner.ApplyDamageInRange` 한 경로뿐이라, 전용 클래스로 뽑아도 교체 가능한 지점이나 테스트 이득이 새로 생기지 않는다 — "Domain 계층 분리 기준" 문서의 판단 기준(순수성이 실질적으로 필요한가, 교체 가능한 지점을 보여주려는가)에 둘 다 해당하지 않아 현재 구조를 유지한다. 스킬 종류가 늘어 사거리 판정 방식이 갈라지는 시점에 재검토.
  - 200~500마리 베이스라인 프로파일링 캡처는 5주차 항목에서 제외하고 6주차 작업의 첫 단계로 이동했다 — 어차피 6주차 목표 자체가 "재측정 → 풀링 적용 후 수치를 베이스라인과 비교"라 이 시점 수치가 곧 6주차의 Before가 되므로, 별도로 먼저 캡처했다가 다시 재는 이중 작업을 피했다.
- [x] 6주차 코드 레벨 점검(1~3단계) 완료, 프로파일링 캡처(4~5단계)는 백로그로 보류.
  - **Instantiate/Destroy 직접 호출 전수 검색**: `Assets/_Project` 전체에서 3건 발견 — `PooledMonsterSpawner.CreatePooledInstance`의 `Instantiate`는 prewarm/풀 소진 시에만 호출되는 정상 경로, `InstantiateMonsterSpawner`의 `CreatePrimitive`/`Destroy`는 의도된 Before 베이스라인 하네스, `SkillFactoryTests`의 `DestroyImmediate`는 EditMode 테스트 정리 코드. 웨이브 스폰(`WaveSpawnUseCase`)·FSM(`MonsterAI`/`MonsterView`) 등 5주차 신규 코드에 풀링을 우회하는 경로는 없음.
  - **UniTask 클로저/박싱 점검**: `WaveSpawnUseCase.Tick`, `PlayerSkillCastUseCase.Execute`, `MonsterView.Update`, `MonsterCountPresenter`(구독은 생성자에서 1회), `EventBus`(struct 이벤트 + 참조 타입 Subject라 박싱 없음), 두 `InputController`(델리게이트 등록은 Awake 1회)까지 전수 리뷰 — 매 프레임 반복되는 클로저 캡처나 박싱 없음.
  - **foreach/List 컨벤션 점검**: `foreach`는 전체 3건뿐이며 전부 Update 루프 밖(이벤트성 호출) 또는 Before 하네스. Update 루프를 가진 시스템(`JobMonsterMovementSystem`, `FlowFieldMonsterMovementSystem`, `SpatialHashMonsterSeparationSystem`, `MonoMonsterMovementSystem`/`BruteForceMonsterSeparationSystem` 대조군)은 이미 `for` + 인덱스 접근, `FlowFieldGenerator`의 BFS 버퍼는 생성자에서 1회 할당(`Allocator.Persistent`) 후 재사용 — 전부 기존 컨벤션 준수 확인.
  - **프로파일링 캡처(4~5단계)는 보류**: 200~500마리 베이스라인 캡처와 풀링 재측정은 Unity 에디터에서 Development Build + Profiler를 직접 띄워야 하는 수동 작업이라 이번 세션에서는 진행하지 않기로 함(사용자 결정). 코드 레벨 검토 결과 GC Alloc을 유발할 만한 지점은 발견되지 않았으나, 실제 수치 확인은 미완료 상태로 남는다 — 2주차 때 코드는 문제없어 보였지만 에디터 계측 아티팩트가 있었던 전례가 있어, 수치 없이 6주차를 "완전히 끝난 것"으로 간주하지 않는다.
- [x] 7주차: 이동/겹침회피 Job 파이프라인 통합(`MonsterMovementResolver`). 로드맵상 "몬스터 이동/타겟팅 연산을 Job으로"는 5주차부터 `FlowFieldMonsterMovementSystem`+`FlowFieldSeekJob`(`[BurstCompile]` + `IJobParallelForTransform`)으로 이미 완료 상태였다. 남은 건 "MovementResolver.ResolveOverlap을 Job으로 감싸기" — 겹침 회피(`SpatialHashMonsterSeparationSystem`)만 아직 메인 스레드 C# 루프였던 부분이다.
  - **통합 이유는 성능이 아니라 안전성**: 겹침 회피는 3주차 측정(1.197ms→0.528ms)에서 이미 충분히 빨라 병목으로 재진단되지 않았다. 그런데도 Job으로 전환한 이유는, `FlowFieldMonsterMovementSystem`이 이미 자기 소유 `TransformAccessArray`로 몬스터 Transform에 매 프레임 쓰기 Job을 스케줄링하고 있어서, 겹침 회피를 별도의 독립된 Job(별도 `TransformAccessArray`)으로 전환하면 두 시스템이 서로 모르는 채 같은 Transform 집합에 동시에 쓰기 Job을 스케줄링하게 되어 Unity 세이프티 시스템이 레이스 컨디션 예외를 던질 위험이 있었기 때문이다(사용자 결정: 하나로 통합). 이는 5주차에 보류해뒀던 "`MovementResolver`로의 개명·통합 여부는 7주차 착수 시점에 재논의"를 지금 확정한 것이기도 하다.
  - `Infrastructure/Movement/MonsterMovementResolver.cs`가 `FlowFieldMonsterMovementSystem`(이동)과 `SpatialHashMonsterSeparationSystem`(겹침 회피)을 하나로 합쳐 `IMonsterMovementSystem`+`IMonsterSeparationSystem`을 모두 구현한다. `IWalkableGridProvider`는 구현하지 않는다 — 그리드를 공유받아야 할 외부 시스템이 이제 없어졌기 때문(그리드를 직접 소유해 두 Job 모두에 바로 넘김).
  - **Register/Unregister 생명주기 불일치를 명시적 인터페이스 구현으로 해결**: `IMonsterSeparationSystem.Register`는 스폰 즉시(Idle 상태부터), `IMonsterMovementSystem.Register`는 AI가 Chase로 전이할 때만 호출된다(Idle/Attack 상태 몬스터는 이동 정지 — 기존 동작 그대로 보존 필요). 두 인터페이스가 우연히 동일한 시그니처(`Register(MonsterView)`)를 가져 암묵적 구현으로는 하나로 뭉개지므로, `void IMonsterSeparationSystem.Register(...)` / `void IMonsterMovementSystem.Register(...)` 형태의 명시적 인터페이스 구현으로 완전히 분리했다. Separation 쪽이 `TransformAccessArray`/리스트의 실제 추가·제거(스왑 제거 포함)를 담당하고, Movement 쪽은 이미 등록된 인덱스의 `NativeArray<bool> _movementActive` 플래그만 켜고 끈다(`PooledMonsterSpawner.Spawn`이 항상 Separation-Register를 먼저 호출하고 Chase 전이는 그 이후 프레임에 일어나므로 순서는 항상 보장됨). `_movementActive`는 영구 상태라 스왑 제거 시 값도 함께 옮겨야 했다(반면 매 프레임 재구성되는 `_bucketHeads`/`_next`/`_positions`는 성장 시 이전 내용을 보존할 필요가 없음).
  - `Infrastructure/Movement/MonsterSeparationJob.cs`(신규, `IJobParallelForTransform`+`[BurstCompile]`)가 `SpatialHashMonsterSeparationSystem`의 3x3 버킷 겹침 회피 알고리즘을 그대로 Burst 호환 구조(`NativeArray` 입출력)로 옮겼다. 원본과 다른 점: (1) 원본은 pair(i,j)를 한 번만 계산해 양쪽에 동시에 0.5씩 적용했지만, 병렬 Job에서는 각 인덱스가 자기 Transform만 써야 하므로 각자 자기 3x3 이웃 전체를 스캔해 받는 push를 혼자 누적한다(같은 pair를 양쪽에서 한 번씩 계산해 연산량은 늘지만 완전히 병렬 안전함). (2) 이웃 위치(`Positions`)는 이번 프레임 이동 Job 적용 "이전" 스냅샷이라, 자기 자신(이동 후, `TransformAccess`로 읽음)과 이웃(이동 전) 사이에 한 프레임 미만의 시차가 생기지만 프레임당 이동량이 겹침 판정 반경(0.5)에 비해 매우 작아 무시 가능한 근사다 — 원본 클래스 주석도 이미 "완전한 물리가 아닌 근사"라 명시하고 있어 정신은 동일.
  - `FlowFieldSeekJob`에는 `[ReadOnly] NativeArray<bool> MovementActive` 필드와 `Execute` 상단 `if (!MovementActive[index]) return;` 한 줄만 추가했다(그 외 로직 불변).
  - **프레임당 실행 순서**: `Update()`에서 (1) FlowField 재생성 판정(기존과 동일 0.2초 스로틀), (2) 메인 스레드 O(n) 패스로 위치 스냅샷+버킷 구성(기존 `SpatialHashMonsterSeparationSystem`이 이미 하던 것과 동일 비용, 늘지 않음), (3) `FlowFieldSeekJob` 스케줄(`handleA`), (4) `MonsterSeparationJob`을 `handleA`에 의존시켜 스케줄(`handleB = job.Schedule(_transformAccessArray, handleA)`) — 이 의존성 선언이 Unity에게 두 Job의 쓰기 순서를 보장시키고 세이프티 시스템도 이를 인지하게 만드는 실질적인 안전장치다. `LateUpdate()`에서 `handleB.Complete()`.
  - `GameLifetimeScope`의 `FlowFieldMonsterMovementSystem`/`SpatialHashMonsterSeparationSystem` 등록 두 줄을 `MonsterMovementResolver` 한 줄로 교체했다. 두 이전 컴포넌트 파일과 `IWalkableGridProvider`, `MonsterSeekJob`/`JobMonsterMovementSystem`/`MonoMonsterMovementSystem`/`BruteForceMonsterSeparationSystem`은 삭제하지 않고 그대로 뒀다(Before 대조군 보존 관례). `MonsterView.cs`/`PooledMonsterSpawner.cs`는 인터페이스로만 두 시스템을 소비하므로 코드 변경이 전혀 필요 없었다 — 인터페이스 분리 효과가 그대로 증명된 지점.
  - **FlowFieldGenerator의 BFS 병렬화는 조사 결과 지금은 보류**(로드맵의 "레이어별로 나눠 처리하는 방식 조사" 항목). 그리드 규모(80×80)에서 BFS 자체가 이미 충분히 싸고 0.2초 스로틀로만 재계산되므로 병목으로 진단되지 않았다 — 진단 없는 최적화를 하지 않는다는 프로젝트 원칙에 따라 코드 변경 없이 조사 결과만 문서화한다. 병렬화가 실제로 필요해지면 표준 기법(레벨-동기 BFS — 현재 프론티어 셀들을 `IJobParallelFor`로 동시 처리하고 레이어 사이에 동기화 배리어를 두는 방식)을 적용할 수 있다.
  - **미완료(Unity 에디터 수동 작업 필요)**: (1) `Bootstrap.unity`에 새 GameObject 생성 후 `MonsterMovementResolver` 부착, 기존 `FlowFieldMonsterMovementSystem`/`SpatialHashMonsterSeparationSystem`의 Inspector 값(moveSpeed, initialCapacity, gridHalfExtent, cellSize, obstacleLayerMask, regenerateInterval / separationRadius, bucketCount)을 옮겨 적기. (2) 기존 두 컴포넌트는 비활성화만 하고 유지. (3) Play 모드에서 Idle 정지·Chase 이동·장애물 우회·몬스터 간 비겹침 확인. (4) Burst Inspector로 `MonsterSeparationJob` 벡터화 확인과 Timeline 프로파일링 캡처는 6주차에 보류한 프로파일링 캡처와 함께 나중에 일괄 진행.

## Claude Code 작업 시 유의사항

- 이 프로젝트는 포트폴리오이므로, 기능 구현 시 "왜 이렇게 했는지"를 README나 주석에 남기는 작업을 함께 요청받으면 반드시 포함할 것
- 새 시스템 추가 전, 위 아키텍처 원칙(계층 분리, asmdef 참조 규칙)에 위배되지 않는지 먼저 확인할 것
- 최적화 관련 코드 작성 시 Before/After 측정 방법도 함께 제안할 것
