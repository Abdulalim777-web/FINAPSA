using FINAPSA.Data;
using FINAPSA.Models;
using Microsoft.EntityFrameworkCore;

namespace FINAPSA.Data
{
    public static class ClassSeeder
    {
        // These are the fixed classes for FINAPSA.
        // They are seeded once on startup and never created through the UI.
        private static readonly List<(string Name, string Description)> FixedClasses = new()
        {
            ("Creche",        "Early care level"),
            ("Playgroup",     "Early care level"),
            ("Kindergarten",  "Kindergarten level"),
            ("Nursery",       "Nursery level"),
            ("Basic 1",       "Primary level"),
            ("Basic 2",       "Primary level"),
            ("Basic 3",       "Primary level"),
            ("Basic 4",       "Primary level"),
            ("Basic 5",       "Primary level"),
            ("Basic 6",       "Primary level"),
        };

        /// <summary>
        /// Seeds the fixed classes if they don't already exist.
        /// Call this from Program.cs on startup.
        /// </summary>
        public static async Task SeedClassesAsync(FINAPSADbContext db)
        {
            foreach (var (name, description) in FixedClasses)
            {
                // Only add if not already in the database
                var exists = await db.Classes
                    .AnyAsync(c => c.ClassName.ToLower() == name.ToLower());

                if (!exists)
                {
                    db.Classes.Add(new Class
                    {
                        ClassName = name,
                        Description = description
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}