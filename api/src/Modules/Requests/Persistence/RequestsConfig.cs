using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFrete.Modules.Requests.Domain;

namespace MyFrete.Modules.Requests.Persistence;

internal sealed class TransportRequestConfig : IEntityTypeConfiguration<TransportRequest>
{
    public void Configure(EntityTypeBuilder<TransportRequest> b)
    {
        b.ToTable("transport_request", "requests");
        b.HasKey(x => x.Id);

        b.Ignore(x => x.Items);
        b.Property(x => x.ItemsJson).HasColumnName("items").HasColumnType("jsonb");
        b.Property(x => x.OriginAddress).HasMaxLength(500);
        b.Property(x => x.DestinationAddress).HasMaxLength(500);
        b.Property(x => x.OriginPoint).HasColumnType("geography (point, 4326)");
        b.Property(x => x.DestinationPoint).HasColumnType("geography (point, 4326)");
        b.Property(x => x.DistanceSource).HasMaxLength(30);
        b.Property(x => x.EstimatedPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

        b.HasIndex(x => new { x.ClientId, x.CreatedAt });
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.OriginPoint).HasMethod("gist");
    }
}
