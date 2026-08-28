using System.Collections.Generic;
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class GameSessionTests
{
    private const string Json = """
    [ { "id": "hotdog", "width": 16, "edible": true, "movement": "constant", "score": 10, "minEatenToAppear": 0 } ]
    """;

    private static GameSession Session(int seed = 3) =>
        new(FoodTable.FromJson(Json), new SeededRandom(seed),
            jaw: new JawZone(Center: 100f, HalfWidth: 12f),
            spawnX: -20f, retireX: 200f);

    private static GameSession SessionWithItemInJaws()
    {
        var session = Session();
        var jaw = new JawZone(100f, 12f);
        for (var i = 0; i < 10_000; i++)
        {
            session.Tick(1f / 60f);
            if (session.Items.Any(item => jaw.Overlaps(item))) return session;
            if (session.State.IsOver) break;
        }
        Assert.Fail("no item reached the jaw zone");
        return session;
    }

    [Fact]
    public void TickEmitsSpawnedWhenAnItemAppears()
    {
        var session = Session();
        var spawned = new List<GameEvent>();
        for (var i = 0; i < 600; i++) spawned.AddRange(session.Tick(1f / 60f));
        Assert.Contains(spawned, e => e is Spawned);
    }

    [Fact]
    public void ChompingAirCostsAStrike()
    {
        var session = Session();
        var events = session.Chomp();
        Assert.Contains(events, e => e is ChompedAir);
        Assert.Contains(events, e => e is StrikeAdded);
        Assert.Equal(1, session.State.Strikes);
    }

    [Fact]
    public void ChompingAnItemInTheJawsScores()
    {
        var session = SessionWithItemInJaws();
        var events = session.Chomp();
        Assert.Contains(events, e => e is Chomped);
        Assert.DoesNotContain(events, e => e is StrikeAdded);
        Assert.True(session.State.Score > 0);
        Assert.Equal(1, session.State.Eaten);
    }

    [Fact]
    public void AChompedItemLeavesTheBelt()
    {
        var session = SessionWithItemInJaws();
        var before = session.Items.Count;
        session.Chomp();
        Assert.Equal(before - 1, session.Items.Count);
    }

    [Fact]
    public void LettingEdibleFoodPassCostsAStrike()
    {
        var session = Session();
        var events = new List<GameEvent>();
        for (var i = 0; i < 3_000 && session.State.Strikes == 0; i++)
            events.AddRange(session.Tick(1f / 60f));

        Assert.Contains(events, e => e is Passed);
        Assert.Equal(1, session.State.Strikes);
    }

    [Fact]
    public void ThreeStrikesEndsTheRunAndEmitsRunEndedOnce()
    {
        var session = Session();
        var events = new List<GameEvent>();
        events.AddRange(session.Chomp());
        events.AddRange(session.Chomp());
        Assert.False(session.State.IsOver);
        events.AddRange(session.Chomp());
        Assert.True(session.State.IsOver);
        Assert.Single(events.OfType<RunEnded>());
    }

    [Fact]
    public void TickDoesNothingAfterTheRunEnds()
    {
        var session = Session();
        session.Chomp();
        session.Chomp();
        session.Chomp();
        var elapsedAtEnd = session.State.Elapsed;
        Assert.Empty(session.Tick(1f / 60f));
        Assert.Equal(elapsedAtEnd, session.State.Elapsed, precision: 4);
    }

    [Fact]
    public void ChompDoesNothingAfterTheRunEnds()
    {
        var session = Session();
        session.Chomp();
        session.Chomp();
        session.Chomp();
        Assert.Empty(session.Chomp());
        Assert.Equal(3, session.State.Strikes);
    }

    [Fact]
    public void SuspendingTicksDoesNotChangeJudgingWhenTicksResume()
    {
        var a = Session(seed: 11);
        var b = Session(seed: 11);

        for (var i = 0; i < 300; i++) a.Tick(1f / 60f);

        for (var i = 0; i < 150; i++) b.Tick(1f / 60f);
        for (var i = 0; i < 150; i++) b.Tick(1f / 60f);

        Assert.Equal(a.Items.Count, b.Items.Count);
        for (var i = 0; i < a.Items.Count; i++)
        {
            Assert.Equal(a.Items[i].X, b.Items[i].X, precision: 3);
            Assert.Equal(a.Items[i].TypeId, b.Items[i].TypeId);
        }
    }
}
