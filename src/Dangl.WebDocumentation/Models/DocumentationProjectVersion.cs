﻿using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Dangl.WebDocumentation.Models
{
    public class DocumentationProjectVersion
    {
        [MaxLength(60)]
        public string ProjectName { get; set; }
        public DocumentationProject Project { get; set; }
        [MaxLength(40)]
        public string Version { get; set; }
        public Guid FileId { get; set; } = Guid.NewGuid();

        public static void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<DocumentationProjectVersion>()
                .HasKey(v => new {v.ProjectName, v.Version});

            builder.Entity<DocumentationProjectVersion>()
                .HasOne(v => v.Project)
                .WithMany()
                .HasForeignKey(v => v.ProjectName)
                .HasPrincipalKey(p => p.Name);

            builder.Entity<DocumentationProjectVersion>()
                .HasIndex(v => v.ProjectName);

            builder.Entity<DocumentationProjectVersion>()
                .HasIndex(v => v.Version);
        }
    }
}