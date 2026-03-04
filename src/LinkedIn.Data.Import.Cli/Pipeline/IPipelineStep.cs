namespace LinkedIn.Data.Import.Cli.Pipeline;

/// <summary>
/// Defines a single step in the import pipeline.
/// Each step processes the context and returns an updated context.
/// </summary>
public interface IPipelineStep
{
    /// <summary>
    /// Executes this pipeline step.
    /// </summary>
    /// <param name="context">The shared context containing data from previous steps</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated context to pass to the next step</returns>
    Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default);
}
