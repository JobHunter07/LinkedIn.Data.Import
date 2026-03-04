using LinkedIn.Data.Import.Cli.Pipeline;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Tests for the pipeline pattern implementation.
/// Each step should process the context and pass it to the next step.
/// </summary>
public sealed class ImportPipelineTests
{
    [Fact]
    public async Task Pipeline_ShouldExecuteStepsInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        
        var step1 = new TestStep("Step1", executionOrder);
        var step2 = new TestStep("Step2", executionOrder);
        var step3 = new TestStep("Step3", executionOrder);

        var pipeline = new ImportPipeline()
            .AddStep(step1)
            .AddStep(step2)
            .AddStep(step3);

        var context = new ImportContext();

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Equal(3, executionOrder.Count);
        Assert.Equal("Step1", executionOrder[0]);
        Assert.Equal("Step2", executionOrder[1]);
        Assert.Equal("Step3", executionOrder[2]);
    }

    [Fact]
    public async Task Pipeline_ShouldStopOnFailure()
    {
        // Arrange
        var executionOrder = new List<string>();
        
        var step1 = new TestStep("Step1", executionOrder);
        var step2 = new FailingStep("Step2");
        var step3 = new TestStep("Step3", executionOrder);

        var pipeline = new ImportPipeline()
            .AddStep(step1)
            .AddStep(step2)
            .AddStep(step3);

        var context = new ImportContext();

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Single(executionOrder); // Only step1 executed
        Assert.Equal("Step1", executionOrder[0]);
        Assert.False(context.IsSuccess);
    }

    [Fact]
    public async Task Pipeline_ShouldPassContextBetweenSteps()
    {
        // Arrange
        var step1 = new ContextModifyingStep("Value1");
        var step2 = new ContextModifyingStep("Value2");

        var pipeline = new ImportPipeline()
            .AddStep(step1)
            .AddStep(step2);

        var context = new ImportContext();

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Contains("Value1", context.Data.Values);
        Assert.Contains("Value2", context.Data.Values);
    }

    // Test helper classes
    private class TestStep : IPipelineStep
    {
        private readonly string _name;
        private readonly List<string> _executionOrder;

        public TestStep(string name, List<string> executionOrder)
        {
            _name = name;
            _executionOrder = executionOrder;
        }

        public Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
        {
            _executionOrder.Add(_name);
            return Task.FromResult(context);
        }
    }

    private class FailingStep : IPipelineStep
    {
        private readonly string _name;

        public FailingStep(string name)
        {
            _name = name;
        }

        public Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
        {
            context.IsSuccess = false;
            context.ErrorMessage = $"{_name} failed";
            return Task.FromResult(context);
        }
    }

    private class ContextModifyingStep : IPipelineStep
    {
        private readonly string _value;

        public ContextModifyingStep(string value)
        {
            _value = value;
        }

        public Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
        {
            context.Data[_value] = _value;
            return Task.FromResult(context);
        }
    }
}
