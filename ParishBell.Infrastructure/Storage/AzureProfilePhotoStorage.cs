using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using ParishBell.Core.Configuration;
using ParishBell.Core.Constants;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ParishBell.Infrastructure.Storage;

public class AzureProfilePhotoStorage(BlobServiceClient blobServiceClient, IOptions<BlobStorageSettings> options) : IProfilePhotoStorage
{
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly BlobStorageSettings _settings = options.Value;

    // IMPORTANT: With a managed identity there is no account key, so a service SAS is impossible - every read URL is a
    // IMPORTANT:  user delegation SAS. The delegation key is an Entra round trip, so it is cached rather than fetched per photo.
    private UserDelegationKey? _delegationKey;
    private DateTimeOffset _delegationKeyExpiresOn;
    private readonly SemaphoreSlim _delegationKeyLock = new(1, 1);

    // NOTE: Renewed well before it lapses, so a request never races the expiry of a key it just took.
    private static readonly TimeSpan DelegationKeyLifetime = TimeSpan.FromHours(6);
    private static readonly TimeSpan DelegationKeyRenewMargin = TimeSpan.FromMinutes(30);

    public async Task<string> UploadAsync(Guid userId, Stream content, CancellationToken ct = default)
    {
        using var normalised = await NormaliseAsync(content, ct);

        var blobName = BlobNameFor(userId);
        var blobClient = ContainerClient().GetBlobClient(blobName);

        // NOTE: One blob per user, overwritten in place - a change replaces the photo instead of leaving the old one behind.
        await blobClient.UploadAsync(
            normalised,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" } },
            ct);

        return blobName;
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        // NOTE: Idempotent - the row may point at a blob that a retry already removed.
        await ContainerClient().GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task<string> GetReadUrlAsync(string blobName, CancellationToken ct = default)
    {
        var blobClient = ContainerClient().GetBlobClient(blobName);
        var key = await GetDelegationKeyAsync(ct);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _settings.ProfilePhotosContainer,
            BlobName = blobName,
            Resource = "b",

            // NOTE: Backdated a little so a client whose clock runs slow does not reject a URL that is already valid.
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_settings.SasMinutes)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sas = sasBuilder.ToSasQueryParameters(key, _blobServiceClient.AccountName);
        return $"{blobClient.Uri}?{sas}";
    }

    // NOTE: Crops to a centred square and re-encodes as JPEG. Besides the size win, decoding and re-encoding means we
    // NOTE:  serve bytes we produced ourselves - EXIF and anything hidden behind an image extension do not survive.
    private async Task<Stream> NormaliseAsync(Stream content, CancellationToken ct)
    {
        // IMPORTANT: Identify reads only the header, so the pixel count is known before anything is allocated. Decoding
        // IMPORTANT:  first would defeat the point - the whole risk is an image whose compressed size hides its real one.
        // NOTE: The header read has to be rewound before the decode, and an unbuffered upload stream may not seek.
        if (!content.CanSeek)
        {
            var buffered = new MemoryStream();
            await content.CopyToAsync(buffered, ct);
            buffered.Position = 0;
            content = buffered;
        }

        ImageInfo info;

        try
        {
            info = await Image.IdentifyAsync(content, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new BadRequestException(MessageCodes.ValidationPhotoInvalidType);
        }

        if ((long)info.Width * info.Height > _settings.MaxUploadPixels)
            throw new BadRequestException(MessageCodes.ValidationPhotoTooLarge);

        content.Position = 0;

        Image image;

        try
        {
            image = await Image.LoadAsync(content, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            // IMPORTANT: The real content check - a client can claim any content type, but only a decodable image gets this far.
            throw new BadRequestException(MessageCodes.ValidationPhotoInvalidType);
        }

        using (image)
        {
            image.Mutate(x => x
                // IMPORTANT: Phone cameras record rotation in EXIF rather than in the pixels. Applying it before the
                // IMPORTANT:  crop is what stops portrait shots arriving sideways - and the tag is dropped on re-encode.
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(_settings.PhotoSize, _settings.PhotoSize),
                    Mode = ResizeMode.Crop
                }));

            var output = new MemoryStream();
            await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 85 }, ct);
            output.Position = 0;
            return output;
        }
    }

    private BlobContainerClient ContainerClient() =>
        _blobServiceClient.GetBlobContainerClient(_settings.ProfilePhotosContainer);

    // NOTE: Deterministic, so an upload overwrites the previous photo and a delete needs nothing but the user id.
    private static string BlobNameFor(Guid userId) => $"{userId}.jpg";

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
