using System.Net;
using System.Net.Http.Json;
using OrgChart.Core.Models;

namespace OrgChart.IntegrationTests;

/// <summary>
/// Integration tests for employee-related operations.
/// </summary>
public class EmployeeTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public EmployeeTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// Creates a chain of employees with the specified number of levels.
    /// </summary>
    /// <param name="levels">
    /// The levels parameter specifies how many levels deep the employee chain should be.
    /// For example, if levels is 3, the method will create three employees,
    /// each managed by the previous one.</param>
    /// <returns>The last employee created in the chain, or null if no employees were created.</returns>
    private async Task<Employee?> CreateEmployeeChainAsync(int levels)
    {
        Employee? previous = null;
        for (var i = 1; i <= levels; i++)
        {
            var dto = new EmployeeDto($"Employee Level {i}", previous?.Id);
            var response = await _client.PostAsJsonAsync("/api/employee", dto);
            response.EnsureSuccessStatusCode();
            previous = await response.Content.ReadFromJsonAsync<Employee>();
            Assert.NotNull(previous);
        }
        return previous;
    }

    /// <summary>
    /// Tests the creation of an employee and verifies that the created employee is returned.
    /// </summary>
    [Fact]
    public async Task CreateEmployee_ShouldReturnCreatedEmployee()
    {
        // Arrange:
        var dto = new EmployeeDto("Test User", null);

        // Act:
        var response = await _client.PostAsJsonAsync("/api/employee", dto);
        var created = await response.Content.ReadFromJsonAsync<Employee>();

        // Assert:
        Assert.NotNull(created);
        Assert.Equal("Test User", created.Name);
    }

    /// <summary>
    /// Tests the creation of an employee with an invalid manager ID
    /// and verifies that a bad request response is returned.
    /// </summary>
    [Fact]
    public async Task CreateEmployee_WithInvalidManager_ShouldReturnBadRequest()
    {
        // Arrange:
        var dto = new EmployeeDto("Test User", 9999);

        // Act:
        var response = await _client.PostAsJsonAsync("api/employee", dto);

        // Assert:
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests the creation of an employee when the maximum hierarchy depth is exceeded
    /// and verifies that a bad request response is returned.
    /// </summary>
    [Fact]
    public async Task CreateEmployee_WhenMaxDepthExceeded_ShouldReturnBadRequest()
    {
        // Arrange:
        var maxDepth = Core.Constants.Employees.MaxDepth;
        var lastEmployee = await CreateEmployeeChainAsync(maxDepth);
        Assert.NotNull(lastEmployee);

        // Act:
        var exceedingDto = new EmployeeDto("Employee Exceeding Max Depth", lastEmployee!.Id);
        var failResponse = await _client.PostAsJsonAsync("/api/employee", exceedingDto);

        // Assert:
        Assert.False(failResponse.IsSuccessStatusCode, "Expected a failure status code when exceeding max depth");
        var errorContent = await failResponse.Content.ReadAsStringAsync();
        Assert.Contains("Hierarchy depth cannot exceed", errorContent);
    }

    /// <summary>
    /// Tests retrieving an employee by ID and verifies that the correct employee is returned.
    /// </summary>
    [Fact]
    public async Task GetEmployee_ById_ShouldReturnEmployee()
    {
        // Arrange:
        var createDto = new EmployeeDto("Employee For Get", null);
        var createResponse = await _client.PostAsJsonAsync("/api/employee", createDto);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(created);

        // Act:
        var getResponse = await _client.GetAsync($"/api/employee/{created!.Id}");
        getResponse.EnsureSuccessStatusCode();
        var result = await getResponse.Content.ReadFromJsonAsync<EmployeeWithCountDto>();

        // Assert:
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.Name, result.Name);
    }

    /// <summary>
    /// Tests retrieving an employee by a non-existent ID and verifies that a 404 response is returned.
    /// </summary>
    [Fact]
    public async Task GetEmployee_ById_NotFound_ShouldReturn404()
    {
        // Act:
        var response = await _client.GetAsync("/api/employee/99999");

        // Assert:
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Tests updating an employee and verifies that the update is successful.
    /// </summary>
    [Fact]
    public async Task UpdateEmployee_ShouldUpdateSuccessfully()
    {
        // Arrange:
        var createDto = new EmployeeDto("Employee Before Update", null);
        var createResponse = await _client.PostAsJsonAsync("/api/employee", createDto);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(created);

        // Act:
        var updateDto = new EmployeeDto("Employee After Update", null);
        var updateResponse = await _client.PutAsJsonAsync($"/api/employee/{created!.Id}", updateDto);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Assert:
        var getResponse = await _client.GetAsync($"/api/employee/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var updated = await getResponse.Content.ReadFromJsonAsync<EmployeeWithCountDto>();
        Assert.NotNull(updated);
        Assert.Equal("Employee After Update", updated.Name);
    }

    /// <summary>
    /// Tests updating an employee with an invalid manager ID
    /// and verifies that a bad request response is returned.
    /// </summary>
    [Fact]
    public async Task UpdateEmployee_WithInvalidManager_ShouldReturnBadRequest()
    {
        // Arrange:
        var createDto = new EmployeeDto("Employee For Update", null);
        var createResponse = await _client.PostAsJsonAsync("/api/employee", createDto);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(created);

        // Act:
        var updateDto = new EmployeeDto("Employee For Update", 9999);
        var updateResponse = await _client.PutAsJsonAsync($"/api/employee/{created!.Id}", updateDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        var errorMessage = await updateResponse.Content.ReadAsStringAsync();
        Assert.Contains("Manager not found", errorMessage);
    }

    /// <summary>
    /// Tests updating an employee to create a cycle in the hierarchy
    /// and verifies that a bad request response is returned.
    /// </summary>
    [Fact]
    public async Task UpdateEmployee_CycleDetection_ShouldReturnBadRequest()
    {
        // Arrange: create two employees, employee1 and employee2 (employee2 managed by employee1)
        var dto1 = new EmployeeDto("Employee 1", null);
        var response1 = await _client.PostAsJsonAsync("/api/employee", dto1);
        response1.EnsureSuccessStatusCode();
        var employee1 = await response1.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(employee1);

        var dto2 = new EmployeeDto("Employee 2", employee1!.Id);
        var response2 = await _client.PostAsJsonAsync("/api/employee", dto2);
        response2.EnsureSuccessStatusCode();
        var employee2 = await response2.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(employee2);

        // Act: update employee1 to have employee2 as manager (which would create a cycle)
        var updateDto = new EmployeeDto("Employee 1", employee2!.Id);
        var updateResponse = await _client.PutAsJsonAsync($"/api/employee/{employee1.Id}", updateDto);

        // Assert:
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        var errorMessage = await updateResponse.Content.ReadAsStringAsync();
        Assert.Contains("Cycle detected", errorMessage);
    }

    /// <summary>
    /// Tests updating an employee to create a deep cycle in the hierarchy
    /// and verifies that a bad request response is returned.
    /// </summary>
    [Fact]
    public async Task UpdateEmployee_DeepCycleDetection_ShouldReturnBadRequest()
    {
        // Arrange: create a chain of three employees:
        // emp1 -> emp2 -> emp3
        var dto1 = new EmployeeDto("Employee 1", null);
        var response1 = await _client.PostAsJsonAsync("/api/employee", dto1);
        response1.EnsureSuccessStatusCode();
        var emp1 = await response1.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(emp1);

        var dto2 = new EmployeeDto("Employee 2", emp1!.Id);
        var response2 = await _client.PostAsJsonAsync("/api/employee", dto2);
        response2.EnsureSuccessStatusCode();
        var emp2 = await response2.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(emp2);

        var dto3 = new EmployeeDto("Employee 3", emp2!.Id);
        var response3 = await _client.PostAsJsonAsync("/api/employee", dto3);
        response3.EnsureSuccessStatusCode();
        var emp3 = await response3.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(emp3);

        // Act: attempt to update emp1 so that its manager is emp3,
        // creating a cycle: emp1 -> emp2 -> emp3 -> emp1.
        var updateDto = new EmployeeDto("Employee 1 Updated", emp3!.Id);
        var updateResponse = await _client.PutAsJsonAsync($"/api/employee/{emp1.Id}", updateDto);

        // Assert: update should fail due to cycle detection.
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        var errorMessage = await updateResponse.Content.ReadAsStringAsync();
        Assert.Contains("Cycle detected", errorMessage);
    }

    /// <summary>
    /// Tests deleting an employee and verifies that the deletion is successful.
    /// </summary>
    [Fact]
    public async Task DeleteEmployee_ShouldDeleteSuccessfully()
    {
        // Arrange:
        var createDto = new EmployeeDto("Employee To Delete", null);
        var createResponse = await _client.PostAsJsonAsync("/api/employee", createDto);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(created);

        // Act:
        var deleteResponse = await _client.DeleteAsync($"/api/employee/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Assert:
        var getResponse = await _client.GetAsync($"/api/employee/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    /// <summary>
    /// Tests deleting an employee who has subordinates and verifies that
    /// all subordinates are reassigned to the deleted employee's manager.
    /// </summary>
    [Fact]
    public async Task DeleteEmployee_WithSubordinates_ShouldReassignSubordinates()
    {
        // Arrange:
        // Create a manager (Manager A)
        var dtoA = new EmployeeDto("Manager A", null);
        var responseA = await _client.PostAsJsonAsync("/api/employee", dtoA);
        responseA.EnsureSuccessStatusCode();
        var managerA = await responseA.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(managerA);

        // Create an employee (Employee B) with Manager A as its manager.
        var dtoB = new EmployeeDto("Employee B", managerA!.Id);
        var responseB = await _client.PostAsJsonAsync("/api/employee", dtoB);
        responseB.EnsureSuccessStatusCode();
        var employeeB = await responseB.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(employeeB);

        // Create another employee (Employee C) with Employee B as its manager.
        var dtoC = new EmployeeDto("Employee C", employeeB!.Id);
        var responseC = await _client.PostAsJsonAsync("/api/employee", dtoC);
        responseC.EnsureSuccessStatusCode();
        var employeeC = await responseC.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(employeeC);

        // Act:
        // Delete Employee B.
        var deleteResponse = await _client.DeleteAsync($"/api/employee/{employeeB.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Assert:
        // Retrieve Employee C and verify that its manager is now Manager A.
        var getResponse = await _client.GetAsync($"/api/employee/{employeeC.Id}");
        getResponse.EnsureSuccessStatusCode();
        var updatedEmployeeC = await getResponse.Content.ReadFromJsonAsync<EmployeeWithCountDto>();
        Assert.NotNull(updatedEmployeeC);
        Assert.Equal(managerA.Id, updatedEmployeeC.ManagerId);
    }

    /// <summary>
    /// Tests deleting a non-existent employee and verifies that a 404 response is returned.
    /// </summary>
    [Fact]
    public async Task DeleteEmployee_NonExistent_ShouldReturnNotFound()
    {
        // Act: attempt to delete an employee that doesn't exist
        var deleteResponse = await _client.DeleteAsync("/api/employee/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }
}
