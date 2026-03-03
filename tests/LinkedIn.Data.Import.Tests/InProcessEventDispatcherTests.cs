using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Unit tests verifying that <see cref="InProcessEventDispatcher"/> routes
/// events to all registered handlers.
/// </summary>
public sealed class InProcessEventDispatcherTests
{
    private sealed record PingEvent(string Message) : IDomainEvent;
    private sealed record PongEvent(int Value) : IDomainEvent;

    [Fact]
    public async Task PublishAsync_InvokesRegisteredHandler()
    {
        var dispatcher = new InProcessEventDispatcher();
        PingEvent? received = null;
        dispatcher.Register<PingEvent>((evt, _) => { received = evt; return Task.CompletedTask; });

        await dispatcher.PublishAsync(new PingEvent("hello"));

        Assert.NotNull(received);
        Assert.Equal("hello", received.Message);
    }

    [Fact]
    public async Task PublishAsync_InvokesMultipleHandlers()
    {
        var dispatcher = new InProcessEventDispatcher();
        var log = new List<string>();
        dispatcher.Register<PingEvent>((evt, _) => { log.Add("A"); return Task.CompletedTask; });
        dispatcher.Register<PingEvent>((evt, _) => { log.Add("B"); return Task.CompletedTask; });

        await dispatcher.PublishAsync(new PingEvent("test"));

        Assert.Equal(["A", "B"], log);
    }

    [Fact]
    public async Task PublishAsync_DoesNotInvokeHandlerForDifferentEventType()
    {
        var dispatcher = new InProcessEventDispatcher();
        bool pongTriggered = false;
        dispatcher.Register<PongEvent>((_, _) => { pongTriggered = true; return Task.CompletedTask; });

        await dispatcher.PublishAsync(new PingEvent("irrelevant"));

        Assert.False(pongTriggered);
    }

    [Fact]
    public async Task PublishAsync_NoHandlers_DoesNotThrow()
    {
        var dispatcher = new InProcessEventDispatcher();
        // Should complete without throwing.
        await dispatcher.PublishAsync(new PingEvent("no handlers"));
    }
}
