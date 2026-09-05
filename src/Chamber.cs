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

public sealed class WeaponControls
{
    public const int AimMilliseconds = 400;
    public const float HipSpread = 0.08f;
    public const float SightedSpread = 0.003f;
    private bool triggerWasDown;
    private long aimStarted;
    public bool Aiming { get; private set; }
    public bool Reloading { get; private set; }
    public long LastLoad { get; private set; }
    public FiringCycle? Trigger { get; private set; }

    public bool Update(bool left, bool right, bool sneak, long now)
    {
        bool reload = right && sneak;
        if (reload && !Reloading) LastLoad = now;
        Reloading = reload;
        bool aim = right && !sneak;
        if (aim && !Aiming) aimStarted = now;
        Aiming = aim;
        bool pressed = left && !triggerWasDown;
        if (!left || reload) { Trigger?.Stop(); Trigger = null; }
        else if (pressed) Trigger = new FiringCycle(now);
        triggerWasDown = left;
        return pressed && !reload;
    }

    public float AimFraction(long now) => Aiming ? Math.Clamp((now - aimStarted) / (float)AimMilliseconds, 0, 1) : 0;
    public float Spread(long now) => HipSpread + (SightedSpread - HipSpread) * AimFraction(now);
    public bool CanLoad(int rounds, long now) => Reloading && Chamber.CanLoad(rounds, now - LastLoad);
    public void Loaded(long now) => LastLoad = now;
}
