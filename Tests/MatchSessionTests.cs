using System.Collections.Generic;
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class MatchSessionTests
{
    private const string Json = """
    [ { "id": "hotdog", "width": 16, "edible": true, "movement": "constant", "score": 10, "minEatenToAppear": 0 } ]
    """;

    private static readonly OpponentDef Rival = new(
        "penguin", "PIP", "penguin",
        SecondsPerBite: 1.0f, BiteJitter: 0f, PointsPerBite: 40,
        PrizeMoney: 25, Taunt: "hi");

    private static MatchSession Session(int seed = 3, float duration = 30f, int pointsPerBite = 40) =>
        new(FoodTable.FromJson(Json), new SeededRandom(seed),
            jaw: new JawZone(Center: 100f, HalfWidth: 12f),
            spawnX: -20f, retireX: 200f,
            def: new MatchDef(Rival with { PointsPerBite = pointsPerBite }, duration, DifficultyOffset: 0));

    private static readonly JawZone Jaw = new(100f, 12f);

    /// <summary>Ticks until something is in the jaws, then chomps it.</summary>
    private static bool EatOne(MatchSession session, int maxFrames = 4000)
    {
        for (var i = 0; i < maxFrames; i++)
        {
            if (session.Items.Any(item => Jaw.Overlaps(item)))
            {
                session.Chomp();
                return true;
            }

            session.Tick(1f / 60f);
            if (session.State.IsOver) return false;
        }

        return false;
    }

    /// <summary>Plays the match out chomping everything, which is the only way to
    /// survive to the bell: three items allowed past is a disqualification.</summary>
    private static List<GameEvent> PlayPerfectly(MatchSession session, int maxFrames = 5000)
    {
        var events = new List<GameEvent>();

        for (var i = 0; i < maxFrames && !session.State.IsOver; i++)
        {
            if (session.Items.Any(item => Jaw.Overlaps(item)))
            {
                events.AddRange(session.Chomp());
                continue;
            }

            events.AddRange(session.Tick(1f / 60f));
        }

        return events;
    }

    [Fact]
    public void TheClockCountsDown()
    {
        var session = Session(duration: 10f);
        session.Tick(1f);

        Assert.Equal(9f, session.State.TimeRemaining, precision: 3);
    }

    [Fact]
    public void TheRivalScoresOverTime()
    {
        var session = Session();
        for (var i = 0; i < 300; i++) session.Tick(1f / 60f);

        Assert.True(session.OpponentScore > 0);
    }

    [Fact]
    public void TheRivalsBitesAreReportedAsEvents()
    {
        var session = Session();
        var events = new List<GameEvent>();
        for (var i = 0; i < 300; i++) events.AddRange(session.Tick(1f / 60f));

        Assert.Contains(events, e => e is OpponentAte);
    }

    [Fact]
    public void OutscoringTheRivalWinsWhenTimeExpires()
    {
        // A rival that never scores: any point at all takes the match. The match has
        // to run long enough for the first item to actually reach the jaws - at the
        // opening belt speed that is nearly three seconds.
        var session = Session(duration: 12f, pointsPerBite: 0);

        var events = PlayPerfectly(session);

        Assert.Equal(MatchResult.Won, session.State.Result);
        var ended = events.OfType<MatchEnded>().Single();
        Assert.Equal(25, ended.Prize);
    }

    [Fact]
    public void BeingOutscoredLosesAndPaysNothing()
    {
        var session = Session(duration: 12f, pointsPerBite: 5000);

        var events = PlayPerfectly(session);

        Assert.Equal(MatchResult.Lost, session.State.Result);
        Assert.Equal(0, events.OfType<MatchEnded>().Single().Prize);
    }

    [Fact]
    public void ATieGoesToTheRival()
    {
        // Neither scores, so the totals are level at 0 when the clock runs out.
        var session = Session(duration: 1f, pointsPerBite: 0);
        for (var i = 0; i < 200 && !session.State.IsOver; i++) session.Tick(1f / 60f);

        Assert.Equal(MatchResult.Lost, session.State.Result);
    }

    [Fact]
    public void ThreeStrikesDisqualifiesRegardlessOfScore()
    {
        var session = Session(pointsPerBite: 0);

        session.Chomp();
        session.Chomp();
        var events = session.Chomp();

        Assert.Equal(MatchResult.Disqualified, session.State.Result);
        Assert.Equal(MatchResult.Disqualified, events.OfType<MatchEnded>().Single().Result);
    }

    [Fact]
    public void MatchEndedIsEmittedExactlyOnce()
    {
        var session = Session(duration: 12f, pointsPerBite: 0);

        var events = PlayPerfectly(session);

        Assert.Single(events.OfType<MatchEnded>());
    }

    [Fact]
    public void ALongComboTipsIntoFrenzy()
    {
        var session = Session(duration: 600f, pointsPerBite: 0);

        for (var i = 0; i < Frenzy.ComboToTrigger; i++)
        {
            Assert.True(EatOne(session), $"could not eat item {i}");
        }

        Assert.True(session.Frenzy.IsActive);
        Assert.Equal(Frenzy.ComboToTrigger, session.State.Combo);
    }

    [Fact]
    public void FrenzyDoublesTheScoreOfABite()
    {
        var calm = Session(duration: 600f, pointsPerBite: 0);
        for (var i = 0; i < Frenzy.ComboToTrigger - 1; i++) EatOne(calm);
        var beforeFrenzy = calm.State.Score;
        EatOne(calm);                       // this bite trips the frenzy
        var tripping = calm.State.Score - beforeFrenzy;

        var duringFrenzy = calm.State.Score;
        EatOne(calm);                       // this one is scored inside it
        var frenzied = calm.State.Score - duringFrenzy;

        Assert.Equal(tripping * Frenzy.ScoreMultiplier, frenzied);
    }

    [Fact]
    public void FrenzySpeedsTheBelt()
    {
        var session = Session(duration: 600f, pointsPerBite: 0);
        var calmSpeed = session.BeltSpeed;

        for (var i = 0; i < Frenzy.ComboToTrigger; i++) EatOne(session);

        Assert.True(session.BeltSpeed > calmSpeed);
    }

    [Fact]
    public void AStrikeEndsAFrenzyImmediately()
    {
        var session = Session(duration: 600f, pointsPerBite: 0);
        for (var i = 0; i < Frenzy.ComboToTrigger; i++) EatOne(session);
        Assert.True(session.Frenzy.IsActive);

        // Wait out the follow-through forgiveness, then chomp with nothing in the jaws.
        for (var i = 0; i < 30; i++) session.Tick(MatchSession.ChompGraceSeconds / 10f);
        while (session.Items.Any(item => Jaw.Overlaps(item))) session.Tick(1f / 60f);
        session.Chomp();

        Assert.Equal(1, session.State.Strikes);
        Assert.False(session.Frenzy.IsActive);
        Assert.Equal(0, session.State.Combo);
    }

    [Fact]
    public void NothingHappensAfterTheMatchEnds()
    {
        var session = Session(pointsPerBite: 0);
        session.Chomp();
        session.Chomp();
        session.Chomp();

        Assert.Empty(session.Tick(1f / 60f));
        Assert.Empty(session.Chomp());
    }

    [Fact]
    public void LettingFoodPassCostsTheComboButNotAStrike()
    {
        var session = Session(duration: 600f, pointsPerBite: 0);
        Assert.True(EatOne(session), "nothing reached the jaws");
        Assert.Equal(1, session.State.Combo);

        var events = new List<GameEvent>();
        for (var i = 0; i < 5_000 && !events.Exists(e => e is Passed); i++)
        {
            events.AddRange(session.Tick(1f / 60f));
        }

        Assert.Contains(events, e => e is Passed);
        Assert.Equal(0, session.State.Combo);
        Assert.Equal(0, session.State.Strikes);
    }

    [Fact]
    public void DoingNothingAtAllNeverDisqualifies()
    {
        // The belt must not be able to end a match on its own. A player who never
        // presses should lose on score, not be thrown out of the contest.
        var session = Session(duration: 25f, pointsPerBite: 10);

        for (var i = 0; i < 5_000 && !session.State.IsOver; i++) session.Tick(1f / 60f);

        Assert.Equal(0, session.State.Strikes);
        Assert.Equal(MatchResult.Lost, session.State.Result);
    }

    [Fact]
    public void AMissedItemStillEndsAFrenzy()
    {
        var session = Session(duration: 600f, pointsPerBite: 0);
        for (var i = 0; i < Frenzy.ComboToTrigger; i++) Assert.True(EatOne(session));
        Assert.True(session.Frenzy.IsActive);

        var events = new List<GameEvent>();
        for (var i = 0; i < 5_000 && !events.Exists(e => e is Passed); i++)
        {
            events.AddRange(session.Tick(1f / 60f));
        }

        Assert.Contains(events, e => e is Passed);
        Assert.False(session.Frenzy.IsActive);
    }

    [Fact]
    public void AFollowThroughTapRightAfterABiteIsForgiven()
    {
        var session = Session(duration: 600f, pointsPerBite: 0);
        Assert.True(EatOne(session));

        // Second press lands on nothing, immediately after a bite that connected.
        var events = session.Chomp();

        Assert.Empty(events);
        Assert.Equal(0, session.State.Strikes);
    }

    [Fact]
    public void TheForgivenessWindowExpires()
    {
        var session = Session(duration: 600f, pointsPerBite: 0);
        Assert.True(EatOne(session));

        for (var i = 0; i < 30; i++) session.Tick(MatchSession.ChompGraceSeconds / 10f);

        // Well past the window: clear the jaws, then press at nothing.
        while (session.Items.Any(item => Jaw.Overlaps(item))) session.Tick(1f / 60f);
        session.Chomp();

        Assert.Equal(1, session.State.Strikes);
    }

    [Fact]
    public void ASeedReproducesTheWholeMatch()
    {
        var a = Session(seed: 21, duration: 20f);
        var b = Session(seed: 21, duration: 20f);

        for (var i = 0; i < 600; i++)
        {
            a.Tick(1f / 60f);
            b.Tick(1f / 60f);
        }

        Assert.Equal(a.OpponentScore, b.OpponentScore);
        Assert.Equal(a.Items.Count, b.Items.Count);
    }
}
