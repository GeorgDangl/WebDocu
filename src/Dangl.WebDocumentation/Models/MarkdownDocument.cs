using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Dangl.WebDocumentation.Models
{
    public class MarkdownDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(60)]
        public string ProjectName { get; set; }

        [Required, MaxLength(40)]
        public string Version { get; set; }

        [Required, MaxLength(300)]
        public string Path { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; }

        public static void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<MarkdownDocument>()
                .HasIndex(d => new { d.ProjectName, d.Version, d.Path })
                .IsUnique();
            builder.Entity<MarkdownDocument>()
                .HasOne<DocumentationProjectVersion>()
                .WithMany()
                .HasForeignKey(d => new { d.ProjectName, d.Version })
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
