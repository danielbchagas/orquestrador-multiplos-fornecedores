using Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Events;
using Wolverine;

namespace Supplier.Ingestion.Orchestrator.WolverineApi.Infrastructure.Handlers;

public class SupplierAInputReceivedHandler
{
    public static Task Handle(
        SupplierAInputReceived message,
        SupplierInputProcessor processor,
        IMessageBus bus,
        CancellationToken cancellationToken)
        => processor.ProcessAsync(message, bus, cancellationToken);
}
