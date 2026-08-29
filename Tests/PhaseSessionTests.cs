using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class PhaseSessionTests
{
    private static FoodTable Table() => FoodTable.FromJson(
        """
        [
          { "id":"pizza","width":16,"edible":true,"movement":"constant","score":10,"minEatenToAppear":0 },
          { "id":"bomb","width":16,"edible":false,"movement":"constant","score":0,"minEatenToAppear":0 },
          { "id":"slow","width":16,"edible":true,"movement":"constant","score":0,"minEatenToAppear":0,"power":"slow" },
          { "id":"shield","width":14,"edible":true,"movement":"constant","score":0,"minEatenToAppear":0,"power":"shield" }
        ]
        """);

    private static PhaseSession Make(PhaseDef? phase = null) =>
        new(Table(), new SeededRandom(1), new JawZone(90f, 17f),
            spawnX: -20f, retireX: 200f,
            phase: phase ?? Career.Phases[1], difficultyOffset: 0);

    /// <summary>Puts an item exactly in the jaws so a chomp is guaranteed to land.</summary>
    private static FoodItem PlaceInJaws(PhaseSession session, string typeId, bool edible,
                                        int score, string power = "")
    {
        var item = new FoodItem(999, typeId, 90f, 8f, edible, score, Movement.Constant, power);
        session.Place(item);
        return item;
    }

    [Fact]
    public void AThirdStrikeKnocksThePlayerOutOfThePhaseAndConcedesTheRest()
    {
        var session = Make();
        var events = new System.Collections.Generic.List<GameEvent>();

        for (var i = 0; i < 3; i++) events.AddRange(session.Chomp(0));

        Assert.True(session.KnockedOut);

        var knockout = events.OfType<PhaseKnockout>().Single();
        Assert.True(knockout.SecondsConceded > 0f);
    }

    [Fact]
    public void AKnockedOutPhaseStopsScoringButKeepsTicking()
    {
        var session = Make();
        for (var i = 0; i < 3; i++) session.Chomp(0);

        var before = session.PhaseScore;
        PlaceInJaws(session, "pizza", edible: true, score: 10);
        session.Chomp(0);

        Assert.Equal(before, session.PhaseScore);
    }

    [Fact]
    public void AShieldAbsorbsAStrikeInsteadOfTheTeeth()
    {
        var session = Make();
        PlaceInJaws(session, "shield", edible: true, score: 0, power: "shield");
        session.Chomp(0);

        Assert.True(session.Buffs.HasShield);

        // The bite that took the shield bought grace, so let it lapse before missing.
        for (var t = 0f; t < PhaseSession.ChompGraceSeconds + 0.05f; t += 0.02f)
        {
            session.Tick(0.02f, 0, 0);
        }

        session.Chomp(0);   // air

        Assert.Equal(0, session.State.Strikes);
        Assert.False(session.Buffs.HasShield);
    }

    [Fact]
    public void APhaseMultiplierAppliesToBites()
    {
        var hazard = Make(Career.Phases[1]);
        PlaceInJaws(hazard, "pizza", edible: true, score: 10);
        hazard.Chomp(0);

        var feast = Make(Career.Phases[2]);
        PlaceInJaws(feast, "pizza", edible: true, score: 10);
        feast.Chomp(0);

        Assert.Equal(hazard.PhaseScore * 2, feast.PhaseScore);
    }

    [Fact]
    public void BitesAccrueToThePotOnlyWhereCoinsAreLive()
    {
        var hazard = Make(Career.Phases[1]);
        PlaceInJaws(hazard, "pizza", edible: true, score: 10);
        hazard.Chomp(0);
        Assert.False(hazard.Pot.IsEmpty);

        var plain = Make(Career.Phases[0]);
        PlaceInJaws(plain, "pizza", edible: true, score: 10);
        plain.Chomp(0);
        Assert.True(plain.Pot.IsEmpty);
    }

    [Fact]
    public void BitingACoinBanksThePotAndNeverReducesTheScore()
    {
        var session = Make();
        PlaceInJaws(session, "pizza", edible: true, score: 10);
        session.Chomp(0);

        var scoreBeforeCoin = session.PhaseScore;
        Assert.True(session.Pot.Amount > 0);

        PlaceInJaws(session, "coin", edible: true, score: 0, power: "coin");
        var events = session.Chomp(0);

        Assert.True(session.Pot.IsEmpty);
        Assert.True(session.PhaseScore > scoreBeforeCoin);
        Assert.Single(events.OfType<PotBanked>());
    }

    [Fact]
    public void AStrikeWipesThePot()
    {
        var session = Make();
        PlaceInJaws(session, "pizza", edible: true, score: 10);
        session.Chomp(0);
        Assert.False(session.Pot.IsEmpty);

        for (var t = 0f; t < PhaseSession.ChompGraceSeconds + 0.05f; t += 0.02f)
        {
            session.Tick(0.02f, 0, 0);
        }

        var events = session.Chomp(0);   // air

        Assert.True(session.Pot.IsEmpty);
        Assert.Single(events.OfType<PotWiped>());
    }

    [Fact]
    public void HungerWidensTheJawZoneItReportsToTheView()
    {
        var session = Make();
        var narrow = session.EffectiveJaw.HalfWidth;

        for (var t = 0f; t < Hunger.ChargeSeconds + 2f; t += 0.05f)
        {
            session.Tick(0.05f, carriedPlayerScore: 0, opponentScore: 400);
        }

        Assert.True(session.Hunger.IsActive);
        Assert.Equal(narrow * Hunger.JawWidthMultiplier, session.EffectiveJaw.HalfWidth, precision: 3);
    }

    [Fact]
    public void HungerMakesEverythingOnTheBeltEdible()
    {
        var session = Make();
        var bomb = PlaceInJaws(session, "bomb", edible: false, score: 0);

        for (var t = 0f; t < Hunger.ChargeSeconds + 2f; t += 0.05f)
        {
            session.Tick(0.05f, carriedPlayerScore: 0, opponentScore: 400);
        }

        Assert.True(session.Hunger.IsActive);
        Assert.True(session.IsEdibleNow(bomb));
    }
}
