using Unravel.Domain.Gamification;

namespace Unravel.Tests.Gamification;

/// <summary>UC34 — regra pura de recarga de vidas (+1/h até o teto de 7).</summary>
public class LifeRefillTests
{
    private static readonly DateTime Now = new(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void At_cap_grants_nothing_and_resets_clock()
    {
        var r = LifeRefill.Compute(LifeRefill.MaxLives, Now.AddHours(-5), Now);
        Assert.Equal(0, r.Granted);
        Assert.Equal(LifeRefill.MaxLives, r.Lives);
        Assert.Equal(Now, r.Anchor); // âncora vira "agora" enquanto cheio
    }

    [Fact]
    public void Null_anchor_starts_the_clock_without_granting()
    {
        var r = LifeRefill.Compute(5, null, Now);
        Assert.Equal(0, r.Granted);
        Assert.Equal(5, r.Lives);
        Assert.Equal(Now, r.Anchor);
    }

    [Fact]
    public void One_hour_grants_one_and_advances_anchor()
    {
        var anchor = Now.AddHours(-1);
        var r = LifeRefill.Compute(5, anchor, Now);
        Assert.Equal(1, r.Granted);
        Assert.Equal(6, r.Lives);
        Assert.Equal(anchor.AddHours(1), r.Anchor);
    }

    [Fact]
    public void Less_than_an_hour_grants_nothing_and_keeps_anchor()
    {
        var anchor = Now.AddMinutes(-40);
        var r = LifeRefill.Compute(5, anchor, Now);
        Assert.Equal(0, r.Granted);
        Assert.Equal(5, r.Lives);
        Assert.Equal(anchor, r.Anchor);
    }

    [Fact]
    public void Many_hours_capped_at_max_and_resets_clock()
    {
        // 5 vidas, 10h decorridas → +2 (chega a 7), âncora = agora.
        var r = LifeRefill.Compute(5, Now.AddHours(-10), Now);
        Assert.Equal(2, r.Granted);
        Assert.Equal(LifeRefill.MaxLives, r.Lives);
        Assert.Equal(Now, r.Anchor);
    }

    [Fact]
    public void Preserves_leftover_minutes_when_below_cap()
    {
        // 1h30 → +1 e âncora avança exatamente 1h (preserva os 30 min).
        var anchor = Now.AddHours(-1).AddMinutes(-30);
        var r = LifeRefill.Compute(4, anchor, Now);
        Assert.Equal(1, r.Granted);
        Assert.Equal(5, r.Lives);
        Assert.Equal(anchor.AddHours(1), r.Anchor);
    }

    [Fact]
    public void SecondsToNext_is_null_at_cap()
    {
        Assert.Null(LifeRefill.SecondsToNext(LifeRefill.MaxLives, Now.AddHours(-1), Now));
    }

    [Fact]
    public void SecondsToNext_counts_down_within_the_hour()
    {
        // âncora 20 min atrás → faltam ~40 min (2400s) pra próxima vida.
        var secs = LifeRefill.SecondsToNext(5, Now.AddMinutes(-20), Now);
        Assert.Equal(2400, secs);
    }

    [Fact]
    public void SecondsToNext_null_anchor_uses_full_interval()
    {
        Assert.Equal(3600, LifeRefill.SecondsToNext(5, null, Now));
    }

    [Fact]
    public void Above_cap_legacy_value_is_not_reduced()
    {
        // Dado legado (cap antigo 10): 8 vidas. Não reduz; só zera o relógio.
        var r = LifeRefill.Compute(8, Now.AddHours(-5), Now);
        Assert.Equal(0, r.Granted);
        Assert.Equal(8, r.Lives);
        Assert.Equal(Now, r.Anchor);
    }
}
