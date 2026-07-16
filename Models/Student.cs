using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FINAPSA.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string? UserId { get; set; } 
        public User? User { get; set; }
        public string? FullName { get; set; }
        public string? AdmissionNumber { get; set; }
        
        // Keep for backward compatibility
        public string? Class { get; set; }
        
        // New property for Class foreign key
        public int? ClassId { get; set; }

        [ForeignKey("ClassId")]
        public Class? ClassRef { get; set; }
        
        public decimal Balance { get; set; }

        public ICollection<Payment>? Payments { get; set; }
    }
}
