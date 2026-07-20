using R3;

namespace LegionBreak.Application.Events
{
    /// <summary>
    /// 타입 기반 이벤트 버스. 발행자와 구독자가 서로를 몰라도 되게 한다.
    /// </summary>
    public interface IEventBus
    {
        void Publish<T>(T eventData);
        Observable<T> Receive<T>();
    }
}
