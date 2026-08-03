using LegionBreak.Application.Player;
using LegionBreak.Data;
using LegionBreak.Domain.Combat;
using UnityEngine;

namespace LegionBreak.Infrastructure.Player
{
    /// <summary>
    /// IPlayerHealth의 실제 구현. TransformPlayerMotor와 같은 패턴 — Domain.Combat.Health를
    /// 감싸기만 하는 얇은 어댑터이며, 씬의 Player GameObject에 부착한다.
    /// Health는 MonsterView(몬스터 쪽 소비처)와 공유하는 동일 클래스다 — 체력 판정 규칙이
    /// 우연히 같은 채로 갈라지는 분기가 없어 하나로 합쳤다(Health.cs 주석 참고).
    /// </summary>
    public class PlayerHealthController : MonoBehaviour, IPlayerHealth
    {
        [SerializeField] private PlayerData _playerData;

        private Health _health;

        public float MaxHp => _health.MaxHp;
        public float CurrentHp => _health.CurrentHp;
        public bool IsDead => _health.IsDead;

        private void Awake()
        {
            _health = new Health(_playerData.MaxHp);
        }

        public void TakeDamage(float amount)
        {
            _health.TakeDamage(amount);
        }
    }
}
