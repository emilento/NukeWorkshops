using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Tools.SonarScanner;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using static Serilog.Log;

partial class Build
{
    private const string Framework = "net8.0";

    [CI]
    readonly AzurePipelines AzurePipelines;

    AbsolutePath BackendSourceDirectory => RootDirectory / "NukeWorkshops.Server";

    AbsolutePath BackendTestsDirectory => RootDirectory / "NukeWorkshops.Server.Tests";

    AbsolutePath BackendTestResultsDirectory => BackendTestsDirectory / "TestResults";

    AbsolutePath BackendTestResultsArtifact => BackendTestResultsDirectory / "BackendTestResults.zip";

    Project[] BackendTestProjects =>
    [
        Solution.NukeWorkshops_Server_Tests
    ];

    Target BackendClean => _ => _
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

    Target SonarScannerBegin => _ => _
        .OnlyWhenStatic(() => !string.IsNullOrWhiteSpace(SonarQubeToken))
        .Executes(() =>
        {
            SonarScannerTasks.SonarScannerBegin(s => s
                .SetFramework(Framework)
                .SetServer(SonarQubeServer)
                .SetLogin(SonarQubeToken)
                .SetProjectKey(nameof(Solution.NukeWorkshops_Server))
                .SetName(nameof(Solution.NukeWorkshops_Server))
                .SetVersion(GitVersion.FullSemVer)
                .SetSourceExclusions("**/obj/**,**/*.dll,**/*.exe")
                .SetDuplicationExclusions("**/Program.cs")
                .SetCoverageExclusions("**/Program.cs")
                .SetOpenCoverPaths("**/coverage.opencover.xml")
                .SetVSTestReports("**/*.trx")
                .SetSourceEncoding("UTF-8")
            );
        });

    Target SonarScannerEnd => _ => _
        .DependsOn(BackendTestsCodeCoverage)
        .OnlyWhenStatic(() => !string.IsNullOrWhiteSpace(SonarQubeToken))
        .Executes(() =>
        {
            SonarScannerTasks.SonarScannerEnd(s => s.SetLogin(SonarQubeToken));
        });

    Target BackendRestore => _ => _
        .DependsOn(BackendClean)
        .Executes(() =>
            DotNetRestore(s => s
                .SetProjectFile(Solution)
                .EnableNoCache()));

    Target BackendBuild => _ => _
        .DependsOn(SonarScannerBegin)
        .DependsOn(BackendRestore)
        .Executes(() =>
        {
            ReportSummary(s => s
                .AddPairWhenValueNotNull("Version", GitVersion.FullSemVer));

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
                    "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format",
                    "opencover")
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
            var targetDirectory = BackendTestResultsDirectory / "reports";

            ReportGenerator(s => s
                .SetProcessToolPath(
                    NuGetToolPathResolver.GetPackageExecutable(
                        "ReportGenerator",
                        "ReportGenerator.dll",
                        framework: Framework))
                .SetTargetDirectory(targetDirectory)
                .AddReports(BackendTestResultsDirectory / "**/coverage.opencover.xml")
                .AddReportTypes(
                    ReportTypes.Cobertura,
                    ReportTypes.JsonSummary,
                    ReportTypes.HtmlInline_AzurePipelines_Light)
                .SetFileFilters("-*Program.cs")
            );

            targetDirectory.CompressTo(BackendTestResultsArtifact);

            if (AzurePipelines.Instance != null)
            {
                AzurePipelines.Instance.PublishCodeCoverage(
                    AzurePipelinesCodeCoverageToolType.Cobertura,
                    targetDirectory / "Cobertura.xml",
                    targetDirectory,
                    Array.Empty<string>()
                );
            }

            string jsonSummary = targetDirectory / "Summary.json";
            var jsonText = File.ReadAllText(jsonSummary);
            var jsonNode = JsonSerializer.Deserialize<JsonNode>(jsonText);
            var lineCoverage = jsonNode["summary"]["linecoverage"].GetValue<double>() / 100d;

            ReportSummary(s => s
                .AddPairWhenValueNotNull("Line coverage", lineCoverage.ToString("P")));
        });

    Target BackendPublish => _ => _
        .DependsOn(SonarScannerEnd)
        .Executes(() =>
            DotNetPublish(s => s
                .SetProject(Solution.NukeWorkshops_Server)
                .SetConfiguration(Configuration)
                .EnableNoLogo()
                .EnableNoRestore()
                .EnableNoBuild()
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)));
}