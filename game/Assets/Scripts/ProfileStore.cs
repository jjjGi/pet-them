using System;
using System.IO;
using PetThem.Combat;
using UnityEngine;

namespace PetThem.Game
{
    /// <summary>
    /// Reads and writes the between-runs save. All of the rules live in PlayerProfile; this only
    /// moves bytes, so the rules can be checked without the editor.
    /// </summary>
    /// <remarks>
    /// Saving writes a temporary file and then replaces the real one, so a crash partway through
    /// leaves the previous save intact rather than a half-written file the game cannot read.
    /// </remarks>
    public static class ProfileStore
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "profile.json");

        /// <summary>Non-empty when the last load or save had a problem the player should be told about.</summary>
        public static string Status { get; private set; } = "";

        public static PlayerProfile Load()
        {
            Status = "";
            try
            {
                if (!File.Exists(FilePath)) return new PlayerProfile();
                string text = File.ReadAllText(FilePath);
                PlayerProfile profile = JsonUtility.FromJson<PlayerProfile>(text);
                if (profile == null)
                {
                    Status = "Save file was empty. Starting a new profile.";
                    return new PlayerProfile();
                }
                var repairs = profile.Normalize();
                if (repairs.Count > 0)
                {
                    Status = "Save repaired: " + string.Join("; ", repairs);
                    Debug.LogWarning(Status);
                }
                return profile;
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                                              exception is UnauthorizedAccessException)
            {
                // A save that cannot be read must not stop the player from playing.
                Status = "Could not read the save: " + exception.Message;
                Debug.LogWarning(Status);
                return new PlayerProfile();
            }
        }

        public static void Save(PlayerProfile profile)
        {
            if (profile == null) return;
            try
            {
                profile.Normalize();
                string temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(profile, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temporary, FilePath);
            }
            catch (Exception exception) when (exception is IOException ||
                                              exception is UnauthorizedAccessException)
            {
                Status = "Could not save: " + exception.Message;
                Debug.LogWarning(Status);
            }
        }
    }
}
