namespace AgriAssist.Api.ExternalServices.Cloudinary;

public sealed record CloudinaryUploadResult(string Url, string PublicId, string ContentType, long SizeBytes);
