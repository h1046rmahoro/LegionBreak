using System;
using Cysharp.Threading.Tasks;
using LegionBreak.Application.Movement;
using LegionBreak.Application.Player;
using LegionBreak.Data;
using LegionBreak.Domain.Combat;
using LegionBreak.Domain.Monsters;
using LegionBreak.Infrastructure.Movement;
using UnityEngine;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// 몬스터 인스턴스의 스폰-디스폰/체력/AI 상태를 담당하는 View.
    /// HP 판정(Health, 플레이어와 공유하는 도메인 클래스)과 AI 상태(MonsterAI)를 실제로 들고 있어 더는 "더미" 스텁이 아니다
    /// (2026-07-23: DummyMonsterView에서 개명 — HP 시스템이 붙기 전까지는 수명 타이머만
    /// 있는 풀링 파이프라인 검증용 스텁이었으나, 이제 전투 상태를 가진 실제 컴포넌트다).
    /// (2026-08-27: Monster.prefab의 캡슐 프리미티브를 실제 몬스터 모델(ZombiegirlWKurniawan,
    /// Humanoid 리그)의 자식 인스턴스로 교체. 이어서 Mixamo 좀비 애니메이션 팩을 연결하고
    /// Animator를 통해 MonsterAIState(Idle/Chase/Attack/Dead)를 그대로 따라가게 했다 — 판단
    /// 로직은 여전히 MonsterAI(Domain)에 있고, 이 클래스는 그 결과를 Animator에 반영만 한다).
    /// </summary>
    public class MonsterView : MonoBehaviour
    {
        // 문자열 해시를 매 호출 캐싱 — 상태 전이 시에만(매 프레임이 아님) 호출되므로 GC/성능
        // 영향은 미미하지만, 이미 프로젝트 전반에 깔린 "매 프레임 반복되는 GC 원인 제거"
        // 컨벤션과 같은 방향으로 맞춰둔다.
        private static readonly int AnimatorStateParam = Animator.StringToHash("State");

        // "zombie agonizing" 사망 클립의 실제 길이 — MonsterAnimatorSetupTool이 Animator
        // Controller를 생성할 때 이 필드에 클립 길이를 자동으로 써준다(수동으로 값을 옮겨
        // 적을 필요 없음). 기본값 1f는 툴 실행 전 임시값.
        [SerializeField] private float _deathAnimationSeconds = 1f;

        private Health _health;
        private MonsterAI _ai;
        private Animator _animator;
        private bool _isDying;
        private float _attackDamage;
        private IPlayerMotor _playerMotor;
        private IPlayerHealth _playerHealth;
        private IMonsterMovementSystem _movementSystem;
        private Action<MonsterView> _onDeactivated;

        // Health/MonsterAI는 풀링되는 GameObject당 1회만 할당한다. Initialize()는 풀에서
        // 꺼내 재스폰될 때마다 호출되는데, 여기서 new로 다시 만들면 GameObject/컴포넌트는
        // 재사용해도 도메인 상태는 스폰마다 새로 GC Alloc이 발생한다(6주차 프로파일링에서
        // MonsterView.Update() 콜스택 아래 GC.Alloc으로 실측 확인). Awake는 프리워밍 시
        // Instantiate 직후 1회만 호출되므로, 여기서 만든 인스턴스를 Initialize()에서
        // Reset()으로 재사용한다.
        private void Awake()
        {
            _health = new Health(1f);
            _ai = new MonsterAI(0f, 0f, 0f);
            // Animator는 모델 FBX 임포트 시 Humanoid Avatar와 함께 자동 생성되어 루트가 아닌
            // 모델 자식 GameObject에 붙는다(Monster.prefab 구조 참고) — 루트에서
            // GetComponentInChildren으로 찾아 캐싱한다. 애니메이션이 아직 연결되지 않은
            // 프리팹(에디터 툴 실행 전)에서는 null일 수 있어 이후 호출부는 모두 널 조건부(?.)로
            // 접근한다.
            _animator = GetComponentInChildren<Animator>();
        }

        public void Initialize(
            MonsterData data,
            IPlayerMotor playerMotor, IPlayerHealth playerHealth, IMonsterMovementSystem movementSystem,
            Action<MonsterView> onDeactivated)
        {
            _health.Reset(data.MaxHp);
            _ai.Reset(data.ChaseRange, data.AttackRange, data.AttackCooldownSeconds);
            _attackDamage = data.AttackDamage;
            _playerMotor = playerMotor;
            _playerHealth = playerHealth;
            _movementSystem = movementSystem;
            _onDeactivated = onDeactivated;
            _isDying = false;
            // 풀에서 재사용된 인스턴스는 이전 생애의 마지막 애니메이션 상태(Dead 등)가
            // Animator에 그대로 남아있을 수 있다. Update()의 상태-변화 감지 블록은
            // "이전 상태와 다를 때만" 갱신하는데, 리스폰 직후 _ai.CurrentState는 항상 Idle로
            // Reset되므로 새 생애 첫 프레임에도 Idle이 유지되는 경우(아직 플레이어가 안 보임)
            // 변화가 감지되지 않아 Dead 포즈가 그대로 남는다 — 그래서 여기서 명시적으로
            // Idle로 되돌린다.
            _animator?.SetInteger(AnimatorStateParam, (int)MonsterAIState.Idle);
        }

        // 사망 판정(_ai를 Dead로 전이)은 이 자리에서 동기적으로 확정하지만, 실제 디스폰
        // (_onDeactivated 호출)은 사망 애니메이션 재생 시간만큼 미룬다(2026-08-27, 좀비
        // 모델+애니메이션 연결과 함께 결정 — 즉시 사라지면 타격감이 없어 시체가 잠시
        // 남도록 변경). Update()의 다음 프레임을 기다리지 않고 여기서 즉시 _ai.Tick을
        // 호출하는 이유는 기존과 동일(distance는 isDead=true 분기에서 사용되지 않아
        // 0f로 넘겨도 무방 — MonsterAI.cs 참고).
        //
        // _isDying으로 중복 진입을 막는다: Health.TakeDamage 자체는 멱등(0 밑으로 안 내려감)
        // 하지만, 가드가 없으면 디스폰 대기 중(사망 애니메이션 재생 중)인 몬스터가 스플래시
        // 스킬에 또 맞을 때마다 새 UniTask.Delay를 중복 예약하게 되고, 그 몬스터가 풀에
        // 반환되어 완전히 다른 개체로 재사용된 뒤 예전 타이머가 만료되며 엉뚱한 인스턴스를
        // 잘못 디스폰시키는 문제로 이어진다.
        //
        // Chase 상태에서 죽으면 previousState/currentState 비교로 전이를 감지하는 Update()의
        // 등록 해제 블록이 더는 이 전이를 관측하지 못한다(다음 프레임엔 previousState도 이미
        // Dead라 "변화 없음"으로 보임) — 그래서 여기서 직접 등록을 해제해, 시체가 FlowField를
        // 계속 따라 미끄러지는 걸 막는다.
        public void TakeDamage(float amount)
        {
            _health.TakeDamage(amount);
            if (_health.IsDead && !_isDying)
            {
                _isDying = true;

                var wasChasing = _ai.CurrentState == MonsterAIState.Chase;
                _ai.Tick(0f, true, 0f);
                if (wasChasing)
                {
                    _movementSystem.Unregister(this);
                }

                _animator?.SetInteger(AnimatorStateParam, (int)MonsterAIState.Dead);
                DeactivateAfterDeathAnimationAsync().Forget();
            }
        }

        private async UniTaskVoid DeactivateAfterDeathAnimationAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_deathAnimationSeconds));
            _onDeactivated?.Invoke(this);
        }

        private void Update()
        {
            // previousState는 _ai.Tick 호출 "이전"에 반드시 읽어야 한다(Tick 내부에서 갱신됨).
            var previousState = _ai.CurrentState;
            var position = transform.position;
            var distance = Vector2.Distance(new Vector2(position.x, position.z), _playerMotor.Position);
            var currentState = _ai.Tick(distance, _health.IsDead, Time.deltaTime);

            // Register는 반드시 상태 전이 엣지에서만 호출해야 한다 — 중복 호출 시
            // JobMonsterMovementSystem은 인덱스가 깨지고 MonoMonsterMovementSystem은
            // 이동속도가 2배가 된다. currentState == previousState(Chase 유지 등)인
            // 프레임에는 아무 것도 호출하지 않도록 바깥에서 변화 여부를 먼저 확인한다.
            // (Dead로의 전이는 TakeDamage가 동기적으로 먼저 처리하므로 여기서는 관측되지
            // 않는다 — 위 TakeDamage 주석 참고)
            if (currentState != previousState)
            {
                if (currentState == MonsterAIState.Chase)
                {
                    _movementSystem.Register(this);
                }
                else if (previousState == MonsterAIState.Chase)
                {
                    _movementSystem.Unregister(this);
                }

                _animator?.SetInteger(AnimatorStateParam, (int)currentState);
            }

            // MonsterAI.cs의 게이트 설명대로 연결한다: Attack 상태에서 쿨다운이 돌아온
            // 프레임에만 TryConsumeAttack()이 true를 반환하므로, 상태 전이 여부와 무관하게
            // 매 프레임 호출해도 데미지가 중복 적용되지 않는다.
            if (currentState == MonsterAIState.Attack && _ai.TryConsumeAttack())
            {
                _playerHealth.TakeDamage(_attackDamage);
            }
        }
    }
}
