using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using ParishBell.Core.Configuration;
using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.Storage;

public class AzureBlobUrlSigner(BlobServiceClient blobServiceClient, IOptions<BlobStorageSettings> options) : IBlobUrlSigner
{
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly BlobStorageSettings _settings = options.Value;

    // IMPORTANT: Managed identity means no account key, so a service SAS is impossible.
    // IMPORTANT: Every read URL is a user delegation SAS instead.
    // NOTE: The delegation key is an Entra round trip, so it is cached rather than fetched per URL.
    private UserDelegationKey? _delegationKey;
    private DateTimeOffset _delegationKeyExpiresOn;
    private readonly SemaphoreSlim _delegationKeyLock = new(1, 1);

    // NOTE: Renewed early so a request never races the expiry of a key it just took.
    private static readonly TimeSpan DelegationKeyLifetime = TimeSpan.FromHours(6);
    private static readonly TimeSpan DelegationKeyRenewMargin = TimeSpan.FromMinutes(30);

    public async Task<string> SignAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);
        var key = await GetDelegationKeyAsync(ct);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",

            // NOTE: Backdated so a client with a slow clock does not reject a valid URL.
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_settings.SasMinutes)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sas = sasBuilder.ToSasQueryParameters(key, _blobServiceClient.AccountName);
        return $"{blobClient.Uri}?{sas}";
    }

    public async Task<string?> ResignAsync(string? storedUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storedUrl))
            return storedUrl;

        if (!Uri.TryCreate(storedUrl, UriKind.Absolute, out var uri))
            return storedUrl;

        // IMPORTANT: Only re-sign what lives on our own account.
        // NOTE: Anything else is someone else's URL and is returned untouched.
        if (!uri.Host.Equals(_blobServiceClient.Uri.Host, StringComparison.OrdinalIgnoreCase))
            return storedUrl;

        // NOTE: Path is /{container}/{blob}. The blob may contain slashes, so only the first splits.
        var path = uri.AbsolutePath.TrimStart('/');
        var separator = path.IndexOf('/');
        if (separator <= 0 || separator == path.Length - 1)
            return storedUrl;

        var containerName = Uri.UnescapeDataString(path[..separator]);
        var blobName = Uri.UnescapeDataString(path[(separator + 1)..]);

        return await SignAsync(containerName, blobName, ct);
    }

    private async Task<UserDelegationKey> GetDelegationKeyAsync(CancellationToken ct)
    {
        if (_delegationKey is not null && DateTimeOffset.UtcNow < _delegationKeyExpiresOn - DelegationKeyRenewMargin)
            return _delegationKey;

        await _delegationKeyLock.WaitAsync(ct);
        try
        {
            // NOTE: Re-checked inside the lock - whoever was ahead of us has already renewed it.
            if (_delegationKey is not null && DateTimeOffset.UtcNow < _delegationKeyExpiresOn - DelegationKeyRenewMargin)
                return _delegationKey;

            var expiresOn = DateTimeOffset.UtcNow.Add(DelegationKeyLifetime);

            Response<UserDelegationKey> response = await _blobServiceClient.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow.AddMinutes(-5), expiresOn, ct);

            _delegationKey = response.Value;
            _delegationKeyExpiresOn = expiresOn;
            return _delegationKey;
        }
        finally
        {
            _delegationKeyLock.Release();
        }
    }
}
