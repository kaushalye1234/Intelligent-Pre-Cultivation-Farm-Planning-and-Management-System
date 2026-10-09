using System.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgriAssist.Api.Services.Shared;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace AgriAssist.Api.ExternalServices.Cloudinary;

public sealed class CloudinaryService(
    IOptions<CloudinaryOptions> options,
    IConfiguration configuration,
    HttpClient httpClient,
    ILogger<CloudinaryService> logger) : ICloudinaryService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    public async Task<CloudinaryUploadResult> UploadInspectionImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new ApiException(HttpStatusCode.BadRequest, "EMPTY_FILE", "Image file must not be empty.");
        if (file.Length > MaxSizeBytes) throw new ApiException(HttpStatusCode.BadRequest, "FILE_TOO_LARGE", "Image file must be 5 MB or smaller.");
        if (!AllowedContentTypes.Contains(file.ContentType)) throw new ApiException(HttpStatusCode.BadRequest, "INVALID_MIME_TYPE", "Only jpeg, png, and webp images are allowed.");
        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName))) throw new ApiException(HttpStatusCode.BadRequest, "INVALID_EXTENSION", "Only jpg, jpeg, png, and webp files are allowed.");

        var account = ResolveAccount();
        var cloudinary = new CloudinaryDotNet.Cloudinary(account);
        await using var source = file.OpenReadStream();
        using var buffered = new MemoryStream((int)file.Length);
        await source.CopyToAsync(buffered, cancellationToken);
        var bytes = buffered.ToArray();
        ValidateImageSignature(bytes, file.ContentType);
        var contentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        buffered.Position = 0;
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, buffered),
            Folder = "agriassist/inspections",
            UseFilename = false,
            UniqueFilename = true,
            Type = "authenticated"
        };

        var result = await cloudinary.UploadAsync(uploadParams, cancellationToken);
        if (result.Error is not null)
        {
            logger.LogWarning("Cloudinary upload failed: {CloudinaryError}", result.Error.Message);
            throw new ApiException(HttpStatusCode.BadGateway, "CLOUDINARY_UPLOAD_FAILED", "Image upload failed.");
        }

        return new CloudinaryUploadResult(
            result.SecureUrl.ToString(),
            result.PublicId,
            result.AssetId,
            long.TryParse(result.Version, NumberStyles.None, CultureInfo.InvariantCulture, out var storageVersion) ? storageVersion : null,
            string.IsNullOrWhiteSpace(result.Type) ? "authenticated" : result.Type,
            file.ContentType,
            file.Length,
            contentSha256);
    }

    public async Task<CloudinaryRetrievedAsset> RetrieveInspectionImageAsync(
        string publicId,
        long? storageVersion,
        string deliveryType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            throw new ApiException(HttpStatusCode.BadRequest, "IMAGE_ASSET_INVALID", "Stored image asset identity is invalid.");

        var cloudinary = new CloudinaryDotNet.Cloudinary(ResolveAccount());
        var url = cloudinary.Api.UrlImgUp
            .Secure(true)
            .Signed(true)
            .Type(string.IsNullOrWhiteSpace(deliveryType) ? "upload" : deliveryType);
        if (storageVersion.HasValue) url.Version(storageVersion.Value.ToString(CultureInfo.InvariantCulture));
        var signedUrl = url.BuildUrl(publicId);
        using var response = await httpClient.GetAsync(signedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(HttpStatusCode.BadGateway, "IMAGE_RETRIEVAL_FAILED", "Stored inspection image could not be retrieved.");

        if (response.Content.Headers.ContentLength is > MaxSizeBytes)
            throw new ApiException(HttpStatusCode.BadGateway, "IMAGE_RETRIEVAL_TOO_LARGE", "Stored inspection image exceeds the permitted size.");
        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!AllowedContentTypes.Contains(contentType))
            throw new ApiException(HttpStatusCode.BadGateway, "IMAGE_RETRIEVAL_TYPE_INVALID", "Stored inspection image has an unsupported content type.");

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await responseStream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > MaxSizeBytes)
                throw new ApiException(HttpStatusCode.BadGateway, "IMAGE_RETRIEVAL_TOO_LARGE", "Stored inspection image exceeds the permitted size.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var bytes = output.ToArray();
        ValidateImageSignature(bytes, contentType);
        return new CloudinaryRetrievedAsset(bytes, contentType);
    }

    private static void ValidateImageSignature(byte[] bytes, string contentType)
    {
        var valid = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
            _ => false
        };
        if (!valid) throw new ApiException(HttpStatusCode.BadRequest, "INVALID_IMAGE_CONTENT", "Image content does not match its declared type.");
    }

    private Account ResolveAccount()
    {
        var config = options.Value;
        if (!string.IsNullOrWhiteSpace(config.CloudName) &&
            !string.IsNullOrWhiteSpace(config.ApiKey) &&
            !string.IsNullOrWhiteSpace(config.ApiSecret))
        {
            return new Account(config.CloudName, config.ApiKey, config.ApiSecret);
        }

        var cloudinaryUrl = configuration["CLOUDINARY_URL"];
        if (!string.IsNullOrWhiteSpace(cloudinaryUrl) &&
            Uri.TryCreate(cloudinaryUrl, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, "cloudinary", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(uri.UserInfo) &&
            !string.IsNullOrWhiteSpace(uri.Host))
        {
            var credentials = uri.UserInfo.Split(':', 2);
            if (credentials.Length == 2 &&
                !string.IsNullOrWhiteSpace(credentials[0]) &&
                !string.IsNullOrWhiteSpace(credentials[1]) &&
                !credentials[0].Contains('<', StringComparison.Ordinal) &&
                !credentials[1].Contains('<', StringComparison.Ordinal))
            {
                return new Account(uri.Host, Uri.UnescapeDataString(credentials[0]), Uri.UnescapeDataString(credentials[1]));
            }
        }

        throw new ApiException(HttpStatusCode.ServiceUnavailable, "CLOUDINARY_NOT_CONFIGURED", "Cloudinary is not configured.");
    }
}
