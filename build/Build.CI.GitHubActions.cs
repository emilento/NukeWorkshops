using Nuke.Common.CI.GitHubActions;

[GitHubActions(
    "backend-build",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    InvokedTargets = [nameof(BackendAll)],
    On = [GitHubActionsTrigger.PullRequest, GitHubActionsTrigger.Push],
    PublishArtifacts = false)]
[GitHubActions(
    "frontend-build",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    InvokedTargets = [nameof(FrontendAll)],
    On = [GitHubActionsTrigger.PullRequest, GitHubActionsTrigger.Push],
    CacheKeyFiles = [],
    PublishArtifacts = false)]
partial class Build
{

}