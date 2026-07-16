using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FINAPSA.Data;
using FINAPSA.Models;

namespace FINAPSA.Services
{
    public interface IClassService
    {
        Task<List<Class>> GetAllClassesAsync();
        Task<Class?> GetClassByIdAsync(int classId);
        Task<Class> CreateClassAsync(string className, string? description = null);
        Task<Class> UpdateClassAsync(int classId, string className, string? description = null);
        Task<bool> DeleteClassAsync(int classId);
        Task<List<Staff>> GetAvailableTeachersAsync();
        Task<List<ClassTeacher>> GetClassTeachersAsync(int classId);
        Task<ClassTeacher> AssignTeacherToClassAsync(int classId, int staffId, string? subject = null);
        Task<bool> UnassignTeacherFromClassAsync(int classTeacherId);
        Task<ClassTeacher?> GetActiveTeacherForClassAsync(int classId);
    }

    public class ClassService : IClassService
    {
        private readonly FINAPSADbContext _context;

        public ClassService(FINAPSADbContext context)
        {
            _context = context;
        }

        public async Task<List<Class>> GetAllClassesAsync()
        {
            return await _context.Classes
                .Include(c => c.ClassTeachers)
                .ThenInclude(ct => ct.Staff)
                .OrderBy(c => c.ClassName)
                .ToListAsync();
        }

        public async Task<Class?> GetClassByIdAsync(int classId)
        {
            return await _context.Classes
                .Include(c => c.ClassTeachers)
                .ThenInclude(ct => ct.Staff)
                .Include(c => c.Students)
                .FirstOrDefaultAsync(c => c.Id == classId);
        }

        public async Task<Class> CreateClassAsync(string className, string? description = null)
        {
            var classEntity = new Class
            {
                ClassName = className,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            _context.Classes.Add(classEntity);
            await _context.SaveChangesAsync();
            return classEntity;
        }

        public async Task<Class> UpdateClassAsync(int classId, string className, string? description = null)
        {
            var classEntity = await _context.Classes.FindAsync(classId);
            if (classEntity == null)
                throw new InvalidOperationException($"Class with ID {classId} not found");

            classEntity.ClassName = className;
            classEntity.Description = description;
            classEntity.UpdatedAt = DateTime.UtcNow;

            _context.Classes.Update(classEntity);
            await _context.SaveChangesAsync();
            return classEntity;
        }

        public async Task<bool> DeleteClassAsync(int classId)
        {
            var classEntity = await _context.Classes.FindAsync(classId);
            if (classEntity == null)
                return false;

            _context.Classes.Remove(classEntity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Staff>> GetAvailableTeachersAsync()
        {
            // Get all staff members (assuming teachers are staff with specific roles)
            return await _context.Staffs
                .OrderBy(s => s.FullName)
                .ToListAsync();
        }

        public async Task<List<ClassTeacher>> GetClassTeachersAsync(int classId)
        {
            return await _context.ClassTeachers
                .Where(ct => ct.ClassId == classId)
                .Include(ct => ct.Staff)
                .OrderByDescending(ct => ct.AssignedAt)
                .ToListAsync();
        }

        public async Task<ClassTeacher> AssignTeacherToClassAsync(int classId, int staffId, string? subject = null)
        {
            // Check if assignment already exists
            var existingAssignment = await _context.ClassTeachers
                .FirstOrDefaultAsync(ct => ct.ClassId == classId && ct.StaffId == staffId && ct.IsActive);

            if (existingAssignment != null)
                return existingAssignment;

            var assignment = new ClassTeacher
            {
                ClassId = classId,
                StaffId = staffId,
                Subject = subject,
                AssignedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.ClassTeachers.Add(assignment);
            await _context.SaveChangesAsync();
            return assignment;
        }

        public async Task<bool> UnassignTeacherFromClassAsync(int classTeacherId)
        {
            var assignment = await _context.ClassTeachers.FindAsync(classTeacherId);
            if (assignment == null)
                return false;

            assignment.IsActive = false;
            assignment.UnassignedAt = DateTime.UtcNow;

            _context.ClassTeachers.Update(assignment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ClassTeacher?> GetActiveTeacherForClassAsync(int classId)
        {
            return await _context.ClassTeachers
                .Where(ct => ct.ClassId == classId && ct.IsActive)
                .Include(ct => ct.Staff)
                .FirstOrDefaultAsync();
        }
    }
}
