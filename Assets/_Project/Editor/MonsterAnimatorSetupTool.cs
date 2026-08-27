using LegionBreak.Infrastructure.Spawning;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LegionBreak.Editor
{
    /// <summary>
    /// 몬스터 모델(ZombiegirlWKurniawan)에 붙일 AnimatorController를 코드로 생성하고
    /// Monster.prefab의 Animator에 연결하는 1회성 에디터 툴.
    ///
    /// AnimatorController(.controller)를 텍스트/YAML로 직접 손으로 작성하는 대신 이 방식을 택한
    /// 이유: 컨트롤러 에셋은 상태/전이/조건이 서로를 GUID로 참조하는 복잡한 그래프 구조라, 손으로
    /// 편집하면 사소한 실수로 열리지 않는 에셋을 만들 위험이 크다. 반면 UnityEditor.Animations의
    /// AnimatorController API는 Unity가 공식 지원하는 안정된 공개 API라 Editor 안에서 실행하며
    /// 즉시 검증된다 — 9주차(커스텀 에디터 툴) 로드맵과도 같은 방향(코드로 데이터/에셋을 다루는 것)이라
    /// 시기를 앞당겨 미리 이 패턴을 적용했다.
    ///
    /// 상태 파라미터는 MonsterAIState(Idle=0, Chase=1, Attack=2, Dead=3)의 enum 순서값과 그대로
    /// 맞춘 int 하나("State")로 단순화했다 — Animator는 MonsterAI가 이미 계산한 FSM 결과를
    /// 그대로 따라가기만 하면 되므로(판단 로직은 Domain에 있음), 별도의 bool 파라미터 조합이나
    /// Animator 자체의 판단 로직을 두지 않는다.
    /// </summary>
    public static class MonsterAnimatorSetupTool
    {
        private const string AnimationsFolder = "Assets/_Project/Presentation/Spawning/Animations/ZombiegirlWKurniawan";
        private const string ControllerPath = "Assets/_Project/Presentation/Spawning/Model/ZombiegirlWKurniawan/ZombieAnimatorController.controller";
        private const string PrefabPath = "Assets/_Project/Presentation/Spawning/Monster.prefab";

        [MenuItem("Tools/LegionBreak/Build Monster Animator Controller")]
        public static void Build()
        {
            var idleClip = LoadClip("zombie idle.fbx", loop: true);
            var chaseClip = LoadClip("zombie running.fbx", loop: true);
            var attackClip = LoadClip("zombie attack.fbx", loop: false);
            var deadClip = LoadClip("zombie death.fbx", loop: false);

            if (idleClip == null || chaseClip == null || attackClip == null || deadClip == null)
            {
                Debug.LogError("[MonsterAnimatorSetupTool] 필요한 애니메이션 클립을 찾지 못했습니다. " +
                                $"{AnimationsFolder} 아래 파일명을 확인하세요.");
                return;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("State", AnimatorControllerParameterType.Int);

            var stateMachine = controller.layers[0].stateMachine;

            var idleState = stateMachine.AddState("Idle");
            idleState.motion = idleClip;
            var chaseState = stateMachine.AddState("Chase");
            chaseState.motion = chaseClip;
            var attackState = stateMachine.AddState("Attack");
            attackState.motion = attackClip;
            var deadState = stateMachine.AddState("Dead");
            deadState.motion = deadClip;

            stateMachine.defaultState = idleState;

            // MonsterAIState 값(Idle=0/Chase=1/Attack=2/Dead=3)과 1:1로 대응하는 Any State 전이.
            // 상태 갱신은 MonsterView.Update()가 FSM 상태가 "바뀐 프레임에만" State를 새로 쓰므로,
            // 매 프레임 재평가되는 Any State 조건이라도 같은 상태로의 재진입(리스타트)은
            // canTransitionToSelf = false로 막아둔다.
            AddAnyStateTransition(stateMachine, idleState, 0);
            AddAnyStateTransition(stateMachine, chaseState, 1);
            AddAnyStateTransition(stateMachine, attackState, 2);
            AddAnyStateTransition(stateMachine, deadState, 3);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            AssignToPrefab(controller, deadClip.length);

            Debug.Log($"[MonsterAnimatorSetupTool] Animator Controller 생성 및 Monster.prefab 연결 완료: {ControllerPath} " +
                      $"(사망 애니메이션 길이 {deadClip.length:F2}초를 MonsterView._deathAnimationSeconds에 자동 반영)");
        }

        private static void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination, int stateValue)
        {
            var transition = stateMachine.AddAnyStateTransition(destination);
            transition.AddCondition(AnimatorConditionMode.Equals, stateValue, "State");
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.canTransitionToSelf = false;
        }

        private static AnimationClip LoadClip(string fbxFileName, bool loop)
        {
            var path = $"{AnimationsFolder}/{fbxFileName}";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[MonsterAnimatorSetupTool] 모델 임포터를 찾을 수 없습니다: {path}");
                return null;
            }

            // 캐릭터 모델(ZombiegirlWKurniawan)은 Humanoid로 임포트했지만, 애니메이션 전용
            // FBX(Mixamo에서 "without skin"으로 받은 개별 모션 파일)는 Unity가 기본값으로
            // Generic + No Avatar로 임포트한다(실측: animationType=2/avatarSetup=0) — 서로
            // 다른 리그 타입이라 Mecanim이 리타겟팅을 못 해 아예 재생되지 않는다(Idle조차
            // T포즈로 멈춰있던 실제 원인). 여기서 Humanoid로 강제 전환하고 이 FBX 자신의
            // 스켈레톤으로부터 Avatar를 새로 만든다 — 대상 Animator의 Avatar와 물리적으로
            // 같은 객체일 필요는 없다. Mecanim의 Humanoid 리타겟팅은 두 Avatar가 각각 유효한
            // Humanoid로 구성되어 있기만 하면 서로 다른 골격 비율/이름이라도 동작하도록
            // 설계되어 있다.
            var needsReimport = importer.animationType != ModelImporterAnimationType.Human;
            if (needsReimport)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            // Idle/Chase는 반복 재생, Attack/Dead는 1회 재생이어야 하므로 임포트 설정의
            // loopTime을 클립별로 맞춰준다(Mixamo FBX 기본값은 loopTime=false).
            var clipSettings = importer.defaultClipAnimations;
            if (clipSettings.Length > 0)
            {
                var settings = clipSettings[0];
                settings.loopTime = loop;
                importer.clipAnimations = clipSettings;
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    return clip;
                }
            }

            Debug.LogError($"[MonsterAnimatorSetupTool] {path}에서 AnimationClip을 찾지 못했습니다.");
            return null;
        }

        private static void AssignToPrefab(AnimatorController controller, float deathClipLength)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            var animator = prefabRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("[MonsterAnimatorSetupTool] 프리팹에서 Animator 컴포넌트를 찾지 못했습니다 — " +
                                "모델 임포트 시 Humanoid Avatar가 정상 생성됐는지 확인하세요.");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            animator.runtimeAnimatorController = controller;

            // 몬스터 이동은 FlowFieldSeekJob이 매 프레임 transform.position/rotation을 직접
            // 쓰는 방식이라, Animator의 Root Motion이 켜져 있으면 같은 프레임에 두 시스템이
            // Transform을 각자 건드리며 충돌해 순간이동/불규칙한 방향 튐이 발생한다(2026-08-28
            // 실측 확인). 컨트롤러를 재연결할 때마다 이 값을 매번 명시적으로 꺼서, 나중에
            // 누군가 Inspector에서 실수로 다시 켜거나 Animator 재생성 시 기본값(true)으로
            // 되돌아가는 걸 방지한다.
            animator.applyRootMotion = false;

            // 사망 애니메이션 길이를 MonsterView._deathAnimationSeconds에 직접 써준다 — 사람이
            // Animation 창에서 클립 길이를 보고 다시 다른 필드에 타이핑해 옮기는 수작업을
            // 없애, 클립을 나중에 다른 것으로 바꿔도 이 툴만 재실행하면 값이 항상 실제
            // 클립 길이와 어긋나지 않게 유지된다.
            var monsterView = prefabRoot.GetComponent<MonsterView>();
            if (monsterView != null)
            {
                var serializedView = new SerializedObject(monsterView);
                var deathSecondsProperty = serializedView.FindProperty("_deathAnimationSeconds");
                if (deathSecondsProperty != null)
                {
                    deathSecondsProperty.floatValue = deathClipLength;
                    serializedView.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogWarning("[MonsterAnimatorSetupTool] MonsterView._deathAnimationSeconds 필드를 찾지 못해 " +
                                      "자동 반영을 건너뜁니다 — 필드명이 바뀌었는지 확인하세요.");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
