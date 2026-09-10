$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$root = Join-Path $PSScriptRoot ".."

function Write-NoBom($Path, $Content) {
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

Write-NoBom (Join-Path $root "backend\AgriAssist.Api\ExternalServices\Cloudinary\CloudinaryService.cs") @'
using System.Net;
using AgriAssist.Api.Services.Shared;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace AgriAssist.Api.ExternalServices.Cloudinary;

public sealed class CloudinaryService(IOptions<CloudinaryOptions> options, IConfiguration configuration) : ICloudinaryService
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
        await using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "agriassist/inspections",
            UseFilename = false,
            UniqueFilename = true
        };

        var result = await cloudinary.UploadAsync(uploadParams, cancellationToken);
        if (result.Error is not null)
        {
            throw new ApiException(HttpStatusCode.BadGateway, "CLOUDINARY_UPLOAD_FAILED", "Image upload failed.");
        }

        return new CloudinaryUploadResult(result.SecureUrl.ToString(), result.PublicId, file.ContentType, file.Length);
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
'@

foreach ($relativePath in @(".env.example", "backend\AgriAssist.Api\.env.example")) {
    $path = Join-Path $root $relativePath
    $content = [System.IO.File]::ReadAllText($path)
    if ($content -notmatch "CLOUDINARY_URL=") {
        $content = $content.Replace("Cloudinary__ApiSecret=", "Cloudinary__ApiSecret=`r`nCLOUDINARY_URL=cloudinary://<your_api_key>:<your_api_secret>@rekujqqt")
    }
    $content = $content.Replace("Cloudinary__CloudName=", "Cloudinary__CloudName=rekujqqt")
    Write-NoBom $path $content
}

$envPath = Join-Path $root "backend\AgriAssist.Api\.env"
if (Test-Path $envPath) {
    $content = [System.IO.File]::ReadAllText($envPath)
    if ($content -match "Cloudinary__CloudName=.*") {
        $content = [Regex]::Replace($content, "Cloudinary__CloudName=.*", "Cloudinary__CloudName=rekujqqt")
    } else {
        $content += "`r`nCloudinary__CloudName=rekujqqt"
    }

    if ($content -notmatch "CLOUDINARY_URL=") {
        $content += "`r`nCLOUDINARY_URL=cloudinary://<your_api_key>:<your_api_secret>@rekujqqt"
    }

    Write-NoBom $envPath $content
}
