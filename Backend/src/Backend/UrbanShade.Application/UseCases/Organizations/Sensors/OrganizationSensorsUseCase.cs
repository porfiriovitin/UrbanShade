using UrbanShade.Application.UseCases.SensorReadings.GetHistory;
using UrbanShade.Application.UseCases.Sensors.GetById;
using UrbanShade.Communication.Requests.SensorReadings;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Responses.SensorReadings;
using UrbanShade.Communication.Responses.Sensors;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Organizations.Sensors;

public sealed class OrganizationSensorsUseCase : IOrganizationSensorsUseCase
{
    private readonly IOrganizationRepository _organizations;
    private readonly ISensorRepository _sensors;
    private readonly ISensorReadingRepository _readings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrganizationAccess _access;

    public OrganizationSensorsUseCase(IOrganizationRepository organizations, ISensorRepository sensors, ISensorReadingRepository readings, IUnitOfWork unitOfWork, OrganizationAccess access)
    {
        _organizations = organizations;
        _sensors = sensors;
        _readings = readings;
        _unitOfWork = unitOfWork;
        _access = access;
    }

    public async Task Assign(Guid actorId, Guid organizationId, Guid sensorId, CancellationToken cancellationToken)
    {
        await _access.GetActor(actorId, true, cancellationToken);

        await using var transaction = await _unitOfWork.BeginTransaction(cancellationToken);
        var organization = await _organizations.FindForUpdate(organizationId, cancellationToken);
        if (organization is null || !organization.Active)
            throw new OrganizationException(ErrorKind.NotFound, "Active organization is unavailable.");

        var sensor = await _organizations.FindSensorForUpdate(sensorId, cancellationToken);
        if (sensor is null)
            throw new OrganizationException(ErrorKind.NotFound, "Sensor is unavailable.");
        if (sensor.OrganizationId.HasValue && sensor.OrganizationId != organizationId)
            throw new OrganizationException(ErrorKind.Conflict, "Sensor already belongs to another organization.");

        sensor.AssignOrganization(organizationId);
        await _unitOfWork.SaveChanges(cancellationToken);
        await transaction.Commit(cancellationToken);
    }

    public async Task<List<SensorResponse>> GetAll(Guid actorId, Guid organizationId, CancellationToken cancellationToken)
    {
        await _access.Require(organizationId, actorId, cancellationToken);

        return (await _organizations.GetSensors(organizationId, cancellationToken)).Select(GetSensorByIdUseCase.ToResponse).ToList();
    }

    public async Task<PagedResponse<SensorReadingResponse>> GetHistory(Guid actorId, Guid organizationId, Guid sensorId, GetSensorReadingsRequest request, CancellationToken cancellationToken)
    {
        await RequireSensor(actorId, organizationId, sensorId, cancellationToken);

        var result = new GetSensorReadingsValidator().Validate(request);
        if (!result.IsValid)
            throw new OrganizationException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);

        var history = await _readings.GetHistory(sensorId, request.StartDate?.UtcDateTime, request.EndDate?.UtcDateTime, request.Page, request.PageSize, cancellationToken);

        return new PagedResponse<SensorReadingResponse>
        {
            Items = history.Items.Select(GetSensorReadingsUseCase.ToResponse).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = history.TotalItems
        };
    }

    public async Task<SensorReadingResponse?> GetLatest(Guid actorId, Guid organizationId, Guid sensorId, CancellationToken cancellationToken)
    {
        await RequireSensor(actorId, organizationId, sensorId, cancellationToken);

        var reading = await _readings.GetLatest(sensorId, cancellationToken);

        return reading is null ? null : GetSensorReadingsUseCase.ToResponse(reading);
    }

    private async Task RequireSensor(Guid actorId, Guid organizationId, Guid sensorId, CancellationToken cancellationToken)
    {
        await _access.Require(organizationId, actorId, cancellationToken);

        var sensor = await _sensors.FindById(sensorId, cancellationToken);
        if (sensor is null || sensor.OrganizationId != organizationId)
            throw new OrganizationException(ErrorKind.NotFound, "Organization sensor is unavailable.");
    }
}
