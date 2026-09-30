using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderSubmission.Application.Abstractions;

namespace OrderSubmission.Infrastructure.Persistence;

internal sealed class UnitOfWork(OrderingDbContext context) : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.ChangeTracker.Clear();
            throw new DuplicateKeyException("A row with the same unique key already exists.", exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
}
