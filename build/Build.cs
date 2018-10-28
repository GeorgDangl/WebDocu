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
using Nuke.Azure.KeyVault;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;

class Build : NukeBuild
{
    // Console application entry point. Also defines the default target.
    public static int Main() => Execute<Build>(x => x.Compile);

    [KeyVaultSettings(
        BaseUrlParameterName = nameof(KeyVaultBaseUrl),
        ClientIdParameterName = nameof(KeyVaultClientId),
        ClientSecretParameterName = nameof(KeyVaultClientSecret))]
    readonly KeyVaultSettings KeyVaultSettings;

    [Parameter] string KeyVaultBaseUrl;
    [Parameter] string KeyVaultClientId;
    [Parameter] string KeyVaultClientSecret;
    [KeyVault] KeyVault KeyVault;

    [Parameter] readonly string WebDeployUsernameSecretName;
    [Parameter] readonly string WebDeployPasswordSecretName;
    [Parameter] readonly string WebDeployPublishUrlSecretName;
    [Parameter] readonly string WebDeploySiteNameSecretName;

    [Parameter] readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Solution("Dangl.WebDocumentation.sln")] readonly Solution Solution;
    AbsolutePath SolutionDirectory => Solution.Directory;
    AbsolutePath OutputDirectory => SolutionDirectory / "output";
    AbsolutePath SourceDirectory => SolutionDirectory / "src";

    Target Clean => _ => _
            .Executes(() =>
            {
                DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
                EnsureCleanDirectory(OutputDirectory);
            });

    Target Restore => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                DotNetRestore();
            });

    Target Compile => _ => _
            .DependsOn(Restore)
            .Executes(() =>
            {
                DotNetBuild(x => x
                    .SetConfiguration(Configuration)
                    .EnableNoRestore());
            });

    Target Coverage => _ => _
        .DependsOn(Compile)
        .Requires(() => Configuration.EqualsOrdinalIgnoreCase("Debug")) // Required for coverage data gathering
        .Executes(() =>
        {
            var testProjectDirectory = SourceDirectory / "Dangl.WebDocumentation.Tests";

            DotCoverAnalyse(x => x
                .SetTargetExecutable(ToolPathResolver.GetPathExecutable("dotnet"))
                .SetTargetWorkingDirectory(testProjectDirectory)
                .SetTargetArguments($"test --no-build --test-adapter-path:. \"--logger:xunit;LogFilePath={OutputDirectory / "testresults.xml"}\"")
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
        .Requires(() => WebDeployUsernameSecretName)
        .Requires(() => WebDeployPasswordSecretName)
        .Requires(() => WebDeployPublishUrlSecretName)
        .Requires(() => WebDeploySiteNameSecretName)
        .Executes(async () =>
        {
            var webDeployUsername = await KeyVault.GetSecret(WebDeployUsernameSecretName);
            var webDeployPassword = await KeyVault.GetSecret(WebDeployPasswordSecretName);
            var webDeployPublishUrl = await KeyVault.GetSecret(WebDeployPublishUrlSecretName);
            var webDeploySiteName = await KeyVault.GetSecret(WebDeploySiteNameSecretName);

            WebDeploy(s => s.SetSourcePath(OutputDirectory)
                .SetUsername(webDeployUsername)
                .SetPassword(webDeployPassword)
                .SetEnableAppOfflineRule(true)
                .SetPublishUrl(webDeployPublishUrl.TrimEnd('/') + "/msdeploy.axd?site=" + webDeploySiteName)
                .SetSiteName(webDeploySiteName)
                .SetEnableDoNotDeleteRule(false)
                .SetWrapAppOffline(true));
        });
}
