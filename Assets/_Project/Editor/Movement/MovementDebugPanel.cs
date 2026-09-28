using UnityEditor;
using UnityEditor.Overlays;

namespace LegionBreak.Editor.Movement
{
    /// <summary>
    /// MovementDebugGizmo의 레이어 토글과 실시간 수치를 보여주는 씬 뷰 오버레이 패널.
    /// 씬 뷰 툴바 오버레이 메뉴(` 키) 또는 Tools > LegionBreak > Movement Debug Overlay로 연다.
    /// 패널이 꺼진 씬 뷰에는 기즈모도 그리지 않으므로, 이 패널의 표시 여부가 곧 전체 on/off다.
    ///
    /// 수치는 MovementDebugGizmo가 Repaint마다 MonsterSeparationJob과 같은 순회로 다시 센 값이라,
    /// "Spatial Hash 거리 비교 횟수 vs 전수 검사(n×(n-1))"를 실제 런타임 분포 그대로 보여준다 —
    /// 3주차 프로파일링의 O(n²) → O(n) 근사 결과를 숫자로 재확인하는 용도.
    /// </summary>
    [Overlay(typeof(SceneView), MovementDebugPanel.OverlayId, "Movement Debug")]
    public class MovementDebugPanel : IMGUIOverlay
    {
        public const string OverlayId = "LegionBreak/MovementDebug";

        [MenuItem("Tools/LegionBreak/Movement Debug Overlay")]
        private static void Open()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            if (sceneView.TryGetOverlay(OverlayId, out var overlay))
            {
                overlay.displayed = true;
                overlay.collapsed = false;
            }

            sceneView.Focus();
        }

        public override void OnGUI()
        {
            MovementDebugGizmo.ShowWalkableGrid = EditorGUILayout.ToggleLeft("Walkable Grid (장애물)", MovementDebugGizmo.ShowWalkableGrid);
            MovementDebugGizmo.ShowDistanceHeatmap = EditorGUILayout.ToggleLeft("BFS 거리 히트맵", MovementDebugGizmo.ShowDistanceHeatmap);
            MovementDebugGizmo.ShowFlowArrows = EditorGUILayout.ToggleLeft("Flow Field 방향", MovementDebugGizmo.ShowFlowArrows);
            MovementDebugGizmo.ShowHashCells = EditorGUILayout.ToggleLeft("Spatial Hash 셀", MovementDebugGizmo.ShowHashCells);
            MovementDebugGizmo.ShowSelectedQuery = EditorGUILayout.ToggleLeft("선택 몬스터 이웃 조회", MovementDebugGizmo.ShowSelectedQuery);

            EditorGUILayout.Space(4f);

            // LegionBreak.Application 네임스페이스가 UnityEngine.Application을 가려서 전체 이름을 쓴다.
            if (!UnityEngine.Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 모드에서 MonsterMovementResolver의 런타임 상태를 그립니다.", MessageType.Info);
                return;
            }

            var s = MovementDebugGizmo.Stats;
            if (!s.Available)
            {
                EditorGUILayout.HelpBox("씬에서 활성 MonsterMovementResolver를 찾지 못했습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Flow Field", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"그리드 {s.GridWidth}×{s.GridHeight}  장애물 {s.BlockedCells}셀");
            EditorGUILayout.LabelField(s.HasFlowField
                ? $"최대 BFS 거리 {s.MaxDistance}  도달 불가 {s.UnreachableCells}셀"
                : "아직 생성 전(몬스터가 등록되면 생성)");

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Spatial Hash (겹침 회피)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"몬스터 {s.MonsterCount}  점유 셀 {s.OccupiedCells}  셀당 최대 {s.MaxPerCell}");
            EditorGUILayout.LabelField($"거리 비교 {s.PairChecks:N0}회 / 전수 검사 {s.BruteForcePairChecks:N0}회");
            if (s.BruteForcePairChecks > 0)
            {
                EditorGUILayout.LabelField($"→ 전수 대비 {100.0 * s.PairChecks / s.BruteForcePairChecks:0.##}%  (해시 충돌분 {s.HashCollisionChecks:N0}회)");
            }

            if (MovementDebugGizmo.ShowSelectedQuery && !s.HasSelection)
            {
                EditorGUILayout.LabelField("Hierarchy에서 몬스터를 선택하면 이웃 조회를 표시", EditorStyles.miniLabel);
            }
        }
    }
}
