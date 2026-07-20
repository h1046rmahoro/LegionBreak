using System;
using System.Collections.Generic;
using R3;

namespace LegionBreak.Application.Events
{
    /// <summary>
    /// IEventBus의 기본 구현. 이벤트 타입마다 Subject를 지연 생성해 보관한다.
    /// Domain/Infrastructure 어디에도 속하지 않는 범용 Application 서비스라 별도
    /// 어댑터 없이 이 계층에 바로 구현한다(CLAUDE.md 폴더 구조상 "Application: 이벤트 버스").
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, object> _subjectsByType = new Dictionary<Type, object>();

        public void Publish<T>(T eventData)
        {
            GetOrCreateSubject<T>().OnNext(eventData);
        }

        public Observable<T> Receive<T>()
        {
            return GetOrCreateSubject<T>();
        }

        private Subject<T> GetOrCreateSubject<T>()
        {
            var type = typeof(T);
            if (!_subjectsByType.TryGetValue(type, out var subject))
            {
                subject = new Subject<T>();
                _subjectsByType[type] = subject;
            }

            return (Subject<T>)subject;
        }
    }
}
