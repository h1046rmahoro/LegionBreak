using Object = UnityEngine.Object;

namespace LegionBreak.Editor.Validation
{
    /// <summary>
    /// Error: 런타임 예외나 게임 진행 불가로 이어지는 문제(참조 누락, 체력 0 등). 빌드를 막는다.
    /// Warning: 동작은 하지만 의도와 다를 가능성이 큰 값(쿨다운 0 등). 빌드는 통과시키고 로그만 남긴다.
    /// </summary>
    internal enum ValidationSeverity
    {
        Warning,
        Error,
    }

    internal readonly struct ValidationIssue
    {
        public ValidationIssue(ValidationSeverity severity, string message, string location, Object context, Object pingFallback)
        {
            Severity = severity;
            Message = message;
            Location = location;
            Context = context;
            PingFallback = pingFallback;
        }

        public ValidationSeverity Severity { get; }
        public string Message { get; }

        // 표시용 위치 문자열 (에셋 경로, 또는 "씬 > 하이어라키 경로").
        public string Location { get; }

        // 클릭 시 선택할 대상. 검사하려고 잠깐 열었다 닫은 씬의 오브젝트는 검사 후 파괴되어
        // null이 되므로, 그때는 PingFallback(해당 씬 에셋)을 대신 가리킨다.
        public Object Context { get; }
        public Object PingFallback { get; }
    }
}
