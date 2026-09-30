using Microsoft.EntityFrameworkCore;
using OrderSubmission.Application.Abstractions;
using OrderSubmission.Application.Idempotency;

namespace OrderSubmission.Infrastructure.Persistence.Repositories;

internal sealed class IdempotencyRepository(OrderingDbContext context) : IIdempotencyRepository
{
    public Task<IdempotencyRecord?> FindAsync(IdempotencyKey key, CancellationToken cancellationToken) =>
        context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.Key == key, cancellationToken);

    public void Add(IdempotencyRecord record) => context.IdempotencyRecords.Add(record);
}
