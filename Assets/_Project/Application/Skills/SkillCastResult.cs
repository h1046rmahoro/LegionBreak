namespace LegionBreak.Application.Skills
{
    /// <summary>
    /// 스킬 시전 시도 결과. 쿨다운 중이면 Success=false로 데미지 없이 반환된다.
    /// IsCritical은 아직 없음 — 크리티컬 확률 판정 시스템이 생기면 함께 추가한다.
    /// </summary>
    public readonly struct SkillCastResult
    {
        public static readonly SkillCastResult Failed = new SkillCastResult(false, 0f, 0);

        public bool Success { get; }
        public float Damage { get; }
        public int HitCount { get; }

        public SkillCastResult(bool success, float damage, int hitCount)
        {
            Success = success;
            Damage = damage;
            HitCount = hitCount;
        }
    }
}
