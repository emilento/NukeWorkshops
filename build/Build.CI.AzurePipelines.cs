using Nuke.Common.CI.AzurePipelines;

[AzurePipelines(
    suffix: "frontend",
    AzurePipelinesImage.UbuntuLatest,
    FetchDepth = 0,
    PullRequestsDisabled = false,
    InvokedTargets = [nameof(FrontendAll)],
    NonEntryTargets = [nameof(FrontendClean), nameof(FrontendRestore), nameof(FrontendBuild)],
    ExcludedTargets = [nameof(BackendClean), nameof(BackendBuild), nameof(BackendRestore), nameof(BackendTests), nameof(BackendTestsCodeCoverage), nameof(BackendAll)],
    CacheKeyFiles = [],
    CachePaths = []
)]
[AzurePipelines(
    suffix: "backend",
    AzurePipelinesImage.UbuntuLatest,
    FetchDepth = 0,
    PullRequestsDisabled = false,
    InvokedTargets = [nameof(BackendAll)],
    NonEntryTargets = [nameof(BackendPublish), nameof(SonarScannerBegin), nameof(SonarScannerEnd), nameof(BackendClean), nameof(BackendRestore), nameof(BackendBuild), nameof(BackendTests), nameof(BackendTestsCodeCoverage)],
    ExcludedTargets = [nameof(FrontendClean), nameof(FrontendRestore), nameof(FrontendBuild), nameof(FrontendAll)]
)]
partial class Build
{

}