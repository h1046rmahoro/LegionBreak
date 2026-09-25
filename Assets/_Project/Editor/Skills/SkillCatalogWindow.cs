using System;
using System.Collections.Generic;
using LegionBreak.Data;
using UnityEditor;
using UnityEngine;

namespace LegionBreak.Editor.Skills
{
    /// <summary>
    /// 프로젝트의 모든 SkillData 에셋을 한 창에서 리스트업하고, 새로 만들고/복제/삭제하고,
    /// 스탯을 인라인으로 편집하는 툴. 목적은 "코드 수정 없이 신규 스킬을 만들 수 있다"를
    /// 실제로 시연하는 것(CLAUDE.md 9주차 로드맵).
    /// </summary>
    public sealed class SkillCatalogWindow : EditorWindow
    {
        private const string DataFolderParent = "Assets/_Project/Data";
        private const string SkillFolder = "Assets/_Project/Data/Skills";

        private readonly List<SkillData> _skills = new List<SkillData>();
        private SkillData _selected;
        private SerializedObject _selectedSO;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        [MenuItem("Tools/LegionBreak/Skill Catalog")]
        private static void Open()
        {
            GetWindow<SkillCatalogWindow>("Skill Catalog").minSize = new Vector2(520, 300);
        }

        private void OnEnable() => Refresh();

        private void OnProjectChange() => Refresh();

        private void Refresh()
        {
            _skills.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    _skills.Add(asset);
                }
            }

            _skills.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

            if (_selected == null || !_skills.Contains(_selected))
            {
                Select(_skills.Count > 0 ? _skills[0] : null);
            }
        }

        private void Select(SkillData skill)
        {
            _selected = skill;
            _selectedSO = skill != null ? new SerializedObject(skill) : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("스킬", EditorStyles.boldLabel);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (var skill in _skills)
            {
                if (skill == null)
                {
                    continue;
                }

                var on = skill == _selected;
                var label = string.IsNullOrEmpty(skill.DisplayName) ? skill.name : $"{skill.DisplayName}  ({skill.SkillId})";
                if (GUILayout.Toggle(on, label, "Button") && !on)
                {
                    Select(skill);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("＋ 새 스킬"))
            {
                CreateSkill();
            }

            using (new EditorGUI.DisabledScope(_selected == null))
            {
                if (GUILayout.Button("복제"))
                {
                    DuplicateSelected();
                }

                if (GUILayout.Button("삭제"))
                {
                    DeleteSelected();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_selected == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 스킬을 고르거나 '＋ 새 스킬'을 누르세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            _selectedSO.Update();

            EditorGUILayout.LabelField("스탯", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_selectedSO.FindProperty("_skillId"));
            EditorGUILayout.PropertyField(_selectedSO.FindProperty("_displayName"));
            EditorGUILayout.PropertyField(_selectedSO.FindProperty("_baseDamage"));
            EditorGUILayout.PropertyField(_selectedSO.FindProperty("_cooldownSeconds"));
            EditorGUILayout.PropertyField(_selectedSO.FindProperty("_range"));

            _selectedSO.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ---- 에셋 조작 ----

        private void CreateSkill()
        {
            EnsureSkillFolder();
            var skill = CreateInstance<SkillData>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{SkillFolder}/NewSkill.asset");
            AssetDatabase.CreateAsset(skill, path);
            AssetDatabase.SaveAssets();
            Refresh();
            Select(AssetDatabase.LoadAssetAtPath<SkillData>(path));
        }

        private void DuplicateSelected()
        {
            var src = AssetDatabase.GetAssetPath(_selected);
            var dst = AssetDatabase.GenerateUniqueAssetPath(src);
            if (AssetDatabase.CopyAsset(src, dst))
            {
                AssetDatabase.SaveAssets();
                Refresh();
                Select(AssetDatabase.LoadAssetAtPath<SkillData>(dst));
            }
        }

        private void DeleteSelected()
        {
            if (!EditorUtility.DisplayDialog("스킬 삭제", $"'{_selected.name}' 에셋을 삭제할까요?", "삭제", "취소"))
            {
                return;
            }

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selected));
            Select(null);
            Refresh();
        }

        private static void EnsureSkillFolder()
        {
            if (!AssetDatabase.IsValidFolder(SkillFolder))
            {
                AssetDatabase.CreateFolder(DataFolderParent, "Skills");
            }
        }
    }
}
