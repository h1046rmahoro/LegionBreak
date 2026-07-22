namespace LegionBreak.Domain.Monsters
{
    /// <summary>
    /// 몬스터 체력 판정. 데미지를 받아 죽음을 판단하는 실제 도메인 규칙이라
    /// Skill 쿨다운 판정과 같은 이유로 Domain에 상태로 둔다.
    /// </summary>
    public sealed class MonsterHealth
    {
        public float MaxHp { get; }
        public float CurrentHp { get; private set; }
        public bool IsDead => CurrentHp <= 0f;

        public MonsterHealth(float maxHp)
        {
            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        public void TakeDamage(float amount)
        {
            CurrentHp = System.Math.Max(0f, CurrentHp - amount);
        }
    }
}
