using Microsoft.EntityFrameworkCore;
using OrgChart.Core.Interfaces;
using OrgChart.Core.Models;

namespace OrgChart.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly OrgChartDbContext _context;
    public EmployeeRepository(OrgChartDbContext context)
    {
        _context = context;
    }

    public async Task<Employee?> GetByIdOrDefault(int id)
    {
        return await _context.Employees
            .Include(e => e.Subordinates)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<Employee>> GetAll()
    {
        return await _context.Employees.ToListAsync();
    }

    public async Task<Employee> AddEmployee(Employee employee)
    {
        await _context.AddAsync(employee);
        return employee;
    }

    public Task UpdateEmployee(Employee employee)
    {
        _context.Update(employee);
        return _context.SaveChangesAsync();
    }

    public Task DeleteEmployee(Employee employee)
    {
        _context.Remove(employee);
        return _context.SaveChangesAsync();
    }

    public Task<int> GetSubordinateCount(int employeeId)
    {
        return GetSubordinateCountAsync(employeeId);
    }

    public async Task<int> GetHierarchyDepth(int employeeId)
    {
        var hierarchyDepth = 1;
        var employee = await GetByIdOrDefault(employeeId);

        while (employee?.ManagerId != null)
        {
            hierarchyDepth++;
            employee = await GetByIdOrDefault(employee.ManagerId.Value);
        }

        return hierarchyDepth;
    }

    public async Task<bool> HasCycle(int employeeId, int newManagerId)
    {
        if (newManagerId == employeeId)
        {
            return true;
        }

        var employee = await GetByIdOrDefault(newManagerId);
        while (employee?.ManagerId != null)
        {
            if (employee.ManagerId == employeeId)
            {
                return true;
            }
            employee = await GetByIdOrDefault(employee.ManagerId.Value);
        }

        return false;
    }

    private async Task<int> GetSubordinateCountAsync(int employeeId, int currentHierarchyLevel = 0)
    {
        var suboridnateCount = 0;
        var employee = await GetByIdOrDefault(employeeId);

        if (employee != null)
        {
            foreach (var subordinate in employee.Subordinates)
            {
                suboridnateCount += await GetSubordinateCountAsync(subordinate.Id, currentHierarchyLevel + 1);
            }
        }

        return suboridnateCount;
    }
}