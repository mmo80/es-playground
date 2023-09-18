// ReSharper disable ClassNeverInstantiated.Global
namespace WebApi.Event;

public record Order(
    Guid Id, 
    decimal Total, 
    DateTimeOffset CreatedAt)
{
    public bool IsPaid { get; set; }
    public bool IsShipped { get; set; }
    public bool IsCancelled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private List<object> _events = new();

    public static Order Create(OrderCreated evt) 
        => new(evt.OrderId, evt.Total, evt.CreatedAt) { UpdatedAt = evt.CreatedAt };

    public void CompletePayment()
    {
        if (IsPaid || IsShipped) return;
        if (IsCancelled) throw new InvalidOperationException("Order is cancelled");

        _events.Add(new OrderPaid(Id, DateTimeOffset.Now));
    }

    public void Ship()
    {
        if (IsShipped) return;
        if (IsCancelled) throw new InvalidOperationException("Order is cancelled");

        _events.Add(new OrderShipped(Id, DateTimeOffset.Now));
    }

    public void Cancel()
    {
        if (IsCancelled || IsShipped) return;

        _events.Add(new OrderCancelled(Id, DateTimeOffset.Now));
    }

    public IEnumerable<object> GetUncommittedEvents() 
        => _events;

    public Order Apply(OrderPaid evt) 
        => this with { IsPaid = true, UpdatedAt = evt.PaidAt };
    public Order Apply(OrderShipped evt) 
        => this with { IsShipped = true, UpdatedAt = evt.ShippedAt  };
    public Order Apply(OrderCancelled evt) 
        => this with { IsCancelled = true, UpdatedAt = evt.CancelledAt  };
}

public record OrderCreated(
    Guid OrderId, 
    decimal Total,
    DateTimeOffset CreatedAt);
public record OrderPaid(
    Guid OrderId,
    DateTimeOffset PaidAt);
public record OrderShipped(
    Guid OrderId,
    DateTimeOffset ShippedAt);
public record OrderCancelled(
    Guid OrderId,
    DateTimeOffset CancelledAt);

