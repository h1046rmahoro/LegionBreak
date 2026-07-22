namespace LegionBreak.Application.Skills
{
    public interface IPlayerSkillCastUseCase
    {
        void Tick(float deltaTime);
        SkillCastResult Execute();
    }
}
