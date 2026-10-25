// Agent reliability evaluation for OpsLattice.
//
// Asks both airport agents the same questions several times and grades the
// answers against ground truth computed from the live simulation data.
// Read-only: it never advances the simulation.
//
// Usage (Node 18+, stack running via docker-start.bat):
//   node scripts/agent-eval.mjs [baseUrl] [repeats]
//   node scripts/agent-eval.mjs http://localhost:8080 3
//
// Writes scripts/agent-eval-results.json with every raw answer.

import { writeFileSync } from "node:fs";

const baseUrl = (process.argv[2] ?? "http://localhost:8080").replace(/\/$/, "");
const repeats = Number(process.argv[3] ?? 3);
const agents = ["airport-operations", "airport-operations-planned"];
const home = "IAH";

const getJson = async (path) => {
  const response = await fetch(baseUrl + path);
  if (!response.ok) throw new Error(`${path} -> HTTP ${response.status}`);
  return response.json();
};

const flights = await getJson("/api/flights");
const simulation = await getJson("/api/simulation");
const simDate = simulation.currentTime.slice(0, 10);
const nextDate = new Date(Date.parse(simDate) + 86_400_000).toISOString().slice(0, 10);
const dateOf = (f) => f.scheduledTime.slice(0, 10);
const count = (pred) => flights.filter(pred).length;
const longDate = (iso, comma = true) => {
  const d = new Date(iso + "T00:00:00Z");
  const month = d.toLocaleString("en-US", { month: "long", timeZone: "UTC" });
  return `${month} ${d.getUTCDate()}${comma ? "," : ""} ${d.getUTCFullYear()}`;
};
const monthDay = longDate(simDate).split(",")[0];

const arrivals = flights.filter((f) => f.type === "Arrival");
const departures = flights.filter((f) => f.type === "Departure");
const busiest = (list, key) =>
  Object.entries(list.reduce((a, f) => ((a[f[key]] = (a[f[key]] ?? 0) + 1), a), {}))
    .sort((a, b) => b[1] - a[1])[0][0];
const arrFrom = busiest(arrivals, "origin");
const arrFrom2 = [...new Set(arrivals.map((f) => f.origin))].filter((c) => c !== arrFrom)[0];
const depTo = busiest(departures, "destination");
const depTo2 = [...new Set(departures.map((f) => f.destination))].filter((c) => c !== depTo).at(-1);
const status = (s) => count((f) => f.status === s);

// expect: number | number[] | "CLARIFY" | {date} | {list: [...]}
const questions = [
  { q: "How many flights are there in total?", expect: flights.length },
  { q: `How many arrivals are scheduled for ${longDate(simDate)}?`, expect: count((f) => f.type === "Arrival" && dateOf(f) === simDate) },
  { q: `How many departures are scheduled for ${longDate(simDate)}?`, expect: count((f) => f.type === "Departure" && dateOf(f) === simDate) },
  { q: `How many arrivals and departures are scheduled for ${longDate(simDate)}?`, expect: [count((f) => f.type === "Arrival" && dateOf(f) === simDate), count((f) => f.type === "Departure" && dateOf(f) === simDate)] },
  { q: "How many flights are there today?", expect: count((f) => dateOf(f) === simDate) },
  { q: "How many flights are scheduled tomorrow?", expect: count((f) => dateOf(f) === nextDate) },
  { q: `How many flights on ${monthDay}?`, expect: "CLARIFY" },
  { q: `How many flights are arriving from ${arrFrom}?`, expect: count((f) => f.type === "Arrival" && f.origin === arrFrom) },
  { q: `How many flights depart from ${home} to ${depTo}?`, expect: count((f) => f.type === "Departure" && f.destination === depTo) },
  { q: `How many departures to ${depTo2} on ${simDate}?`, expect: count((f) => f.type === "Departure" && f.destination === depTo2 && dateOf(f) === simDate) },
  { q: `How many arrivals from ${arrFrom2} on ${longDate(simDate)}?`, expect: count((f) => f.type === "Arrival" && f.origin === arrFrom2 && dateOf(f) === simDate) },
  { q: "How many flights currently have status Scheduled?", expect: status("Scheduled") },
  { q: "How many flights have landed so far?", expect: status("Landed") },
  { q: "What is the current simulation date?", expect: { date: simDate } },
  { q: `List the flights departing from ${home} to ${depTo} on ${longDate(simDate)}.`, expect: { list: departures.filter((f) => f.destination === depTo && dateOf(f) === simDate).map((f) => f.flightNumber) } },
  { q: `How many arrivals to ${home} come from ${arrFrom2}?`, expect: count((f) => f.type === "Arrival" && f.origin === arrFrom2) },
  { q: `How many departures to ${depTo2} on ${longDate(simDate, false)}?`, expect: count((f) => f.type === "Departure" && f.destination === depTo2 && dateOf(f) === simDate) },
  { q: `How many flights are scheduled on ${longDate(nextDate)}?`, expect: count((f) => dateOf(f) === nextDate) },
  { q: `How many flights from ${arrFrom} arrive at ${home}?`, expect: count((f) => f.type === "Arrival" && f.origin === arrFrom) },
  { q: `How many arrivals are scheduled for ${longDate(simDate)}, and how many flights are approaching right now?`, expect: [count((f) => f.type === "Arrival" && dateOf(f) === simDate), status("Approaching")] },
];

const flightNumberRe = /\b[A-Z0-9]{2}\d{3,4}\b/g;
// Any question mark means the agent asked something back instead of answering.
const asksQuestion = (a) => a.includes("?") || /^CLARIFICATION:/i.test(a.trim());
const failed = (a) => /\bfailed\b|couldn't complete|could not complete|error occurred/i.test(a);
const hasNumber = (a, n) =>
  n === 0
    ? /\b0\b|\bzero\b|\bnone\b|\bno (?:flights|arrivals|departures)\b|\bno\b[^.]*\bflights\b/i.test(a)
    : new RegExp(`(?<![\\d-])${n}(?![\\d-])`).test(a.replace(/\b\d{4}-\d{2}-\d{2}\b/g, ""));

function grade(expect, answer) {
  const a = String(answer ?? "");
  if (expect === "CLARIFY") return /year/i.test(a) && asksQuestion(a) ? "pass" : "fail";
  if (failed(a)) return "error";
  if (asksQuestion(a)) return "clarify";
  if (typeof expect === "number") return hasNumber(a, expect) ? "pass" : "wrong";
  if (Array.isArray(expect)) return expect.every((n) => hasNumber(a, n)) ? "pass" : "wrong";
  if (expect.date) {
    const ok = a.includes(expect.date) || a.includes(longDate(expect.date));
    return ok ? "pass" : "wrong";
  }
  if (expect.list) {
    const mentioned = new Set(a.match(flightNumberRe) ?? []);
    const allPresent = expect.list.every((n) => mentioned.has(n));
    const noExtras = [...mentioned].every((n) => expect.list.includes(n));
    return allPresent && noExtras ? "pass" : "wrong";
  }
  return "wrong";
}

const results = [];
for (let rep = 1; rep <= repeats; rep++) {
  for (const [i, item] of questions.entries()) {
    for (const agent of agents) {
      const started = performance.now();
      let answer;
      try {
        const response = await fetch(baseUrl + "/api/assistant/query", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ question: item.q, agentName: agent }),
        });
        const body = await response.json();
        answer = body.answer ?? JSON.stringify(body);
      } catch (error) {
        answer = `failed: ${error}`;
      }
      const ms = Math.round(performance.now() - started);
      const verdict = grade(item.expect, answer);
      results.push({ rep, id: i + 1, agent, question: item.q, expect: item.expect, verdict, ms, answer });
      process.stdout.write(`rep ${rep} q${String(i + 1).padStart(2)} ${agent.padEnd(27)} ${verdict.padEnd(7)} ${ms} ms\n`);
    }
  }
}

console.log("\nSummary");
for (const agent of agents) {
  const mine = results.filter((r) => r.agent === agent);
  const by = (v) => mine.filter((r) => r.verdict === v).length;
  const times = mine.map((r) => r.ms).sort((a, b) => a - b);
  console.log(
    `${agent.padEnd(27)} pass ${by("pass")}/${mine.length}  wrong ${by("wrong")}  clarify ${by("clarify")}  error ${by("error")}  ` +
      `median ${times[Math.floor(times.length / 2)]} ms  max ${times.at(-1)} ms`,
  );
}
console.log("\nPer question (pass count out of " + repeats + ")");
for (const [i, item] of questions.entries()) {
  const row = agents.map((agent) => results.filter((r) => r.id === i + 1 && r.agent === agent && r.verdict === "pass").length);
  console.log(`q${String(i + 1).padStart(2)}  agentic ${row[0]}  planned ${row[1]}  ${item.q}`);
}

writeFileSync(new URL("./agent-eval-results.json", import.meta.url), JSON.stringify(results, null, 2));
