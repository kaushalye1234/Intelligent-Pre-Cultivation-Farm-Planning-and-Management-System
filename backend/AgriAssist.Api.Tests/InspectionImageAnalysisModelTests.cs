using AgriAssist.Api.Models.Inspections;

namespace AgriAssist.Api.Tests;

public sealed class InspectionImageAnalysisModelTests
{
    [Fact]
    public void New_image_and_analysis_have_safe_defaults()
    {
        var image = new InspectionImage();
        var analysis = new InspectionImageAnalysis();

        Assert.False(image.IsRepresentativeForAi);
        Assert.Equal("upload", image.DeliveryType);
        Assert.Equal(InspectionImageAnalysisStatus.Running, analysis.Status);
        Assert.False(analysis.IsTerminal);
        Assert.Equal(1, analysis.Version);
    }

    [Theory]
    [InlineData(InspectionImageAnalysisStatus.Succeeded)]
    [InlineData(InspectionImageAnalysisStatus.Failed)]
    [InlineData(InspectionImageAnalysisStatus.TimedOut)]
    [InlineData(InspectionImageAnalysisStatus.Interrupted)]
    [InlineData(InspectionImageAnalysisStatus.Stale)]
    public void Non_running_analysis_statuses_are_terminal(InspectionImageAnalysisStatus status)
    {
        var analysis = new InspectionImageAnalysis { Status = status };

        Assert.True(analysis.IsTerminal);
    }

    [Fact]
    public void Review_defaults_are_append_only_payload_friendly()
    {
        var review = new InspectionImageAnalysisReview();

        Assert.Equal("[]", review.EditedFieldsJson);
        Assert.True(review.ReviewedAt <= DateTime.UtcNow);
    }
}
