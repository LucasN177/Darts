using DartsApp.Core.Engine;
using DartsApp.Core.Models;

namespace DartsApp.Core.Tests;

public class FastInputTests
{
    private static readonly PlayerRef[] Solo = [new(null, "Solo")];

    private static X01Engine X01(int start = 501, InOutRule outRule = InOutRule.Double) =>
        new(GameSettings.DefaultFor(GameMode.X01) with { StartScore = start, OutRule = outRule, FastInput = true }, Solo);

    private static void Apply(GameEngine e, TurnTotalResult result)
    {
        Assert.True(result.IsValid, result.Error);
        foreach (var d in result.Darts) e.Throw(d);
    }

    [Theory]
    [InlineData(163)]
    [InlineData(166)]
    [InlineData(169)]
    [InlineData(172)]
    [InlineData(179)]
    [InlineData(181)]
    [InlineData(-1)]
    public void ImpossibleTotals_AreRejected(int total) =>
        Assert.False(FastInput.ToDarts(X01(), total).IsValid);

    [Fact]
    public void EveryPossibleTotal_DecomposesExactly()
    {
        for (var t = 0; t <= 180; t++)
        {
            if (!FastInput.IsPossible(t)) continue;
            var darts = FastInput.Decompose(t);
            Assert.Equal(3, darts.Count);
            Assert.Equal(t, darts.Sum(d => d.Score));
        }
    }

    [Fact]
    public void Score_SubtractsTotal_AndCountsThreeDarts()
    {
        var e = X01();
        Apply(e, FastInput.ToDarts(e, 140));
        Assert.Equal(361, e.RemainingOf(0));
        var r = e.GetResults()[0];
        Assert.Equal(3, r.DartsThrown);
        Assert.Equal(1, r.Count140);
    }

    [Fact]
    public void Checkout_NeedsDartCount_AndFinishesWithDouble()
    {
        var e = X01(start: 100);
        Assert.True(FastInput.WouldFinish(e, 100));
        Assert.False(FastInput.ToDarts(e, 100).IsValid);              // ohne Dartanzahl
        Assert.Equal([2, 3], FastInput.CheckoutDartOptions(100, InOutRule.Double));
        Assert.False(FastInput.ToDarts(e, 100, checkoutDarts: 1).IsValid);

        var result = FastInput.ToDarts(e, 100, checkoutDarts: 3);
        Assert.Equal(TurnTotalKind.Checkout, result.Kind);
        Assert.Equal(3, result.Darts.Count);
        Apply(e, result);
        Assert.True(e.IsFinished);
        var r = e.GetResults()[0];
        Assert.Equal(3, r.DartsThrown);
        Assert.Equal(100, r.HighestCheckout);
    }

    [Theory]
    [InlineData(40, 60)]   // über den Rest
    [InlineData(41, 40)]   // Rest 1 bei Double Out
    [InlineData(32, 32)]   // genau Rest, aber kein Doppel (checkoutDarts = 0)
    public void Bust_KeepsRemaining(int remaining, int total)
    {
        var e = X01(start: remaining);
        var result = FastInput.ToDarts(e, total, checkoutDarts: total == remaining ? 0 : null);
        Assert.Equal(TurnTotalKind.Bust, result.Kind);
        Apply(e, result);
        Assert.False(e.IsFinished);
        Assert.Equal(remaining, e.RemainingOf(0));
        Assert.True(e.Turns[0].Bust);
    }

    [Fact]
    public void Bust_NeverTurnsIntoCheckout()
    {
        for (var rem = 2; rem <= 180; rem++)
        for (var total = rem + 1; total <= 180; total++)
        {
            if (!FastInput.IsPossible(total)) continue;
            var e = X01(start: rem);
            Apply(e, FastInput.ToDarts(e, total));
            Assert.False(e.IsFinished, $"Rest {rem}, Summe {total}");
            Assert.Equal(rem, e.RemainingOf(0));
        }
    }

    [Theory]
    [InlineData(InOutRule.Double)]
    [InlineData(InOutRule.Single)]
    [InlineData(InOutRule.Master)]
    public void BustButton_KeepsRemaining_WithThreeDarts(InOutRule outRule)
    {
        for (var rem = 1; rem <= 200; rem++)
        {
            var e = X01(start: rem, outRule: outRule);
            if (!FastInput.CanBust(e))
            {
                Assert.False(FastInput.Bust(e).IsValid);
                continue;
            }
            var result = FastInput.Bust(e);
            Assert.Equal(TurnTotalKind.Bust, result.Kind);
            Assert.Equal(3, result.Darts.Count);
            Apply(e, result);
            Assert.False(e.IsFinished, $"Rest {rem}");
            Assert.Equal(rem, e.RemainingOf(0));
            Assert.True(e.Turns[0].Bust, $"Rest {rem}");
            Assert.Equal(0, e.Turns[0].Score);
        }
    }

    [Theory]
    [InlineData(2, InOutRule.Double, true)]
    [InlineData(170, InOutRule.Double, true)]
    [InlineData(181, InOutRule.Double, true)]   // 180 → Rest 1
    [InlineData(182, InOutRule.Double, false)]
    [InlineData(180, InOutRule.Single, false)]  // mehr als 180 geht nicht
    [InlineData(501, InOutRule.Double, false)]
    public void BustButton_OnlyWhenBustIsPossible(int remaining, InOutRule outRule, bool expected) =>
        Assert.Equal(expected, FastInput.CanBust(X01(start: remaining, outRule: outRule)));

    [Fact]
    public void Catch40_UsesCheckoutDartsForPoints()
    {
        var e = new CheckoutTrainingEngine(GameSettings.DefaultFor(GameMode.Catch40) with { FastInput = true }, Solo);
        Apply(e, FastInput.ToDarts(e, 61, checkoutDarts: 2));
        Assert.Equal(3, e.PointsOf(0));
        Assert.Equal(62, e.TargetOf(0));
    }

    [Fact]
    public void AllFives_TotalDecides()
    {
        var e = new AllFivesEngine(GameSettings.DefaultFor(GameMode.AllFives) with { FastInput = true }, Solo);
        Assert.Null(e.CheckoutTarget);
        Apply(e, FastInput.ToDarts(e, 45));
        Assert.Equal(9, e.PointsOf(0));
    }

    [Fact]
    public void Availability_DependsOnModeAndInRule()
    {
        Assert.True(GameModes.Get(GameMode.X01).FastInputAvailable(GameSettings.DefaultFor(GameMode.X01)));
        Assert.False(GameModes.Get(GameMode.X01).FastInputAvailable(GameSettings.DefaultFor(GameMode.X01) with { InRule = InOutRule.Double }));
        Assert.False(GameModes.Get(GameMode.Cricket).FastInputAvailable(GameSettings.DefaultFor(GameMode.Cricket)));
        Assert.Equal(8, GameModes.All.Count(m => m.SupportsFastInput));
    }
}
