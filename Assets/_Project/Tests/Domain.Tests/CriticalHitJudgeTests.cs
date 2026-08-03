using LegionBreak.Domain.Skills;
using NUnit.Framework;

namespace LegionBreak.Domain.Tests
{
    public class CriticalHitJudgeTests
    {
        [Test]
        public void Judge_RollBelowChance_ReturnsTrue()
        {
            var judge = new CriticalHitJudge();

            var isCritical = judge.Judge(criticalChance: 0.5f, roll01: 0.4f);

            Assert.IsTrue(isCritical);
        }

        [Test]
        public void Judge_RollAboveChance_ReturnsFalse()
        {
            var judge = new CriticalHitJudge();

            var isCritical = judge.Judge(criticalChance: 0.5f, roll01: 0.6f);

            Assert.IsFalse(isCritical);
        }

        [Test]
        public void Judge_RollEqualsChance_ReturnsFalse()
        {
            var judge = new CriticalHitJudge();

            var isCritical = judge.Judge(criticalChance: 0.5f, roll01: 0.5f);

            Assert.IsFalse(isCritical);
        }
    }
}
