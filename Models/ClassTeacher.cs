using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FINAPSA.Models
{
    public class ClassTeacher
    {
        public int Id { get; set; }

        // ── Class FK ─────────────────────────────────
        [Required]
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public Class? Class { get; set; }

        // ── Staff FK ─────────────────────────────────
        [Required]
        public int StaffId { get; set; }

        [ForeignKey("StaffId")]
        public Staff? Staff { get; set; }

        // ── Assignment details ────────────────────────
        [StringLength(100)]
        public string? Subject { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Set when IsActive is flipped to false (required by ClassService)
        public DateTime? UnassignedAt { get; set; }
    }
}