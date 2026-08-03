using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParishBell.Core.Configuration;

namespace ParishBell.Infrastructure.Push;

/// <summary>
/// Creates the single process-wide <see cref="FirebaseApp"/> from configured service-account
/// credentials. Registered as a singleton so the SDK is initialized exactly once, the first time
/// a push is sent. In mock mode no app is created and <see cref="Enabled"/> is false.
/// </summary>
public sealed class FirebaseAppInitializer
{
    // NOTE: True when the Firebase Admin SDK is live; false in mock mode (pushes are logged, not sent).
    public bool Enabled { get; }

    public FirebaseAppInitializer(IOptions<FcmSettings> options, ILogger<FirebaseAppInitializer> logger)
    {
        var settings = options.Value;

        if (settings.MockMode)
        {
            Enabled = false;
            logger.LogWarning("FCM is in MOCK mode — push notifications will be logged, not delivered.");
            return;
        }

        // NOTE: FirebaseApp.Create throws if called twice, so guard on the default instance.
        // NOTE: The initializer is a singleton, but this also covers hosting reloads.
        if (FirebaseApp.DefaultInstance is null)
        {
            // NOTE: A Firebase key is always a service-account credential.
            // NOTE: CredentialFactory is the non-obsolete entry point; ToGoogleCredential adapts it.
            var credential = (!string.IsNullOrWhiteSpace(settings.CredentialsJson)
                    ? CredentialFactory.FromJson<ServiceAccountCredential>(settings.CredentialsJson)
                    : CredentialFactory.FromFile<ServiceAccountCredential>(settings.CredentialsPath))
                .ToGoogleCredential();

            FirebaseApp.Create(new AppOptions { Credential = credential });
        }

        Enabled = true;
        logger.LogInformation("Firebase Admin SDK initialized for push notifications.");
    }
}
