using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Application.Interfaces;

/// <summary>
/// Writes an immutable JSON snapshot of a finalized order to disk (demonstrates combining
/// file-system storage with the relational database — the DB stays the source of truth,
/// the JSON file is a denormalized, human-readable export for support/audit purposes).
/// </summary>
public interface IInvoiceJsonWriter
{
    Task<string> WriteInvoiceAsync(Order order, CancellationToken cancellationToken = default);
}
