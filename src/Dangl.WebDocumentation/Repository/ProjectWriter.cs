using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Dangl.WebDocumentation.Models;

namespace Dangl.WebDocumentation.Repository
{
    public static class ProjectWriter
    {
        public static bool CreateProjectFilesFromZip(ZipArchive projectArchive, string physicalProjectsRootDirectory, Guid projectId, ApplicationDbContext context)
        {
            try
            {
                var databaseProject = context.DocumentationProjects.FirstOrDefault(project => project.Id == projectId);
                if (databaseProject == null)
                {
                    throw new ArgumentException(nameof(projectId) + " is not a valid Guid for a project.");
                }
                var newGuid = Guid.NewGuid();
                // Generate a Guid under which to store the upload
                var newProjectDir = Path.Combine(physicalProjectsRootDirectory, newGuid.ToString());
                Directory.CreateDirectory(newProjectDir);
                foreach (var fileEntry in projectArchive.Entries)
                {
                    var neededPath = new FileInfo(Path.Combine(newProjectDir, fileEntry.FullName)).Directory.FullName;
                    if (!Directory.Exists(neededPath))
                    {
                        Directory.CreateDirectory(neededPath);
                    }
                    // Copy only when it's a file and not a folder
                    if (fileEntry.Length > 0)
                    {
                        using (var currentEntryStream = fileEntry.Open())
                        {
                            using (var fileStream = File.Create(Path.Combine(neededPath, fileEntry.Name)))
                            {
                                currentEntryStream.CopyTo(fileStream);
                            }
                        }
                    }
                }
                // Delete previous folder and set guid to new folder
                var oldGuid = databaseProject.FolderGuid;
                databaseProject.FolderGuid = newGuid;
                context.SaveChanges();
                var oldFolder = Path.Combine(physicalProjectsRootDirectory, oldGuid.ToString());
                if (Directory.Exists(oldFolder) || oldGuid != Guid.Empty)
                {
                    Directory.Delete(oldFolder, true);
                }
                return true;
            }
            catch
            {
                // Could not write the files to disk
                return false;
            }
        }
    }
}
