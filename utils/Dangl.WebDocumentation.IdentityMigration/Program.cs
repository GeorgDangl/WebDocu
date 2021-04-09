using Dangl.Identity.Data;
using Dangl.WebDocumentation.IdentityMigration.Standalone;
using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.IdentityMigration
{
    class Program
    {
        // Provide the connection string to perform the migration
        private static readonly string _danglIdentityConnectionString = "";
        private static readonly string _danglDocuSourceConnectionString = "";
        private static readonly string _danglDocuDestConnectionString = "";

        private static StandaloneDbContext _sourceDanglDocuContext;
        private static ApplicationDbContext _destinationDanglDocuContext;
        private static DanglIdentityDbContext _danglIdentityDbContext;
        private static readonly Dictionary<string, Guid> _destinationUserIdBySourceUserId = new Dictionary<string, Guid>();

        static async Task Main(string[] args)
        {
            var serviceProvider = GetServiceProvider();

            _sourceDanglDocuContext = serviceProvider.GetRequiredService<StandaloneDbContext>();
            _destinationDanglDocuContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _danglIdentityDbContext = serviceProvider.GetRequiredService<DanglIdentityDbContext>();

            using var destinationDanglDocuTransaction = await _destinationDanglDocuContext.Database.BeginTransactionAsync();
            using var danglIdentityTransaction = await _danglIdentityDbContext.Database.BeginTransactionAsync();

            await MigrateProjectsAsync();
            await MigrateProjectVersionsAsync();
            await MigrateProjectAssetFilesAsync();
            await MigrateUsersAsync();
            await MigrateProjectUserAccessAsync();

            await destinationDanglDocuTransaction.CommitAsync();
            await danglIdentityTransaction.CommitAsync();
        }

        private static IServiceProvider GetServiceProvider()
        {
            var builder = new ServiceCollection();

            builder.AddDbContext<DanglIdentityDbContext>(o => o.UseSqlServer(_danglIdentityConnectionString));
            builder.AddDbContext<StandaloneDbContext>(o => o.UseSqlServer(_danglDocuSourceConnectionString));
            builder.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(_danglDocuDestConnectionString));

            builder.AddLogging(c => c.AddDebug().AddConsole());

            return builder.BuildServiceProvider();
        }

        private static async Task MigrateProjectsAsync()
        {
            foreach (var sourceProject in (await _sourceDanglDocuContext.DocumentationProjects.ToListAsync()))
            {
                _destinationDanglDocuContext.DocumentationProjects.Add(new Models.DocumentationProject
                {
                    ApiKey = sourceProject.ApiKey,
                    FolderGuid = sourceProject.FolderGuid,
                    Id = sourceProject.Id,
                    IsPublic = sourceProject.IsPublic,
                    Name = sourceProject.Name,
                    PathToIndex = sourceProject.PathToIndex
                });
            }
            await _destinationDanglDocuContext.SaveChangesAsync();
        }

        private static async Task MigrateProjectVersionsAsync()
        {
            foreach (var sourceDocumentationProjectVersion in (await _sourceDanglDocuContext.DocumentationProjectVersions.ToListAsync()))
            {
                _destinationDanglDocuContext.DocumentationProjectVersions.Add(new Models.DocumentationProjectVersion
                {
                    CreatedAtUtc = sourceDocumentationProjectVersion.CreatedAtUtc,
                    FileId = sourceDocumentationProjectVersion.FileId,
                    MarkdownChangelog = sourceDocumentationProjectVersion.MarkdownChangelog,
                    ProjectName = sourceDocumentationProjectVersion.ProjectName,
                    Version = sourceDocumentationProjectVersion.Version
                });
            }
            await _destinationDanglDocuContext.SaveChangesAsync();
        }

        private static async Task MigrateProjectAssetFilesAsync()
        {
            foreach (var sourceProjectVersionAssetFile in (await _sourceDanglDocuContext.ProjectVersionAssetFiles.ToListAsync()))
            {
                _destinationDanglDocuContext.ProjectVersionAssetFiles.Add(new Models.ProjectVersionAssetFile
                {
                    FileId = sourceProjectVersionAssetFile.FileId,
                    FileName = sourceProjectVersionAssetFile.FileName,
                    FileSizeInBytes = sourceProjectVersionAssetFile.FileSizeInBytes,
                    ProjectName = sourceProjectVersionAssetFile.ProjectName,
                    Version = sourceProjectVersionAssetFile.Version,
                });
            }
            await _destinationDanglDocuContext.SaveChangesAsync();
        }

        private static async Task MigrateUsersAsync()
        {
            var now = DateTime.UtcNow;
            var sourceUsers = await _sourceDanglDocuContext.Users.ToListAsync();
            foreach (var sourceUser in sourceUsers)
            {
#pragma warning disable RCS1155 // Add call to 'ConfigureAwait' (or vice versa).
                var danglIdentityUser = await _danglIdentityDbContext.Users.FirstOrDefaultAsync(u => u.Email.ToUpper() == sourceUser.Email.ToUpper());
#pragma warning restore RCS1155 // Add call to 'ConfigureAwait' (or vice versa).
                if (danglIdentityUser == null)
                {
                    // Should add the user in Dangl.Identity
                    danglIdentityUser = new Identity.Data.Models.ApplicationUser
                    {
                        Email = sourceUser.Email,
                        EmailConfirmed = true,
                        Id = new Guid(sourceUser.Id),
                        IdenticonId = Guid.NewGuid(),
                        LastLoginDateUtc = now,
                        NormalizedEmail = sourceUser.NormalizedEmail,
                        NormalizedUserName = sourceUser.NormalizedUserName,
                        RegisterDateUtc = now,
                        UserName = sourceUser.UserName,
                        SecurityStamp = Guid.NewGuid().ToString(),
                        ConcurrencyStamp = Guid.NewGuid().ToString()
                    };
                    _danglIdentityDbContext.Users.Add(danglIdentityUser);
                    await _danglIdentityDbContext.SaveChangesAsync();

                    _danglIdentityDbContext.UserEvents.Add(new Identity.Data.Models.UserEvent
                    {
                        DateUtc = now,
                        EventType = Identity.Shared.Enums.UserEventType.UsernameChanged,
                        UserId = danglIdentityUser.Id,
                        Data = JsonConvert.SerializeObject(new
                        {
                            UserName = danglIdentityUser.UserName,
                            AdditionalInformation = "Migrated from DanglDocu"
                        })
                    });
                    _danglIdentityDbContext.UserEvents.Add(new Identity.Data.Models.UserEvent
                    {
                        DateUtc = now,
                        EventType = Identity.Shared.Enums.UserEventType.EmailChanged,
                        UserId = danglIdentityUser.Id,
                        Data = JsonConvert.SerializeObject(new
                        {
                            Email = danglIdentityUser.Email
                        })
                    });
                    _danglIdentityDbContext.UserEvents.Add(new Identity.Data.Models.UserEvent
                    {
                        DateUtc = now,
                        EventType = Identity.Shared.Enums.UserEventType.EmailConfirmed,
                        UserId = danglIdentityUser.Id,
                        Data = JsonConvert.SerializeObject(new
                        {
                            Email = danglIdentityUser.Email
                        })
                    });
                    await _danglIdentityDbContext.SaveChangesAsync();
                }

                _destinationUserIdBySourceUserId.Add(sourceUser.Id, danglIdentityUser.Id);

                var destinationUser = new Models.ApplicationUser
                {
                    Id = danglIdentityUser.Id,
                    IdenticonId = danglIdentityUser.IdenticonId,
                    Email = danglIdentityUser.Email,
                    NormalizedEmail = danglIdentityUser.NormalizedEmail,
                    EmailConfirmed = danglIdentityUser.EmailConfirmed,
                    UserName = danglIdentityUser.UserName,
                    NormalizedUserName = danglIdentityUser.NormalizedUserName,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };
                _destinationDanglDocuContext
                    .Users
                    .Add(destinationUser);
                await _destinationDanglDocuContext.SaveChangesAsync();

                var existingProjectNotifications = await _sourceDanglDocuContext
                    .UserClaims
                    .Where(u => u.UserId == sourceUser.Id)
                    .Where(c => c.ClaimType == "receive-stable-notifications"
                        || c.ClaimType == "receive-beta-notifications")
                    .ToListAsync();

                foreach (var existingProjectNotification in existingProjectNotifications)
                {
                    var receiveBetaNotifications = existingProjectNotification.ClaimType == "receive-beta-notifications";
                    var projectId = (await _destinationDanglDocuContext
                        .DocumentationProjects
                        .SingleAsync(p => p.Name == existingProjectNotification.ClaimValue)).Id;

                    var notification = await _destinationDanglDocuContext.UserProjectNotifications
                        .FirstOrDefaultAsync(upn => upn.UserId == danglIdentityUser.Id
                            && upn.ProjectId == projectId);
                    if (notification == null)
                    {
                        notification = new UserProjectNotification
                        {
                            ProjectId = projectId,
                            ReceiveBetaNotifications = receiveBetaNotifications,
                            UserId = danglIdentityUser.Id
                        };
                        _destinationDanglDocuContext.UserProjectNotifications.Add(notification);
                    }
                    else if (receiveBetaNotifications)
                    {
                        notification.ReceiveBetaNotifications = true;
                    }

                    await _destinationDanglDocuContext.SaveChangesAsync();
                }
            }
        }

        private static async Task MigrateProjectUserAccessAsync()
        {
            foreach (var sourceProjectUserAccess in (await _sourceDanglDocuContext.UserProjects.ToListAsync()))
            {
                _destinationDanglDocuContext.UserProjects.Add(new Models.UserProjectAccess
                {
                    ProjectId = sourceProjectUserAccess.ProjectId,
                    UserId= _destinationUserIdBySourceUserId[sourceProjectUserAccess.UserId]
                });
            }
            await _destinationDanglDocuContext.SaveChangesAsync();
        }
    }
}
