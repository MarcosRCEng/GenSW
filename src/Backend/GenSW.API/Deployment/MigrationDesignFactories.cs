using GenSW.Infrastructure.Catalog;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GenSW.API.Deployment;

// SQL scripts can be reviewed without supplying a live connection.
public sealed class ErpMigrationDesignFactory : IDesignTimeDbContextFactory<GenSWDbContext>
{
    public GenSWDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GenSWDbContext>().UseNpgsql().Options);
}

public sealed class CatalogMigrationDesignFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql().Options);
}
