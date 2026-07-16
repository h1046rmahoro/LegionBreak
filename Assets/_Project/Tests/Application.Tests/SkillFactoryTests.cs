using LegionBreak.Application.Skills;
using LegionBreak.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LegionBreak.Application.Tests
{
    public class SkillFactoryTests
    {
        private SkillData _skillData;

        [SetUp]
        public void SetUp()
        {
            _skillData = ScriptableObject.CreateInstance<SkillData>();

            var so = new SerializedObject(_skillData);
            so.FindProperty("_skillId").stringValue = "fireball";
            so.FindProperty("_displayName").stringValue = "Fireball";
            so.FindProperty("_baseDamage").floatValue = 12f;
            so.FindProperty("_cooldownSeconds").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_skillData);
        }

        [Test]
        public void Create_MapsAllFieldsFromSkillData()
        {
            var skill = SkillFactory.Create(_skillData);

            Assert.AreEqual("fireball", skill.Id);
            Assert.AreEqual("Fireball", skill.DisplayName);
            Assert.AreEqual(12f, skill.BaseDamage);
            Assert.AreEqual(2f, skill.CooldownSeconds);
        }
    }
}
