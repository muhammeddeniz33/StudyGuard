var builder =
    WebApplication.CreateBuilder(args);


// ====================================================
// RAILWAY PORT
// ====================================================

// Railway production ortamında PORT değişkeni verir.
// Local geliştirmede PORT yoksa Visual Studio'nun
// launchSettings.json ayarlarına dokunmuyoruz.

string? railwayPort =
    Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(railwayPort))
{
    builder.WebHost.UseUrls(
        $"http://0.0.0.0:{railwayPort}"
    );
}


// ====================================================
// SERVICES
// ====================================================

builder.Services.AddOpenApi();


// ====================================================
// APP BUILD
// ====================================================

var app =
    builder.Build();


// ====================================================
// STATIC ADMIN PANEL
// ====================================================

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


// Local geliştirmede HTTP kullanıyoruz.
// Railway dışarıdan HTTPS sağlayacak.
// app.UseHttpsRedirection();


// ====================================================
// SECURITY
// ====================================================

// Development ortamında fallback anahtarları kullanılır.
// Production ortamında Railway environment variable
// tanımlanmazsa server başlamaz.

string ADMIN_KEY =
    Environment.GetEnvironmentVariable(
        "STUDYGUARD_ADMIN_KEY"
    )
    ??
    (
        app.Environment.IsDevelopment()
            ? "SG-Admin-9876-X"
            : throw new InvalidOperationException(
                "STUDYGUARD_ADMIN_KEY tanımlı değil."
            )
    );


string DEVICE_KEY =
    Environment.GetEnvironmentVariable(
        "STUDYGUARD_DEVICE_KEY"
    )
    ??
    (
        app.Environment.IsDevelopment()
            ? "SG-Device-4521-Y"
            : throw new InvalidOperationException(
                "STUDYGUARD_DEVICE_KEY tanımlı değil."
            )
    );


// ====================================================
// DATABASE
// ====================================================

DatabaseService database =
    new DatabaseService();


// ====================================================
// AUTH HELPERS
// ====================================================

bool IsAdminAuthorized(
    HttpRequest request)
{
    if (!request.Headers.TryGetValue(
        "X-Admin-Key",
        out var key))
    {
        return false;
    }

    return key == ADMIN_KEY;
}


bool IsDeviceAuthorized(
    HttpRequest request)
{
    if (!request.Headers.TryGetValue(
        "X-Device-Key",
        out var key))
    {
        return false;
    }

    return key == DEVICE_KEY;
}


// ====================================================
// SERVER HEALTH
// ====================================================

app.MapGet(
    "/api/health",
    () =>
    {
        return Results.Ok(
            new
            {
                status =
                    "ONLINE",

                time =
                    DateTimeOffset.UtcNow
            }
        );
    });


// ====================================================
// DEVICE HEARTBEAT
// ====================================================

app.MapPost(
    "/api/device/heartbeat",
    (
        HttpRequest httpRequest,
        DeviceHeartbeatRequest request
    ) =>
    {
        if (!IsDeviceAuthorized(
            httpRequest))
        {
            return Results.Unauthorized();
        }

        database.UpdateHeartbeat(
            request.DeviceName
        );

        return Results.Ok(
            new
            {
                received =
                    true,

                serverTime =
                    DateTimeOffset.UtcNow
            }
        );
    });


// ====================================================
// ADMIN - DEVICE STATUS
// ====================================================

app.MapGet(
    "/api/admin/device-status",
    (HttpRequest request) =>
    {
        if (!IsAdminAuthorized(request))
        {
            return Results.Unauthorized();
        }

        DeviceState device =
            database.GetDeviceState();

        bool online =
            false;

        double? secondsAgo =
            null;

        if (device.LastHeartbeat.HasValue)
        {
            secondsAgo =
                (
                    DateTimeOffset.UtcNow -
                    device.LastHeartbeat.Value
                ).TotalSeconds;

            online =
                secondsAgo <= 15;
        }

        StudyGuardConfig config =
            database.GetConfig();

        return Results.Ok(
            new
            {
                deviceName =
                    device.DeviceName,

                online =
                    online,

                lastSeen =
                    device.LastHeartbeat,

                secondsAgo =
                    secondsAgo,

                studyModeEnabled =
                    config.StudyModeEnabled
            }
        );
    });


// ====================================================
// DEVICE - CONFIG AL
// ====================================================

app.MapGet(
    "/api/config",
    (HttpRequest request) =>
    {
        if (!IsDeviceAuthorized(request))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            database.GetConfig()
        );
    });


// ====================================================
// ADMIN - CONFIG AL
// ====================================================

app.MapGet(
    "/api/admin/config",
    (HttpRequest request) =>
    {
        if (!IsAdminAuthorized(request))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            database.GetConfig()
        );
    });


// ====================================================
// ADMIN - MESAJ GÜNCELLE
// ====================================================

app.MapPost(
    "/api/admin/config/message",
    (
        HttpRequest httpRequest,
        UpdateMessageRequest request
    ) =>
    {
        if (!IsAdminAuthorized(
            httpRequest))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(
            request.Message))
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "Mesaj boş olamaz."
                }
            );
        }

        StudyGuardConfig config =
            database.UpdateMessage(
                request.Message.Trim()
            );

        return Results.Ok(config);
    });


// ====================================================
// ADMIN - STUDY MODE AÇ / KAPAT
// ====================================================

app.MapPost(
    "/api/admin/config/study-mode",
    (
        HttpRequest httpRequest,
        UpdateStudyModeRequest request
    ) =>
    {
        if (!IsAdminAuthorized(
            httpRequest))
        {
            return Results.Unauthorized();
        }

        StudyGuardConfig config =
            database.UpdateStudyMode(
                request.Enabled
            );

        return Results.Ok(config);
    });


// ====================================================
// DEVICE - İHLAL GÖNDER
// ====================================================

app.MapPost(
    "/api/violations",
    (
        HttpRequest httpRequest,
        CreateViolationRequest request
    ) =>
    {
        if (!IsDeviceAuthorized(
            httpRequest))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(
            request.Domain))
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "Domain boş olamaz."
                }
            );
        }

        ViolationEntry entry =
            database.AddViolation(
                request.Domain.Trim()
            );

        return Results.Ok(entry);
    });


// ====================================================
// ADMIN - İHLALLERİ GÖR
// ====================================================

app.MapGet(
    "/api/admin/violations",
    (HttpRequest request) =>
    {
        if (!IsAdminAuthorized(request))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            database.GetViolations(100)
        );
    });


// ====================================================
// SERVER START
// ====================================================

app.Run();