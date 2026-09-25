using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegionBreak.Editor.Validation
{
    /// <summary>
    /// 빌드 시 BalanceDataValidator를 자동 실행해 Error가 하나라도 있으면 빌드를 중단시킨다.
    /// 검증 창은 사람이 눌러야 돌지만, 이 게이트는 누가 빌드하든 항상 돈다 — "데이터 실수가
    /// 배포물까지 가지 않게 하는" 마지막 방어선. Warning은 의도된 값일 수 있어 로그만 남긴다.
    ///
    /// 씬은 IPreprocessBuildWithReport에서 직접 열지 않고 IProcessSceneWithReport로 검사한다.
    /// 빌드 파이프라인이 씬을 하나씩 로드해 넘겨주는 공식 훅이라 빌드 중 씬 열기/닫기를
    /// 직접 할 필요가 없다.
    /// </summary>
    internal sealed class BalanceDataBuildGate : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var issues = new List<ValidationIssue>();
            BalanceDataValidator.ValidateDataAssets(issues);
            BalanceDataValidator.ValidatePrefabs(issues);
            FailOnErrors(issues, "데이터 에셋/프리팹");
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // Play 모드 진입 시에도 호출된다(report == null). Play는 막지 않는다 — 에디터에서
            // 값을 바꿔가며 실험하는 흐름은 방해하지 않고, 배포물만 보호하는 게 목적이다.
            if (report == null)
            {
                return;
            }

            var issues = new List<ValidationIssue>();
            BalanceDataValidator.ValidateScene(scene, issues);
            FailOnErrors(issues, scene.path);
        }

        private static void FailOnErrors(List<ValidationIssue> issues, string target)
        {
            var errorCount = 0;
            foreach (var issue in issues)
            {
                var text = $"[Data Validator] {issue.Message}\n{issue.Location}";
                if (issue.Severity == ValidationSeverity.Error)
                {
                    errorCount++;
                    Debug.LogError(text, issue.Context);
                }
                else
                {
                    Debug.LogWarning(text, issue.Context);
                }
            }

            if (errorCount > 0)
            {
                throw new BuildFailedException(
                    $"[Data Validator] {target}에서 데이터 에러 {errorCount}건 — 빌드 중단. Tools > LegionBreak > Balance Data Validator에서 확인하세요.");
            }
        }
    }
}
