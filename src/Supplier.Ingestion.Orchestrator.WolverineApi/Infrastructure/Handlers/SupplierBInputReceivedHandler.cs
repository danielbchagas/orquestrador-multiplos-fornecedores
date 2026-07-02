using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;
using Wolverine;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Handlers;

public class SupplierBInputReceivedHandler
{
    public static Task Handle(
        SupplierBInputReceived message,
        SupplierInputProcessor processor,
        IMessageBus bus,
        CancellationToken cancellationToken)
        => processor.ProcessAsync(message, bus, cancellationToken);
}
