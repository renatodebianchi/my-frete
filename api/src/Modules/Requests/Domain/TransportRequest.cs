using System.Text.Json;
using NetTopologySuite.Geometries;

namespace MyFrete.Modules.Requests.Domain;

public enum RequestStatus
{
    Draft = 0,
    Searching = 1,
    Hired = 2,
    AwaitingScheduleDecision = 3,
    ScheduledSearching = 4,
    Scheduled = 5,
    Completed = 6,
    Unfulfilled = 7,
    Cancelled = 8,
}

public enum RequestKind
{
    Immediate = 0,
    Scheduled = 1,
}

public sealed record RequestItem(string Description, int Quantity);

public static class RequestStatusExtensions
{
    public static string ToWire(this RequestStatus s) => s switch
    {
        RequestStatus.AwaitingScheduleDecision => "awaiting_schedule_decision",
        RequestStatus.ScheduledSearching => "scheduled_searching",
        _ => s.ToString().ToLowerInvariant(),
    };
}

public sealed class TransportRequest
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid ClientId { get; init; }

    public string ItemsJson { get; private set; } = "[]";

    public IReadOnlyList<RequestItem> Items =>
        JsonSerializer.Deserialize<List<RequestItem>>(ItemsJson) ?? [];

    public int EstimatedWeightGrams { get; init; }

    public required string OriginAddress { get; init; }

    public Point OriginPoint { get; init; } = default!;

    public required string DestinationAddress { get; init; }

    public Point DestinationPoint { get; init; } = default!;

    public double DistanceMeters { get; set; }

    public string DistanceSource { get; set; } = "geodesic_fallback";

    public decimal EstimatedPrice { get; set; }

    public string Currency { get; set; } = "BRL";

    public Guid PricingRuleId { get; set; }

    public RequestKind Kind { get; private set; } = RequestKind.Immediate;

    public DateOnly? ScheduledDate { get; private set; }

    public RequestStatus Status { get; private set; } = RequestStatus.Draft;

    public Guid? AssignedProfessionalId { get; private set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public void SetItems(IEnumerable<RequestItem> items) =>
        ItemsJson = JsonSerializer.Serialize(items.ToList());

    public void ConfirmImmediate(DateTimeOffset now)
    {
        Status = RequestStatus.Searching;
        UpdatedAt = now;
    }

    public void MarkExhausted(DateTimeOffset now)
    {
        if (Status is RequestStatus.Searching)
        {
            Status = RequestStatus.AwaitingScheduleDecision;
            UpdatedAt = now;
        }
    }

    public void Assign(Guid professionalId, DateTimeOffset now)
    {
        AssignedProfessionalId = professionalId;
        Status = Kind == RequestKind.Scheduled ? RequestStatus.Scheduled : RequestStatus.Hired;
        UpdatedAt = now;
    }

    public bool TryCancel(DateTimeOffset now)
    {
        if (Status is RequestStatus.Completed or RequestStatus.Cancelled or RequestStatus.Unfulfilled)
        {
            return false;
        }

        Status = RequestStatus.Cancelled;
        UpdatedAt = now;
        return true;
    }

    public void ChooseSchedule(DateOnly date, DateTimeOffset now)
    {
        Kind = RequestKind.Scheduled;
        ScheduledDate = date;
        Status = RequestStatus.ScheduledSearching;
        UpdatedAt = now;
    }

    public void MarkUnfulfilled(DateTimeOffset now)
    {
        Status = RequestStatus.Unfulfilled;
        UpdatedAt = now;
    }

    public void MarkCompleted(DateTimeOffset now)
    {
        Status = RequestStatus.Completed;
        UpdatedAt = now;
    }
}
