﻿using Nuke.CoberturaConverter;
using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Tools.WebConfigTransformRunner;
using Nuke.Common.Utilities;
using System.IO;
using static Nuke.CoberturaConverter.CoberturaConverterTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.IO.Globbing;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using System;
using static Nuke.Common.IO.TextTasks;
using Nuke.Common.Tools.GitVersion;
using static Nuke.Common.IO.HttpTasks;
using Newtonsoft.Json.Linq;
using System.Linq;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.AzureKeyVault;
using Nuke.Common.IO;
using static Nuke.Common.Tools.Docker.DockerTasks;
using Nuke.Common.Tools.Docker;
using System.Threading.Tasks;
using Nuke.Common.Tools.Slack;
using Nuke.Common.Git;
using Nuke.Common.Tools.Teams;
using Nuke.Common.Tools.Coverlet;
using System.Xml.Linq;

class Build : NukeBuild
{
    // Console application entry point. Also defines the default target.
    public static int Main() => Execute<Build>(x => x.Compile);

    [AzureKeyVaultConfiguration(
        BaseUrlParameterName = nameof(KeyVaultBaseUrl),
        ClientIdParameterName = nameof(KeyVaultClientId),
        ClientSecretParameterName = nameof(KeyVaultClientSecret),
        TenantIdParameterName = nameof(KeyVaultTenantId))]
    readonly AzureKeyVaultConfiguration KeyVaultSettings;

    [Parameter] string KeyVaultBaseUrl;
    [Parameter] string KeyVaultClientId;
    [Parameter] string KeyVaultClientSecret;
    [Parameter] string KeyVaultTenantId;
    [AzureKeyVault] AzureKeyVault KeyVault;

    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion(Framework = "netcoreapp3.1")] GitVersion GitVersion;

    [AzureKeyVaultSecret] string DockerRegistryUrl;
    [AzureKeyVaultSecret] string DockerRegistryUsername;
    [AzureKeyVaultSecret] string DockerRegistryPassword;
    [AzureKeyVaultSecret] readonly string DanglCiCdTeamsWebhookUrl;

    [AzureKeyVaultSecret] string DanglCiCdSlackWebhookUrl;

    [Parameter] readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Solution("Dangl.WebDocumentation.sln")] readonly Solution Solution;
    AbsolutePath SolutionDirectory => Solution.Directory;
    AbsolutePath OutputDirectory => SolutionDirectory / "output";
    AbsolutePath SourceDirectory => SolutionDirectory / "src";

    string DockerImageName => "dangldocu";

    protected override void OnBuildInitialized()
    {
        if (GitVersion == null)
        {
            // Sometimes, GitVersion failed to initialize on the build server for the GitVersionAttribute
            // Since log output is disabled there, we're enabling it here to be able to see what error occurred
            Serilog.Log.Information("Failed to get GitVersion automatically, trying to obtain it manually with NoFetch specified");
            GitVersion = GitVersionTasks.GitVersion(s => s
                    .SetNoFetch(true)
                    .SetFramework("netcoreapp3.1")).Result;
        }

        base.OnBuildInitialized();
    }

    protected override void OnTargetFailed(string target)
    {
        if (IsServerBuild)
        {
            SendTeamsMessage("Build Failed", $"Target {target} failed for Dangl.WebDocumentation, " +
                        $"Branch: {GitRepository.Branch}", true);
        }
    }

    void SendTeamsMessage(string title, string message, bool isError)
    {
        if (!string.IsNullOrWhiteSpace(DanglCiCdTeamsWebhookUrl))
        {
            var themeColor = isError ? "f44336" : "00acc1";
            try
            {
                TeamsTasks
                    .SendTeamsMessage(m => m
                        .SetTitle(title)
                        .SetText(message)
                        .SetThemeColor(themeColor),
                        DanglCiCdTeamsWebhookUrl);
            }
            catch
            {
                Serilog.Log.Warning("Failed to send a Teams message");
            }
        }
    }

    Target Clean => _ => _
            .Executes(() =>
            {
                GlobDirectories(SourceDirectory, "**/bin", "**/obj").ForEach(d => ((AbsolutePath)d).DeleteDirectory());
                OutputDirectory.CreateOrCleanDirectory();
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
            filePath.WriteAllText(content);
        });

    Target Restore => _ => _
            .DependsOn(Clean)
            .DependsOn(GenerateVersion)
            .Executes(() =>
            {
                try
                {
                    DotNetRestore();
                }
                catch
                {
                    DotNetRestore(r => r.EnableNoCache());
                }
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
            var testProjectPath = SolutionDirectory / "test" / "Dangl.WebDocumentation.Tests" / "Dangl.WebDocumentation.Tests.csproj";

            try
            {
                DotNetTest(x => x
                    .SetResultsDirectory(OutputDirectory)
                    .SetDataCollector("XPlat Code Coverage")
                    .AddRunSetting("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format", "cobertura")
                    .AddRunSetting("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include", "[Dangl.WebDocumentation*]*")
                    .AddRunSetting("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute", "Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute")
                    .EnableNoBuild()
                    .SetTestAdapterPath(".")
                    .SetLoggers($"xunit;LogFilePath={OutputDirectory / "testresults.xml"}")
                    .SetProjectFile(testProjectPath));
            }
            finally
            {
                // Merge coverage reports, otherwise they might not be completely
                // picked up by Jenkins
                ReportGenerator(c => c
                    .SetFramework("net7.0")
                    .SetReports(OutputDirectory / "**/*cobertura.xml")
                    .SetTargetDirectory(OutputDirectory)
                    .SetReportTypes(ReportTypes.Cobertura));

                MakeSourceEntriesRelativeInCoberturaFormat(OutputDirectory / "Cobertura.xml");
            }
        });

    private void MakeSourceEntriesRelativeInCoberturaFormat(AbsolutePath coberturaReportPath)
    {
        var originalText = coberturaReportPath.ReadAllText();
        var xml = XDocument.Parse(originalText);

        var xDoc = XDocument.Load(coberturaReportPath);

        var sourcesEntry = xDoc
            .Root
            .Elements()
            .Where(e => e.Name.LocalName == "sources")
            .Single();

        string basePath;
        if (sourcesEntry.HasElements)
        {
            var elements = sourcesEntry.Elements().ToList();
            basePath = elements
                .Select(e => e.Value)
                .OrderBy(p => p.Length)
                .First();
            foreach (var element in elements)
            {
                if (element.Value != basePath)
                {
                    element.Remove();
                }
            }
        }
        else
        {
            basePath = sourcesEntry.Value;
        }

        Serilog.Log.Information($"Normalizing Cobertura report to base path: \"{basePath}\"");

        var filenameAttributes = xDoc
            .Root
            .Descendants()
            .Where(d => d.Attributes().Any(a => a.Name.LocalName == "filename"))
            .Select(d => d.Attributes().First(a => a.Name.LocalName == "filename"));
        foreach (var filenameAttribute in filenameAttributes)
        {
            if (filenameAttribute.Value.StartsWith(basePath))
            {
                filenameAttribute.Value = filenameAttribute.Value.Substring(basePath.Length);
            }
        }

        xDoc.Save(coberturaReportPath);
    }

    Target BuildDocker => _ => _
        .DependsOn(GenerateVersion)
        .Requires(() => Configuration.EqualsOrdinalIgnoreCase("Release"))
        .Executes(() =>
        {
            DockerPull(c => c.SetName("mcr.microsoft.com/dotnet/aspnet:7.0"));
            DockerPull(c => c.SetName("mcr.microsoft.com/dotnet/sdk:7.0"));
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
                .SetProcessWorkingDirectory(OutputDirectory));

            OutputDirectory.CreateOrCleanDirectory();
        });

    Target PushDocker => _ => _
        .DependsOn(BuildDocker)
        .Requires(() => DockerRegistryUrl)
        .Requires(() => DockerRegistryUsername)
        .Requires(() => DockerRegistryPassword)
        .Requires(() => DanglCiCdSlackWebhookUrl)
        .OnlyWhenDynamic(() => !(Nuke.Common.CI.Jenkins.Jenkins.Instance is Nuke.Common.CI.Jenkins.Jenkins) || (Nuke.Common.CI.Jenkins.Jenkins.Instance as Nuke.Common.CI.Jenkins.Jenkins).ChangeId == null)
        .Executes(async () =>
        {
            DockerLogin(x => x
                .SetUsername(DockerRegistryUsername)
                .SetServer(DockerRegistryUrl)
                .SetPassword(DockerRegistryPassword)
                .DisableProcessLogOutput());

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

        Assert.True(statusIsAtLatestVersion, $"Status at {appStatusUrl} does not indicate latest version.");
        Serilog.Log.Information($"App at {appStatusUrl} is at latest version {GitVersion.NuGetVersionV2}");
    }

    private async Task PushDockerWithTag(string tag)
    {
        var imageTag = $"{DockerRegistryUrl}/{DockerImageName}:{tag}";
        DockerTag(c => c
            .SetSourceImage(DockerImageName + ":" + "dev")
            .SetTargetImage(imageTag));
        DockerPush(c => c
            .SetName(imageTag));

        var message = $"A new container version was pushed for DanglDocu, Version {GitVersion.NuGetVersionV2}, Tag {imageTag}";
        await SlackTasks.SendSlackMessageAsync(c => c
            .SetUsername("Dangl CI Build")
            .SetAttachments(new SlackMessageAttachment()
                .SetText(message)
                .SetColor("good")
                .SetFields(new[]
                {
                    new SlackMessageField
                    ()
                    .SetTitle("Tag")
                    .SetValue(imageTag)
                })),
                DanglCiCdSlackWebhookUrl);
        SendTeamsMessage("Docker Push", message, false);
    }
}
