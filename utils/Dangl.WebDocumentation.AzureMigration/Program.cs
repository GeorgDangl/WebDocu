using Dangl.AspNetCore.FileHandling.Azure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.AzureMigration
{
    class Program
    {
        private static AzureBlobFileManager _azureBlobFileManager;

        static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Please provide two arguments in the following order:");
                Console.WriteLine("1. Absolute path to the local projects folder as the migration source");
                Console.WriteLine("2. Azure Blob Storage connection string");
                return;
            }

            var localFolder = args[0];
            var storageConnectionString = args[1];

            _azureBlobFileManager = new AzureBlobFileManager(storageConnectionString);
            await _azureBlobFileManager.EnsureContainerCreated(AppConstants.PROJECTS_CONTAINER);

            var projectIds = GetAllProjectIds(localFolder);
            for (var i = 0; i < projectIds.Count; i++)
            {
                Console.WriteLine($"Migrating project {i + 1} of {projectIds.Count}...");
                await MigrateProjectAsync(projectIds[i], localFolder);
            }

            Console.WriteLine("Migration finished");
        }

        private static List<Guid> GetAllProjectIds(string rootFolder)
        {
            var directories = Directory.GetDirectories(rootFolder)
                .Select(dir => Path.GetFileName(dir))
                .Select(dir => new Guid(dir))
                .ToList();
            return directories;
        }

        private static async Task MigrateProjectAsync(Guid projectId, string rootFolder)
        {
            var projectVersionIds = GetAllProjectVersionIds(projectId, rootFolder);
            for (var i = 0; i < projectVersionIds.Count; i++)
            {
                Console.WriteLine($"  Migrating version {i + 1} of {projectVersionIds.Count}");

                var zipArchivePath = Path.Combine(rootFolder, projectId.ToString(), projectVersionIds[i] + ".zip");
                using (var zipArchiveStream = File.Open(zipArchivePath, FileMode.Open))
                {
                    var fileName = Path.Combine(projectId.ToString(), projectVersionIds[i] + ".zip");
                    await _azureBlobFileManager.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, fileName, zipArchiveStream);
                }
            }
        }

        private static List<Guid> GetAllProjectVersionIds(Guid projectId, string rootFolder)
        {
            var projectFolder = Path.Combine(rootFolder, projectId.ToString());
            var versionIds = Directory.GetFiles(projectFolder)
                .Select(file => Path.GetFileName(file))
                .Select(file => file.Substring(0, file.Length - 4))
                .Select(file => new Guid(file))
                .ToList();
            return versionIds;
        }
    }
}
