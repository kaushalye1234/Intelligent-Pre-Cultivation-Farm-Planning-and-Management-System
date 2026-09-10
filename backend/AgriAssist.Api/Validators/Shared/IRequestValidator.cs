namespace AgriAssist.Api.Validators.Shared;

public interface IRequestValidator<in TRequest>
{
    IReadOnlyList<string> Validate(TRequest request);
}
