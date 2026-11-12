using FluentValidation;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoRegions.UpdateReference;

public interface IUpdateReferenceRegionUseCase
{
    Task Execute(Guid id, UpdateReferenceRegionRequest request, CancellationToken cancellationToken = default);
}

public sealed class UpdateReferenceRegionValidator : AbstractValidator<UpdateReferenceRegionRequest>
{
    public UpdateReferenceRegionValidator()
    {
        RuleFor(request => request.ReferenceRegionId).NotEqual(Guid.Empty).When(request => request.ReferenceRegionId.HasValue);
    }
}

public sealed class UpdateReferenceRegionUseCase : IUpdateReferenceRegionUseCase
{
    private readonly IGeoRegionRepository _regions;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateReferenceRegionUseCase(IGeoRegionRepository regions, IUnitOfWork unitOfWork)
    {
        _regions = regions;
        _unitOfWork = unitOfWork;
    }

    public async Task Execute(Guid id, UpdateReferenceRegionRequest request, CancellationToken cancellationToken = default)
    {
        Validate(id, request);

        var region = await _regions.FindById(id, cancellationToken) ?? throw new GeoException(ErrorKind.NotFound, "Active region not found.");
        if (request.ReferenceRegionId.HasValue && await _regions.FindById(request.ReferenceRegionId.Value, cancellationToken) is null)
            throw new GeoException(ErrorKind.NotFound, "Active reference region not found.");

        region.UpdateReference(request.ReferenceRegionId);
        await _unitOfWork.SaveChanges(cancellationToken);
    }

    private static void Validate(Guid id, UpdateReferenceRegionRequest request)
    {
        var result = new UpdateReferenceRegionValidator().Validate(request);
        if (!result.IsValid)
            throw new GeoException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
        if (id == Guid.Empty || request.ReferenceRegionId == id)
            throw new GeoException(ErrorKind.Validation, "A different reference region is required.");
    }
}
