public sealed record LlmPerformanceMetrics(
    double LoadDurationMs,
    double PromptEvalDurationMs,
    double EvalDurationMs,
    int PromptEvalCount,
    int EvalCount);
