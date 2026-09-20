using System.Security.Cryptography;
using PetThem.Combat;

namespace PetThem.Server;

public sealed record RunOutcome(PlayerProfile Profile, int CoinsAwarded, bool AlreadyCounted);

public enum UnlockResult { Bought, AlreadyOwned, NotEnoughCoins, NotForSale }

public sealed record UnlockOutcome(PlayerProfile Profile, UnlockResult Result, int Price);

/// <summary>
/// Where a player's profile lives, and the only place it is allowed to change.
/// </summary>
/// <remarks>
/// The interesting methods are whole operations rather than load-and-save pairs. Paying out a run
/// means "has this run been counted, and if not add its coins and mark it counted" -- three steps
/// that must not interleave with another request doing the same. Handing callers a profile to
/// mutate and hand back would put that invariant in every endpoint instead of in one place.
///
/// It also means the in-memory implementation and a database one differ by a lock versus a
/// transaction, and nothing above this interface has to know which it is talking to.
/// </remarks>
public interface IProfileStore
{
    Contracts.SessionResponse CreateGuest();

    /// <summary>The player a token belongs to, or null when the token is unknown.</summary>
    string? PlayerFor(string token);

    PlayerProfile Load(string playerId);

    /// <summary>
    /// Pays out a run once. A repeat of a run id already seen changes nothing and says so, because
    /// a phone that loses signal mid-upload will send the same run again and must not be paid
    /// twice for it.
    /// </summary>
    RunOutcome ApplyRun(string playerId, string runId, int coins);

    UnlockOutcome ApplyUnlock(string playerId, PetId pet, BalanceConfig config);
}

/// <summary>
/// Keeps everything in memory. Real enough to hold the invariants and to be checked, and it goes
/// away when the process does -- a database implementation replaces it without the API moving.
/// </summary>
public sealed class InMemoryProfileStore : IProfileStore
{
    private sealed class Player
    {
        public PlayerProfile Profile = new();
        public readonly HashSet<string> CountedRuns = new(StringComparer.Ordinal);
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Player> players = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> tokens = new(StringComparer.Ordinal);

    public Contracts.SessionResponse CreateGuest()
    {
        string playerId = "g_" + Token(9);
        string token = Token(32);
        lock (gate)
        {
            players[playerId] = new Player();
            tokens[token] = playerId;
        }
        return new Contracts.SessionResponse(playerId, token);
    }

    public string? PlayerFor(string token)
    {
        lock (gate) return tokens.TryGetValue(token, out string? playerId) ? playerId : null;
    }

    public PlayerProfile Load(string playerId)
    {
        lock (gate) return Copy(Required(playerId).Profile);
    }

    public RunOutcome ApplyRun(string playerId, string runId, int coins)
    {
        lock (gate)
        {
            Player player = Required(playerId);
            if (!player.CountedRuns.Add(runId))
                return new RunOutcome(Copy(player.Profile), 0, true);
            player.Profile.AddRunReward(coins);
            return new RunOutcome(Copy(player.Profile), coins, false);
        }
    }

    public UnlockOutcome ApplyUnlock(string playerId, PetId pet, BalanceConfig config)
    {
        lock (gate)
        {
            Player player = Required(playerId);
            int price = PlayerProfile.PriceOf(pet, config);
            // Asked apart from Unlock so the caller can say which of the three it was. Unlock
            // itself only answers yes or no, on purpose: it must never half-buy anything.
            if (player.Profile.IsUnlocked(pet))
                return new UnlockOutcome(Copy(player.Profile), UnlockResult.AlreadyOwned, price);
            if (price <= 0)
                return new UnlockOutcome(Copy(player.Profile), UnlockResult.NotForSale, price);
            if (player.Profile.coins < price)
                return new UnlockOutcome(Copy(player.Profile), UnlockResult.NotEnoughCoins, price);

            player.Profile.Unlock(pet, config);
            return new UnlockOutcome(Copy(player.Profile), UnlockResult.Bought, price);
        }
    }

    private Player Required(string playerId) =>
        players.TryGetValue(playerId, out Player? player)
            ? player
            : throw new KeyNotFoundException($"No such player: {playerId}");

    // Handed out rather than shared, so nothing outside the lock is holding live state.
    private static PlayerProfile Copy(PlayerProfile from) => new()
    {
        schemaVersion = from.schemaVersion,
        coins = from.coins,
        runsFinished = from.runsFinished,
        unlockedPets = (string[])from.unlockedPets.Clone(),
    };

    private static string Token(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
