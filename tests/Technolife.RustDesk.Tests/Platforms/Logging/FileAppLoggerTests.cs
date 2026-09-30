using Technolife.RustDesk.Platforms.Logging;

namespace Technolife.RustDesk.Tests.Platforms.Logging;

public sealed class FileAppLoggerTests
{
    private const string SensitiveConfiguration = "sensitive-exported-configuration";

    [Fact]
    public void CreatesLogAndRecordsStructuredLevels()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var logger = new FileAppLogger(directory);

            logger.Info("Detection started.");
            logger.Warning("Validation is limited.");
            logger.Error("Configuration failed.", "ProcessFailed.");

            var content = File.ReadAllText(logger.Destination);

            Assert.True(File.Exists(logger.Destination));
            Assert.Contains("[INFO] Detection started.", content);
            Assert.Contains("[WARN] Validation is limited.", content);
            Assert.Contains("[ERROR] Configuration failed.", content);
            Assert.Contains("Technical details: ProcessFailed.", content);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RedactsSensitiveConfigurationFromMessagesAndTechnicalDetails()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var logger = new FileAppLogger(directory, [SensitiveConfiguration]);

            logger.Info($"Applying {SensitiveConfiguration}.");
            logger.Error(
                "Configuration failed.",
                $"Unexpected value {SensitiveConfiguration}.");

            var content = File.ReadAllText(logger.Destination);

            Assert.DoesNotContain(SensitiveConfiguration, content);
            Assert.Contains("[REDACTED]", content);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"technolife-rustdesk-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
