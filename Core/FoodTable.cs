using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CrocGame.Core;

/// <summary>
/// One row of food.json. The table decides what may appear; Difficulty decides how
/// often. Movement here is the type's default; the spawn director may substitute a
/// harder strategy as difficulty rises.
/// </summary>
public sealed record FoodType(
    string Id,
    float Width,
    bool Edible,
    string Movement,
    int Score,
    int MinEatenToAppear,
    int Weight = 1,

    /// <summary>A buff id from PowerUp.Parse, or "" for ordinary food. Buffs are never
    /// offered to ordinary spawning; the director asks for them by name.</summary>
    string Power = "");

public sealed class FoodTable
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly List<FoodType> _types;

    private FoodTable(List<FoodType> types) => _types = types;

    public IReadOnlyList<FoodType> Types => _types;

    public static FoodTable FromJson(string json) =>
        new(JsonSerializer.Deserialize<List<FoodType>>(json, Options) ?? new List<FoodType>());

    /// <summary>Types eligible to spawn right now. May legitimately be empty.</summary>
    public IReadOnlyList<FoodType> Available(int eaten, bool edible) =>
        _types.Where(t => t.Power == "" && t.Edible == edible && eaten >= t.MinEatenToAppear).ToList();

    /// <summary>Buffs eligible to spawn right now.</summary>
    public IReadOnlyList<FoodType> PowerUps(int eaten) =>
        _types.Where(t => t.Power != "" && eaten >= t.MinEatenToAppear).ToList();

    /// <summary>One row by id, or null. Used by the director to place guarded spawns.</summary>
    public FoodType? ById(string id) => _types.FirstOrDefault(t => t.Id == id);
}
