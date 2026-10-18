using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OpsLattice.Api.IntegrationTests;

public sealed class SimulationEndpointTests
{
    [Fact]
    public async Task GetFlights_AfterSimulationAdvance_ShouldReflectActiveRuntime()
    {
        // Arrange: a fresh application and simulation for this test.
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var before = await client.GetFromJsonAsync<List<FlightTestResponse>>(
            "/api/flights");

        Assert.NotNull(before);
        Assert.NotEmpty(before);

        // Act: advance simulation by 12 hours.
        var response = await client.PostAsJsonAsync(
            "/api/simulation/advance",
            new { Minutes = 720 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await client.GetFromJsonAsync<List<FlightTestResponse>>(
            "/api/flights");

        // Assert
        Assert.NotNull(after);
        Assert.Equal(before.Count, after.Count);

        Assert.True(
            before.Zip(after).Any(pair =>
                pair.First.Status != pair.Second.Status),
            "Expected at least one flight status to change after advancing the simulation.");
    }

    [Fact]
    public async Task GetGates_ShouldReturnActiveSimulationGates()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // Act
        var gates = await client.GetFromJsonAsync<List<GateTestResponse>>(
            "/api/gates");

        // Assert
        Assert.NotNull(gates);
        Assert.Equal(20, gates.Count);

        Assert.All(gates, gate =>
        {
            Assert.False(string.IsNullOrWhiteSpace(gate.Code));
            Assert.Equal("Available", gate.Status);
            Assert.Null(gate.AssignedFlightNumber);
            Assert.Null(gate.OccupyingFlightNumber);
        });
    }

    private sealed record GateTestResponse(
        string Code,
        string Status,
        string? AssignedFlightNumber,
        string? OccupyingFlightNumber);

    private sealed record FlightTestResponse(
        string FlightNumber,
        string Status);
}
