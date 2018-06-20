using Nuke.CoberturaConverter;
using Nuke.Common;
using Nuke.Common.Tools.DotCover;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Tools.WebConfigTransformRunner;
using Nuke.Common.Utilities;
using Nuke.WebDeploy;
using System.IO;
using static Nuke.CoberturaConverter.CoberturaConverterTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotCover.DotCoverTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using static Nuke.Common.Tools.WebConfigTransformRunner.WebConfigTransformRunnerTasks;
using static Nuke.WebDeploy.WebDeployTasks;

class Build : NukeBuild
{
    // Console application entry point. Also defines the default target.
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter] readonly string WebDeployUsername;
    [Parameter] readonly string WebDeployPassword;
    [Parameter] readonly string WebDeployPublishUrl;
    [Parameter] readonly string WebDeploySiteName;

    Target Clean => _ => _
            .Executes(() =>
            {
                DeleteDirectories(GlobDirectories(SolutionDirectory, "**/bin", "**/obj"));
                EnsureCleanDirectory(OutputDirectory);
            });

    Target Restore => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                DotNetRestore(s => DefaultDotNetRestore);
            });

    Target Compile => _ => _
            .DependsOn(Restore)
            .Executes(() =>
            {
                DotNetBuild(s => DefaultDotNetBuild);
            });

    Target Coverage => _ => _
        .DependsOn(Compile)
        .Requires(() => Configuration.EqualsOrdinalIgnoreCase("Debug")) // Required for coverage data gathering
        .Executes(() =>
        {
            var testProjectDirectory = SourceDirectory / "Dangl.WebDocumentation.Tests";

            DotCoverAnalyse(x => x
                .SetTargetExecutable(GetToolPath())
                .SetTargetWorkingDirectory(testProjectDirectory)
                .SetTargetArguments($"xunit -nobuild \"-xml {OutputDirectory / "testresults.xml"}\"")
                .SetFilters("+:Dangl.WebDocumentation")
                .SetAttributeFilters("System.CodeDom.Compiler.GeneratedCodeAttribute")
                .SetOutputFile(OutputDirectory / "dotCover.xml")
                .SetReportType(DotCoverReportType.DetailedXml));

            //// This is the report that's pretty and visualized in Jenkins
            ReportGenerator(c => c
                .SetReports(OutputDirectory / "dotCover.xml")
                .SetTargetDirectory(OutputDirectory / "CoverageReport"));

            //// This is the report in Cobertura format that integrates so nice in Jenkins
            //// dashboard and allows to extract more metrics and set build health based
            //// on coverage readings
            DotCoverToCobertura(s => s
                    .SetInputFile(OutputDirectory / "dotCover.xml")
                    .SetOutputFile(OutputDirectory / "cobertura.xml"))
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        });

    Target Publish => _ => _
        .DependsOn(Restore)
        .Requires(() => Configuration.EqualsOrdinalIgnoreCase("Release"))
        .Executes(() =>
        {
            DotNetPublish(s => s.SetProject(SourceDirectory / "Dangl.WebDocumentation")
                    .SetOutput(OutputDirectory)
                    .SetConfiguration(Configuration));

            var publishEnvironmentName = "Production";

            WebConfigTransformRunner(p => p.SetWebConfigFilename(OutputDirectory / "web.config")
                .SetTransformFilename(OutputDirectory / $"web.{publishEnvironmentName}.config")
                .SetOutputFilename(OutputDirectory / "web.config"));

            foreach (var configFileToDelete in GlobFiles(OutputDirectory, "web.*.config"))
            {
                File.Delete(configFileToDelete);
            }

            foreach (var configFileToDelete in GlobFiles(OutputDirectory, "appsettings.*.json"))
            {
                if (!configFileToDelete.EndsWithOrdinalIgnoreCase($"{publishEnvironmentName}.json"))
                {
                    File.Delete(configFileToDelete);
                }
            }
        });

    Target Deploy => _ => _
        .DependsOn(Publish)
        .Requires(() => WebDeployUsername)
        .Requires(() => WebDeployPassword)
        .Requires(() => WebDeployPublishUrl)
        .Requires(() => WebDeploySiteName)
        .Executes(() =>
        {
            WebDeploy(s => s.SetSourcePath(OutputDirectory)
                .SetUsername(WebDeployUsername)
                .SetPassword(WebDeployPassword)
                .SetEnableAppOfflineRule(true)
                .SetPublishUrl(WebDeployPublishUrl)
                .SetSiteName(WebDeploySiteName)
                .SetEnableDoNotDeleteRule(false)
                .SetWrapAppOffline(true));
        });
}
