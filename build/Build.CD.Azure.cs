using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Pulumi;
using static Serilog.Log;

partial class Build
{
    AbsolutePath InfrastructureDirectory => RootDirectory / "infra";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    AbsolutePath DeploymentFile => ArtifactsDirectory / "deployment.zip";

    Target ProvisionInfra => _ => _
        .Description("Provision the infrastructure on Azure")
        .Executes(() =>
        {
            PulumiTasks.PulumiUp(_ => _
                .SetCwd(InfrastructureDirectory)
                .SetStack("dev")
                .EnableSkipPreview());
        });

    Target Deploy => _ => _
        .Produces(DeploymentFile)
        .DependsOn(BackendAll)
        .DependsOn(FrontendAll)
        .DependsOn(ProvisionInfra)
        .Executes(async () =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();

            FileSystemTasks.CopyDirectoryRecursively(
                BackendSourceDirectory / "bin" / Configuration / "net8.0" / "publish",
                ArtifactsDirectory,
                DirectoryExistsPolicy.Merge);

            FileSystemTasks.CopyDirectoryRecursively(
                FrontendSourceDirectory / "dist",
                ArtifactsDirectory,
                DirectoryExistsPolicy.Merge);

            Information("Compressing {Directory} to deployment.zip...", ArtifactsDirectory);
            ArtifactsDirectory.CompressTo(DeploymentFile);

            Information("Deploying...");
            var publishingUserName = GetPulumiOutput("publishingUserName");
            var publishingPassword = GetPulumiOutput("publishingPassword");
            var base64Auth = Convert.ToBase64String(Encoding.Default.GetBytes($"{publishingUserName}:{publishingPassword}"));
            var zipDeployRequestUrl = $"https://{GetPulumiOutput("webAppName")}.scm.azurewebsites.net/api/zipdeploy";
            using var stream = new MemoryStream(File.ReadAllBytes(DeploymentFile));
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
            using var content = new StreamContent(stream);
            var response = await httpClient.PostAsync(zipDeployRequestUrl, content);
            Information("Deployment completed");
        });

    string GetPulumiOutput(string outputName)
    {
        PulumiTasks.PulumiStackSelect(_ => _
            .SetCwd(InfrastructureDirectory)
            .SetStackName("dev"));

        return PulumiTasks.PulumiStackOutput(_ => _
                .SetCwd(InfrastructureDirectory)
                .SetPropertyName(outputName)
                .EnableShowSecrets()
                .DisableProcessLogOutput())
            .StdToText();
    }
}
