using UrbanShade.Application.UseCases.Cities.GetById;
using UrbanShade.Communication.Requests.Cities;
using UrbanShade.Communication.Responses.Cities;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Cities.Register;

public sealed class RegisterCityUseCase : IRegisterCityUseCase
{
    private readonly ICityRepository _cities;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCityUseCase(ICityRepository cities, IUnitOfWork unitOfWork)
    {
        _cities = cities;
        _unitOfWork = unitOfWork;
    }

    public async Task<CityResponse> Execute(RegisterCityRequest request, CancellationToken cancellationToken = default)
    {
        var result = new RegisterCityValidator().Validate(request);
        if (!result.IsValid)
            throw new CityException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
        if (await _cities.IbgeCodeExists(request.IbgeCode.Trim(), cancellationToken))
            throw new CityException(ErrorKind.Conflict, "City IBGE code already registered.");

        var city = new City(request.Name, request.State, request.IbgeCode);
        _cities.Add(city);
        await _unitOfWork.SaveChanges(cancellationToken);
        return GetCityByIdUseCase.ToResponse(city);
    }
}
