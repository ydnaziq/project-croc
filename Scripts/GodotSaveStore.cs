using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>Persists to user://crocgame.save. Never throws: a bad save is a fresh save.</summary>
public sealed class GodotSaveStore : ISaveStore
{
    private const string Path = "user://crocgame.save";

    public SaveData Load()
    {
        if (!FileAccess.FileExists(Path)) return new SaveData();

        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushWarning($"Could not open {Path}; starting from defaults.");
            return new SaveData();
        }

        return SaveData.FromJson(file.GetAsText());
    }

    public void Save(SaveData data)
    {
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushWarning($"Could not write {Path}; progress not saved.");
            return;
        }

        file.StoreString(data.ToJson());
    }
}
