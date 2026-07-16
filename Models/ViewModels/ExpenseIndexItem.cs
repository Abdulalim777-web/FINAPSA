using System;

namespace FINAPSA.Models.ViewModels
{
    public class ExpenseIndexItem
    {
        public int? ExpenseId { get; set; }
        public int? SalaryId { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string? Note { get; set; }
        public string? ReceiptPath { get; set; }
        public bool IsSalary { get; set; }
        public bool IsPaidSalary { get; set; }
        public string? StaffName { get; set; }
    }
}
