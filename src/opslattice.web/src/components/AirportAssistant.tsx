
import { useState, type FormEvent } from "react";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

type AgentName =
  | "airport-operations"
  | "airport-operations-planned"
  | "gate";

interface LlmExecution {
  iteration: number;
  durationMs: number;
  requestedTools: boolean;
  requestedToolNames: string[];
}

interface AgentToolExecution {
  toolName: string;
  arguments: Record<string, unknown>;
  result: string;
  durationMs: number;
}

interface AgentExecutionResult {
  answer: string;
  agentName?: string;
  executionId?: string;
  totalDurationMs?: number;
  llmExecutions?: LlmExecution[];
  toolExecutions?: AgentToolExecution[];
}

function formatJson(value: unknown): string {
  if (typeof value !== "string") {
    return JSON.stringify(value ?? {}, null, 2);
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

const codeStyle: React.CSSProperties = {
  backgroundColor: "#1e1e1e",
  color: "#ffffff",
  fontFamily: "Consolas, 'Cascadia Code', monospace",
  fontSize: "13px",
  lineHeight: 1.6,
  padding: "12px",
  borderRadius: "8px",
  overflowX: "auto",
  whiteSpace: "pre-wrap",
  overflowWrap: "anywhere",
};

export default function AirportAssistant() {
  const [agentName, setAgentName] =
    useState<AgentName>("airport-operations");

  const [question, setQuestion] = useState("");
  const [result, setResult] =
    useState<AgentExecutionResult | null>(null);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedQuestion = question.trim();

    if (!trimmedQuestion || loading) {
      return;
    }

    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/assistant/query`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            question: trimmedQuestion,
            agentName,
          }),
        }
      );

      if (!response.ok) {
        const details = await response.text();

        throw new Error(
          `Assistant request failed (${response.status}): ${details}`
        );
      }

      const data =
        (await response.json()) as AgentExecutionResult;

      setResult(data);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "An unexpected error occurred."
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <section
      style={{
        border: "1px solid #d1d5db",
        borderRadius: "10px",
        padding: "16px",
      }}
    >
      <h2 style={{ marginTop: 0 }}>Airport Assistant</h2>
      <p
        style={{
          marginTop: "-6px",
          marginBottom: "12px",
          fontSize: "12px",
          color: "#6b7280",
        }}
      >
        UI version: planned-agent-selector-v1
      </p>

      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: "12px" }}>
          <label htmlFor="agent-select">
            <strong>Agent</strong>
          </label>

          <select
            id="agent-select"
            value={agentName}
            onChange={(event) =>
              setAgentName(event.target.value as AgentName)
            }
            disabled={loading}
            style={{
              display: "block",
              marginTop: "6px",
              padding: "8px",
              minWidth: "220px",
            }}
          >
            <option value="airport-operations">
              Airport Operations (Agentic)
            </option>
            <option value="airport-operations-planned">
              Airport Operations (Planned)
            </option>
            <option value="gate">
              Gate Operations
            </option>
          </select>
        </div>

        <div style={{ marginBottom: "12px" }}>
          <label htmlFor="assistant-question">
            <strong>Question</strong>
          </label>

          <textarea
            id="assistant-question"
            value={question}
            onChange={(event) =>
              setQuestion(event.target.value)
            }
            disabled={loading}
            placeholder="How many flights are going to DFW?"
            rows={3}
            style={{
              display: "block",
              boxSizing: "border-box",
              width: "100%",
              marginTop: "6px",
              padding: "10px",
              fontFamily: "inherit",
              fontSize: "14px",
            }}
          />
        </div>

        <button
          type="submit"
          disabled={loading || !question.trim()}
          style={{
            padding: "9px 18px",
            cursor: loading ? "wait" : "pointer",
          }}
        >
          {loading ? "Processing..." : "Ask Assistant"}
        </button>
      </form>

      {error && (
        <div
          role="alert"
          style={{
            marginTop: "16px",
            color: "#b91c1c",
            whiteSpace: "pre-wrap",
          }}
        >
          {error}
        </div>
      )}

      {result && (
        <div style={{ marginTop: "20px" }}>
          <h3>Answer</h3>

          <p style={{ whiteSpace: "pre-wrap" }}>
            {result.answer}
          </p>

          <details style={{ marginTop: "20px" }} open>
            <summary
              style={{
                cursor: "pointer",
                fontWeight: "bold",
              }}
            >
              Execution Details
            </summary>

            <div style={{ marginTop: "12px" }}>
              <p>
                <strong>Agent:</strong>{" "}
                {result.agentName ?? agentName}
              </p>

              {result.executionId && (
                <p>
                  <strong>Execution ID:</strong>{" "}
                  {result.executionId}
                </p>
              )}

              {result.totalDurationMs !== undefined && (
                <p>
                  <strong>Total Duration:</strong>{" "}
                  {result.totalDurationMs} ms
                </p>
              )}

              <h4>LLM Executions</h4>

              {(result.llmExecutions ?? []).length === 0 ? (
                <p>No LLM executions recorded.</p>
              ) : (
                result.llmExecutions!.map((execution, index) => (
                  <div key={index}>
                    <p>
                      <strong>
                        Iteration {execution.iteration}:
                      </strong>{" "}
                      {execution.durationMs} ms
                    </p>

                    <p>
                      Requested Tools:{" "}
                      {execution.requestedToolNames?.length
                        ? execution.requestedToolNames.join(", ")
                        : "None"}
                    </p>
                  </div>
                ))
              )}

              <h4>MCP Tool Executions</h4>

              {(result.toolExecutions ?? []).length === 0 ? (
                <p>No MCP tool executions recorded.</p>
              ) : (
                result.toolExecutions!.map((execution, index) => (
                  <div
                    key={index}
                    style={{
                      marginBottom: "20px",
                      paddingBottom: "12px",
                      borderBottom: "1px solid #e5e7eb",
                    }}
                  >
                    <p>
                      <strong>{execution.toolName}</strong>
                      {" — "}
                      {execution.durationMs} ms
                    </p>

                    <p>
                      <strong>Arguments</strong>
                    </p>

                    <pre style={codeStyle}>
                      {formatJson(execution.arguments)}
                    </pre>

                    <p>
                      <strong>Result</strong>
                    </p>    

                    <pre style={codeStyle}>
                      {formatJson(execution.result)}
                    </pre>
                  </div>
                ))
              )}
            </div>
          </details>
        </div>
      )}
    </section>
  );
}

