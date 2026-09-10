namespace AgriAssist.Api.ExternalServices.Cloudinary;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> UploadInspectionImageAsync(IFormFile file, CancellationToken cancellationToken);
}
