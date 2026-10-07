using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Application.Common;

public class IdempotencyService : IIdempotencyService
{
    private readonly IAppDbContext _db;

    public IdempotencyService(IAppDbContext db) => _db = db;

    public async Task<CachedIdempotentResponse?> TryGetAsync(long userId, string key, string requestPath, CancellationToken ct = default)
    {
        var record = await _db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key && r.RequestPath == requestPath, ct);

        return record is null ? null : new CachedIdempotentResponse(record.ResponseStatusCode, record.ResponseBody);
    }

    public async Task SaveAsync(long userId, string key, string requestPath, int statusCode, string body, CancellationToken ct = default)
    {
        var exists = await _db.IdempotencyRecords
            .AnyAsync(r => r.UserId == userId && r.Key == key && r.RequestPath == requestPath, ct);
        if (exists)
        {
            return;
        }

        _db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            UserId = userId,
            Key = key,
            RequestPath = requestPath,
            ResponseStatusCode = statusCode,
            ResponseBody = body
        });

        await _db.SaveChangesAsync(ct);
    }
}
