namespace ShopeeMini.Application.Interfaces;

public record CachedIdempotentResponse(int StatusCode, string Body);

/// <summary>
/// Backs the Idempotency-Key mechanism for unsafe, side-effecting endpoints (e.g. checkout).
/// Without it, a client retry after a dropped response (timeout, connection reset) resends the
/// same POST and the server has no way to tell "retry of an already-applied request" apart from
/// "a brand new request" — the effect (stock decrement, order creation) would happen twice.
/// </summary>
public interface IIdempotencyService
{
    Task<CachedIdempotentResponse?> TryGetAsync(long userId, string key, string requestPath, CancellationToken ct = default);
    Task SaveAsync(long userId, string key, string requestPath, int statusCode, string body, CancellationToken ct = default);
}
