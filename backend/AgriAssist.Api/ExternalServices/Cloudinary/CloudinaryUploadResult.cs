namespace AgriAssist.Api.ExternalServices.Cloudinary;

public sealed record CloudinaryUploadResult(
    string Url,
    string PublicId,
    string? AssetId,
    long? StorageVersion,
    string DeliveryType,
    string ContentType,
    long SizeBytes,
    string ContentSha256);

public sealed record CloudinaryRetrievedAsset(byte[] Bytes, string ContentType);
