using System.Collections.Generic;

namespace LegionBreak.Domain.Waves
{
    public readonly struct WaveTickResult
    {
        public IReadOnlyList<int> WaveIndexesToSpawn { get; }
        public bool IsSequenceComplete { get; }

        public WaveTickResult(IReadOnlyList<int> waveIndexesToSpawn, bool isSequenceComplete)
        {
            WaveIndexesToSpawn = waveIndexesToSpawn;
            IsSequenceComplete = isSequenceComplete;
        }
    }
}
