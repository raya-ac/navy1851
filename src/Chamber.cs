namespace Navy1851;

// Only this count is persisted. Use timers are deliberately transient.
public static class Chamber
{
    public const int Capacity = 6;
    public const int CockMilliseconds = 350;
    public const int ReloadMilliseconds = 1400;
    public const int CooldownMilliseconds = 700;
    public static int Clamp(int rounds) => Math.Clamp(rounds, 0, Capacity);
    public static bool CanFire(int rounds, long held, long now, long nextShot)
        => Clamp(rounds) > 0 && held >= CockMilliseconds && now >= nextShot;
    public static bool CanLoad(int rounds, long held)
        => Clamp(rounds) < Capacity && held >= ReloadMilliseconds;
}

// A single held trigger. Exhaustion or release ends it; a delayed tick never bursts.
public sealed class FiringCycle(long started)
{
    public bool Active { get; private set; } = true;
    public long NextShot { get; private set; }
    public void Stop() => Active = false;

    public bool TryFire(int rounds, long now, long actorNextShot, out int remaining)
    {
        remaining = Chamber.Clamp(rounds);
        if (!Active) return false;
        if (remaining == 0) { Stop(); return false; }
        if (!Chamber.CanFire(remaining, now - started, now, Math.Max(NextShot, actorNextShot))) return false;
        remaining--;
        NextShot = now + Chamber.CooldownMilliseconds;
        if (remaining == 0) Stop();
        return true;
    }
}
