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

    public static Order Create(OrderCreated evt) 
        => new(evt.OrderId, evt.Total, evt.CreatedAt) { UpdatedAt = evt.CreatedAt };

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

