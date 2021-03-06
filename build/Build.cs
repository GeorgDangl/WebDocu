﻿using Nuke.CoberturaConverter;
using Nuke.Common;
using Nuke.Common.Tools.DotCover;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Tools.WebConfigTransformRunner;
using Nuke.Common.Utilities;
using System.IO;
using static Nuke.CoberturaConverter.CoberturaConverterTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotCover.DotCoverTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using System;
using static Nuke.Common.IO.TextTasks;
using Nuke.Common.Tools.GitVersion;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using static Nuke.Common.IO.HttpTasks;
using Newtonsoft.Json.Linq;
using System.Linq;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.AzureKeyVault.Attributes;
using Nuke.Common.Tools.AzureKeyVault;
using Nuke.Common.IO;
using static Nuke.Common.Tools.Docker.DockerTasks;
using Nuke.Common.Tools.Docker;
using System.Threading.Tasks;
using Nuke.Common.Tools.Slack;

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

    [GitVersion(Framework = "netcoreapp3.1")] readonly GitVersion GitVersion;

    [KeyVaultSecret] string DockerRegistryUrl;
    [KeyVaultSecret] string DockerRegistryUsername;
    [KeyVaultSecret] string DockerRegistryPassword;

    [KeyVaultSecret] string DanglCiCdSlackWebhookUrl;

    [Parameter] readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Solution("Dangl.WebDocumentation.sln")] readonly Solution Solution;
    AbsolutePath SolutionDirectory => Solution.Directory;
    AbsolutePath OutputDirectory => SolutionDirectory / "output";
    AbsolutePath SourceDirectory => SolutionDirectory / "src";

    string DockerImageName => "dangldocu";

    Target Clean => _ => _
            .Executes(() =>
            {
                GlobDirectories(SourceDirectory, "**/bin", "**/obj").ForEach(DeleteDirectory);
                EnsureCleanDirectory(OutputDirectory);
            });

    Target GenerateVersion => _ => _
        .Executes(() =>
        {
            var buildDate = DateTime.UtcNow;

            var filePath = SourceDirectory / "Dangl.WebDocumentation" / "Services" / "VersionsService.cs";

            var currentDateUtc = $"new DateTime({buildDate.Year}, {buildDate.Month}, {buildDate.Day}, {buildDate.Hour}, {buildDate.Minute}, {buildDate.Second}, DateTimeKind.Utc)";

            var content = $@"using System;

namespace Dangl.WebDocumentation.Services
{{
    // This file is automatically generated
    [System.CodeDom.Compiler.GeneratedCode(""GitVersionBuild"", """")]
    public static class VersionsService
    {{
        public static string Version => ""{GitVersion.NuGetVersionV2}"";
        public static string CommitInfo => ""{GitVersion.FullBuildMetaData}"";
        public static string CommitDate => ""{GitVersion.CommitDate}"";
        public static string CommitHash => ""{GitVersion.Sha}"";
        public static string InformationalVersion => ""{GitVersion.InformationalVersion}"";
        public static DateTime BuildDateUtc {{ get; }} = {currentDateUtc};
    }}
}}";
            WriteAllText(filePath, content);
        });

    Target Restore => _ => _
            .DependsOn(Clean)
            .DependsOn(GenerateVersion)
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
        .Executes(async () =>
        {
            var testProjectDirectory = SolutionDirectory / "test" / "Dangl.WebDocumentation.Tests";

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
                .SetFramework("netcoreapp2.1")
                .SetReports(OutputDirectory / "dotCover.xml")
                .SetTargetDirectory(OutputDirectory / "CoverageReport"));

            //// This is the report in Cobertura format that integrates so nice in Jenkins
            //// dashboard and allows to extract more metrics and set build health based
            //// on coverage readings
            await DotCoverToCobertura(s => s
                    .SetInputFile(OutputDirectory / "dotCover.xml")
                    .SetOutputFile(OutputDirectory / "cobertura.xml"))
                .ConfigureAwait(false);
        });

    Target BuildDocker => _ => _
        .DependsOn(GenerateVersion)
        .Requires(() => Configuration.EqualsOrdinalIgnoreCase("Release"))
        .Executes(() =>
        {
            DockerPull(c => c.SetName("mcr.microsoft.com/dotnet/core/aspnet:3.1-bionic"));
            DotNetPublish(s => s
                .SetProject(SourceDirectory / "Dangl.WebDocumentation")
                .SetOutput(OutputDirectory)
                .SetConfiguration(Configuration));

            foreach (var configFileToDelete in GlobFiles(OutputDirectory, "web*.config"))
            {
                File.Delete(configFileToDelete);
            }

            CopyFile(SourceDirectory / "Dangl.WebDocumentation" / "Dockerfile_CI", OutputDirectory / "Dockerfile", Nuke.Common.IO.FileExistsPolicy.Overwrite);

            DockerBuild(c => c
                .SetFile(OutputDirectory / "Dockerfile")
                .SetTag(DockerImageName + ":dev")
                .SetPath(".")
                .SetWorkingDirectory(OutputDirectory));

            EnsureCleanDirectory(OutputDirectory);
        });

    Target PushDocker => _ => _
        .DependsOn(BuildDocker)
        .Requires(() => DockerRegistryUrl)
        .Requires(() => DockerRegistryUsername)
        .Requires(() => DockerRegistryPassword)
        .Requires(() => DanglCiCdSlackWebhookUrl)
        .OnlyWhenDynamic(() => Nuke.Common.CI.Jenkins.Jenkins.Instance == null
                || Nuke.Common.CI.Jenkins.Jenkins.Instance.ChangeId == null)
        .Executes(async () =>
        {
            DockerLogin(x => x
                .SetUsername(DockerRegistryUsername)
                .SetServer(DockerRegistryUrl)
                .SetPassword(DockerRegistryPassword)
                .DisableLogOutput());

            await PushDockerWithTag("dev");

            if (GitVersion.BranchName.Equals("master") || GitVersion.BranchName.Equals("origin/master"))
            {
                await PushDockerWithTag("latest");
                await PushDockerWithTag(GitVersion.SemVer);
                await EnsureAppIsAtLatestVersionAsync("https://docs.dangl-it.com/api/status");
            }
        });

    async Task EnsureAppIsAtLatestVersionAsync(string appStatusUrl)
    {
        var timeoutInSeconds = 180;
        var statusIsAtLatestVersion = false;
        var start = DateTime.UtcNow;
        while (!statusIsAtLatestVersion && DateTime.UtcNow < start.AddSeconds(timeoutInSeconds))
        {
            try
            {
                var stagingVersion = JObject.Parse(await HttpDownloadStringAsync(appStatusUrl))["version"].ToString();
                statusIsAtLatestVersion = stagingVersion == GitVersion.NuGetVersionV2;
            }
            catch
            {
                await Task.Delay(1_000);
            }
        }

        ControlFlow.Assert(statusIsAtLatestVersion, $"Status at {appStatusUrl} does not indicate latest version.");
        Logger.Normal($"App at {appStatusUrl} is at latest version {GitVersion.NuGetVersionV2}");
    }

    private async Task PushDockerWithTag(string tag)
    {
        DockerTag(c => c
            .SetSourceImage(DockerImageName + ":" + "dev")
            .SetTargetImage($"{DockerRegistryUrl}/{DockerImageName}:{tag}"));
        DockerPush(c => c
            .SetName($"{DockerRegistryUrl}/{DockerImageName}:{tag}"));

        await SlackTasks.SendSlackMessageAsync(c => c
            .SetUsername("Dangl CI Build")
            .SetAttachments(new SlackMessageAttachment()
                .SetText($"A new container version was pushed for DanglDocu, Version {GitVersion.NuGetVersionV2}")
                .SetColor("good")
                .SetFields(new[]
                {
                    new SlackMessageField
                    ()
                    .SetTitle("Tag")
                    .SetValue($"{DockerRegistryUrl}/{DockerImageName}:{tag}")
                })),
                DanglCiCdSlackWebhookUrl);
    }
}
