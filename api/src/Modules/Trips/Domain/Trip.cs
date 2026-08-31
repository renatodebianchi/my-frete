using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyFrete.Modules.Trips.Domain;

public enum TripStatus
{
    Contratada = 0,
    EmAndamento = 1,
    Entregue = 2,
    Confirmada = 3,
    Contestada = 4,
    Cancelada = 5,
}

public enum ClientDeliveryResponse
{
    Confirmada = 0,
    Contestada = 1,
}

public sealed class Trip
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid RequestId { get; init; }

    public Guid ClientId { get; init; }

    public Guid ProfessionalId { get; init; }

    public TripStatus Status { get; private set; } = TripStatus.Contratada;

    public decimal AgreedAmount { get; private set; }

    public string Currency { get; init; } = "BRL";

    public DateTimeOffset? DeliveredAt { get; private set; }

    public ClientDeliveryResponse? ClientResponse { get; private set; }

    public DateTimeOffset? ClientRespondedAt { get; private set; }

    public DateTimeOffset? VerificationNotifiedAt { get; set; }

    public bool PaymentSettledOutsideApp { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsActive => Status is TripStatus.Contratada or TripStatus.EmAndamento;

    public bool TrySetAgreedAmount(decimal amount, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return false;
        }

        AgreedAmount = amount;
        UpdatedAt = now;
        return true;
    }

    public bool TryDeliver(DateTimeOffset now)
    {
        if (Status is not (TripStatus.Contratada or TripStatus.EmAndamento))
        {
            return false;
        }

        Status = TripStatus.Entregue;
        DeliveredAt = now;
        UpdatedAt = now;
        return true;
    }

    public bool TryClientRespond(ClientDeliveryResponse response, DateTimeOffset now)
    {
        if (Status is not TripStatus.Entregue)
        {
            return false;
        }

        ClientResponse = response;
        ClientRespondedAt = now;
        Status = response == ClientDeliveryResponse.Confirmada ? TripStatus.Confirmada : TripStatus.Contestada;
        UpdatedAt = now;
        return true;
    }

    public bool TryCancel(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return false;
        }

        Status = TripStatus.Cancelada;
        UpdatedAt = now;
        return true;
    }

    public void MarkSettledOutsideApp(DateTimeOffset now)
    {
        PaymentSettledOutsideApp = true;
        SettledAt = now;
        UpdatedAt = now;
    }

    public static Trip Create(Guid requestId, Guid clientId, Guid professionalId, decimal amount, string currency, DateTimeOffset now) => new()
    {
        RequestId = requestId,
        ClientId = clientId,
        ProfessionalId = professionalId,
        AgreedAmount = amount,
        Currency = currency,
        CreatedAt = now,
        UpdatedAt = now,
    };
}

internal sealed class TripConfig : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> b)
    {
        b.ToTable("trip", "trips");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => new { x.ProfessionalId, x.Status });
        b.HasIndex(x => new { x.ClientId, x.CreatedAt });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ClientResponse).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AgreedAmount).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).HasMaxLength(3);
    }
}
