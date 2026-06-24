using AutoMapper;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Migrations;

namespace ChatBoxPRJ.Business.Services;

public sealed class ApplicationInitializer(
    DatabaseMigrationManager migrationManager,
    DatabaseSeeder seeder,
    IPasswordHasher passwordHasher,
    IMapper mapper) : IApplicationInitializer
{
    public async Task InitializeAsync(
        string adminCode,
        string adminEmail,
        string adminPassword,
        CancellationToken ct = default)
    {
        mapper.ConfigurationProvider.AssertConfigurationIsValid();
        await migrationManager.MigrateAsync(ct);
        await seeder.SeedAsync(adminCode, adminEmail, passwordHasher.Hash(adminPassword), ct);
    }
}
