namespace Mokou_Character.SkillVisuals;

/// <summary>舍身击的连放时钟：只有冲刺、收势和等待耗用窗口，每次蓄力暂停计时。</summary>
internal sealed class SacrificeCastTiming
{
    internal const int ChargeTicks = 60;
    internal const int WindowTicks = 3 * 60;
    internal int Elapsed { get; private set; }
    internal bool Expired => Elapsed >= WindowTicks;
    internal void Reset() => Elapsed = 0;
    internal void Update(bool charging)
    {
        if (!charging && !Expired) Elapsed++;
    }
}
