using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpsLattice.Scenarios.Airport.Simulation;
using OpsLattice.Simulator.Simulation;
using Xunit;

namespace OpsLattice.Api.IntegrationTests;

public sealed class ApplicationStartupTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApplicationStartupTests(
        WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Application_ShouldStartSuccessfully()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/simulation");

        Assert.True(
            response.IsSuccessStatusCode,
            $"Unexpected status code: {response.StatusCode}");
    }

    [Fact]
    public void DependencyInjection_ShouldResolveSimulationRunner()
    {
        using var scope = _factory.Services.CreateScope();

        var runner = scope.ServiceProvider
            .GetRequiredService<SimulationRunner>();

        Assert.NotNull(runner);
    }

    [Fact]
    public void DependencyInjection_ShouldResolveAirportSimulationManager()
    {
        using var scope = _factory.Services.CreateScope();

        var manager = scope.ServiceProvider
            .GetRequiredService<AirportSimulationManager>();

        Assert.NotNull(manager);
    }

    [Fact]
    public async Task GetFlights_ShouldReturnSuccess()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/flights");

        Assert.Equal(
            System.Net.HttpStatusCode.OK,
            response.StatusCode);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task AdvanceSimulation_WithInvalidMinutes_ShouldReturnBadRequest(
        int minutes)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/simulation/advance",
            new { Minutes = minutes });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

}

