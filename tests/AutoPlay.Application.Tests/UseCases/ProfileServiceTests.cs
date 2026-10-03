using AutoPlay.Application.Ports;
using AutoPlay.Application.Tests.Fakes;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Model;

namespace AutoPlay.Application.Tests.UseCases;

public class ProfileServiceTests
{
    private readonly InMemoryRepository _repository = new();
    private readonly ProfileService _service;

    public ProfileServiceTests() => _service = new ProfileService(_repository);

    [Fact]
    public void CreateProfile_saves_a_profile_with_a_trimmed_name()
    {
        var result = _service.CreateProfile("  Hero Wars ");

        Assert.True(result.Succeeded);
        Assert.Equal("Hero Wars", result.Value!.Name);
        Assert.Equal([result.Value], _repository.Profiles);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateProfile_requires_a_name(string name)
    {
        var result = _service.CreateProfile(name);

        Assert.Equal(["The profile name is required."], result.Errors);
        Assert.Empty(_repository.Profiles);
    }

    [Fact]
    public void CreateProfile_rejects_a_duplicate_name_ignoring_case()
    {
        _repository.Profiles.Add(new Profile { Name = "Hero Wars" });

        var result = _service.CreateProfile("hero wars");

        Assert.Equal(["Another profile is already named 'hero wars'."], result.Errors);
        Assert.Single(_repository.Profiles);
    }

    [Fact]
    public void CreateProfile_reports_storage_errors()
    {
        _repository.Failure = new PersistenceException("Disk full");

        Assert.Equal(["Disk full"], _service.CreateProfile("Game").Errors);
    }

    [Fact]
    public void RememberTargetRegion_saves_the_region_on_the_profile()
    {
        var profile = new Profile { Name = "Game" };
        var region = new PixelRect(10, 20, 800, 600);

        _service.RememberTargetRegion(profile, region);

        Assert.Equal(region, Assert.Single(_repository.Profiles).LastTargetRegion);
    }

    [Fact]
    public void DeleteProfile_removes_it()
    {
        var profile = _service.CreateProfile("Game").Value!;

        _service.DeleteProfile(profile.Id);

        Assert.Empty(_service.GetProfiles());
    }
}
