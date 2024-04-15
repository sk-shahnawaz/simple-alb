using FluentValidation;

using Shared;

namespace LoadBalancer;

public sealed class ApplicationDtoValidator
    : AbstractValidator<ApplicationDto>
{
    public ApplicationDtoValidator()
    {
        RuleFor(x => x.Scheme)
            .NotEmpty()
            .Must(x => x is not null && ApplySchemeValidator(x));

        RuleFor(x => x.Host)
            .NotEmpty();

        RuleFor(x => x.Port)
            .Must(x => x > 0);

        RuleFor(x => x.HealthCheckPath)
            .NotEmpty();

        RuleFor(x => x.Timeout)
            .InclusiveBetween(0, 5);
    }

    private static bool ApplySchemeValidator(string scheme) =>
        scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
        || scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
}
