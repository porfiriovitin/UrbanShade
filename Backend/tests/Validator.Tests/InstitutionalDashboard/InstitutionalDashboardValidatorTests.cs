using UrbanShade.Application.UseCases.InstitutionalDashboard;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Domain.InstitutionalDashboard;

namespace Validator.Tests.InstitutionalDashboard;

public sealed class InstitutionalDashboardValidatorTests
{
    [Fact]
    public void Defaults_AcceptExplicitOrganization()
    {
        Assert.True(new InstitutionalDashboardValidator(new()).Validate(new InstitutionalDashboardRequest() { OrganizationId = Guid.NewGuid() }).IsValid);
    }

    [Fact]
    public void History_RejectsLocalOrUnspecifiedDatesAndExcessiveRange()
    {
        var validator = new InstitutionalDashboardValidator(new());
        Assert.False(validator.Validate(new InstitutionalDashboardRequest() { OrganizationId = Guid.NewGuid(), StartDate = DateTime.Now.AddDays(-1), EndDate = DateTime.Now }).IsValid);
        Assert.False(validator.Validate(new InstitutionalDashboardRequest() { OrganizationId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-366), EndDate = DateTime.UtcNow }).IsValid);
        Assert.False(validator.Validate(new InstitutionalDashboardRequest() { OrganizationId = Guid.NewGuid(), StartDate = DateTime.UtcNow }).IsValid);
    }

    [Fact]
    public void Filters_RejectDuplicateIdsAndNonFiniteBounds()
    {
        var id = Guid.NewGuid();
        var validator = new InstitutionalDashboardValidator(new());
        Assert.False(validator.Validate(new InstitutionalDashboardRequest() { OrganizationId = id, RegionIds = [id, id] }).IsValid);
        Assert.False(validator.Validate(new InstitutionalDashboardRequest() { OrganizationId = id, Bbox = "0,0,Infinity,1" }).IsValid);
    }

    [Fact]
    public void Settings_RejectInvalidLimitsAndCacheDurations()
    {
        Assert.Throws<InvalidOperationException>(() => new InstitutionalDashboardSettings { MaxExportRows = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new InstitutionalDashboardSettings { OverviewCacheMinutes = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new InstitutionalDashboardSettings { MaxPageSize = 10, DefaultPageSize = 20 }.Validate());
    }
}
