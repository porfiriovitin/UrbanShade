using FluentValidation;
using UrbanShade.Communication.Requests.Routes;
using UrbanShade.Domain.Routing;

namespace UrbanShade.Application.UseCases.Routes.Compare;

public sealed class CompareRoutesValidator : AbstractValidator<CompareRoutesRequest>
{
    public CompareRoutesValidator(RoutingSettings settings)
    {
        RuleFor(request => request.Origin).NotNull().SetValidator(new RoutePointValidator());
        RuleFor(request => request.Destination).NotNull().SetValidator(new RoutePointValidator());
        RuleFor(request => request.TravelMode).Equal("WALKING");
        RuleFor(request => request).Must(request => request.Origin is null || request.Destination is null ||
            request.Origin.Latitude != request.Destination.Latitude || request.Origin.Longitude != request.Destination.Longitude).WithMessage("Origin and destination must be different.");
        RuleFor(request => request).Must(request => request.Origin is null || request.Destination is null ||
            Distance(request.Origin.Latitude, request.Origin.Longitude, request.Destination.Latitude, request.Destination.Longitude) <= settings.MaximumWalkingDistanceMeters)
            .WithMessage("Origin and destination exceed the maximum walking distance.");
    }

    public static double Distance(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var latitude = (latitude2 - latitude1) * Math.PI / 180;
        var longitude = (longitude2 - longitude1) * Math.PI / 180;
        var a = Math.Pow(Math.Sin(latitude / 2), 2) + Math.Cos(latitude1 * Math.PI / 180) * Math.Cos(latitude2 * Math.PI / 180) * Math.Pow(Math.Sin(longitude / 2), 2);
        return 6371008.8 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    private sealed class RoutePointValidator : AbstractValidator<RoutePointRequest>
    {
        public RoutePointValidator()
        {
            RuleFor(point => point.Latitude).Must(value => double.IsFinite(value) && value is >= -90 and <= 90);
            RuleFor(point => point.Longitude).Must(value => double.IsFinite(value) && value is >= -180 and <= 180);
        }
    }
}
