using UrbanShade.Communication.Requests.Routes;
using UrbanShade.Communication.Responses.Routes;

namespace UrbanShade.Application.UseCases.Routes.Compare;

public interface ICompareRoutesUseCase
{
    Task<RouteComparisonResponse> Execute(CompareRoutesRequest request, CancellationToken cancellationToken = default);
}
