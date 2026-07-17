namespace LegionBreak.Application.UI
{
    /// <summary>
    /// MVP의 View 포트. Presenter는 이 인터페이스로만 View에 접근하고, 구체 MonoBehaviour를
    /// 직접 참조하지 않는다.
    /// </summary>
    public interface IMonsterCountView
    {
        void SetCount(int count);
    }
}
