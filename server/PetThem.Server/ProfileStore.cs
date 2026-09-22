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
/// It is also the seam that let the store move from a dictionary to SQLite without a single
/// endpoint changing: what was a lock became a transaction, and nothing above this interface had
/// to know which it was talking to.
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
