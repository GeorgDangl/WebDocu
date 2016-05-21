using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dangl.WebDocumentation.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<DocumentationProject> DocumentationProjects { get; set; }

        public DbSet<UserProjectAccess> UserProjects { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Make the Name unique so it can be used as a single identifier for a project ( -> Urls may contain the project name instead of the Guid)
            builder.Entity<DocumentationProject>()
                .HasIndex(Entity => Entity.Name)
                .IsUnique();

            // Make the ApiKey unique so it can be used as a single identifier for a project
            builder.Entity<DocumentationProject>()
                .HasIndex(Entity => Entity.ApiKey)
                .IsUnique();

            // Composite key for UserProject Access
            builder.Entity<UserProjectAccess>()
                .HasKey(Entity => new {Entity.ProjectId, Entity.UserId});

            builder.Entity<UserProjectAccess>()
                .HasOne(Entity => Entity.Project)
                .WithMany(Project => Project.UserAccess)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}