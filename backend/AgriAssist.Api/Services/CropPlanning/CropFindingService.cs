using System.Net;
using System.Text;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.CropPlanning;

public sealed class CropFindingService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IRequestValidator<SuggestCropsRequest> suggestCropsValidator,
    IRequestValidator<SuggestVarietiesRequest> suggestVarietiesValidator,
    IRequestValidator<DiscoverReferencesRequest> discoverReferencesValidator,
    ICropFindingAIClient aiClient,
    ILogger<CropFindingService> logger) : ICropFindingService
{
    private static readonly string[] ProhibitedRuleTerms =
    [
        "resourcerequirement", "fertilizerquantity", "irrigationquantity",
        "seedquantity", "inventory", "resourcequantity"
    ];

    public async Task<CropSuggestionsResponse> SuggestCropsAsync(
        SuggestCropsRequest request,
        CancellationToken cancellationToken)
    {
        Validate(suggestCropsValidator.Validate(request));
        var adminId = RequireAdmin();
        var response = await CallAsync(
            "SuggestCrops",
            () => aiClient.SuggestCropsAsync(
                new SuggestCropsInput(adminId, CleanOptional(request.Context), request.MaxSuggestions),
                cancellationToken),
            cancellationToken);

        var existing = (await dbContext.CropTypes
                .AsNoTracking()
                .Select(crop => new { crop.Id, crop.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(crop => NormalizeName(crop.Name))
            .ToDictionary(group => group.Key, group => group.First());

        var suggestions = response.Suggestions.Select(suggestion =>
        {
            var match = existing.GetValueOrDefault(NormalizeName(suggestion.Name));
            return suggestion with
            {
                AlreadyExists = match is not null,
                ExistingCropTypeId = match?.Id
            };
        }).ToArray();

        return response with { Suggestions = suggestions };
    }

    public async Task<VarietySuggestionsResponse> SuggestVarietiesAsync(
        SuggestVarietiesRequest request,
        CancellationToken cancellationToken)
    {
        Validate(suggestVarietiesValidator.Validate(request));
        var adminId = RequireAdmin();
        var crop = await dbContext.CropTypes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CropTypeId, cancellationToken)
            ?? throw NotFound("Crop type");

        var response = await CallAsync(
            "SuggestVarieties",
            () => aiClient.SuggestVarietiesAsync(
                new SuggestVarietiesInput(
                    adminId,
                    crop.Id,
                    crop.Name,
                    CleanOptional(request.Context),
                    request.MaxSuggestions),
                cancellationToken),
            cancellationToken);

        var existing = (await dbContext.CropVarieties.AsNoTracking()
                .Where(variety => variety.CropTypeId == crop.Id)
                .Select(variety => new { variety.Id, variety.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(variety => NormalizeName(variety.Name))
            .ToDictionary(group => group.Key, group => group.First());

        var suggestions = response.Suggestions.Select(suggestion =>
        {
            var match = existing.GetValueOrDefault(NormalizeName(suggestion.Name));
            return suggestion with
            {
                AlreadyExists = match is not null,
                ExistingCropVarietyId = match?.Id
            };
        }).ToArray();

        return response with { Suggestions = suggestions };
    }

    public async Task<ReferenceDiscoveryResponse> DiscoverReferencesAsync(
        DiscoverReferencesRequest request,
        CancellationToken cancellationToken)
    {
        Validate(discoverReferencesValidator.Validate(request));
        var adminId = RequireAdmin();
        var crop = await dbContext.CropTypes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CropTypeId, cancellationToken)
            ?? throw NotFound("Crop type");

        string? varietyName = null;
        if (request.CropVarietyId.HasValue)
        {
            var variety = await dbContext.CropVarieties.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == request.CropVarietyId && item.CropTypeId == crop.Id,
                    cancellationToken)
                ?? throw new ApiException(
                    HttpStatusCode.BadRequest,
                    "CROP_VARIETY_MISMATCH",
                    "The selected crop variety does not belong to the selected crop.");
            varietyName = variety.Name;
        }

        var response = await CallAsync(
            "DiscoverReferences",
            () => aiClient.DiscoverReferencesAsync(
                new DiscoverReferencesInput(
                    adminId,
                    crop.Id,
                    crop.Name,
                    request.CropVarietyId,
                    varietyName,
                    CleanOptional(request.Region)),
                cancellationToken),
            cancellationToken);

        var existingReferences = (await dbContext.CropReferenceProfiles.AsNoTracking()
                .Where(reference => reference.SourceUrl != null && reference.SourceUrl != "")
                .Select(reference => new { reference.Id, reference.SourceUrl })
                .ToListAsync(cancellationToken))
            .Select(reference => new { reference.Id, Url = NormalizeUrl(reference.SourceUrl!) })
            .Where(reference => reference.Url is not null)
            .GroupBy(reference => reference.Url!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        var filteredRuleCount = 0;
        var drafts = response.SourceDrafts.Select(draft =>
        {
            var normalizedFinalUrl = NormalizeUrl(draft.Source.FinalUrl);
            var normalizedOriginalUrl = NormalizeUrl(draft.Source.OriginalUrl);
            Guid? existingReferenceId = null;
            if (normalizedFinalUrl is not null && existingReferences.TryGetValue(normalizedFinalUrl, out var finalMatchId))
                existingReferenceId = finalMatchId;
            if (!existingReferenceId.HasValue
                && normalizedOriginalUrl is not null
                && existingReferences.TryGetValue(normalizedOriginalUrl, out var originalMatchId))
                existingReferenceId = originalMatchId;

            var safeItems = draft.Items.Where(item =>
            {
                var permitted = !IsProhibitedResourceRule(item);
                if (!permitted) filteredRuleCount++;
                return permitted;
            }).ToArray();

            return draft with
            {
                Source = draft.Source with
                {
                    ExistingReference = existingReferenceId.HasValue,
                    ExistingReferenceId = existingReferenceId
                },
                Items = safeItems
            };
        }).ToArray();

        var warnings = response.Warnings.ToList();
        if (filteredRuleCount > 0)
        {
            warnings.Add($"{filteredRuleCount} resource or inventory rule suggestion(s) were removed because CropFinding may only propose non-resource rules.");
            logger.LogWarning(
                "CropFinding response {RequestId} contained {FilteredRuleCount} prohibited resource rules",
                response.RequestId,
                filteredRuleCount);
        }

        return response with { SourceDrafts = drafts, Warnings = warnings };
    }

    private async Task<T> CallAsync<T>(
        string action,
        Func<Task<T>> call,
        CancellationToken cancellationToken)
    {
        try
        {
            return await call();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("CropFinding action {Action} timed out", action);
            throw new ApiException(
                HttpStatusCode.GatewayTimeout,
                "CROP_FINDING_TIMEOUT",
                "The AI discovery request timed out. Existing form values were not changed.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "CropFinding action {Action} could not reach the AI service", action);
            throw new ApiException(
                HttpStatusCode.BadGateway,
                "CROP_FINDING_UNAVAILABLE",
                "The AI discovery service is unavailable. Existing form values were not changed.");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "CropFinding action {Action} failed", action);
            throw new ApiException(
                HttpStatusCode.BadGateway,
                "CROP_FINDING_FAILED",
                "The AI discovery service could not complete the request. Existing form values were not changed.");
        }
    }

    private Guid RequireAdmin()
    {
        if (!currentUser.IsInRole(ApplicationRole.Admin) || !currentUser.UserId.HasValue)
            throw new ApiException(HttpStatusCode.Forbidden, "ADMIN_REQUIRED", "An Admin account is required.");
        return currentUser.UserId.Value;
    }

    private static bool IsProhibitedResourceRule(ReferenceDraftItem item)
    {
        if (!string.Equals(item.Field, "structuredRule", StringComparison.Ordinal)) return false;
        var normalized = new string(item.SuggestedValue.GetRawText()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return ProhibitedRuleTerms.Any(normalized.Contains);
    }

    private static string NormalizeName(string value) =>
        string.Join(' ', value.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private static string? NormalizeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")) return null;
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        if ((builder.Scheme == "http" && builder.Port == 80)
            || (builder.Scheme == "https" && builder.Port == 443)) builder.Port = -1;
        builder.Host = builder.Host.ToLowerInvariant();
        builder.Path = builder.Path.Length > 1 ? builder.Path.TrimEnd('/') : builder.Path;
        return builder.Uri.AbsoluteUri;
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
            throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", string.Join(" ", errors));
    }

    private static ApiException NotFound(string name) =>
        new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");
}
