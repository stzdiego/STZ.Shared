using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace STZ.Shared.Bases;
using Microsoft.EntityFrameworkCore;

public class DbContextBase : DbContext
{
    private readonly IConfiguration _configuration;
    private readonly ILogger? _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    
    public DbContextBase(IConfiguration configuration, ILogger? logger = null, IHttpContextAccessor? httpContextAccessor = null)
    {
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var migrationAssemblyName = _configuration["MigrationAssembly"];
            optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly(migrationAssemblyName));
        }
    }

    public override int SaveChanges()
    {
        try
        {
            ApplyAuditInformation();
            return base.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving changes to the database.");
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            ApplyAuditInformation();
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving changes to the database.");
            throw;
        }
    }

    private void ApplyAuditInformation()
    {
        Guid? parsedUserId = null;

        if (_httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirst("sub")?.Value;
            if (Guid.TryParse(userId, out var extractedUserId))
            {
                parsedUserId = extractedUserId;
            }
        }

        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.Entity.GetType().IsSubclassOf(typeof(AuditBase<>)) && (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)))
        {
            dynamic entity = entry.Entity;
            var now = DateTime.UtcNow;

            switch (entry.State)
            {
                case EntityState.Added:
                    entity.CreatedAt = now;
                    entity.CreatedBy = parsedUserId ?? Guid.Empty;
                    entity.IsDeleted = false;
                    break;

                case EntityState.Modified:
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = parsedUserId;
                    // Evitar modificar las fechas de creación y eliminación
                    entry.Property("CreatedAt").IsModified = false;
                    entry.Property("CreatedBy").IsModified = false;
                    entry.Property("DeletedAt").IsModified = false;
                    entry.Property("DeletedBy").IsModified = false;
                    break;

                case EntityState.Deleted:
                    entity.DeletedAt = now;
                    entity.DeletedBy = parsedUserId;
                    entity.IsDeleted = true;
                    entry.State = EntityState.Modified; // Soft delete
                    break;
            }
        }
    }
}