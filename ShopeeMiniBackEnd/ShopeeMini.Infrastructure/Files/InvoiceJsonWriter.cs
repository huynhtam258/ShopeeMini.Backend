using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Infrastructure.Files;

public class InvoiceJsonWriter : IInvoiceJsonWriter
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public InvoiceJsonWriter(IConfiguration configuration)
    {
        _directory = configuration["InvoiceStorage:Directory"] ?? "App_Data/invoices";
        Directory.CreateDirectory(_directory);
    }

    public async Task<string> WriteInvoiceAsync(Order order, CancellationToken cancellationToken = default)
    {
        var snapshot = new
        {
            order.OrderCode,
            order.UserId,
            order.TotalAmount,
            Status = order.Status.ToString(),
            order.CreatedAt,
            Items = order.Items.Select(i => new
            {
                i.ProductSkuId,
                i.ProductNameSnapshot,
                i.VariantNameSnapshot,
                i.UnitPrice,
                i.DiscountPercentApplied,
                i.Quantity,
                i.LineTotal
            })
        };

        var fileName = $"{order.OrderCode}.json";
        var path = Path.Combine(_directory, fileName);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);

        return path;
    }
}
