namespace LegionBreak.Application.Skills
{
    /// <summary>
    /// System.Random 기반이라 UnityEngine/외부 IO 의존이 없어, EventBus와 같은 이유로
    /// 별도 Infrastructure 어댑터 없이 Application에 바로 구현한다.
    /// </summary>
    public sealed class RandomProvider : IRandomProvider
    {
        private readonly System.Random _random = new System.Random();

        public float NextFloat01()
        {
            return (float)_random.NextDouble();
        }
    }
}
