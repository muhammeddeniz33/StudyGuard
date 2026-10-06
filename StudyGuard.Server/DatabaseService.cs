using Microsoft.Data.Sqlite;
using System.Globalization;

public class DatabaseService
{
    private readonly string connectionString;

    public DatabaseService()
    {
        string dataDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                "data"
            );

        Directory.CreateDirectory(
            dataDirectory
        );

        string databasePath =
            Path.Combine(
                dataDirectory,
                "studyguard.db"
            );

        connectionString =
            $"Data Source={databasePath}";

        Initialize();
    }


    // ==================================================
    // DATABASE INITIALIZE
    // ==================================================

    private void Initialize()
    {
        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Config
        (
            Id INTEGER PRIMARY KEY,
            BlockMessage TEXT NOT NULL,
            StudyModeEnabled INTEGER NOT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Violations
        (
            Id TEXT PRIMARY KEY,
            Timestamp TEXT NOT NULL,
            Domain TEXT NOT NULL,
            Action TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS DeviceStatus
        (
            Id INTEGER PRIMARY KEY,
            DeviceName TEXT NOT NULL,
            LastHeartbeat TEXT NULL
        );
        """;

        command.ExecuteNonQuery();


        // ----------------------------------------------
        // DEFAULT CONFIG
        // ----------------------------------------------

        using SqliteCommand configCommand =
            connection.CreateCommand();

        configCommand.CommandText =
        """
        INSERT OR IGNORE INTO Config
        (
            Id,
            BlockMessage,
            StudyModeEnabled,
            UpdatedAt
        )
        VALUES
        (
            1,
            $message,
            1,
            $updatedAt
        );
        """;

        configCommand.Parameters.AddWithValue(
            "$message",
            "Dersine devam et. Serbest zamanda tekrar deneyebilirsin."
        );

        configCommand.Parameters.AddWithValue(
            "$updatedAt",
            DateTimeOffset.UtcNow.ToString("O")
        );

        configCommand.ExecuteNonQuery();


        // ----------------------------------------------
        // DEFAULT DEVICE
        // ----------------------------------------------

        using SqliteCommand deviceCommand =
            connection.CreateCommand();

        deviceCommand.CommandText =
        """
        INSERT OR IGNORE INTO DeviceStatus
        (
            Id,
            DeviceName,
            LastHeartbeat
        )
        VALUES
        (
            1,
            'Kardes-PC',
            NULL
        );
        """;

        deviceCommand.ExecuteNonQuery();
    }


    // ==================================================
    // CONFIG
    // ==================================================

    public StudyGuardConfig GetConfig()
    {
        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            BlockMessage,
            StudyModeEnabled,
            UpdatedAt
        FROM Config
        WHERE Id = 1;
        """;

        using SqliteDataReader reader =
            command.ExecuteReader();

        if (!reader.Read())
        {
            throw new Exception(
                "Config bulunamadı."
            );
        }

        return new StudyGuardConfig
        {
            BlockMessage =
                reader.GetString(0),

            StudyModeEnabled =
                reader.GetInt64(1) == 1,

            UpdatedAt =
                DateTimeOffset.Parse(
                    reader.GetString(2),
                    CultureInfo.InvariantCulture
                )
        };
    }


    public StudyGuardConfig UpdateMessage(
        string message)
    {
        DateTimeOffset updatedAt =
            DateTimeOffset.UtcNow;

        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE Config
        SET
            BlockMessage = $message,
            UpdatedAt = $updatedAt
        WHERE Id = 1;
        """;

        command.Parameters.AddWithValue(
            "$message",
            message
        );

        command.Parameters.AddWithValue(
            "$updatedAt",
            updatedAt.ToString("O")
        );

        command.ExecuteNonQuery();

        return GetConfig();
    }


    public StudyGuardConfig UpdateStudyMode(
        bool enabled)
    {
        DateTimeOffset updatedAt =
            DateTimeOffset.UtcNow;

        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE Config
        SET
            StudyModeEnabled = $enabled,
            UpdatedAt = $updatedAt
        WHERE Id = 1;
        """;

        command.Parameters.AddWithValue(
            "$enabled",
            enabled ? 1 : 0
        );

        command.Parameters.AddWithValue(
            "$updatedAt",
            updatedAt.ToString("O")
        );

        command.ExecuteNonQuery();

        return GetConfig();
    }


    // ==================================================
    // VIOLATIONS
    // ==================================================

    public ViolationEntry AddViolation(
        string domain)
    {
        ViolationEntry entry =
            new()
            {
                Id =
                    Guid.NewGuid(),

                Timestamp =
                    DateTimeOffset.UtcNow,

                Domain =
                    domain,

                Action =
                    "BLOCKED"
            };

        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO Violations
        (
            Id,
            Timestamp,
            Domain,
            Action
        )
        VALUES
        (
            $id,
            $timestamp,
            $domain,
            $action
        );
        """;

        command.Parameters.AddWithValue(
            "$id",
            entry.Id.ToString()
        );

        command.Parameters.AddWithValue(
            "$timestamp",
            entry.Timestamp.ToString("O")
        );

        command.Parameters.AddWithValue(
            "$domain",
            entry.Domain
        );

        command.Parameters.AddWithValue(
            "$action",
            entry.Action
        );

        command.ExecuteNonQuery();

        return entry;
    }


    public List<ViolationEntry> GetViolations(
        int limit = 100)
    {
        List<ViolationEntry> result =
            new();

        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            Timestamp,
            Domain,
            Action
        FROM Violations
        ORDER BY Timestamp DESC
        LIMIT $limit;
        """;

        command.Parameters.AddWithValue(
            "$limit",
            limit
        );

        using SqliteDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new ViolationEntry
                {
                    Id =
                        Guid.Parse(
                            reader.GetString(0)
                        ),

                    Timestamp =
                        DateTimeOffset.Parse(
                            reader.GetString(1),
                            CultureInfo.InvariantCulture
                        ),

                    Domain =
                        reader.GetString(2),

                    Action =
                        reader.GetString(3)
                }
            );
        }

        return result;
    }


    // ==================================================
    // DEVICE HEARTBEAT
    // ==================================================

    public void UpdateHeartbeat(
        string deviceName)
    {
        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE DeviceStatus
        SET
            DeviceName =
                CASE
                    WHEN LENGTH(TRIM($deviceName)) > 0
                    THEN $deviceName
                    ELSE DeviceName
                END,

            LastHeartbeat =
                $heartbeat

        WHERE Id = 1;
        """;

        command.Parameters.AddWithValue(
            "$deviceName",
            deviceName ?? ""
        );

        command.Parameters.AddWithValue(
            "$heartbeat",
            DateTimeOffset.UtcNow.ToString("O")
        );

        command.ExecuteNonQuery();
    }


    public DeviceState GetDeviceState()
    {
        using SqliteConnection connection =
            OpenConnection();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            DeviceName,
            LastHeartbeat
        FROM DeviceStatus
        WHERE Id = 1;
        """;

        using SqliteDataReader reader =
            command.ExecuteReader();

        if (!reader.Read())
        {
            return new DeviceState
            {
                DeviceName =
                    "Kardes-PC"
            };
        }

        DateTimeOffset? heartbeat =
            null;

        if (!reader.IsDBNull(1))
        {
            heartbeat =
                DateTimeOffset.Parse(
                    reader.GetString(1),
                    CultureInfo.InvariantCulture
                );
        }

        return new DeviceState
        {
            DeviceName =
                reader.GetString(0),

            LastHeartbeat =
                heartbeat
        };
    }


    // ==================================================
    // CONNECTION
    // ==================================================

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection =
            new(connectionString);

        connection.Open();

        return connection;
    }
}


// ====================================================
// DATABASE MODELS
// ====================================================

public class StudyGuardConfig
{
    public string BlockMessage { get; set; } = "";

    public bool StudyModeEnabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}


public class ViolationEntry
{
    public Guid Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Domain { get; set; } = "";

    public string Action { get; set; } = "";
}


public class DeviceState
{
    public string DeviceName { get; set; } = "";

    public DateTimeOffset? LastHeartbeat { get; set; }
}


public class UpdateMessageRequest
{
    public string Message { get; set; } = "";
}


public class UpdateStudyModeRequest
{
    public bool Enabled { get; set; }
}


public class CreateViolationRequest
{
    public string Domain { get; set; } = "";
}


public class DeviceHeartbeatRequest
{
    public string DeviceName { get; set; } = "";
}