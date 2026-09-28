using System.Runtime.CompilerServices;

// 이동 파이프라인(MonsterMovementResolver/FlowFieldGenerator)의 내부 버퍼를 씬 뷰 디버그
// 오버레이(Editor/Movement/MovementDebugGizmo)에만 노출한다. public으로 열면 Presentation 등
// 런타임 어셈블리도 이 버퍼에 의존할 수 있게 되므로, 접근 범위를 Editor 어셈블리로 한정한다.
[assembly: InternalsVisibleTo("LegionBreak.Editor")]
