namespace LegionBreak.Domain.Combat
{
    /// <summary>
    /// 체력 판정. 데미지를 받아 죽음을 판단하는 실제 도메인 규칙이라 Skill 쿨다운 판정과
    /// 같은 이유로 Domain에 상태로 둔다.
    /// 원래 MonsterHealth/PlayerHealth로 각자 두고 있었으나, 두 판정 규칙이 우연히 동일한
    /// 형태(MaxHp/CurrentHp/IsDead/TakeDamage)인 채로 실제로는 아무 분기도 갈라지지 않아
    /// (플레이어 리젠/실드처럼 몬스터와 달라지는 기능이 아직 없음) 하나로 합쳤다.
    /// 나중에 한쪽만 달라지는 기능이 생기면 그때 다시 갈라진다 — Domain 분리 기준의
    /// "실제로 교체 가능한 지점을 보여주려는 것인가" 판단 그대로.
    /// </summary>
    public sealed class Health
    {
        public float MaxHp { get; private set; }
        public float CurrentHp { get; private set; }
        public bool IsDead => CurrentHp <= 0f;

        public Health(float maxHp)
        {
            Reset(maxHp);
        }

        // 몬스터는 오브젝트 풀링으로 GameObject/컴포넌트를 재사용하지만, Health를 스폰마다
        // new로 다시 만들면 그 재사용 효과가 도메인 상태에는 적용되지 않아 GC Alloc이
        // 그대로 남는다(6주차 프로파일링에서 실측 확인). 그래서 인스턴스는 최초 1회만 만들고
        // 재스폰 시에는 Reset으로 값만 되돌린다.
        public void Reset(float maxHp)
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
