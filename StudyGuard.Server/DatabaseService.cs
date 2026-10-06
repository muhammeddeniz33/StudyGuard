using Npgsql;
using System.Globalization;

public class DatabaseService
{
    private readonly string connectionString;

    public DatabaseService()
    {
        string? databaseUrl =
            Environment.GetEnvironmentVariable(
                "STUDYGUARD_DATABASE_URL"
            );

        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException(
                "STUDYGUARD_DATABASE_URL tanımlı değil."
            );
        }

        connectionString =
            ConvertDatabaseUrlToConnectionString(
                databaseUrl
            );

        Initialize();
    }


    // ==================================================
    // NEON DATABASE URL -> NPGSQL CONNECTION STRING
    // ==================================================

    private string ConvertDatabaseUrlToConnectionString(
        string databaseUrl)
    {
        Uri uri =
            new Uri(databaseUrl);

        string[] userInfo =
            uri.UserInfo.Split(
                ':',
                2
            );

        if (userInfo.Length != 2)
        {
            throw new InvalidOperationException(
                "Database URL kullanıcı bilgileri geçersiz."
            );
        }

        string username =
            Uri.UnescapeDataString(
                userInfo[0]
            );

        string password =
            Uri.UnescapeDataString(
                userInfo[1]
            );

        string database =
            uri.AbsolutePath
                .Trim('/');

        NpgsqlConnectionStringBuilder builder =
            new NpgsqlConnectionStringBuilder
            {
                Host =
                    uri.Host,

                Port =
                    uri.Port > 0
                        ? uri.Port
                        : 5432,

                Username =
                    username,

                Password =
                    password,

                Database =
                    database,

                SslMode =
                    SslMode.Require,

                Pooling =
                    true,

                MaxPoolSize =
                    20,

                Timeout =
                    15,

                CommandTimeout =
                    30
            };

        return builder.ConnectionString;
    }


    // ==================================================
    // DATABASE INITIALIZE
    // ==================================================

    private void Initialize()
    {
        using NpgsqlConnection connection =
            OpenConnection();

        using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS config
        (
            id INTEGER PRIMARY KEY,
            block_message TEXT NOT NULL,
            study_mode_enabled BOOLEAN NOT NULL,
            updated_at TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS violations
        (
            id UUID PRIMARY KEY,
            timestamp TIMESTAMPTZ NOT NULL,
            domain TEXT NOT NULL,
            action TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS device_status
        (
            id INTEGER PRIMARY KEY,
            device_name TEXT NOT NULL,
            last_heartbeat TIMESTAMPTZ NULL
        );
        """;

        command.ExecuteNonQuery();


        // ----------------------------------------------
        // DEFAULT CONFIG
        // ----------------------------------------------

        using NpgsqlCommand configCommand =
            connection.CreateCommand();

        configCommand.CommandText =
        """
        INSERT INTO config
        (
            id,
            block_message,
            study_mode_enabled,
            updated_at
        )
        VALUES
        (
            1,
            @message,
            TRUE,
            @updatedAt
        )
        ON CONFLICT (id)
        DO NOTHING;
        """;

        configCommand.Parameters.AddWithValue(
            "message",
            "Dersine devam et. Serbest zamanda tekrar deneyebilirsin."
        );

        configCommand.Parameters.AddWithValue(
            "updatedAt",
            DateTimeOffset.UtcNow
        );

        configCommand.ExecuteNonQuery();


        // ----------------------------------------------
        // DEFAULT DEVICE
        // ----------------------------------------------

        using NpgsqlCommand deviceCommand =
            connection.CreateCommand();

        deviceCommand.CommandText =
        """
        INSERT INTO device_status
        (
            id,
            device_name,
            last_heartbeat
        )
        VALUES
        (
            1,
            'Kardes-PC',
            NULL
        )
        ON CONFLICT (id)
        DO NOTHING;
        """;

        deviceCommand.ExecuteNonQuery();
    }


    // ==================================================
    // CONFIG
    // ==================================================

    public StudyGuardConfig GetConfig()
    {
        using NpgsqlConnection connection =
            OpenConnection();

        using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            block_message,
            study_mode_enabled,
            updated_at
        FROM config
        WHERE id = 1;
        """;

        using NpgsqlDataReader reader =
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
                reader.GetBoolean(1),

            UpdatedAt =
                reader.GetFieldValue<DateTimeOffset>(2)
        };
    }


    public StudyGuardConfig UpdateMessage(
        string message)
    {
        DateTimeOffset updatedAt =
            DateTimeOffset.UtcNow;

        using NpgsqlConnection connection =
            OpenConnection();

        using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE config
        SET
            block_message = @message,
            updated_at = @updatedAt
        WHERE id = 1;
        """;

        command.Parameters.AddWithValue(
            "message",
            message
        );

        command.Parameters.AddWithValue(
            "updatedAt",
            updatedAt
        );

        command.ExecuteNonQuery();

        return GetConfig();
    }


    public StudyGuardConfig UpdateStudyMode(
        bool enabled)
    {
        DateTimeOffset updatedAt =
            DateTimeOffset.UtcNow;

        using NpgsqlConnection connection =
            OpenConnection();

        using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE config
        SET
            study_mode_enabled = @enabled,
            updated_at = @updatedAt
        WHERE id = 1;
        """;

        command.Parameters.AddWithValue(
            "enabled",
            enabled
        );

        command.Parameters.AddWithValue(
            "updatedAt",
            updatedAt
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

        using NpgsqlConnection connection =
            OpenConnection();

        using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO violations
        (
            id,
            timestamp,
            domain,
            action
        )
        VALUES
        (
            @id,
            @timestamp,
            @domain,
            @action
        );
        """;

        command.Parameters.AddWithValue(
            "id",
            entry.Id
        );

        command.Parameters.AddWithValue(
            "timestamp",
            entry.Timestamp
        );

        command.Parameters.AddWithValue(
            "domain",
            entry.Domain
        );

        command.Parameters.AddWithValue(
            "action",
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

        using NpgsqlConnection connection =
            OpenConnection();

        using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            id,
            timestamp,
            domain,
            action
        FROM violations
        ORDER BY timestamp DESC
        LIMIT @limit;
        """;

        command.Parameters.AddWithValue(
            "limit",
            limit
        );

        using NpgsqlDataReader reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            result.Add(
                new ViolationEntry
                {
                    Id =
                        reader.GetGuid(0),

                    Timestamp =
                        reader.GetFieldValue<DateTimeOffset>(1),

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
        using NpgsqlConnection connection =
            OpenConnection();

        using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        UPDATE device_status
        SET
            device_name =
                CASE
                    WHEN LENGTH(TRIM(@deviceName)) > 0
                    THEN @deviceName
                    ELSE device_name
                END,

            last_heartbeat =
                @heartbeat

        WHERE id = 1;
        """;

        command.Parameters.AddWithValue(
            "deviceName",
            deviceName ?? ""
        );

        command.Parameters.AddWithValue(
            "heartbeat",
            DateTimeOffset.UtcNow
        );

        command.ExecuteNonQuery();
    }


    public DeviceState GetDeviceState()
    {
        using NpgsqlConnection connection =
            OpenConnection();

        using NpgsqlCommand command =
            connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            device_name,
            last_heartbeat
        FROM device_status
        WHERE id = 1;
        """;

        using NpgsqlDataReader reader =
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
                reader.GetFieldValue<DateTimeOffset>(1);
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

    private NpgsqlConnection OpenConnection()
    {
        NpgsqlConnection connection =
            new NpgsqlConnection(
                connectionString
            );

        connection.Open();

        return connection;
    }
}


// ====================================================
// MODELS
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