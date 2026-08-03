using LegionBreak.Data;
using LegionBreak.Domain.Waves;

namespace LegionBreak.Application.Waves
{
    /// <summary>
    /// WaveData(ScriptableObject)를 Domain의 WaveDirector로 변환한다. SkillFactory와
    /// 동일한 이유로, Presentation/Infrastructure가 WaveData를 직접 들고 다니지 않고
    /// 이 변환을 거친 WaveDirector만 사용하게 하기 위한 경계다.
    /// </summary>
    public static class WaveDirectorFactory
    {
        public static WaveDirector Create(WaveData data)
        {
            var waves = new WaveDefinition[data.Waves.Length];
            for (var i = 0; i < waves.Length; i++)
            {
                var entry = data.Waves[i];
                waves[i] = new WaveDefinition(entry.StartTimeSeconds, entry.MonsterCount, entry.SpawnIntervalSeconds);
            }

            return new WaveDirector(waves);
        }
    }
}
