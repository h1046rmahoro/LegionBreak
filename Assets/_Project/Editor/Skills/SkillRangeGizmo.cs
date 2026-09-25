using LegionBreak.Data;
using LegionBreak.Presentation.Skills;
using UnityEditor;
using UnityEngine;

namespace LegionBreak.Editor.Skills
{
    /// <summary>
    /// 선택한 SkillData의 사거리(Range)를 씬 뷰에 원으로 즉시 시각화하고, 핸들을 드래그해
    /// 반경을 직접 조절하게 하는 툴(CLAUDE.md 9주차). 인스펙터에 숫자를 입력하고 Play해서
    /// 확인하는 대신, 씬에서 바로 보며 튜닝한다.
    ///
    /// 스킬은 클릭 지점 중심의 원형 스플래시(PooledMonsterSpawner.ApplyDamageInRange가 XZ 거리로
    /// 판정)라 각도 개념이 없어 부채꼴이 아니라 XZ 평면의 원으로 그린다.
    ///
    /// 기준 위치: Play 중이 아니면 월드 원점, Play 중이면 실제로 마지막 시전에 성공한 발동
    /// 지점(PlayerSkillInputController.LastCastPoint, 아직 시전 전이면 원점).
    /// 주의: GameLifetimeScope가 시작 시 SkillData를 Skill로 한 번 변환해 굽기 때문에 Play 중
    /// Range를 바꿔도 실제 판정은 다음 Play부터 반영된다. 기즈모는 에셋의 현재 값을 그린다.
    /// </summary>
    [InitializeOnLoad]
    public static class SkillRangeGizmo
    {
        // 핸들 드래그 시 값이 0.1234처럼 지저분해지지 않게 잡는 스냅 단위.
        private const float RangeSnap = 0.05f;

        private static readonly Color FillColor = new Color(1f, 0.35f, 0.2f, 0.15f);
        private static readonly Color LineColor = new Color(1f, 0.35f, 0.2f, 0.95f);

        private static PlayerSkillInputController _controller;
        private static bool _controllerSearched;
        private static Vector2? _lastSeenCastPoint;

        static SkillRangeGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += RepaintOnCastPointChange;
            EditorApplication.playModeStateChanged += _ =>
            {
                _controller = null;
                _controllerSearched = false;
                _lastSeenCastPoint = null;
                SceneView.RepaintAll();
            };
            Selection.selectionChanged += SceneView.RepaintAll;
        }

        private static SkillData GetTarget()
        {
            // 프로젝트 창에서 SkillData를 직접 선택했으면 그것을, 아니면 Skill Catalog 창의 선택을 쓴다.
            return Selection.activeObject as SkillData ?? SkillCatalogWindow.CurrentSelected;
        }

        private static Vector3 ResolveCenter()
        {
            // LegionBreak.Application 네임스페이스가 UnityEngine.Application을 가려서 전체 이름을 쓴다.
            if (UnityEngine.Application.isPlaying)
            {
                var controller = GetController();
                if (controller != null && controller.LastCastPoint.HasValue)
                {
                    var p = controller.LastCastPoint.Value;
                    return new Vector3(p.x, 0f, p.y);
                }
            }

            return Vector3.zero;
        }

        private static PlayerSkillInputController GetController()
        {
            if (_controller == null && !_controllerSearched)
            {
                _controller = Object.FindFirstObjectByType<PlayerSkillInputController>();
                _controllerSearched = _controller == null;
            }

            return _controller;
        }

        // Play 중 시전할 때마다 씬 뷰가 다시 그려지도록: 마지막 발동 지점이 바뀔 때만 갱신한다.
        private static void RepaintOnCastPointChange()
        {
            if (!UnityEngine.Application.isPlaying || GetTarget() == null)
            {
                return;
            }

            var controller = GetController();
            var current = controller != null ? controller.LastCastPoint : null;
            if (current != _lastSeenCastPoint)
            {
                _lastSeenCastPoint = current;
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var skill = GetTarget();
            if (skill == null)
            {
                return;
            }

            var center = ResolveCenter();
            var so = new SerializedObject(skill);
            var rangeProp = so.FindProperty("_range");
            var range = rangeProp.floatValue;

            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = FillColor;
                Handles.DrawSolidDisc(center, Vector3.up, range);
                Handles.color = LineColor;
                Handles.DrawWireDisc(center, Vector3.up, range);
                Handles.DrawSolidDisc(center, Vector3.up, HandleUtility.GetHandleSize(center) * 0.04f);
                Handles.Label(
                    center + Vector3.up * 0.2f,
                    $"{skill.DisplayName}  Range {range:0.##}",
                    EditorStyles.boldLabel);
            }

            // 반경 조절 핸들: 원의 +X 지점을 잡아 끌면 Range가 바뀐다.
            var handlePos = center + Vector3.right * range;
            var size = HandleUtility.GetHandleSize(handlePos) * 0.1f;

            EditorGUI.BeginChangeCheck();
            Handles.color = LineColor;
            var moved = Handles.Slider(handlePos, Vector3.right, size, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                var newRange = Mathf.Max(0f, Mathf.Round((moved.x - center.x) / RangeSnap) * RangeSnap);
                so.Update();
                rangeProp.floatValue = newRange;
                so.ApplyModifiedProperties(); // Undo(Ctrl+Z) 기록 포함
            }
        }
    }
}
