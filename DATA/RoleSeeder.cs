using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using FINAPSA.Models;

public static class RoleSeeder
{
    private static readonly string[] Roles = { "Admin", "Bursar", "Teacher", "Student" };

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    public static async Task SeedAdminUserAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<User>>();

        string adminEmail = "admin@schoolportal.com";
        string adminPassword = "Admin@123";
        string adminUserName = "ADMIN";
        string adminFullName = "ADMIN";

        // Check if admin user exists by username first, then by email
        var adminUser = await userManager.FindByNameAsync(adminUserName);
        if (adminUser == null)
            adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new User
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = adminFullName
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create admin user: {string.Join(", ", result.Errors)}");
            }
        }
        else
        {
            // Ensure username and fullname are set as expected for the ADMIN shortcut
            var update = false;
            if (adminUser.UserName != adminUserName)
            {
                adminUser.UserName = adminUserName;
                update = true;
            }
            if (adminUser.FullName != adminFullName)
            {
                adminUser.FullName = adminFullName;
                update = true;
            }
            if (update)
            {
                await userManager.UpdateAsync(adminUser);
            }
        }

        // Assign Admin role if not already assigned
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}
