namespace LinkedIn.Data.Import.Cli.Pipeline;

/// <summary>
/// Orchestrates the execution of multiple pipeline steps in sequence.
/// Stops execution if any step fails (IsSuccess = false).
/// </summary>
public sealed class ImportPipeline
{
    private readonly List<IPipelineStep> _steps = [];

    /// <summary>
    /// Adds a step to the pipeline.
    /// </summary>
    public ImportPipeline AddStep(IPipelineStep step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Executes all pipeline steps in sequence.
    /// Stops if any step sets IsSuccess = false.
    /// </summary>
    public async Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var step in _steps)
        {
            context = await step.ExecuteAsync(context, cancellationToken);
            
            if (!context.IsSuccess)
            {
                // Stop pipeline execution on failure
                break;
            }
        }

        return context;
    }
}
