using System.Collections.Generic;
using System.Text;
using LegionBreak.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LegionBreak.Editor.Validation
{
    /// <summary>
    /// 밸런스 데이터(ScriptableObject) 수치와, 그 데이터를 들고 있는 씬/프리팹의 참조를 검사한다.
    /// "실무에서 데이터 실수를 어떻게 방지하는가"에 대한 답으로, 창(BalanceDataValidatorWindow)과
    /// 빌드 게이트(BalanceDataBuildGate)가 같은 규칙을 공유한다.
    ///
    /// 검사는 두 종류로 나뉜다.
    /// 1. 수치 규칙 — 데이터 타입별로 "이 값이면 게임이 어떻게 이상해지는가"를 아는 규칙이라
    ///    타입마다 직접 작성한다(쿨다운 0, 체력 0, AttackRange > ChaseRange 등).
    /// 2. 참조 규칙 — 필드 이름을 하드코딩하지 않고 SerializedProperty를 범용 순회한다.
    ///    Data 어셈블리의 SO 타입 필드가 비어 있거나, 어떤 참조든 끊겨(Missing) 있거나,
    ///    Addressable 참조가 비어 있으면 잡는다. 나중에 컴포넌트에 데이터 필드가 추가돼도
    ///    이 파일을 고치지 않고 자동으로 검사 대상이 된다.
    /// </summary>
    internal static class BalanceDataValidator
    {
        private const string ProjectRoot = "Assets/_Project";

        private static HashSet<string> _dataTypeNames;

        public static void ValidateAll(List<ValidationIssue> issues)
        {
            ValidateDataAssets(issues);
            ValidatePrefabs(issues);
            ValidateBuildScenes(issues);
        }

        // ---- 1. 수치 규칙 ----

        public static void ValidateDataAssets(List<ValidationIssue> issues)
        {
            var skillIdOwners = new Dictionary<string, SkillData>();
            foreach (var skill in LoadAll<SkillData>())
            {
                ValidateSkill(skill, skillIdOwners, issues);
            }

            foreach (var combat in LoadAll<CombatBalanceData>())
            {
                ValidateCombatBalance(combat, issues);
            }

            foreach (var monster in LoadAll<MonsterData>())
            {
                ValidateMonster(monster, issues);
            }

            foreach (var player in LoadAll<PlayerData>())
            {
                ValidatePlayer(player, issues);
            }

            foreach (var wave in LoadAll<WaveData>())
            {
                ValidateWave(wave, issues);
            }
        }

        private static void ValidateSkill(SkillData skill, Dictionary<string, SkillData> skillIdOwners, List<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(skill.SkillId))
            {
                AddAsset(issues, ValidationSeverity.Warning, skill, "SkillId가 비어 있음");
            }
            else if (skillIdOwners.TryGetValue(skill.SkillId, out var owner))
            {
                AddAsset(issues, ValidationSeverity.Warning, skill, $"SkillId '{skill.SkillId}'가 '{owner.name}'와 중복됨");
            }
            else
            {
                skillIdOwners.Add(skill.SkillId, skill);
            }

            if (skill.CooldownSeconds <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Warning, skill, $"쿨다운이 {skill.CooldownSeconds} — 입력할 때마다 시전되어 쿨다운 게이트가 무력화됨");
            }

            if (skill.BaseDamage <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Warning, skill, $"기본 데미지가 {skill.BaseDamage} — 맞아도 몬스터가 죽지 않음");
            }

            if (skill.Range <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Warning, skill, $"사거리가 {skill.Range} — 스플래시 반경 안에 들어오는 몬스터가 없음");
            }
        }

        private static void ValidateCombatBalance(CombatBalanceData combat, List<ValidationIssue> issues)
        {
            if (combat.CriticalChance < 0f || combat.CriticalChance > 1f)
            {
                AddAsset(issues, ValidationSeverity.Warning, combat, $"크리티컬 확률이 {combat.CriticalChance} — 0~1 범위 밖이라 항상/절대 크리티컬로 고정됨");
            }

            if (combat.CriticalDamageMultiplier < 1f)
            {
                AddAsset(issues, ValidationSeverity.Warning, combat, $"크리티컬 배율이 {combat.CriticalDamageMultiplier} — 1 미만이라 크리티컬이 오히려 약함");
            }
        }

        private static void ValidateMonster(MonsterData monster, List<ValidationIssue> issues)
        {
            // Health.IsDead는 CurrentHp <= 0이라, 스폰 즉시 죽은 상태로 시작한다.
            if (monster.MaxHp <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Error, monster, $"최대 체력이 {monster.MaxHp} — 스폰되자마자 사망 상태");
            }

            if (monster.ChaseRange <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Warning, monster, $"추격 범위가 {monster.ChaseRange} — 몬스터가 Idle에서 벗어나지 않음");
            }

            // MonsterAI FSM에서 AttackRange > ChaseRange면 Chase에 진입한 바로 다음 Tick에
            // Attack으로 넘어가, 접근하지 않고 추격 범위 밖 거리에서도 공격이 들어간다.
            if (monster.AttackRange > monster.ChaseRange)
            {
                AddAsset(issues, ValidationSeverity.Warning, monster, $"공격 범위({monster.AttackRange})가 추격 범위({monster.ChaseRange})보다 큼 — 접근 없이 먼 거리에서 공격함");
            }

            if (monster.AttackCooldownSeconds <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Warning, monster, $"공격 쿨다운이 {monster.AttackCooldownSeconds} — 매 프레임 공격(프레임레이트에 따라 DPS가 달라짐)");
            }

            if (monster.AttackDamage <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Warning, monster, $"공격력이 {monster.AttackDamage} — 플레이어가 피해를 받지 않음");
            }
        }

        private static void ValidatePlayer(PlayerData player, List<ValidationIssue> issues)
        {
            if (player.MaxHp <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Error, player, $"최대 체력이 {player.MaxHp} — 시작하자마자 게임오버");
            }
        }

        private static void ValidateWave(WaveData wave, List<ValidationIssue> issues)
        {
            if (wave.SpawnRadius <= 0f)
            {
                AddAsset(issues, ValidationSeverity.Warning, wave, $"스폰 반경이 {wave.SpawnRadius} — 모든 몬스터가 한 점에 겹쳐 스폰됨");
            }

            // WaveDirectorFactory가 Waves.Length를 바로 읽으므로 null이면 게임 시작 시 예외.
            if (wave.Waves == null)
            {
                AddAsset(issues, ValidationSeverity.Error, wave, "웨이브 배열이 null — WaveDirectorFactory에서 예외 발생");
                return;
            }

            if (wave.Waves.Length == 0)
            {
                AddAsset(issues, ValidationSeverity.Warning, wave, "웨이브가 하나도 없음 — 몬스터가 스폰되지 않음");
            }

            for (var i = 0; i < wave.Waves.Length; i++)
            {
                var entry = wave.Waves[i];
                if (entry.MonsterCount <= 0)
                {
                    AddAsset(issues, ValidationSeverity.Warning, wave, $"웨이브 {i}: 마릿수가 {entry.MonsterCount} — 빈 웨이브");
                }

                // WaveDirector는 한 Tick에 웨이브당 최대 1마리만 내보내므로 간격 0은
                // "프레임당 1마리"가 되어 스폰 속도가 프레임레이트에 종속된다.
                if (entry.SpawnIntervalSeconds <= 0f)
                {
                    AddAsset(issues, ValidationSeverity.Warning, wave, $"웨이브 {i}: 스폰 간격이 {entry.SpawnIntervalSeconds} — 프레임당 1마리로 프레임레이트에 종속됨");
                }

                if (entry.StartTimeSeconds < 0f)
                {
                    AddAsset(issues, ValidationSeverity.Warning, wave, $"웨이브 {i}: 시작 시각이 {entry.StartTimeSeconds} — 음수라 0초와 동일하게 동작");
                }
            }
        }

        // ---- 2. 참조 규칙 ----

        public static void ValidatePrefabs(List<ValidationIssue> issues)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ProjectRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // 모델(FBX) 임포트 결과도 t:Prefab에 걸리지만, 우리가 직접 구성하는 데이터가 아니라 제외.
                if (!path.EndsWith(".prefab"))
                {
                    continue;
                }

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null)
                {
                    // 프리팹 에셋 내부 자식 오브젝트는 Selection에 올려도 보이지 않으므로 루트를 가리킨다.
                    ValidateHierarchy(root.transform, path, root, null, issues);
                }
            }
        }

        public static void ValidateBuildScenes(List<ValidationIssue> issues)
        {
            var isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;

            foreach (var buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled)
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path) == null)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "Build Settings에 등록된 씬 파일이 없음", buildScene.path, null, null));
                    continue;
                }

                var scene = SceneManager.GetSceneByPath(buildScene.path);
                var openedHere = false;
                if (!scene.isLoaded)
                {
                    // Play 중엔 씬을 에디터 방식으로 열 수 없다. 이미 열린 씬만 검사한다.
                    if (isPlaying)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Play 중이라 열려 있지 않은 씬은 검사하지 않음", buildScene.path, null,
                            AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path)));
                        continue;
                    }

                    scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                    openedHere = true;
                }

                try
                {
                    ValidateScene(scene, issues);
                }
                finally
                {
                    if (openedHere)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }

        public static void ValidateScene(Scene scene, List<ValidationIssue> issues)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            foreach (var root in scene.GetRootGameObjects())
            {
                ValidateHierarchy(root.transform, scene.name, null, sceneAsset, issues);
            }
        }

        private static void ValidateHierarchy(Transform root, string locationPrefix, Object contextOverride, Object pingFallback, List<ValidationIssue> issues)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                var location = $"{locationPrefix} > {HierarchyPath(t, root)}";
                var context = contextOverride != null ? contextOverride : go;

                // 비활성 오브젝트/컴포넌트(예: 대조군으로 꺼둔 MonsterSpawnTester)는 실행되지 않으므로
                // 참조 검사에서 뺀다. 단, Missing Script는 켜고 끄는 것과 무관한 파손이라 항상 잡는다.
                var active = IsActiveInTree(t);

                var components = go.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, "Missing Script — 스크립트가 삭제되었거나 GUID가 깨짐", location, context, pingFallback));
                        continue;
                    }

                    if (!active || (component is Behaviour behaviour && !behaviour.enabled))
                    {
                        continue;
                    }

                    ValidateSerializedReferences(component, location, context, pingFallback, issues);
                }
            }
        }

        private static void ValidateSerializedReferences(Component component, string location, Object context, Object pingFallback, List<ValidationIssue> issues)
        {
            var so = new SerializedObject(component);
            var prop = so.GetIterator();
            var enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;
                var field = $"{component.GetType().Name}.{prop.displayName}";

                switch (prop.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        if (prop.name == "m_Script" || prop.objectReferenceValue != null)
                        {
                            break;
                        }

                        // 값이 null인데 인스턴스 ID가 남아 있으면 "할당은 됐었지만 대상이 사라진" Missing 상태.
                        if (prop.objectReferenceInstanceIDValue != 0)
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Error, $"{field}: 참조가 끊김(Missing)", location, context, pingFallback));
                        }
                        else if (IsDataReference(prop.type))
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Error, $"{field}: 데이터 에셋이 할당되지 않음", location, context, pingFallback));
                        }

                        break;

                    case SerializedPropertyType.String:
                        enterChildren = false;
                        break;

                    case SerializedPropertyType.Generic when prop.type.StartsWith("AssetReference"):
                        enterChildren = false;
                        ValidateAssetReference(prop, field, location, context, pingFallback, issues);
                        break;
                }
            }
        }

        private static void ValidateAssetReference(SerializedProperty prop, string field, string location, Object context, Object pingFallback, List<ValidationIssue> issues)
        {
            var guidProp = prop.FindPropertyRelative("m_AssetGUID");
            if (guidProp == null)
            {
                return;
            }

            var guid = guidProp.stringValue;
            if (string.IsNullOrEmpty(guid))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, $"{field}: Addressable 참조가 비어 있음", location, context, pingFallback));
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, $"{field}: Addressable 참조가 가리키는 에셋이 없음(GUID {guid})", location, context, pingFallback));
            }
        }

        // SerializedProperty.type은 스크립트 타입 참조를 "PPtr<$SkillData>" 형태로 준다.
        private static bool IsDataReference(string propertyType)
        {
            const string prefix = "PPtr<$";
            if (!propertyType.StartsWith(prefix) || !propertyType.EndsWith(">"))
            {
                return false;
            }

            var typeName = propertyType.Substring(prefix.Length, propertyType.Length - prefix.Length - 1);
            return DataTypeNames.Contains(typeName);
        }

        // Data 어셈블리의 SO 타입 목록. 도메인 리로드 시 static이 초기화되므로 타입 추가도 자동 반영된다.
        private static HashSet<string> DataTypeNames
        {
            get
            {
                if (_dataTypeNames != null)
                {
                    return _dataTypeNames;
                }

                _dataTypeNames = new HashSet<string>();
                var dataAssembly = typeof(SkillData).Assembly;
                foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
                {
                    if (type.Assembly == dataAssembly)
                    {
                        _dataTypeNames.Add(type.Name);
                    }
                }

                return _dataTypeNames;
            }
        }

        // ---- 유틸 ----

        private static IEnumerable<T> LoadAll<T>() where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { ProjectRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }

        private static void AddAsset(List<ValidationIssue> issues, ValidationSeverity severity, Object asset, string message)
        {
            issues.Add(new ValidationIssue(severity, message, AssetDatabase.GetAssetPath(asset), asset, null));
        }

        // 프리팹 에셋은 씬 밖에 있어 activeInHierarchy가 의미 없으므로 activeSelf를 부모까지 직접 따라간다.
        private static bool IsActiveInTree(Transform t)
        {
            for (var current = t; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        private static string HierarchyPath(Transform t, Transform root)
        {
            var sb = new StringBuilder(t.name);
            for (var current = t; current != root && current.parent != null;)
            {
                current = current.parent;
                sb.Insert(0, '/').Insert(0, current.name);
            }

            return sb.ToString();
        }
    }
}
