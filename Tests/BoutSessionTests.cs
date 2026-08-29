using System.Collections.Generic;
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class BoutSessionTests
{
    private static FoodTable Table() => FoodTable.FromJson(
        """
        [
          { "id":"pizza","width":16,"edible":true,"movement":"constant","score":10,"minEatenToAppear":0 },
          { "id":"bomb","width":16,"edible":false,"movement":"constant","score":0,"minEatenToAppear":0 }
        ]
        """);

    private static MatchDef Def() =>
        new(Career.Ladder[0], DurationSeconds: 0f, DifficultyOffset: 0);

    private static BoutSession Make(int seed = 1) =>
        new(Table(), new SeededRandom(seed), new JawZone(90f, 17f),
            spawnX: -20f, retireX: 200f, def: Def(), phases: Career.Phases);

    /// <summary>Runs a whole bout, advancing through each interlude the moment it opens.</summary>
    private static List<GameEvent> RunToTheBell(BoutSession bout)
    {
        var events = new List<GameEvent>(bout.Start());

        for (var i = 0; i < 20000 && bout.Result == BoutResult.InProgress; i++)
        {
            if (bout.AwaitingInterlude) events.AddRange(bout.BeginNextPhase());
            else events.AddRange(bout.Tick(0.02f));
        }

        return events;
    }

    [Fact]
    public void ABoutRunsExactlyThreePhases()
    {
        var bout = Make();
        var events = RunToTheBell(bout);

        Assert.Equal(3, events.OfType<PhaseStarted>().Count());
        Assert.Equal(3, events.OfType<PhaseEnded>().Count());
        Assert.Equal(new[] { 0, 1, 2 }, events.OfType<PhaseStarted>().Select(e => e.PhaseIndex));
    }

    [Fact]
    public void ABoutEndsOnceAndOnlyAtTheBell()
    {
        var bout = Make();
        var events = RunToTheBell(bout);

        Assert.Single(events.OfType<BoutEnded>());
        Assert.NotEqual(BoutResult.InProgress, bout.Result);
    }

    [Fact]
    public void ScoreCarriesAcrossPhases()
    {
        var bout = Make();
        bout.Start();

        bout.Current.Place(new FoodItem(500, "pizza", 90f, 8f, true, 10, Movement.Constant));
        bout.Chomp();

        var afterPhaseOne = bout.PlayerScore;
        Assert.True(afterPhaseOne > 0);

        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();

        Assert.Equal(afterPhaseOne, bout.PlayerScore);
        Assert.Equal(0, bout.Current.PhaseScore);
    }

    [Fact]
    public void StrikesResetAtEachPhaseBoundary()
    {
        var bout = Make();
        bout.Start();

        bout.Chomp();
        bout.Chomp();
        Assert.Equal(2, bout.Current.State.Strikes);

        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();

        Assert.Equal(0, bout.Current.State.Strikes);
    }

    [Fact]
    public void AKnockoutEndsThePhaseNotTheBout()
    {
        var bout = Make();
        bout.Start();

        for (var i = 0; i < 3; i++) bout.Chomp();

        Assert.True(bout.Current.KnockedOut);
        Assert.Equal(BoutResult.InProgress, bout.Result);

        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();

        Assert.Equal(1, bout.PhaseIndex);
        Assert.False(bout.Current.KnockedOut);
    }

    [Fact]
    public void AKnockedOutPhaseStillAdvancesTheRival()
    {
        var bout = Make();
        bout.Start();
        for (var i = 0; i < 3; i++) bout.Chomp();

        var before = bout.OpponentScore;
        while (!bout.AwaitingInterlude) bout.Tick(0.02f);

        Assert.True(bout.OpponentScore > before);
    }

    [Fact]
    public void ThePotDoesNotSurviveAPhaseBoundary()
    {
        var bout = Make();
        bout.Start();
        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();   // now in HAZARD, where the pot is live

        bout.Current.Place(new FoodItem(501, "pizza", 90f, 8f, true, 10, Movement.Constant));
        bout.Chomp();
        Assert.False(bout.Current.Pot.IsEmpty);

        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();

        Assert.True(bout.Current.Pot.IsEmpty);
    }

    [Fact]
    public void TheBoutIsDecidedOnCarriedTotalScore()
    {
        var bout = Make();
        RunToTheBell(bout);

        var expected = bout.PlayerScore > bout.OpponentScore ? BoutResult.Won : BoutResult.Lost;
        Assert.Equal(expected, bout.Result);
    }

    [Fact]
    public void ASeedReproducesAWholeBoutIdentically()
    {
        var first = RunToTheBell(Make(seed: 4242));
        var second = RunToTheBell(Make(seed: 4242));

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.Select(e => e.GetType().Name), second.Select(e => e.GetType().Name));
    }
}
