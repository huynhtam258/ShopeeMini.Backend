using ShopeeMini.Domain.Common;

namespace ShopeeMini.Domain.Entities;

/// <summary>
/// Stores the first response produced for a given (User, Idempotency-Key, Endpoint) triple,
/// so a retried request replays the original result instead of re-executing the side effect.
/// </summary>
public class IdempotencyRecord : BaseEntity
{
    public long UserId { get; set; }
    public string Key { get; set; } = default!;
    public string RequestPath { get; set; } = default!;
    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = default!;
}
