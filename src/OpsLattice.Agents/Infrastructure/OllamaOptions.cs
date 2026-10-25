namespace OpsLattice.Agents.Infrastructure;

public sealed class OllamaOptions
{
    public string Model { get; set; } =
        "qwen2.5:7b-instruct-q4_K_M";

    // Ollama defaults to a 4K context on GPUs with less than 24 GB, and it
    // silently drops whatever does not fit (for example the start of a long
    // system prompt). 8K fits comfortably on an 8 GB GPU with 7-9B models.
    public int ContextLength { get; set; } = 8192;

    // Reasoning models such as Qwen3 and Qwen3.5 think by default, which
    // multiplies response time. false turns it off; null leaves the model's
    // default. Models without a thinking mode ignore this setting.
    public bool? Think { get; set; } = false;

    public string KeepAlive { get; set; } = "30m";
}
