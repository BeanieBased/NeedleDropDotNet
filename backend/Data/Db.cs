using Microsoft.Data.SqlClient;

namespace NeedleDrop.Api.Data;

/// <summary>
/// Talks to a real SQL Server Express LocalDB database — the same LocalDB
/// instance Visual Studio ships with and browses through "SQL Server Object
/// Explorer". Nothing fancy: no ORM, no migrations tool, just plain
/// ADO.NET so the schema is easy to read and easy to change while it's
/// still "not in its final form" per the assignment.
///
/// On startup this creates the NeedleDropDb database if it doesn't exist,
/// creates three tables if they don't exist, and seeds them with sample
/// rows the first time so there's always something to SELECT.
///
/// The two leaderboard tables are deliberately minimal — just an Id, the
/// player's 3-letter Initials, and their Score — since that's all a
/// leaderboard needs.
/// </summary>
public static class Db
{
    // Local dev (Visual Studio, no Docker involved): use SQL Server Express
    // LocalDB, same as before — no DB_HOST env var will be set in that case.
    //
    // Inside Docker: the "database" compose service is a real SQL Server
    // Linux container, reachable by its service name and using SQL
    // authentication (LocalDB's Windows-integrated auth doesn't exist in a
    // Linux container). compose.yaml sets DB_HOST/DB_USER/DB_PASSWORD to
    // point here.
    private static readonly string MasterConnectionString = BuildConnectionString("master");
    private static readonly string DbConnectionString = BuildConnectionString("NeedleDropDb");

    private static string BuildConnectionString(string database)
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST");

        if (string.IsNullOrWhiteSpace(host))
        {
            // (localdb)\MSSQLLocalDB is the instance name Visual Studio installs by default.
            return $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True;";
        }

        var user = Environment.GetEnvironmentVariable("DB_USER") ?? "sa";
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "YourStrong!Passw0rd";
        return $"Server={host};Database={database};User Id={user};Password={password};TrustServerCertificate=True;";
    }

    public static void Initialize()
    {
        // 1. Make sure the database itself exists.
        using (var conn = new SqlConnection(MasterConnectionString))
        {
            conn.Open();
            using var cmd = new SqlCommand(
                "IF DB_ID('NeedleDropDb') IS NULL CREATE DATABASE NeedleDropDb;", conn);
            cmd.ExecuteNonQuery();
        }

        // 2. Make sure the tables exist.
        using (var conn = new SqlConnection(DbConnectionString))
        {
            conn.Open();
            using var cmd = new SqlCommand(@"
                IF OBJECT_ID('dbo.Tracks', 'U') IS NULL
                CREATE TABLE dbo.Tracks (
                    Id              INT IDENTITY PRIMARY KEY,
                    SpotifyTrackId  NVARCHAR(50)  NOT NULL,
                    Title           NVARCHAR(200) NOT NULL,
                    Artist          NVARCHAR(200) NOT NULL,
                    Streams         BIGINT        NULL
                );

                IF OBJECT_ID('dbo.SongLeaderboard', 'U') IS NULL
                CREATE TABLE dbo.SongLeaderboard (
                    Id         INT IDENTITY PRIMARY KEY,
                    Initials   CHAR(3)     NOT NULL,
                    Score      INT         NOT NULL
                );

                IF OBJECT_ID('dbo.StreamsLeaderboard', 'U') IS NULL
                CREATE TABLE dbo.StreamsLeaderboard (
                    Id         INT IDENTITY PRIMARY KEY,
                    Initials   CHAR(3)     NOT NULL,
                    Score      INT         NOT NULL
                );
            ", conn);
            cmd.ExecuteNonQuery();
        }

        SeedIfEmpty();
    }

    private static void SeedIfEmpty()
    {
        using var conn = new SqlConnection(DbConnectionString);
        conn.Open();

        using (var check = new SqlCommand("SELECT COUNT(*) FROM dbo.Tracks", conn))
        {
            var count = (int)check.ExecuteScalar();
            if (count == 0)
            {
                using var seed = new SqlCommand(@"
                    INSERT INTO dbo.Tracks (SpotifyTrackId, Title, Artist, Streams) VALUES
                    ('t1', 'Sample Track A', 'Artist One',   812000000),
                    ('t2', 'Sample Track B', 'Artist Two',   640000000),
                    ('t3', 'Sample Track C', 'Artist Three', 1204000000),
                    ('t4', 'Sample Track D', 'Artist Four',  990000000),
                    ('t5', 'Sample Track E', 'Artist Five',  455000000),
                    ('t6', 'Sample Track F', 'Artist Six',   610000000);
                ", conn);
                seed.ExecuteNonQuery();
            }
        }

        using (var check = new SqlCommand("SELECT COUNT(*) FROM dbo.SongLeaderboard", conn))
        {
            var count = (int)check.ExecuteScalar();
            if (count == 0)
            {
                using var seed = new SqlCommand(@"
                    INSERT INTO dbo.SongLeaderboard (Initials, Score) VALUES
                    ('JWB', 8), ('ALT', 7), ('MRQ', 6);
                ", conn);
                seed.ExecuteNonQuery();
            }
        }

        using (var check = new SqlCommand("SELECT COUNT(*) FROM dbo.StreamsLeaderboard", conn))
        {
            var count = (int)check.ExecuteScalar();
            if (count == 0)
            {
                using var seed = new SqlCommand(@"
                    INSERT INTO dbo.StreamsLeaderboard (Initials, Score) VALUES
                    ('JWB', 83), ('ABC', 61), ('QRS', 44);
                ", conn);
                seed.ExecuteNonQuery();
            }
        }
    }

    // ================= Queries =================

    public static List<TrackRow> GetAllTracks()
    {
        var results = new List<TrackRow>();
        using var conn = new SqlConnection(DbConnectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT * FROM dbo.Tracks ORDER BY Id;", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new TrackRow(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("SpotifyTrackId")),
                reader.GetString(reader.GetOrdinal("Title")),
                reader.GetString(reader.GetOrdinal("Artist")),
                reader.IsDBNull(reader.GetOrdinal("Streams")) ? null : reader.GetInt64(reader.GetOrdinal("Streams"))
            ));
        }
        return results;
    }

    public static List<SongLeaderboardRow> GetSongLeaderboard()
    {
        var results = new List<SongLeaderboardRow>();
        using var conn = new SqlConnection(DbConnectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT * FROM dbo.SongLeaderboard ORDER BY Score DESC;", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SongLeaderboardRow(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Initials")).Trim(),
                reader.GetInt32(reader.GetOrdinal("Score"))
            ));
        }
        return results;
    }

    public static List<StreamsLeaderboardRow> GetStreamsLeaderboard()
    {
        var results = new List<StreamsLeaderboardRow>();
        using var conn = new SqlConnection(DbConnectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT * FROM dbo.StreamsLeaderboard ORDER BY Score DESC;", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new StreamsLeaderboardRow(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Initials")).Trim(),
                reader.GetInt32(reader.GetOrdinal("Score"))
            ));
        }
        return results;
    }

    // ================= Inserts =================

    public static void InsertSongScore(string initials, int score)
    {
        using var conn = new SqlConnection(DbConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(
            "INSERT INTO dbo.SongLeaderboard (Initials, Score) VALUES (@Initials, @Score);", conn);
        cmd.Parameters.AddWithValue("@Initials", initials.PadRight(3).Substring(0, 3));
        cmd.Parameters.AddWithValue("@Score", score);
        cmd.ExecuteNonQuery();
    }

    public static void InsertStreamsScore(string initials, int score)
    {
        using var conn = new SqlConnection(DbConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(
            "INSERT INTO dbo.StreamsLeaderboard (Initials, Score) VALUES (@Initials, @Score);", conn);
        cmd.Parameters.AddWithValue("@Initials", initials.PadRight(3).Substring(0, 3));
        cmd.Parameters.AddWithValue("@Score", score);
        cmd.ExecuteNonQuery();
    }
}

public record TrackRow(int Id, string SpotifyTrackId, string Title, string Artist, long? Streams);
public record SongLeaderboardRow(int Id, string Initials, int Score);
public record StreamsLeaderboardRow(int Id, string Initials, int Score);
