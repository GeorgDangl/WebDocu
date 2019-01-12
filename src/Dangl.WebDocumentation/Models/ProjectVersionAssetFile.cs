using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace Dangl.WebDocumentation.Models
{
    public class ProjectVersionAssetFile
    {
        [MaxLength(60)]
        public string ProjectName { get; set; }

        [MaxLength(40)]
        public string Version { get; set; }

        [MaxLength(256), Required]
        public string FileName { get; set; }

        public Guid FileId { get; set; } = Guid.NewGuid();
        public long FileSizeInBytes { get; set; }

        public DocumentationProjectVersion ProjectVersion { get; set; }

        public static void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<ProjectVersionAssetFile>()
                .HasKey(v => v.FileId);

            // Makes the Name of the project unique so it can be used as a single identifier for a project
            // ( -> Urls may contain the project name instead of the Guid)
            builder.Entity<ProjectVersionAssetFile>()
                .HasOne(v => v.ProjectVersion)
                .WithMany(v => v.AssetFiles)
                .HasForeignKey(v => new { v.ProjectName, v.Version })
                // Restrict to ensure all asset files are deleted before a version is removed
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProjectVersionAssetFile>()
                .HasIndex(v => v.ProjectName);

            builder.Entity<ProjectVersionAssetFile>()
                .HasIndex(v => v.Version);

            builder.Entity<ProjectVersionAssetFile>()
                .HasIndex(v => v.FileName);
        }
    }
}
