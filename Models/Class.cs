using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FINAPSA.Models
{
    public class Class
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ClassName { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        // Timestamps — required by ClassService
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Non-nullable collections fix the CS8620 ThenInclude warnings
        public ICollection<ClassTeacher> ClassTeachers { get; set; }
            = new List<ClassTeacher>();

        public ICollection<Student> Students { get; set; }
            = new List<Student>();
    }
}