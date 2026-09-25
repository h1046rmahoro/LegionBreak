using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LegionBreak.Editor.Validation
{
    /// <summary>
    /// BalanceDataValidator 결과를 에러/경고 리스트로 보여주고, 항목을 클릭하면 문제 에셋/오브젝트로
    /// 이동시키는 창. 검사 규칙 자체는 빌드 게이트(BalanceDataBuildGate)와 공유한다 — 여기서
    /// 초록불이면 빌드도 통과한다는 게 보장되도록.
    ///
    /// 창을 열 때 자동 실행하지 않는다. 검사는 Build Settings의 씬 중 열려 있지 않은 것을
    /// 잠깐 additive로 열었다 닫는데, OnEnable은 스크립트 재컴파일마다 다시 불리므로 자동
    /// 실행하면 코드를 저장할 때마다 하이어라키가 깜빡인다.
    /// </summary>
    public sealed class BalanceDataValidatorWindow : EditorWindow
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
        private bool _hasRun;
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private int _errorCount;
        private int _warningCount;
        private Vector2 _scroll;

        private GUIContent _errorIcon;
        private GUIContent _warningIcon;
        private GUIStyle _messageStyle;
        private GUIStyle _locationStyle;

        [MenuItem("Tools/LegionBreak/Balance Data Validator")]
        private static void Open()
        {
            GetWindow<BalanceDataValidatorWindow>("Data Validator").minSize = new Vector2(560, 260);
        }

        private void Run()
        {
            _issues.Clear();
            BalanceDataValidator.ValidateAll(_issues);

            // 에러를 위로. 같은 심각도 안에서는 검사 순서(에셋 → 프리팹 → 씬)를 유지한다.
            var ordered = new List<ValidationIssue>(_issues.Count);
            ordered.AddRange(_issues.FindAll(i => i.Severity == ValidationSeverity.Error));
            ordered.AddRange(_issues.FindAll(i => i.Severity == ValidationSeverity.Warning));
            _issues.Clear();
            _issues.AddRange(ordered);

            _errorCount = _issues.FindAll(i => i.Severity == ValidationSeverity.Error).Count;
            _warningCount = _issues.Count - _errorCount;
            _hasRun = true;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (!_hasRun)
            {
                EditorGUILayout.HelpBox(
                    "'검증 실행'을 누르면 Assets/_Project의 밸런스 데이터 수치, 프리팹, Build Settings 씬의 참조를 검사합니다.\n" +
                    "에러가 있으면 빌드도 차단됩니다.",
                    MessageType.Info);
                return;
            }

            if (_issues.Count == 0)
            {
                EditorGUILayout.HelpBox("문제 없음.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var issue in _issues)
            {
                var isError = issue.Severity == ValidationSeverity.Error;
                if ((isError && !_showErrors) || (!isError && !_showWarnings))
                {
                    continue;
                }

                DrawIssue(issue, isError);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("검증 실행", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                Run();
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!_hasRun))
            {
                _showErrors = GUILayout.Toggle(_showErrors, new GUIContent($" {_errorCount}", _errorIcon.image), EditorStyles.toolbarButton, GUILayout.Width(50));
                _showWarnings = GUILayout.Toggle(_showWarnings, new GUIContent($" {_warningCount}", _warningIcon.image), EditorStyles.toolbarButton, GUILayout.Width(50));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawIssue(ValidationIssue issue, bool isError)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(isError ? _errorIcon : _warningIcon, GUILayout.Width(20), GUILayout.Height(20));
            EditorGUILayout.BeginVertical();
            GUILayout.Label(issue.Message, _messageStyle);
            GUILayout.Label(issue.Location, _locationStyle);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            var rect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SelectTarget(issue);
                Event.current.Use();
            }
        }

        private static void SelectTarget(ValidationIssue issue)
        {
            if (issue.Context != null)
            {
                Selection.activeObject = issue.Context;
                EditorGUIUtility.PingObject(issue.Context);
            }
            else if (issue.PingFallback != null)
            {
                // 검사용으로 잠깐 열었다 닫은 씬의 오브젝트 — 씬 에셋을 대신 가리킨다.
                EditorGUIUtility.PingObject(issue.PingFallback);
            }
        }

        private void EnsureStyles()
        {
            if (_messageStyle != null)
            {
                return;
            }

            _errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml");
            _warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
            _messageStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            _locationStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }
    }
}
