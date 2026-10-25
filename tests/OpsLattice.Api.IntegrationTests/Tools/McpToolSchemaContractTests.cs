using System.ComponentModel;
using Xunit;

namespace OpsLattice.Api.IntegrationTests.Tools;

public sealed class McpToolSchemaContractTests
{
    [Fact]
    public void GetFlightCountContract_ShouldExposeStatusParameter()
    {
        var method = typeof(global::FlightTools).GetMethod(nameof(global::FlightTools.GetFlightCount));
        Assert.NotNull(method);

        var parameterNames = method!.GetParameters()
            .Select(p => p.Name!)
            .ToArray();

        Assert.Contains(parameterNames, name =>
            string.Equals(name, "status", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(parameterNames, name =>
            string.Equals(name, "origin", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parameterNames, name =>
            string.Equals(name, "destination", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parameterNames, name =>
            string.Equals(name, "flightType", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parameterNames, name =>
            string.Equals(name, "date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetFlightCountDescription_ShouldDistinguishStatusFromScheduledDate()
    {
        var description = GetDescription(nameof(global::FlightTools.GetFlightCount));

        Assert.Contains("status", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scheduled date does not imply status=Scheduled", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("does not support status filtering", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchFlightsContract_ShouldExposeAllFilters()
    {
        var method = typeof(global::FlightTools).GetMethod(nameof(global::FlightTools.SearchFlights));
        Assert.NotNull(method);

        var parameterNames = method!.GetParameters()
            .Select(p => p.Name!)
            .ToArray();

        string[] expected = ["origin", "destination", "flightType", "date", "status"];

        Assert.Equal(expected, parameterNames);
    }

    private static string GetDescription(string methodName)
    {
        var method = typeof(global::FlightTools).GetMethod(methodName);
        Assert.NotNull(method);

        return method!
            .GetCustomAttributes(typeof(DescriptionAttribute), inherit: false)
            .OfType<DescriptionAttribute>()
            .Select(a => a.Description)
            .Single();
    }
}
