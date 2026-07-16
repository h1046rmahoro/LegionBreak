using UnityEngine;

namespace LegionBreak.Data
{
    /// <summary>
    /// 스킬 스탯(밸런스 수치)을 하드코딩하지 않고 에셋으로 관리하기 위한 ScriptableObject.
    /// Domain의 Skill로 변환되어 사용되며(Application.Skills.SkillFactory), 이 클래스 자체는
    /// 게임 로직에서 직접 참조되지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillData", menuName = "LegionBreak/Skill Data")]
    public sealed class SkillData : ScriptableObject
    {
        [SerializeField] private string _skillId;
        [SerializeField] private string _displayName;
        [SerializeField] private float _baseDamage = 10f;
        [SerializeField] private float _cooldownSeconds = 1f;

        public string SkillId => _skillId;
        public string DisplayName => _displayName;
        public float BaseDamage => _baseDamage;
        public float CooldownSeconds => _cooldownSeconds;
    }
}
