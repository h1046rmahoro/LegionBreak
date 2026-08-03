namespace LegionBreak.Application.Player
{
    /// <summary>
    /// 플레이어 체력에 데미지를 적용하는 포트(port). Infrastructure에서 구현한다.
    /// IPlayerMotor와 같은 이유로 Application에 인터페이스를 두고, 실제 판정 상태
    /// (Domain.Combat.Health, 몬스터와 공유하는 클래스)는 Infrastructure 구현체가 감싼다.
    /// </summary>
    public interface IPlayerHealth
    {
        float MaxHp { get; }
        float CurrentHp { get; }
        bool IsDead { get; }
        void TakeDamage(float amount);
    }
}
