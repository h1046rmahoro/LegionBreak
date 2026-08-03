namespace LegionBreak.Domain.Skills
{
    public interface ICriticalHitJudge
    {
        bool Judge(float criticalChance, float roll01);
    }
}
