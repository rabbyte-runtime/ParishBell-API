using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using ParishBell.Core.DTOs.Push;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.Push;

public class FcmPushNotificationService(
    FirebaseAppInitializer firebase,
    IUserDeviceRepository deviceRepository,
    ILogger<FcmPushNotificationService> logger) : IPushNotificationService
{
    private readonly FirebaseAppInitializer _firebase = firebase;
    private readonly IUserDeviceRepository _deviceRepository = deviceRepository;
    private readonly ILogger<FcmPushNotificationService> _logger = logger;

    // NOTE: FCM's SendEach accepts at most 500 messages per request.
    private const int MaxTokensPerBatch = 500;

    // NOTE: This service targets FCM (Android) tokens. iOS/APNs delivery is a separate transport.
    private static readonly short AndroidPlatform = (short)DevicePlatform.Android;

    public async Task<PushSendResult> SendToUserAsync(Guid userId, PushNotification notification, CancellationToken ct = default)
    {
        var tokens = await _deviceRepository.GetTokensAsync(userId, AndroidPlatform, ct);
        return await SendToTokensAsync(tokens, notification, ct);
    }

    public async Task<PushSendResult> SendToUsersAsync(IReadOnlyCollection<Guid> userIds, PushNotification notification, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return PushSendResult.Empty;

        var tokens = await _deviceRepository.GetTokensAsync(userIds, AndroidPlatform, ct);
        return await SendToTokensAsync(tokens, notification, ct);
    }

    private async Task<PushSendResult> SendToTokensAsync(IReadOnlyList<string> tokens, PushNotification notification, CancellationToken ct)
    {
        if (tokens.Count == 0) return PushSendResult.Empty;

        // IMPORTANT: MockMode — log instead of sending (dev/testing without Firebase credentials).
        if (!_firebase.Enabled)
        {
            _logger.LogWarning("════════════════════════════════════════");
            _logger.LogWarning("MOCK PUSH - {Title}", notification.Title);
            _logger.LogWarning("Body: {Body}", notification.Body);
            _logger.LogWarning("Recipients: {Count} device token(s)", tokens.Count);
            _logger.LogWarning("════════════════════════════════════════");
            return new PushSendResult { SuccessCount = tokens.Count };
        }

        // NOTE: Notification/Data are identical for every recipient, so build them once and reuse.
        var payload = new Notification { Title = notification.Title, Body = notification.Body };
        var data = notification.Data is null ? null : new Dictionary<string, string>(notification.Data);

        var result = new PushSendResult();
        var staleTokens = new List<string>();

        foreach (var batch in tokens.Chunk(MaxTokensPerBatch))
        {
            // IMPORTANT: We address by FCM registration token (what the Android client's getToken()/onNewToken
            //            returns and what user_devices stores). FirebaseAdmin 3.6.0 marks Tokens obsolete in
            //            favour of Firebase Installation IDs, but FIDs are a different identifier and migrating
            //            to them is out of scope for this token-based integration — hence the local suppression.
#pragma warning disable CS0618
            var message = new MulticastMessage
            {
                Tokens = batch,
                Notification = payload,
                Data = data,
            };
#pragma warning restore CS0618

            BatchResponse response;
            try
            {
                response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, ct);
            }
            catch (FirebaseMessagingException ex)
            {
                // NOTE: A whole-batch failure (auth/config/transport). Count the batch as failed and move on;
                //       one bad batch must not sink the rest of the fan-out.
                _logger.LogError(ex, "FCM multicast send failed for a batch of {Count} token(s).", batch.Length);
                result.FailureCount += batch.Length;
                continue;
            }

            result.SuccessCount += response.SuccessCount;
            result.FailureCount += response.FailureCount;

            // NOTE: Responses line up by index with the tokens we sent. Collect tokens FCM says are dead.
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var send = response.Responses[i];
                if (send.IsSuccess) continue;

                var code = send.Exception?.MessagingErrorCode;
                if (code is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument or MessagingErrorCode.SenderIdMismatch)
                    staleTokens.Add(batch[i]);
            }
        }

        if (staleTokens.Count > 0)
        {
            await _deviceRepository.RemoveByTokensAsync(staleTokens, ct);
            result.PrunedTokenCount = staleTokens.Count;
            _logger.LogInformation("Pruned {Count} stale FCM token(s).", staleTokens.Count);
        }

        return result;
    }
}
