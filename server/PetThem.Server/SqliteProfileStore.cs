using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PetThem.Combat;

namespace PetThem.Server;

/// <summary>
/// Profiles on disk, in SQLite.
/// </summary>
/// <remarks>
/// The interface was built as a transaction boundary rather than a load-and-save pair so that this
/// swap would not move the API, and it did not: every endpoint is untouched. What was a lock is
/// now a transaction, which is the same promise made by a machine that can be restarted.
///
/// **Paying a run once is a database constraint here, not a check in code.** counted_runs has
/// (player_id, run_id) as its primary key, and the claim is an INSERT whose row count is the
/// answer. Code that asks "has this been counted?" and then counts it has a gap between the two
/// questions; an insert against a primary key has no gap to have.
///
/// Two things guard that at once, and the checks do not separate them: these transactions take
/// the write lock at BEGIN, which serialises them anyway, and the primary key would still refuse
/// a duplicate if they did not. Eight simultaneous uploads of one run are paid once with the
/// transactions deferred as well as immediate, so neither has been shown to be the one carrying
/// it. That is belt and braces rather than a measurement, and it is worth saying so.
///
/// A connection per operation, not one shared. The first attempt kept a single open connection and
/// fell over the moment two uploads arrived together -- SQLite will not start a second transaction
/// on a connection that is already in one. Pooling makes opening cheap, and it is what lets the
/// constraint above actually be tested under concurrency rather than hidden behind a lock.
///
/// Written in SQL rather than through an ORM. The whole point of this file is the transactions and
/// the constraint, and both are clearer when they are visible.
/// </remarks>
public sealed class SqliteProfileStore : IProfileStore
{
    /// <summary>
    /// What this version of the server expects the database to look like.
    /// </summary>
    /// <remarks>
    /// Stored in the file so an older database can be recognised and brought forward rather than
    /// silently misread. There is one version today; the point is that there is somewhere for the
    /// second one to go.
    /// </remarks>
    public const int SchemaVersion = 1;

    /// <summary>How long a writer waits for another writer before giving up.</summary>
    private const int BusyMilliseconds = 5000;

    private readonly string connectionString;

    /// <param name="dataSource">The database file. Created if it is not there yet.</param>
    public SqliteProfileStore(string dataSource)
    {
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        Migrate();
    }

    public Contracts.SessionResponse CreateGuest()
    {
        string playerId = "g_" + Token(9);
        string token = Token(32);
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = Write(connection);
        Execute(transaction,
            "INSERT INTO players (player_id, coins, runs_finished, unlocked_pets, created_utc)" +
            " VALUES ($p, 0, 0, $pets, $now)",
            ("$p", playerId), ("$pets", PlayerProfile.StarterPet.ToString()), ("$now", Now));
        Execute(transaction,
            "INSERT INTO sessions (token, player_id, created_utc) VALUES ($t, $p, $now)",
            ("$t", token), ("$p", playerId), ("$now", Now));
        transaction.Commit();
        return new Contracts.SessionResponse(playerId, token);
    }

    public string? PlayerFor(string token)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT player_id FROM sessions WHERE token = $t";
        command.Parameters.AddWithValue("$t", token);
        return command.ExecuteScalar() as string;
    }

    public PlayerProfile Load(string playerId)
    {
        using SqliteConnection connection = Open();
        return Read(connection, null, playerId);
    }

    public RunOutcome ApplyRun(string playerId, string runId, int coins)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = Write(connection);
        PlayerProfile profile = Read(connection, transaction, playerId);

        // The insert is the claim on this run. Losing the race means another upload of the same
        // run already paid it, which is exactly what the caller should be told -- there is no
        // separate "have we seen this?" query that could be answered before the winner commits.
        using (SqliteCommand claim = connection.CreateCommand())
        {
            claim.Transaction = transaction;
            claim.CommandText =
                "INSERT OR IGNORE INTO counted_runs (player_id, run_id, coins, counted_utc)" +
                " VALUES ($p, $r, $c, $now)";
            claim.Parameters.AddWithValue("$p", playerId);
            claim.Parameters.AddWithValue("$r", runId);
            claim.Parameters.AddWithValue("$c", coins);
            claim.Parameters.AddWithValue("$now", Now);
            if (claim.ExecuteNonQuery() == 0)
            {
                transaction.Commit();
                return new RunOutcome(profile, 0, true);
            }
        }

        profile.AddRunReward(coins);
        Save(transaction, playerId, profile);
        transaction.Commit();
        return new RunOutcome(profile, coins, false);
    }

    public UnlockOutcome ApplyUnlock(string playerId, PetId pet, BalanceConfig config)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = Write(connection);
        PlayerProfile profile = Read(connection, transaction, playerId);
        int price = PlayerProfile.PriceOf(pet, config);

        if (profile.IsUnlocked(pet))
            return Done(transaction, profile, UnlockResult.AlreadyOwned, price);
        if (price <= 0)
            return Done(transaction, profile, UnlockResult.NotForSale, price);
        if (profile.coins < price)
            return Done(transaction, profile, UnlockResult.NotEnoughCoins, price);

        profile.Unlock(pet, config);
        Save(transaction, playerId, profile);
        transaction.Commit();
        return new UnlockOutcome(profile, UnlockResult.Bought, price);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using SqliteCommand pragma = connection.CreateCommand();
        // WAL lets readers carry on while one writer works, and the timeout is what turns a
        // simultaneous write from an error into a short wait.
        pragma.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={BusyMilliseconds};";
        pragma.ExecuteNonQuery();
        return connection;
    }

    /// <summary>
    /// A transaction that takes the write lock immediately rather than on first write.
    /// </summary>
    /// <remarks>
    /// Every one of these reads the profile and then writes it. A deferred transaction would take
    /// a read lock first and try to upgrade, and two of those upgrading at once is a deadlock that
    /// no timeout resolves -- each is waiting for the other to let go of a lock it will not release
    /// until it has finished. Taking the write lock up front turns that into a queue.
    /// </remarks>
    private static SqliteTransaction Write(SqliteConnection connection) =>
        connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

    private static UnlockOutcome Done(SqliteTransaction transaction, PlayerProfile profile,
        UnlockResult result, int price)
    {
        transaction.Commit();
        return new UnlockOutcome(profile, result, price);
    }

    private static PlayerProfile Read(SqliteConnection connection, SqliteTransaction? transaction,
        string playerId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT coins, runs_finished, unlocked_pets FROM players WHERE player_id = $p";
        command.Parameters.AddWithValue("$p", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) throw new KeyNotFoundException($"No such player: {playerId}");

        var profile = new PlayerProfile
        {
            coins = reader.GetInt32(0),
            runsFinished = reader.GetInt32(1),
            unlockedPets = reader.GetString(2).Split(',', StringSplitOptions.RemoveEmptyEntries),
        };
        // The same repair the game runs on a save file. A row edited by hand is no different from
        // a save file edited by hand, and losing a profile is worse than whatever the odd value
        // claimed.
        profile.Normalize();
        return profile;
    }

    private static void Save(SqliteTransaction transaction, string playerId, PlayerProfile profile) =>
        Execute(transaction,
            "UPDATE players SET coins = $c, runs_finished = $r, unlocked_pets = $pets" +
            " WHERE player_id = $p",
            ("$c", profile.coins), ("$r", profile.runsFinished),
            ("$pets", string.Join(",", profile.unlockedPets)), ("$p", playerId));

    private static void Execute(SqliteTransaction transaction, string sql,
        params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    private void Migrate()
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = Write(connection);
        Execute(transaction, "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL)");

        int found;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT version FROM schema_version";
            found = command.ExecuteScalar() is long version ? (int)version : 0;
        }

        if (found > SchemaVersion)
            throw new InvalidOperationException(
                $"This database is version {found} and this server understands {SchemaVersion}. " +
                "It was written by a newer server; running an older one against it would lose data.");

        if (found < 1)
        {
            Execute(transaction,
                "CREATE TABLE players (" +
                " player_id TEXT PRIMARY KEY," +
                " coins INTEGER NOT NULL," +
                " runs_finished INTEGER NOT NULL," +
                " unlocked_pets TEXT NOT NULL," +
                " created_utc TEXT NOT NULL)");
            Execute(transaction,
                "CREATE TABLE sessions (" +
                " token TEXT PRIMARY KEY," +
                " player_id TEXT NOT NULL REFERENCES players(player_id)," +
                " created_utc TEXT NOT NULL)");
            // The primary key is the idempotency. A second upload of the same run cannot insert,
            // whatever else is happening at the same time.
            Execute(transaction,
                "CREATE TABLE counted_runs (" +
                " player_id TEXT NOT NULL," +
                " run_id TEXT NOT NULL," +
                " coins INTEGER NOT NULL," +
                " counted_utc TEXT NOT NULL," +
                " PRIMARY KEY (player_id, run_id))");
            Execute(transaction, "DELETE FROM schema_version");
            Execute(transaction, "INSERT INTO schema_version (version) VALUES ($v)",
                ("$v", SchemaVersion));
        }

        transaction.Commit();
    }

    private static string Now => DateTime.UtcNow.ToString("O");

    private static string Token(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
