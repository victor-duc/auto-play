using System.Text.Json.Nodes;
using AutoPlay.Adapters.Persistence.Tests.Fakes;
using AutoPlay.Application.Ports;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Adapters.Persistence.Tests;

/// <summary>
/// Checks that the files written by the store match the published JSON schemas, carry the format version,
/// and that files of older or newer versions are handled.
/// </summary>
public sealed class FileFormatTests : IDisposable
{
    private const int CurrentVersion = 2;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "AutoPlayTests", Guid.NewGuid().ToString());
    private readonly FileProfileStore _store;
    private readonly Profile _profile = new() { Name = "Hero Wars" };

    public FileFormatTests()
    {
        _store = new FileProfileStore(_root, new FakeImageCodec());
        _store.SaveProfile(_profile);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string ProfileDirectory => Path.Combine(_root, _profile.Id.ToString());

    private string ProfileJson => File.ReadAllText(Path.Combine(ProfileDirectory, "profile.json"));

    // ---- Files written by the store are valid against the schemas

    [Fact]
    public void Profile_without_target_region_matches_the_schema()
    {
        Assert.Empty(JsonSchemas.Validate(JsonSchemas.Profile, ProfileJson));
    }

    [Fact]
    public void Profile_with_target_region_matches_the_schema()
    {
        _profile.LastTargetRegion = new PixelRect(-1920, 95, 1280, 720);
        _store.SaveProfile(_profile);

        Assert.Empty(JsonSchemas.Validate(JsonSchemas.Profile, ProfileJson));
    }

    [Fact]
    public void Screen_matches_the_schema()
    {
        var withDefaultThreshold = new Location
        {
            Name = "Raid",
            Bounds = new NormalizedRect(0.7, 0.8, 0.1, 0.05),
            ClickPoint = new NormalizedPoint(0.75, 0.825),
        };
        var withThreshold = new Location
        {
            Name = "Close",
            Positioning = PositioningMode.Anchored,
            Bounds = new NormalizedRect(0, 0, 1, 1),
            ClickPoint = new NormalizedPoint(0.5, 0.5),
            MatchThreshold = 0.9,
            MatchColors = true,
        };
        var screen = new Screen
        {
            Name = "Campaign map",
            RecordedRegionSize = new PixelSize(1280, 720),
            Locations = [withDefaultThreshold, withThreshold],
        };
        var image = TestImages.Solid(2, 2);
        _store.SaveScreen(_profile.Id, screen, image, new Dictionary<Guid, RawImage> { [withDefaultThreshold.Id] = image, [withThreshold.Id] = image });

        var json = File.ReadAllText(Path.Combine(ProfileDirectory, "screens", screen.Id.ToString(), "screen.json"));

        Assert.Empty(JsonSchemas.Validate(JsonSchemas.Screen, json));
    }

    [Fact]
    public void Sequence_matches_the_schema()
    {
        var sequence = new Sequence
        {
            Name = "Daily raids",
            RepeatCount = 0,
            Steps =
            [
                new SequenceStep { ScreenId = Guid.NewGuid(), LocationId = Guid.NewGuid() },
                new SequenceStep
                {
                    ScreenId = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    Delay = new DelayRange(200, 400),
                    VerificationTimeoutMs = 5000,
                    Verify = false,
                },
                SequenceStep.Wait(new DelayRange(1000, 2000)),
                new SequenceStep { Kind = StepKind.RunSequence, SequenceId = Guid.NewGuid() },
            ],
            Schedule = new SequenceSchedule { Time = new TimeOnly(8, 30), Days = [DayOfWeek.Monday, DayOfWeek.Friday] },
        };
        _store.SaveSequence(_profile.Id, sequence);

        var json = File.ReadAllText(Path.Combine(ProfileDirectory, "sequences", $"{sequence.Id}.json"));

        Assert.Empty(JsonSchemas.Validate(JsonSchemas.Sequence, json));
    }

    [Fact]
    public void Settings_match_the_schema_and_round_trip()
    {
        var store = new JsonSettingsStore(Path.Combine(_root, "settings.json"));
        Assert.Equal(AutoPlay.Application.Settings.ThemePreference.System, store.Load().Theme);

        store.Save(new AutoPlay.Application.Settings.AppSettings
        {
            Theme = AutoPlay.Application.Settings.ThemePreference.Dark,
            StopWithEscape = false,
            QuickCaptureHotkey = "Ctrl+Alt+F10",
            DataFolder = @"D:\AutoPlay",
        });

        Assert.Empty(JsonSchemas.Validate(JsonSchemas.Settings, File.ReadAllText(Path.Combine(_root, "settings.json"))));
        var loaded = store.Load();
        Assert.Equal(AutoPlay.Application.Settings.ThemePreference.Dark, loaded.Theme);
        Assert.False(loaded.StopWithEscape);
        Assert.Equal("Ctrl+Alt+F10", loaded.QuickCaptureHotkey);
        Assert.Equal(@"D:\AutoPlay", loaded.DataFolder);
    }

    [Fact]
    public void Corrupted_settings_are_a_persistence_error()
    {
        var path = Path.Combine(_root, "settings.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, "{ broken");

        Assert.Throws<PersistenceException>(() => new JsonSettingsStore(path).Load());
    }

    // ---- Content of the files

    [Fact]
    public void Files_start_with_the_current_schema_version()
    {
        var firstProperty = JsonNode.Parse(ProfileJson)!.AsObject().First();

        Assert.Equal("schemaVersion", firstProperty.Key);
        Assert.Equal(CurrentVersion, firstProperty.Value!.GetValue<int>());
    }

    [Fact]
    public void Computed_properties_are_not_written()
    {
        _profile.LastTargetRegion = new PixelRect(1, 2, 3, 4);
        _store.SaveProfile(_profile);

        var region = JsonNode.Parse(ProfileJson)!["lastTargetRegion"]!.AsObject();

        Assert.Equal(["left", "top", "width", "height"], region.Select(p => p.Key));
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("screen")]
    [InlineData("sequence")]
    [InlineData("settings")]
    public void Schemas_target_the_current_version(string name)
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schemas", $"{name}.schema.json")))!;

        Assert.Equal(CurrentVersion, schema["properties"]!["schemaVersion"]!["const"]!.GetValue<int>());
    }

    // ---- The schemas are strict

    [Fact]
    public void Schema_rejects_unknown_properties()
    {
        var json = JsonNode.Parse(ProfileJson)!;
        json["lastTargetRegion"] = new JsonObject { ["left"] = 0, ["top"] = 0, ["width"] = 10, ["height"] = 10, ["right"] = 10 };

        Assert.NotEmpty(JsonSchemas.Validate(JsonSchemas.Profile, json.ToJsonString()));
    }

    [Fact]
    public void Schema_rejects_missing_or_invalid_values()
    {
        var missingName = JsonNode.Parse(ProfileJson)!.AsObject();
        missingName.Remove("name");
        var invalidThreshold = JsonNode.Parse(ProfileJson)!;
        invalidThreshold["defaults"]!["matchThreshold"] = 1.5;
        var otherVersion = JsonNode.Parse(ProfileJson)!;
        otherVersion["schemaVersion"] = CurrentVersion + 1;

        Assert.NotEmpty(JsonSchemas.Validate(JsonSchemas.Profile, missingName.ToJsonString()));
        Assert.NotEmpty(JsonSchemas.Validate(JsonSchemas.Profile, invalidThreshold.ToJsonString()));
        Assert.NotEmpty(JsonSchemas.Validate(JsonSchemas.Profile, otherVersion.ToJsonString()));
    }

    [Fact]
    public void Schema_requires_a_duration_for_a_wait()
    {
        var sequence = new Sequence { Name = "S", Steps = [SequenceStep.Wait(new DelayRange(1, 2))] };
        _store.SaveSequence(_profile.Id, sequence);
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(ProfileDirectory, "sequences", $"{sequence.Id}.json")))!;
        json["steps"]![0]!["delay"] = null;

        Assert.NotEmpty(JsonSchemas.Validate(JsonSchemas.Sequence, json.ToJsonString()));
    }

    [Fact]
    public void Schedule_round_trips()
    {
        var sequence = new Sequence
        {
            Name = "S",
            Schedule = new SequenceSchedule { Enabled = false, Time = new TimeOnly(23, 59, 30), Days = [DayOfWeek.Sunday] },
        };
        _store.SaveSequence(_profile.Id, sequence);

        var schedule = Assert.Single(_store.LoadSequences(_profile.Id)).Schedule!;

        Assert.False(schedule.Enabled);
        Assert.Equal(new TimeOnly(23, 59, 30), schedule.Time);
        Assert.Equal([DayOfWeek.Sunday], schedule.Days);
    }

    // ---- Older and newer versions

    [Fact]
    public void Files_written_before_versioning_are_still_read()
    {
        // Format 0: no schemaVersion, computed properties included (as written by AutoPlay before versioning).
        File.WriteAllText(Path.Combine(ProfileDirectory, "profile.json"), $$"""
            {
              "id": "{{_profile.Id}}",
              "name": "Legacy",
              "lastTargetRegion": {
                "left": 120, "top": 95, "width": 1280, "height": 720,
                "right": 1400, "bottom": 815,
                "size": { "width": 1280, "height": 720, "isEmpty": false },
                "topLeft": { "x": 120, "y": 95 },
                "center": { "x": 760, "y": 455 },
                "isEmpty": false
              },
              "defaults": { "verificationTimeoutMs": 10000, "retryIntervalMs": 250, "matchThreshold": 0.7, "searchMargin": 0.5, "userTakeoverTolerancePx": 10 }
            }
            """);

        var profile = Assert.Single(_store.LoadProfiles());

        Assert.Equal("Legacy", profile.Name);
        Assert.Equal(new PixelRect(120, 95, 1280, 720), profile.LastTargetRegion);
        Assert.Equal(0.7, profile.Defaults.MatchThreshold);
    }

    [Fact]
    public void Saving_a_legacy_file_upgrades_it_to_the_current_version()
    {
        File.WriteAllText(Path.Combine(ProfileDirectory, "profile.json"), $$"""{ "id": "{{_profile.Id}}", "name": "Legacy" }""");

        _store.SaveProfile(Assert.Single(_store.LoadProfiles()));

        Assert.Empty(JsonSchemas.Validate(JsonSchemas.Profile, ProfileJson));
    }

    [Fact]
    public void Files_written_by_version_1_are_read_with_defaults()
    {
        var screenId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var screenDirectory = Path.Combine(ProfileDirectory, "screens", screenId.ToString());
        Directory.CreateDirectory(screenDirectory);
        File.WriteAllText(Path.Combine(screenDirectory, "screen.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{screenId}}",
              "name": "Map",
              "recordedRegionSize": { "width": 100, "height": 50 },
              "defaultDelay": { "minMs": 1, "maxMs": 2 },
              "locations": [
                {
                  "id": "{{locationId}}", "name": "A", "positioning": "Proportional",
                  "bounds": { "x": 0, "y": 0, "width": 0.5, "height": 0.5 },
                  "clickPoint": { "x": 0.25, "y": 0.25 }, "matchThreshold": null
                }
              ]
            }
            """);

        var location = Assert.Single(Assert.Single(_store.LoadScreens(_profile.Id)).Locations);

        Assert.Equal("A", location.Name);
        Assert.False(location.MatchColors);
    }

    [Fact]
    public void Files_from_a_newer_version_are_refused()
    {
        File.WriteAllText(Path.Combine(ProfileDirectory, "profile.json"), $$"""{ "schemaVersion": 99, "id": "{{_profile.Id}}", "name": "Future" }""");

        var error = Assert.Throws<PersistenceException>(() => _store.LoadProfiles());

        Assert.Contains("newer version of AutoPlay", error.Message);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public void Invalid_schema_versions_are_refused(string version)
    {
        File.WriteAllText(Path.Combine(ProfileDirectory, "profile.json"), $$"""{ "schemaVersion": {{version}}, "id": "{{_profile.Id}}", "name": "Broken" }""");

        Assert.Throws<PersistenceException>(() => _store.LoadProfiles());
    }

    [Fact]
    public void Run_history_matches_the_schema_and_keeps_the_last_run_of_each_sequence()
    {
        var sequenceId = Guid.NewGuid();
        var endedAt = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.FromHours(2));
        _store.SaveLastRun(_profile.Id, new AutoPlay.Application.Execution.RunSummary
        {
            SequenceId = sequenceId,
            EndedAt = endedAt.AddHours(-1),
            Status = AutoPlay.Application.Execution.RunStatus.Completed,
        });
        _store.SaveLastRun(_profile.Id, new AutoPlay.Application.Execution.RunSummary
        {
            SequenceId = sequenceId,
            EndedAt = endedAt,
            Status = AutoPlay.Application.Execution.RunStatus.VerificationFailed,
            Message = "'Map / Attack' was not found.",
        });

        Assert.Empty(JsonSchemas.Validate(JsonSchemas.RunHistory, File.ReadAllText(Path.Combine(ProfileDirectory, "runs.json"))));
        var run = Assert.Single(_store.LoadLastRuns(_profile.Id));
        Assert.Equal(endedAt, run.EndedAt);
        Assert.Equal(AutoPlay.Application.Execution.RunStatus.VerificationFailed, run.Status);
        Assert.Equal("'Map / Attack' was not found.", run.Message);
    }

    [Fact]
    public void Missing_run_history_is_empty()
    {
        Assert.Empty(_store.LoadLastRuns(Guid.NewGuid()));
    }
}
