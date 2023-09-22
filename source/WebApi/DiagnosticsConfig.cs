using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WebApi;

public static class DiagnosticsConfig
{
    public const string ServiceName = "es-otel-bff";
    public static Meter Meter = new(ServiceName);

    public static Counter<long> CustomerCount = Meter.CreateCounter<long>(DiagnosticsNames.CustomerCount, "The number of customers");
    public static Counter<long> OrderCount = Meter.CreateCounter<long>(DiagnosticsNames.OrderCount, "The number of orders");

    public static ActivitySource ActivitySource = new(ServiceName);

    public static void AddOrderMetrics() => OrderCount.Add(1);
    public static void AddCustomerMetrics() => CustomerCount.Add(1);
}



public static class DiagnosticsNames
{
    public const string CustomerCount = "customer.count";
    public const string OrderCount = "order.count";

    public const string CreateOrderMarkupName = "Create Order With Markup";
    public const string OrderId = "order.id";
}