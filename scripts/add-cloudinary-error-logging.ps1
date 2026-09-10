$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$path = Join-Path $PSScriptRoot "..\backend\AgriAssist.Api\ExternalServices\Cloudinary\CloudinaryService.cs"
$content = [System.IO.File]::ReadAllText($path)
$content = $content.Replace(
    'public sealed class CloudinaryService(IOptions<CloudinaryOptions> options, IConfiguration configuration) : ICloudinaryService',
    'public sealed class CloudinaryService(IOptions<CloudinaryOptions> options, IConfiguration configuration, ILogger<CloudinaryService> logger) : ICloudinaryService')
$content = $content.Replace(
    'throw new ApiException(HttpStatusCode.BadGateway, "CLOUDINARY_UPLOAD_FAILED", "Image upload failed.");',
    'logger.LogWarning("Cloudinary upload failed: {CloudinaryError}", result.Error.Message);' + "`r`n" + '            throw new ApiException(HttpStatusCode.BadGateway, "CLOUDINARY_UPLOAD_FAILED", "Image upload failed.");')
[System.IO.File]::WriteAllText($path, $content, $encoding)
