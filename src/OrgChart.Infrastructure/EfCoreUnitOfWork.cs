using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using OrgChart.Core.Interfaces;

namespace OrgChart.Infrastructure;

public class EfCoreUnitOfWork : IUnitOfWork
{
    private readonly OrgChartDbContext _context;
    private IDbContextTransaction? _transaction;
    private readonly IHostEnvironment _environment;

    public EfCoreUnitOfWork(OrgChartDbContext context, IHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task BeginTransaction()
    {
        if (_environment.IsEnvironment("Testing")) return;

        if (_transaction is not null)
            return;

        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task Commit()
    {
        try
        {
            await _context.SaveChangesAsync();

            if (_environment.IsEnvironment("Testing")) return;

            if (_transaction is null)
                throw new InvalidOperationException("Transaction not started or already committed/rolled back.");

            await _transaction.CommitAsync();
        }
        finally
        {
            if (_transaction != null && !_environment.IsEnvironment("Testing"))
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task Rollback()
    {
        if (_environment.IsEnvironment("Testing")) return;

        if (_transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public void Dispose()
    {
        if (!_environment.IsEnvironment("Testing"))
        {
            _transaction?.Dispose();
        }
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
