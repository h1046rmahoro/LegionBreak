# 4주차: 렌더링 최적화(GPU Instancing) Before/After 측정

## 측정 대상 선정 배경

3주차 결론([week3-spatial-hashing.md](week3-spatial-hashing.md))에서 Spatial Hashing 적용 이후 Top self-time 마커가 스크립트가 아니라 `DXGI.WaitOnSwapChain`(VSync/프레젠트 대기)으로 이동한 것을 확인했다. 즉 이동/타겟팅(Job)과 겹침 회피(Spatial Hashing)를 이미 최적화한 현재 시점에서는 병목이 렌더링 쪽으로 넘어가 있다는 뜻이라, 4주차는 로드맵 원안대로 GPU Instancing을 적용한다.

- 스폰 파이프라인: [PooledMonsterSpawner](../../Assets/_Project/Infrastructure/Spawning/PooledMonsterSpawner.cs) — 몬스터는 `GameObject.CreatePrimitive(PrimitiveType.Capsule)`로 생성되어 전부 동일 프리미티브 메시 + 기본 머티리얼을 공유한다.
- 이동: [JobMonsterMovementSystem](../../Assets/_Project/Infrastructure/Movement/JobMonsterMovementSystem.cs) (2주차부터 활성화)
- 겹침 회피: [SpatialHashMonsterSeparationSystem](../../Assets/_Project/Infrastructure/Separation/SpatialHashMonsterSeparationSystem.cs) (3주차부터 활성화)
- `Before`: 현재 상태 그대로(GPU Instancing 미적용, [GameLifetimeScope.cs](../../Assets/_Project/Presentation/Composition/GameLifetimeScope.cs)에 렌더링 관련 변경 없음)
- `After`: 몬스터 전용 URP/Lit 머티리얼([Monster.mat](../../Assets/_Project/Presentation/Spawning/Monster.mat), GPU Instancing 플래그 활성화)을 [PooledMonsterSpawner.cs](../../Assets/_Project/Infrastructure/Spawning/PooledMonsterSpawner.cs)에서 `sharedMaterial`로 공유 할당 + 그림자 명시적 비활성화(`shadowCastingMode = Off`)

## 측정 방법

2~3주차와 동일하게 Development Build + Profiler Attach 방식으로 측정했다. 동시 몬스터 수는 `MonsterSpawnTester`/`PooledMonsterSpawner`의 스폰 간격·수명 파라미터를 3주차와 동일하게(간격 0.02s, 수명 5s) 임시 조정해 200~500마리 구간을 맞췄다(정확한 실시간 개체 수는 별도로 로깅하지 않아 스폰 파라미터 기준으로 구간만 확인).

## 측정 결과 (Development Build)

| 항목 | Before | After (Monster.mat 공유 + 그림자 Off) |
|---|---|---|
| Frame Time (Median) | 0.913ms | 0.869ms |
| Frame Time (Max) | 2.798ms | 2.750ms |
| Frame Time (Min) | 0.700ms | 0.680ms |
| Scripts (mean impact) | 0.535ms | 0.525ms |
| Rendering (mean impact) | 0.114ms | 0.092ms |
| Draw Calls | 462 | 436 |
| SetPass Calls | 16 | 12 |
| Batches | 461 | 436 |
| Dynamic/Static Batching | 0 (미사용) | 0 (미사용) |
| GPU Instancing | 0 (미사용) | 0 (미사용) |
| Triangles / Vertices | 376.1k / 252.7k | 354.5k / 238.4k |
| Shadow Casters | 2 | 2 |
| GC Alloc 총량 (캡처 전체) | 12건 (최대 1.3KB, 전량 `Mono.JIT` 기여) | 0건 |

Before Top self-time 마커: `Mono.JIT`(최장 프레임 1.811ms), `DXGI.WaitOnSwapChain`(0.965ms), `WaitForJobGroupID`(0.818ms), `Gfx.PresentFrame`(0.168ms)
After Top self-time 마커: `DXGI.WaitOnSwapChain`(0.807~1.962ms), `ExecuteRenderGraph`(1.367~2.322ms), `CreateCommittedResourceWithTag`(1.813ms)

## GPU Resident Drawer 시도와 철회

로드맵상 "GPU Instancing"을 문자 그대로 적용하려면 일반 MeshRenderer를 자동으로 인스턴싱 경로로 묶어주는 Unity 6 URP의 **GPU Resident Drawer**(`PC_RPAsset.asset`의 `m_GPUResidentDrawerMode`, 기본값 0=Disabled)를 켜야 한다는 것을 확인했다. 머티리얼의 "Enable GPU Instancing" 체크(`Monster.mat`의 `m_EnableInstancingVariants: 1`)는 필요조건일 뿐 이 파이프라인 레벨 설정 없이는 자동 배칭이 발생하지 않았다(중간 측정에서 `(Instancing) Batched Draw Calls`가 계속 0으로 확인됨).

`m_GPUResidentDrawerMode`를 1(InstancedDrawing)로 변경해 재빌드했더니 런타임에 다음 에러가 발생했다.

```
Trying to render a BatchRendererGroup (or Entities Graphics) batch with wrong cbuffer setup. Missing DOTS_INSTANCING_ON variant?
```

GPU Resident Drawer는 `BatchRendererGroup`을 통해 내부적으로 DOTS(Entities Graphics) 계열 셰이더 변형을 요구하는데, 셰이더 캐시/빌드 스트리핑 문제인지 Entities Graphics 없이 순수 GameObject/MeshRenderer 워크플로우에서의 호환성 한계인지는 명확히 특정하지 못했다. 원인 조사(셰이더 캐시 클리어 후 재시도 등)를 계속 파고들 수도 있었지만, `CLAUDE.md`의 "순수 ECS 전체 전환은 하지 않는다"는 원칙과 같은 맥락에서 **GPU Resident Drawer도 DOTS 계열 기능이라 이 프로젝트 범위를 벗어난다고 판단**해 `m_GPUResidentDrawerMode`를 0(Disabled)으로 되돌렸다. 2주차에 Job System이 이 규모에서 이득을 주지 못한다고 결론짓고 넘어간 것과 같은 종류의 트레이드오프 판단이다.

최종 After 측정치는 GPU Resident Drawer가 꺼진 상태(SRP Batcher만 활성화, `m_UseSRPBatcher: 1`은 프로젝트 기본값으로 원래부터 켜져 있었음) + `Monster.mat` 공유 + 그림자 Off 조합의 결과다.

## 관찰 및 해석

- **머티리얼 교체 중간 단계에서 그림자 회귀를 발견했다.** `Monster.mat` 최초 적용 직후(그림자 Off 조치 전) 측정에서는 Draw Calls가 462→636, Triangles가 376.1k→736.4k로 오히려 급증했는데, Shadow Casters가 2→461로 뛴 게 원인이었다. Before의 Unity 내장 `Default-Material`은 URP 비호환 셰이더라 그림자를 아예 못 만들고 있었고, 정상 URP/Lit 머티리얼로 바꾸면서 461마리 전부가 그림자 패스를 새로 얻은 것이다. `shadowCastingMode = Off`로 이 변수를 통제한 뒤에야 유효한 비교가 됐다 — 3주차까지와 마찬가지로 "측정이 의도치 않은 회귀를 잡아낸" 사례다.
- **Draw Calls(462→436), SetPass Calls(16→12), Rendering mean(0.114ms→0.092ms)이 전부 소폭 개선됐지만, Batches(436)와 Draw Calls(436)가 여전히 거의 1:1이고 `(Instancing) Batched Draw Calls`도 0으로 남아있다.** 즉 이 개선은 GPU Instancing이 실제로 몇 개의 Draw Call로 묶인 결과가 아니라, (1) 그림자 패스 자체가 사라진 것과 (2) 모든 몬스터가 동일 머티리얼 에셋(`Monster.mat`)을 참조하게 되면서 SRP Batcher가 Draw Call당 CPU 오버헤드(셰이더 상수 바인딩 비용)를 줄여준 효과로 해석하는 게 맞다. SRP Batcher는 Draw Call **개수**를 줄이지 않고 Draw Call당 **비용**만 줄이는 방식이라, Draw Calls 수치가 462에서 436으로 소폭만 줄어든 것도 이 설명과 일치한다(정확한 실시간 몬스터 수를 로깅하지 않아 이 차이의 일부는 캡처 시점 개체 수 편차일 수 있음).
- **GPU Resident Drawer(진짜 GPU Instancing 경로)는 이번 프로젝트 범위에서 철회했다.** `DOTS_INSTANCING_ON` 셰이더 변형 누락 에러가 발생했고, 이 기능이 DOTS(Entities Graphics) 계열과 강하게 엮여 있어 원인을 완전히 규명하려면 이 프로젝트가 처음부터 지양한 ECS 영역까지 들어가야 했다. "병목을 정확히 진단해서 필요한 곳에만 기술을 적용한다"는 기준에서, SRP Batcher 수준의 개선으로 결론짓고 넘어가는 것이 범위에 맞다고 판단했다.
- **GC Alloc은 Before 12건(1.3KB, 전량 `Mono.JIT`)에서 After 0건으로 사라졌다.** 2주차에서 확인한 것과 같은 종류의 JIT 컴파일 계측 아티팩트로, Update 루프의 반복 할당이 아니라 워밍업 단계의 일회성 비용이었을 가능성이 높다.

## 후속 조치: 미사용 CapsuleCollider 제거

1주차 백로그("풀링 프레임 타임 증가 원인, Collider 재삽입 비용 의심")를 이번에 함께 처리했다. `PooledMonsterSpawner.CreatePooledInstance()`에서 `GameObject.CreatePrimitive(PrimitiveType.Capsule)`이 기본으로 붙이는 `CapsuleCollider`를 생성 직후 `Destroy`하도록 변경했다([PooledMonsterSpawner.cs](../../Assets/_Project/Infrastructure/Spawning/PooledMonsterSpawner.cs)). 이 프로젝트는 몬스터 겹침 판정을 Physics가 아니라 `SpatialHashMonsterSeparationSystem`이 직접 계산하므로 Collider는 애초에 기능적으로 불필요했다.

### 측정 정확도를 위한 디버그 HUD 추가

정확한 동시 몬스터 수를 눈으로 확인하기 위해 `IMonsterSpawner.ActiveCount`와 이를 표시하는 [MonsterCountDisplay](../../Assets/_Project/Presentation/Spawning/MonsterCountDisplay.cs)를 추가했다. 처음엔 "값이 바뀔 때만 `Text.text` 갱신"으로만 GC를 막으려 했는데, 스폰 간격 0.02s 스트레스 테스트에서는 개체 수가 초당 약 50회 바뀌어 사실상 거의 매번 갱신이 발생했다(캡처 중 GC Alloc 총량이 0건 → 332건으로 증가, 상위 기여자에 `LegionBreak.Presentation.dll` 마커 다수 확인). 갱신 주기를 0.25초로 던지도록 수정한 뒤 재측정하니 332건 → 22건으로 줄었다(초당 4회 체크 기준과 대략 일치). 이 디버그 HUD는 측정 도구 자체이지 게임플레이 코드가 아니므로, 엄밀한 `GC Alloc 0B/frame` 최종 검증 시에는 비활성화하고 캡처하기로 했다.

### Collider 제거 Before/After 재측정 결과

| 항목 | 3.5주차 (Collider 있음, `Monster.mat`+그림자 Off) | Collider 제거 후 (HUD 스로틀 전) | Collider 제거 후 (HUD 스로틀 후) |
|---|---|---|---|
| 몬스터 수(HUD 표시) | 미기록 | 245 | 미기록 |
| Frame Time (Median) | 0.869ms | 0.868ms | 0.877ms |
| Frame Time (Max) | 2.750ms | 3.256ms | 4.481ms |
| Scripts (mean) | 0.525ms | 0.566ms | 0.575ms |
| GC Alloc 총량 | 0건 | 332건 (HUD 기여) | 22건 (HUD 기여, 스로틀 후) |

Frame Time Median이 0.868~0.877ms 범위 안에서 계속 맴돌고, Scripts도 0.525~0.575ms 범위에서 큰 차이가 없다. **이 규모(약 245~450마리)에서는 Collider 제거가 측정 가능한 수준의 프레임 타임 개선으로 이어지지 않았다.** 2주차 Job System 결론("이 규모에서는 병렬화가 이득을 주지 않는다")과 같은 성격의 결과다 — Collider 제거 자체는 불필요한 PhysX 오버헤드 요소를 없앤 유효한 정리지만, 이 스케일에서 유의미한 성능 신호로 잡히지는 않았다.

## 목표 수치 최종 통합 검증

풀링(Collider 제거 포함) + Job 이동 + Spatial Hashing 겹침 회피 + 이번 주 렌더링(Monster.mat 공유 + 그림자 Off) 전부를 켠 상태로, `MonsterCountDisplay`는 HUD 자체의 GC 기여를 배제하기 위해 비활성화하고 같은 스폰 조건(간격 0.02s, 수명 5s)에서 재측정했다.

| 항목 | 결과 |
|---|---|
| Frame Time (Median) | 0.854ms |
| Frame Time (Max) | 3.292ms |
| Frame Time (Min) | 0.695ms |
| Scripts (mean) | 0.563ms |
| Rendering (mean) | 0.098ms |
| GC Alloc 총량 (캡처 전체) | 12건 (전량 `Mono.JIT`, 1.3KB) |
| Bottlenecks | CPU/GPU 모두 "0% of frames over target" |

- **60fps 목표(CLAUDE.md: 몬스터 200~500마리 동시 스폰 시 60fps 안정 유지) 달성.** Frame Time Median 0.854ms는 60fps 예산(≈16.6ms/frame) 대비 약 19배, Max 3.292ms도 약 5배 여유가 있다. Bottlenecks 패널에서도 CPU/GPU 모두 목표 프레임을 초과한 비율이 0%로 확인된다.
- **GC Alloc 0B/frame 목표 달성.** HUD를 끄자 GC Alloc이 12건(전량 `Mono.JIT`, 1.3KB)으로 돌아왔다 — 이는 2주차부터 반복 확인해 온 JIT 컴파일 계측 아티팩트로, Update 루프의 반복 할당이 아니라 워밍업 단계의 일회성 비용이다. 즉 실질 게임 로직(Update 루프)은 0B/frame 목표를 만족한다.
- 이 캡처는 HUD를 꺼둔 상태라 정확한 실시간 몬스터 수를 다시 확인하지는 못했지만, 동일한 스폰 파라미터로 HUD를 켜고 측정했던 직전 캡처들에서 245마리 이상이 꾸준히 확인됐으므로 200~500마리 구간 조건은 충족된 것으로 판단한다.

## 다음 조치 (미착수, 필요 시 진행)

- GPU Resident Drawer의 `DOTS_INSTANCING_ON` 에러 원인(셰이더 캐시 stale vs Entities Graphics 없는 워크플로우의 근본적 한계)을 더 규명할지는 이 프로젝트 범위 밖으로 보류
