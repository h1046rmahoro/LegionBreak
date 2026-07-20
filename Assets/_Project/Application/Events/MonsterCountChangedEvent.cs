namespace LegionBreak.Application.Events
{
    public readonly struct MonsterCountChangedEvent
    {
        public int Count { get; }

        public MonsterCountChangedEvent(int count)
        {
            Count = count;
        }
    }
}
