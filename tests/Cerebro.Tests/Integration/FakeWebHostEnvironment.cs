using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Cerebro.Tests.Integration;

// Environnement minimal pour tester en isolation les services qui n'ont besoin que de
// ContentRootPath (ScreenshotStore, FileSessionActivityStore), sans monter tout un
// WebApplicationFactory.
internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "Cerebro.Tests";
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
