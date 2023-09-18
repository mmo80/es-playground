using Marten.Events.Aggregation;

namespace WebApi.Event;

public class OrderModelProjection : SingleStreamProjection<OrderModel>
{
    public static OrderModel Create(OrderCreated evt) 
        => new(evt.OrderId, evt.Total, OrderStatus.Created, evt.CreatedAt);

    public OrderModel Apply(OrderPaid evt, OrderModel current) 
        => current with { Status = OrderStatus.Paid, UpdatedAt = evt.PaidAt };
    public OrderModel Apply(OrderShipped evt, OrderModel current) 
        => current with { Status = OrderStatus.Shipped, UpdatedAt = evt.ShippedAt  };
    public OrderModel Apply(OrderCancelled evt, OrderModel current) 
        => current with { Status = OrderStatus.Cancelled, UpdatedAt = evt.CancelledAt  };
}


public record OrderModel(
    Guid Id, 
    decimal Total,
    OrderStatus Status,
    DateTimeOffset UpdatedAt,
    int Version = 1);

public enum OrderStatus
{
    Created,
    Paid,
    Shipped,
    Cancelled
}