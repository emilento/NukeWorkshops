using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Nuke.Common.Utilities.Collections;
using static Serilog.Log;

partial class Build
{
    AbsolutePath FrontendSourceDirectory => RootDirectory / "nukeworkshops.client";

    Target FrontendClean => _ => _
        .Executes(() =>
        {
            FrontendSourceDirectory
                .GlobDirectories("**/node_modules")
                .ForEach(d => d.DeleteDirectory());

            Information("Frontend clean completed");
        });

    Target FrontendRestore => _ => _
        .DependsOn(FrontendClean)
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
}