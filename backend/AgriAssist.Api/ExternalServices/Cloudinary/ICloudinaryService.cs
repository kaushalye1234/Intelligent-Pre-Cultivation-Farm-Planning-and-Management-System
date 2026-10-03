namespace AgriAssist.Api.ExternalServices.Cloudinary;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> UploadInspectionImageAsync(IFormFile file, CancellationToken cancellationToken);
    Task<CloudinaryRetrievedAsset> RetrieveInspectionImageAsync(
        string publicId,
        long? storageVersion,
        string deliveryType,
        CancellationToken cancellationToken);
}
