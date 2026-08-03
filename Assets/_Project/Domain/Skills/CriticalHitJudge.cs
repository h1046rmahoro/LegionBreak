namespace LegionBreak.Domain.Skills
{
    /// <summary>
    /// 크리티컬 발동 여부를 판정하는 도메인 규칙. 난수(roll01) 생성 자체는 도메인 지식이
    /// 아니므로 Application이 만들어 순수 입력값으로 넘기고, 여기서는 그 값과 확률을
    /// 비교해 크리티컬인지만 판정한다.
    /// </summary>
    public sealed class CriticalHitJudge : ICriticalHitJudge
    {
        public bool Judge(float criticalChance, float roll01)
        {
            return roll01 < criticalChance;
        }
    }
}
