using LegionBreak.Application.Events;
using LegionBreak.Application.Spawning;
using LegionBreak.Domain.Skills;
using UnityEngine;

namespace LegionBreak.Application.Skills
{
    public sealed class PlayerSkillCastUseCase : IPlayerSkillCastUseCase
    {
        private readonly Skill _skill;
        private readonly ISkillDamageCalculator _damageCalculator;
        private readonly IMonsterSpawner _monsterSpawner;
        private readonly ICriticalHitJudge _criticalHitJudge;
        private readonly IRandomProvider _randomProvider;
        private readonly IEventBus _eventBus;
        private readonly float _criticalChance;

        public PlayerSkillCastUseCase(
            Skill skill,
            ISkillDamageCalculator damageCalculator,
            IMonsterSpawner monsterSpawner,
            ICriticalHitJudge criticalHitJudge,
            IRandomProvider randomProvider,
            IEventBus eventBus,
            float criticalChance)
        {
            _skill = skill;
            _damageCalculator = damageCalculator;
            _monsterSpawner = monsterSpawner;
            _criticalHitJudge = criticalHitJudge;
            _randomProvider = randomProvider;
            _eventBus = eventBus;
            _criticalChance = criticalChance;
        }

        public void Tick(float deltaTime)
        {
            _skill.Tick(deltaTime);
        }

        public SkillCastResult Execute(Vector2 targetPoint)
        {
            if (!_skill.IsReady)
            {
                return SkillCastResult.Failed;
            }

            var isCritical = _criticalHitJudge.Judge(_criticalChance, _randomProvider.NextFloat01());
            var damage = _damageCalculator.Calculate(_skill, isCritical);
            var hitCount = _monsterSpawner.ApplyDamageInRange(targetPoint, _skill.Range, damage);
            _skill.ConsumeCooldown();

            var result = new SkillCastResult(true, damage, hitCount, isCritical);
            // 시전 성공 시에만 발행 — 플레이어 공격 애니메이션(PlayerAnimationView)의 소비처.
            // 쿨다운 실패 시엔 아무 시각 변화가 없어야 하므로 발행하지 않는다.
            _eventBus.Publish(result);
            return result;
        }
    }
}
