using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using static Serilog.Log;

[DotNetVerbosityMapping]
[ShutdownDotNetAfterServerBuild]
partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.All);

    protected override void OnBuildInitialized()
    {
        Information("🚀 Build process started...");

        base.OnBuildInitialized();
    }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter]
    [Secret]
    readonly string SonarQubeToken = string.Empty;

    [Parameter]
    readonly string SonarQubeServer = "http://192.168.144.120:9000";

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    [GitVersion(Framework = "net8.0", NoCache = true, NoFetch = true)]
    readonly GitVersion GitVersion;

    Target All => _ => _
        .DependsOn(
            BackendAll,
            FrontendAll
        );

    Target BackendAll => _ => _
        .DependsOn(
            SonarScannerBegin,
            BackendRestore,
            BackendBuild,
            BackendTests,
            BackendTestsCodeCoverage,
            SonarScannerEnd
        );

    Target FrontendAll => _ => _
        .DependsOn(
            FrontendRestore,
            FrontendBuild
        );
}
