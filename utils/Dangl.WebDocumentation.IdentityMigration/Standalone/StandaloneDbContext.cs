using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dangl.WebDocumentation.IdentityMigration.Standalone
{
    public class StandaloneDbContext : IdentityDbContext<ApplicationUser>
    {
        public StandaloneDbContext(DbContextOptions<StandaloneDbContext> options) : base(options) { }

        public DbSet<DocumentationProject> DocumentationProjects { get; set; }
        public DbSet<DocumentationProjectVersion> DocumentationProjectVersions { get; set; }
        public DbSet<ProjectVersionAssetFile> ProjectVersionAssetFiles { get; set; }

        public DbSet<UserProjectAccess> UserProjects { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            DocumentationProjectVersion.OnModelCreating(builder);
            ProjectVersionAssetFile.OnModelCreating(builder);

            // Make the ApiKey unique so it can be used as a single identifier for a project
            builder.Entity<DocumentationProject>()
                .HasIndex(entity => entity.ApiKey)
                .IsUnique();

            // Composite key for UserProject Access
            builder.Entity<UserProjectAccess>()
                .HasKey(entity => new {entity.ProjectId, entity.UserId});

            builder.Entity<UserProjectAccess>()
                .HasOne(entity => entity.Project)
                .WithMany(project => project.UserAccess)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApplicationUser>()
                .HasMany(user => user.Roles)
                .WithOne()
                .HasForeignKey(r => r.UserId);
        }
    }
}
