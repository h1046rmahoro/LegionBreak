using LegionBreak.Domain.Monsters;
using NUnit.Framework;

namespace LegionBreak.Domain.Tests
{
    public class MonsterAITests
    {
        private const float ChaseRange = 10f;
        private const float AttackRange = 2f;
        private const float AttackCooldownSeconds = 1f;

        [Test]
        public void Tick_DistanceBeyondChaseRange_StaysIdle()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);

            var state = ai.Tick(distanceToPlayer: 15f, isDead: false, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Idle, state);
        }

        [Test]
        public void Tick_DistanceWithinChaseRange_TransitionsIdleToChase()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);

            var state = ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Chase, state);
        }

        [Test]
        public void Tick_DistanceWithinAttackRange_TransitionsChaseToAttack()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);
            ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);

            var state = ai.Tick(distanceToPlayer: 1f, isDead: false, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Attack, state);
        }

        [Test]
        public void Tick_DistanceBeyondAttackRange_TransitionsAttackToChase()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);
            ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);
            ai.Tick(distanceToPlayer: 1f, isDead: false, deltaTime: 0.1f);

            var state = ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Chase, state);
        }

        [Test]
        public void Tick_DistanceBeyondChaseRange_TransitionsChaseToIdle()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);
            ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);

            var state = ai.Tick(distanceToPlayer: 15f, isDead: false, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Idle, state);
        }

        [Test]
        public void Tick_IsDeadTrueFromIdle_TransitionsToDead()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);

            var state = ai.Tick(distanceToPlayer: 15f, isDead: true, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Dead, state);
        }

        [Test]
        public void Tick_IsDeadTrueFromChase_TransitionsToDead()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);
            ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);

            var state = ai.Tick(distanceToPlayer: 5f, isDead: true, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Dead, state);
        }

        [Test]
        public void Tick_IsDeadTrueFromAttack_TransitionsToDead()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);
            ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);
            ai.Tick(distanceToPlayer: 1f, isDead: false, deltaTime: 0.1f);

            var state = ai.Tick(distanceToPlayer: 1f, isDead: true, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Dead, state);
        }

        [Test]
        public void Tick_IsDeadFalseAfterDead_StaysDead()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);
            ai.Tick(distanceToPlayer: 15f, isDead: true, deltaTime: 0.1f);

            var state = ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);

            Assert.AreEqual(MonsterAIState.Dead, state);
        }

        [Test]
        public void TryConsumeAttack_NotInAttackState_ReturnsFalse()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);

            var consumed = ai.TryConsumeAttack();

            Assert.IsFalse(consumed);
        }

        [Test]
        public void TryConsumeAttack_InAttackStateAndCooldownReady_ReturnsTrueAndResetsCooldown()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);
            ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);
            ai.Tick(distanceToPlayer: 1f, isDead: false, deltaTime: 0.1f);

            var consumed = ai.TryConsumeAttack();

            Assert.IsTrue(consumed);
        }

        [Test]
        public void TryConsumeAttack_ImmediatelyAfterConsume_ReturnsFalse()
        {
            var ai = new MonsterAI(ChaseRange, AttackRange, AttackCooldownSeconds);
            ai.Tick(distanceToPlayer: 5f, isDead: false, deltaTime: 0.1f);
            ai.Tick(distanceToPlayer: 1f, isDead: false, deltaTime: 0.1f);
            ai.TryConsumeAttack();

            var consumed = ai.TryConsumeAttack();

            Assert.IsFalse(consumed);
        }
    }
}
