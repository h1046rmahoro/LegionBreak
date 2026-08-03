namespace LegionBreak.Application.Skills
{
    /// <summary>
    /// 스킬 시전 시도 결과. 쿨다운 중이면 Success=false로 데미지 없이 반환된다.
    /// </summary>
    public readonly struct SkillCastResult
    {
        public static readonly SkillCastResult Failed = new SkillCastResult(false, 0f, 0, false);

        public bool Success { get; }
        public float Damage { get; }
        public int HitCount { get; }
        public bool IsCritical { get; }

        public SkillCastResult(bool success, float damage, int hitCount, bool isCritical)
        {
            Success = success;
            Damage = damage;
            HitCount = hitCount;
            IsCritical = isCritical;
        }
    }
}
