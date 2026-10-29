using GameStore.Api.Data;
using GameStore.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Api.Extensions;

public static class WebApplicationExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.MigrateAsync();

        foreach (var roleName in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        var seedEmail = app.Configuration["SeedAdmin:Email"];
        var seedPassword = app.Configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
        {
            await DbSeeder.SeedAsync(db);
            return;
        }

        var existingUser = await userManager.FindByEmailAsync(seedEmail);

        if (existingUser is null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = seedEmail,
                Email = seedEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, seedPassword);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        await DbSeeder.SeedAsync(db);
    }
}