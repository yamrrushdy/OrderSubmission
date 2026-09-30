using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderSubmission.Api.Contracts;
using OrderSubmission.Api.Endpoints;
using OrderSubmission.Application.Orders;

namespace OrderSubmission.IntegrationTests.Infrastructure;

[Collection(ApiTestGroup.Name)]
public abstract class ApiTestBase : IDisposable
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ApiTestBase(OrderingApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Factory.FaultSimulator.Reset();
    }

    protected OrderingApiFactory Factory { get; }

    protected HttpClient Client { get; }

    protected static string NewKey() => Guid.NewGuid().ToString();

    protected static string NewCustomer() => $"CUST-{Guid.NewGuid():N}"[..20];

    protected static SubmitOrderRequest SampleOrder(string? customer = null) => new(
        customer ?? NewCustomer(),
        [new SubmitOrderLineRequest("SKU-100", 2, 19.99m), new SubmitOrderLineRequest("SKU-200", 1, 5.50m)]);

    protected Task<HttpResponseMessage> SubmitAsync(SubmitOrderRequest request, string? idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(request, options: Json)
        };

        if (idempotencyKey is not null)
        {
            message.Headers.Add(OrderEndpoints.IdempotencyKeyHeader, idempotencyKey);
        }

        return Client.SendAsync(message);
    }

    protected async Task<OrderDetails> GetOrderAsync(Guid id)
    {
        var order = await Client.GetFromJsonAsync<OrderDetails>($"/api/orders/{id}", Json);
        return order!;
    }

    protected static async Task<OrderDetails> ReadOrderAsync(HttpResponseMessage response)
    {
        var order = await response.Content.ReadFromJsonAsync<OrderDetails>(Json);
        return order!;
    }

    public void Dispose()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
    }
}
