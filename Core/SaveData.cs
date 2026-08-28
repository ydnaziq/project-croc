using System.Collections.Generic;
using System.Text.Json;

namespace CrocGame.Core;

/// <summary>What survives between runs. Cosmetic unlocks only: nothing here changes
/// difficulty, scoring, or the timing window.</summary>
public sealed class SaveData
{
    public int BestScore { get; set; }
    public int LifetimeEaten { get; set; }
    public List<string> UnlockedIds { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Never throws. A corrupt or missing save costs the player their high score;
    /// crashing on launch costs them the game.
    /// </summary>
    public static SaveData FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SaveData();

        try
        {
            return JsonSerializer.Deserialize<SaveData>(json) ?? new SaveData();
        }
        catch (JsonException)
        {
            return new SaveData();
        }
    }
}

public interface ISaveStore
{
    SaveData Load();
    void Save(SaveData data);
}

/// <summary>Test double. The Godot layer supplies the user:// implementation.</summary>
public sealed class InMemorySaveStore : ISaveStore
{
    private SaveData _data = new();

    public SaveData Load() => _data;

    public void Save(SaveData data) => _data = data;
}
