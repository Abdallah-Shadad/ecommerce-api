using ECommerce.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Infrastructure.Persistence;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // initialize roles
        string[] roles = ["Admin", "Customer"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // main admin user
        var adminEmail = "abdallah.shadad@ecommerce.com";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Abdallah Shadad",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123@");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // custmoers 
        var dummyCustomers = new List<(string Email, string FullName)>
        {
            ("adel.emam@ecommerce.com", "Adel Emam"),
            ("ahmed.helmy@ecommerce.com", "Ahmed Helmy"),
            ("karim.abdelaziz@ecommerce.com", "Karim Abdel Aziz"),
            ("ahmed.elsakka@ecommerce.com", "Ahmed El Sakka"),
            ("mona.zaki@ecommerce.com", "Mona Zaki"),
            ("menna.shalaby@ecommerce.com", "Menna Shalaby"),
            ("mahmoud.abdelaziz@ecommerce.com", "Mahmoud Abdel Aziz"),
            ("nour.elsherif@ecommerce.com", "Nour El Sherif"),
            ("ahmed.ezz@ecommerce.com", "Ahmed Ezz"),
            ("amr.waked@ecommerce.com", "Amr Waked"),
            ("hend.sabry@ecommerce.com", "Hend Sabry"),
            ("yousra@ecommerce.com", "Yousra"),
            ("nelly.karim@ecommerce.com", "Nelly Karim"),
            ("mohamed.henedy@ecommerce.com", "Mohamed Henedy"),
            ("hany.ramzy@ecommerce.com", "Hany Ramzy"),
            ("tamer.hosny@ecommerce.com", "Tamer Hosny"),
            ("amr.youssef@ecommerce.com", "Amr Youssef"),
            ("assaad.fadda@ecommerce.com", "Assaad Younis"),
            ("mohamed.mamdouh@ecommerce.com", "Mohamed Mamdouh"),
            ("amina.khalil@ecommerce.com", "Amina Khalil"),
            ("dorra.zarrouk@ecommerce.com", "Dorra Zarrouk"),
            ("youssef.elcherif@ecommerce.com", "Youssef El Sherif"),
            ("khaled.elsawy@ecommerce.com", "Khaled El Sawy"),
            ("maged.elkedwany@ecommerce.com", "Maged El Kedwany"),
            ("bassem.samra@ecommerce.com", "Bassem Samra"),
            ("eyad.nassar@ecommerce.com", "Eyad Nassar"),
            ("ruby@ecommerce.com", "Ruby"),
            ("ghada.abdelrazek@ecommerce.com", "Ghada Abdel Razek")
        };

        foreach (var (email, fullName) in dummyCustomers)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser == null)
            {
                var customer = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(customer, "Customer123@");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(customer, "Customer");
                }
            }
        }
    }
}