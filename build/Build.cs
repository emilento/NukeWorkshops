using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AppVeyor;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.Npm;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using static Serilog.Log;

[DotNetVerbosityMapping]
[ShutdownDotNetAfterServerBuild]
partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.All);

    [CI]
    readonly AzurePipelines AzurePipelines;

    [CI]
    readonly GitHubActions GitHubActions;

    protected override void OnBuildInitialized()
    {
        Information("🚀 Build process started");

        base.OnBuildInitialized();
    }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    [GitVersion(Framework = "net8.0", NoCache = true, NoFetch = true)]
    readonly GitVersion GitVersion;

    AbsolutePath FrontendSourceDirectory => RootDirectory / "nukeworkshops.client";

    AbsolutePath BackendSourceDirectory => RootDirectory / "NukeWorkshops.Server";

    AbsolutePath BackendTestsDirectory => RootDirectory / "NukeWorkshops.Server.Tests";

    AbsolutePath BackendTestResultsDirectory => BackendTestsDirectory / "TestResults";

    Project[] BackendTestProjects =>
    [
        Solution.NukeWorkshops_Server_Tests
    ];

    Target All => _ => _
        .DependsOn(
            BackendAll,
            FrontendAll
        );

    Target BackendAll => _ => _
        .DependsOn(
            BackendClean,
            BackendBuild,
            BackendTests,
            BackendTestsCodeCoverage
        );

    Target FrontendAll => _ => _
        .DependsOn(
            FrontendClean,
            FrontendBuild
        );

    Target FrontendClean => _ => _
        .Before(FrontendRestore)
        .Executes(() =>
        {
            FrontendSourceDirectory
                .GlobDirectories("**/node_modules")
                .ForEach(d => d.DeleteDirectory());

            Information("Frontend clean completed");
        });

    Target FrontendRestore => _ => _
        .Executes(() =>
        {
            if (!IsLocalBuild)
            {
                NpmTasks.Npm($"version {GitVersion.SemVer}", FrontendSourceDirectory);
            }

            NpmTasks.NpmInstall(settings => settings.SetProcessWorkingDirectory(FrontendSourceDirectory));
        });

    Target FrontendBuild => _ => _
        .DependsOn(FrontendRestore)
        .Executes(() =>
        {
            NpmTasks.NpmRun(settings =>
                settings
                    .SetCommand("build")
                    .SetProcessWorkingDirectory(FrontendSourceDirectory)
            );
        });

    Target BackendClean => _ => _
        .Before(BackendRestore)
        .Executes(() =>
        {
            BackendSourceDirectory
                .GlobDirectories("**/bin", "**/obj")
                .ForEach(d => d.DeleteDirectory());

            BackendTestsDirectory
                .GlobDirectories("**/bin", "**/obj")
                .ForEach(d => d.DeleteDirectory());

            BackendTestResultsDirectory.CreateOrCleanDirectory();

            Information("Backend clean completed");
        });

    Target BackendRestore => _ => _
        .Executes(() =>
            DotNetRestore(s => s
                .SetProjectFile(Solution)
                .EnableNoCache()));

    Target BackendBuild => _ => _
        .DependsOn(BackendRestore)
        .Executes(() =>
        {
            ReportSummary(s => s
                .AddPairWhenValueNotNull("Version", GitVersion.SemVer));

            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoLogo()
                .EnableNoRestore()
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion));
        });

    Target BackendTests => _ => _
        .DependsOn(BackendBuild)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetConfiguration(Configuration)
                .SetProcessEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
                .EnableNoBuild()
                .SetDataCollector("XPlat Code Coverage")
                .SetResultsDirectory(BackendTestResultsDirectory)
                .AddRunSetting(
                    "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.DoesNotReturnAttribute",
                    "DoesNotReturnAttribute")
                .CombineWith(
                    BackendTestProjects,
                    (settings, project) => settings
                        .SetProjectFile(project)
                        .AddLoggers($"trx;LogFileName={project.Name}.trx")
                ),
                completeOnFailure: true);

            ReportSummaryTestOutcome(globFilters: "*.trx");
        });

    static string[] UnitTestResultOutcomes(AbsolutePath path) =>
        XmlTasks.XmlPeek(
            path,
            "/xn:TestRun/xn:Results/xn:UnitTestResult/@outcome",
            ("xn", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"))
        .ToArray();

    void ReportSummaryTestOutcome(params string[] globFilters)
    {
        var resultFiles = BackendTestResultsDirectory.GlobFiles(globFilters);
        var outcomes = resultFiles.SelectMany(UnitTestResultOutcomes).ToList();
        var passedTests = outcomes.Count(outcome => outcome is "Passed");
        var failedTests = outcomes.Count(outcome => outcome is "Failed");
        var skippedTests = outcomes.Count(outcome => outcome is "NotExecuted");

        ReportSummary(_ => _
            .When(failedTests > 0, c => c
                .AddPair("Failed", failedTests.ToString()))
            .AddPair("Passed", passedTests.ToString())
            .When(skippedTests > 0, c => c
                .AddPair("Skipped", skippedTests.ToString())));
    }

    Target BackendTestsCodeCoverage => _ => _
        .DependsOn(BackendTests)
        .Executes(() =>
        {
            ReportGenerator(s => s
                .SetProcessToolPath(
                    NuGetToolPathResolver.GetPackageExecutable(
                        "ReportGenerator",
                        "ReportGenerator.dll",
                        framework: "net8.0"))
                .SetTargetDirectory(BackendTestResultsDirectory / "reports")
                .AddReports(BackendTestResultsDirectory / "**/coverage.cobertura.xml")
                .AddReportTypes(
                    ReportTypes.JsonSummary,
                    ReportTypes.HtmlInline_AzurePipelines_Dark)
                .SetFileFilters("-*Program.cs")
            );

            string link = BackendTestResultsDirectory / "reports" / "index.html";
            Information("Code coverage report: {Link}", $"\x1b]8;;file://{link.Replace('\\', '/')}\x1b\\{link}\x1b]8;;\x1b\\");

            string jsonSummary = BackendTestResultsDirectory / "reports" / "Summary.json";
            var jsonText = File.ReadAllText(jsonSummary);
            var jsonNode = JsonSerializer.Deserialize<JsonNode>(jsonText);
            var lineCoverage = jsonNode["summary"]["linecoverage"].GetValue<double>() / 100f;

            ReportSummary(s => s
                .AddPairWhenValueNotNull("Line coverage", lineCoverage.ToString("P")));
        });
}
