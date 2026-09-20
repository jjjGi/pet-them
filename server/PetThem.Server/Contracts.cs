namespace PetThem.Server;

/// <summary>
/// The wire shapes. Deliberately flat and primitive: an app that is already on someone's phone
/// cannot be changed, so every field here is something we are willing to keep answering to.
/// </summary>
public static class Contracts
{
    /// <summary>
    /// A session. Today it is a guest: calling for one creates a new player out of nothing.
    /// </summary>
    /// <remarks>
    /// Play Games sign-in slots in here without changing the shape -- the request gains a server
    /// auth code, the server exchanges it with Google and returns the token for the player bound
    /// to that Google account. Clients that already store a token keep working.
    /// </remarks>
    public sealed record SessionResponse(string PlayerId, string Token);

    public sealed record ProfileResponse(int SchemaVersion, int Coins, int RunsFinished, string[] UnlockedPets)
    {
        public static ProfileResponse From(PetThem.Combat.PlayerProfile profile) =>
            new(profile.schemaVersion, profile.coins, profile.runsFinished, profile.unlockedPets);
    }

    /// <summary>
    /// A finished run, as the client saw it.
    /// </summary>
    /// <remarks>
    /// It does not carry the coins earned. The client says what happened; the server decides what
    /// that is worth, using the same CoinsFor the game used to show the number. A client asking
    /// for a payout is a client that can ask for any payout.
    ///
    /// The kill count and the clock are still taken on trust -- only replay validation closes
    /// that, and it is not built. What this shape does guarantee is that lying about the fight
    /// cannot become an arbitrary amount of money, and that the same run cannot be paid twice.
    /// </remarks>
    public sealed record RunSubmission(
        string RunId, int Seed, string Weapon, string Pet,
        int Kills, float Seconds, bool BossDefeated);

    public sealed record RunReceipt(
        string RunId, int CoinsAwarded, bool AlreadyCounted, string Twist, ProfileResponse Profile);

    public sealed record UnlockRequest(string Pet);

    /// <summary>One shape for every failure, so a client never has to guess at the body.</summary>
    public sealed record Problem(string Error, string Detail);
}
