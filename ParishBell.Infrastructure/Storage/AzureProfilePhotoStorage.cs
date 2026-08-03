using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using ParishBell.Core.Configuration;
using ParishBell.Core.Constants;
using ParishBell.Core.Exceptions;
using ParishBell.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ParishBell.Infrastructure.Storage;

public class AzureProfilePhotoStorage(
    BlobServiceClient blobServiceClient,
    IBlobUrlSigner urlSigner,
    IOptions<BlobStorageSettings> options) : IProfilePhotoStorage
{
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;

    // NOTE: SAS minting and its delegation-key cache live in one place, shared with announcement media.
    private readonly IBlobUrlSigner _urlSigner = urlSigner;

    private readonly BlobStorageSettings _settings = options.Value;

    public async Task<string> UploadAsync(Guid userId, Stream content, CancellationToken ct = default)
    {
        using var normalised = await NormaliseAsync(content, ct);

        var blobName = BlobNameFor(userId);
        var blobClient = ContainerClient().GetBlobClient(blobName);

        // NOTE: One blob per user, overwritten in place.
        // NOTE: A change replaces the photo instead of orphaning the old one.
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

    public Task<string> GetReadUrlAsync(string blobName, CancellationToken ct = default) =>
        _urlSigner.SignAsync(_settings.ProfilePhotosContainer, blobName, ct);

    // NOTE: Crops to a centred square and re-encodes as JPEG.
    // NOTE: Re-encoding means we serve bytes we produced, so EXIF and hidden payloads do not survive.
    private async Task<Stream> NormaliseAsync(Stream content, CancellationToken ct)
    {
        // IMPORTANT: Identify reads only the header, so pixel count is known before anything is allocated.
        // IMPORTANT: Decoding first would defeat the point - compressed size hides the real one.
        // NOTE: The header read must be rewound before the decode, and an upload stream may not seek.
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
            // IMPORTANT: The real content check - a client can claim any content type.
            // NOTE: Only a decodable image gets past here.
            throw new BadRequestException(MessageCodes.ValidationPhotoInvalidType);
        }

        using (image)
        {
            image.Mutate(x => x
                // IMPORTANT: Phone cameras record rotation in EXIF, not in the pixels.
                // IMPORTANT: Applying it before the crop stops portrait shots arriving sideways.
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
}
