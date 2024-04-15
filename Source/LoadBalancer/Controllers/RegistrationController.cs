using Microsoft.AspNetCore.Mvc;

using FluentValidation;

using Shared;
using MediatR;

namespace LoadBalancer;

[ApiController]
public sealed class RegistrationController(

    ILogger<RegistrationController> logger,
    ApplicationRepository applicationRepository,
    IValidator<ApplicationDto> validator)

    : ControllerBase
{
    [HttpPost]
    [Route("/api/register-application", Name = nameof(RegisterApplicationAsync))]
    public async Task<IActionResult> RegisterApplicationAsync(
        [FromBody] ApplicationDto applicationDto,
        CancellationToken cancellationToken)
    {
        logger.LogError("{@data}", applicationDto);

        var validationResult = await validator.ValidateAsync(
            applicationDto,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var application = (Application)applicationDto;
        if (!applicationRepository.RegisterApplication(application))
        {
            return UnprocessableEntity("Unable to register application.");
        }

        return Ok(new RegistrationResponseDto { Id = application.Id });
    }

    [HttpDelete]
    [Route("api/deregister-application/{id}", Name = nameof(DeregisterApplication))]
    public IActionResult DeregisterApplication(
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromRoute] string id)
    {
        if (!Ulid.TryParse(id, out var result))
        {
            return BadRequest();
        }

        var application = applicationRepository.GetApplication(result);
        if (application is null)
        {
            return NotFound();
        }

        if (!applicationRepository.DeregisterApplication(application.Uri!))
        {
            return (IActionResult)Results.Problem(title: "Failed to de-register application from load balancer.",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: httpContextAccessor?.HttpContext?.TraceIdentifier);
        }
        else
        {
            RoundRobinScheduler.HealthyApplications!.TryRemove(
                    application.Uri!, out _);
        }
        return NoContent();
    }
}
