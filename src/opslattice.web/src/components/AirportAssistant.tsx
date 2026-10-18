
import { useState } from "react";

type AgentName = "airport-operations" | "gate";

interface AgentLlmExecution {
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
  executionId: string;
  answer: string;
  totalDurationMs: number;
  llmExecutions: AgentLlmExecution[];
  toolExecutions: AgentToolExecution[];
}

const agents: { value: AgentName; label: string }[] = [
  { value: "airport-operations", label: "Airport Operations" },
  { value: "gate", label: "Gate Operations" },
];

export default function AirportAssistant() {
  const [agentName, setAgentName] =
    useState<AgentName>("airport-operations");

  const [question, setQuestion] = useState("");
  const [result, setResult] =
    useState<AgentExecutionResult | null>(null);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!question.trim() || loading) return;

    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const response = await fetch(
        "http://localhost:5160/api/assistant/query",
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            question: question.trim(),
            agentName,
          }),
        }
      );

      if (!response.ok) {
        throw new Error(`Request failed: ${response.status}`);
      }

      const data: AgentExecutionResult = await response.json();
      setResult(data);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unexpected error"
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={{ maxWidth: 800, padding: 24 }}>
      <h2>OpsLattice AI Assistant</h2>

      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: 16 }}>
          <label htmlFor="agent">Agent</label>

          <select
            id="agent"
            value={agentName}
            onChange={(event) => {
              setAgentName(event.target.value as AgentName);
              setResult(null);
              setError(null);
            }}
            disabled={loading}
            style={{
              display: "block",
              width: "100%",
              padding: 10,
              marginTop: 6,
            }}
          >
            {agents.map((agent) => (
              <option key={agent.value} value={agent.value}>
                {agent.label}
              </option>
            ))}
          </select>
        </div>

        <div style={{ marginBottom: 16 }}>
          <label htmlFor="question">Question</label>

          <input
            id="question"
            type="text"
            value={question}
            onChange={(event) => setQuestion(event.target.value)}
            placeholder="Ask about airport operations..."
            disabled={loading}
            style={{
              display: "block",
              width: "100%",
              padding: 10,
              marginTop: 6,
            }}
          />
        </div>

        <button type="submit" disabled={loading || !question.trim()}>
          {loading ? "Running..." : "Ask Agent"}
        </button>
      </form>

      {error && (
        <p style={{ color: "red", marginTop: 16 }}>{error}</p>
      )}

      {result && (
        <div style={{ marginTop: 24 }}>
          <h3>Answer</h3>
          <p>{result.answer}</p>

          <h3>Execution Details</h3>

          <p>
            <strong>Agent:</strong> {agentName}
          </p>

          <p>
            <strong>Execution ID:</strong> {result.executionId}
          </p>

          <p>
            <strong>Total Duration:</strong>{" "}
            {result.totalDurationMs} ms
          </p>

          <h4>LLM Executions</h4>

          {result.llmExecutions.map((execution) => (
            <div key={execution.iteration}>
              <p>
                Iteration {execution.iteration}:{" "}
                {execution.durationMs} ms
              </p>

              <p>
                Requested Tools:{" "}
                {execution.requestedToolNames.length > 0
                  ? execution.requestedToolNames.join(", ")
                  : "None"}
              </p>
            </div>
          ))}

          <h4>MCP Tool Executions</h4>

          {result.toolExecutions.map((execution, index) => (
            <div key={index}>
              <p>
                <strong>{execution.toolName}</strong> —{" "}
                {execution.durationMs} ms
              </p>

              <pre
                  style={{
                    backgroundColor: "#1e1e1e",
                    color: "#ffffff",
                    fontFamily: "Consolas, 'Cascadia Code', monospace",
                    fontSize: "13px",
                    lineHeight: 1.6,
                    padding: "16px",
                    borderRadius: "8px",
                    overflowX: "auto",
                    whiteSpace: "pre-wrap",
                    overflowWrap: "anywhere",
                  }}
                >
                  {JSON.stringify(JSON.parse(execution.result), null, 2)}
                </pre>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
