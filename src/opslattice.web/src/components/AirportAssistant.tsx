
import { useState, type FormEvent } from "react";

interface ToolExecution {
  toolName: string;
  arguments: Record<string, unknown>;
  result: string;
  durationMs: number;
}

interface LlmExecution {
  iteration: number;
  durationMs: number;
  requestedTools: boolean;
  requestedToolNames: string[];
}

interface AssistantResponse {
  executionId: string;
  answer: string;
  totalDurationMs: number;
  llmExecutions: LlmExecution[];
  toolExecutions: ToolExecution[];
}

const API_BASE_URL = "http://localhost:5160";

function formatDuration(milliseconds: number): string {
  if (milliseconds >= 1000) {
    return `${(milliseconds / 1000).toFixed(2)} s`;
  }

  return `${milliseconds} ms`;
}

export default function AirportAssistant() {
  const [question, setQuestion] = useState(
    "How many flights are currently scheduled?"
  );

  const [response, setResponse] =
    useState<AssistantResponse | null>(null);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!question.trim() || loading) return;

    setLoading(true);
    setError(null);
    setResponse(null);

    try {
      const httpResponse = await fetch(
        `${API_BASE_URL}/api/assistant/query`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            question: question.trim(),
          }),
        }
      );

      if (!httpResponse.ok) {
        throw new Error(
          `Assistant request failed: HTTP ${httpResponse.status}`
        );
      }

      const data: AssistantResponse = await httpResponse.json();

      setResponse(data);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unexpected error"
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <section
      style={{
        padding: 20,
        border: "1px solid #ccc",
        borderRadius: 8,
      }}
    >
      <h2>AI Operations Assistant</h2>

      <form onSubmit={handleSubmit}>
        <div style={{ display: "flex", gap: 10 }}>
          <input
            type="text"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder="Ask about airport operations..."
            disabled={loading}
            style={{
              flex: 1,
              padding: 10,
            }}
          />

          <button
            type="submit"
            disabled={loading || !question.trim()}
          >
            {loading ? "Thinking..." : "Ask"}
          </button>
        </div>
      </form>

      {error && (
        <p role="alert" style={{ color: "crimson" }}>
          {error}
        </p>
      )}

      {response && (
        <>
          {/* Final Answer */}
          <div style={{ marginTop: 24 }}>
            <h3>Answer</h3>

            <p style={{ whiteSpace: "pre-wrap" }}>
              {response.answer}
            </p>
          </div>

          {/* Execution Summary */}
          <div style={{ marginTop: 24 }}>
            <h3>Agent Execution Trace</h3>

            <p>
              <strong>Execution ID:</strong>{" "}
              <code>{response.executionId ?? "Not available"}</code>
            </p>

            <p>
              <strong>Total Duration:</strong>{" "}
              {response.totalDurationMs == null
                ? "Not available"
                : formatDuration(response.totalDurationMs)}
            </p>

            {/* LLM Executions */}
            <h4>LLM Inference</h4>

            {(response.llmExecutions ?? []).length === 0 ? (
              <p>No LLM execution data available.</p>
            ) : (
              response.llmExecutions.map((execution) => (
                <details
                  key={execution.iteration}
                  style={{
                    border: "1px solid #ddd",
                    borderRadius: 6,
                    padding: 12,
                    marginBottom: 10,
                  }}
                >
                  <summary style={{ cursor: "pointer" }}>
                    LLM Iteration {execution.iteration}
                    {" — "}
                    {formatDuration(execution.durationMs)}
                  </summary>

                  <p>
                    <strong>Requested Tools:</strong>{" "}
                    {execution.requestedTools
                      ? execution.requestedToolNames.join(", ")
                      : "None"}
                  </p>
                </details>
              ))
            )}

            {/* MCP Executions */}
            <h4>MCP Tool Executions</h4>

            {(response.toolExecutions ?? []).length === 0 ? (
              <p>No MCP tools were executed.</p>
            ) : (
              response.toolExecutions.map((execution, index) => (
                <details
                  key={index}
                  style={{
                    border: "1px solid #ddd",
                    borderRadius: 6,
                    padding: 12,
                    marginBottom: 10,
                  }}
                >
                  <summary style={{ cursor: "pointer" }}>
                    {execution.toolName}
                    {" — "}
                    {formatDuration(execution.durationMs)}
                  </summary>

                  <h5>Arguments</h5>

                  <pre
                    style={{
                      whiteSpace: "pre-wrap",
                      overflowWrap: "anywhere",
                    }}
                  >
                    {execution.arguments == null
                      ? "Not available"
                      : JSON.stringify(execution.arguments, null, 2)}
                  </pre>

                  <h5>Tool Result</h5>

                  <pre
                    style={{
                      whiteSpace: "pre-wrap",
                      overflowWrap: "anywhere",
                      maxHeight: 300,
                      overflowY: "auto",
                    }}
                  >
                    {execution.result ?? "Not available"}
                  </pre>
                </details>
              ))
            )}
          </div>
        </>
      )}
    </section>
  );
}