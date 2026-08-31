/*
 * Cross-instance SignalR verification for the PRO Management System.
 *
 * Verifies that realtime notifications published from one running instance
 * are delivered to a client connected to a DIFFERENT instance, through the
 * Redis backplane, and that a client registered for a different user does
 * NOT receive another user's events (group isolation).
 *
 * Prerequisites (run manually before `npm test`):
 *   - Two instance URLs via INSTANCE_A / INSTANCE_B (defaults localhost:5002 /
 *     5003).
 *   - An agent token and a second agent token via AGENT_A_TOKEN /
 *     AGENT_B_TOKEN, OR login credentials via LOGIN_A_EMAIL/LOGIN_A_PASSWORD
 *     (and _B_) to fetch tokens from /api/v1/auth/login.
 *   - An admin token (ADMIN_TOKEN) to trigger an assignment / status change
 *     on instance A that publishes to agent A's group.
 *
 * The script:
 *   1. Connects to both instances as agent A (same user, group user:{id}).
 *   2. Connects to both instances as agent B (control user).
 *   3. Triggers a TaskAssigned / TaskStatusChanged publish on instance A.
 *   4. Asserts the event reached the agent-A connection on the OPPOSITE
 *      instance (cross-instance) and the same instance, and that agent B's
 *      clients received nothing.
 * Exits 0 on success, 1 on failure.
 */

const signalR = require("@microsoft/signalr");

const INSTANCE_A = process.env.INSTANCE_A || "http://localhost:5002";
const INSTANCE_B = process.env.INSTANCE_B || "http://localhost:5003";

const ADMIN_TOKEN =
  process.env.ADMIN_TOKEN || "";
const AGENT_A_TOKEN =
  process.env.AGENT_A_TOKEN ||
  process.env.LOGIN_A_EMAIL ||
  "";
const AGENT_B_TOKEN =
  process.env.AGENT_B_TOKEN ||
  process.env.LOGIN_B_EMAIL ||
  "";

const LOGIN_A_EMAIL = process.env.LOGIN_A_EMAIL;
const LOGIN_A_PASSWORD = process.env.LOGIN_A_PASSWORD;
const LOGIN_B_EMAIL = process.env.LOGIN_B_EMAIL;
const LOGIN_B_PASSWORD = process.env.LOGIN_B_PASSWORD;

async function login(baseUrl, email, password) {
  if (!email || !password) {
    throw new Error(
      `Missing credentials for ${baseUrl}. Provide LOGIN_x_EMAIL/PASSWORD or a token.`
    );
  }
  const res = await fetch(`${baseUrl}/api/v1/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) {
    throw new Error(
      `Login to ${baseUrl} failed (${res.status}): ${await res.text()}`
    );
  }
  const body = await res.json();
  // The service wraps responses in ApiResponse { data: { accessToken } }.
  return body.data.accessToken || body.accessToken || body.token;
}

function buildConnection(url, token) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${url}/hubs/notification`, {
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();
  return conn;
}

function waitFor(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function main() {
  const results = [];
  const assert = (name, ok, detail) => {
    results.push({ name, ok, detail });
    console.log(`  [${ok ? "PASS" : "FAIL"}] ${name}${detail ? " - " + detail : ""}`);
  };

  const tokenA =
    AGENT_A_TOKEN.startsWith("ey") && /\.[^.]+\.[^.]+$/.test(AGENT_A_TOKEN)
      ? AGENT_A_TOKEN
      : await login(INSTANCE_A, LOGIN_A_EMAIL, LOGIN_A_PASSWORD);
  const tokenB =
    AGENT_B_TOKEN.startsWith("ey") && /\.[^.]+\.[^.]+$/.test(AGENT_B_TOKEN)
      ? AGENT_B_TOKEN
      : await login(INSTANCE_B, LOGIN_B_EMAIL, LOGIN_B_PASSWORD);

  if (!ADMIN_TOKEN) {
    throw new Error(
      "ADMIN_TOKEN is required to trigger a publish on instance A."
    );
  }

  console.log("Connecting clients...");

  // Two connections for agent A (one on each instance) and two for agent B.
  const connA1 = buildConnection(INSTANCE_A, tokenA);
  const connA2 = buildConnection(INSTANCE_B, tokenA);
  const connB1 = buildConnection(INSTANCE_A, tokenB);
  const connB2 = buildConnection(INSTANCE_B, tokenB);

  const received = {
    A1: { TaskAssigned: false, TaskStatusChanged: false },
    A2: { TaskAssigned: false, TaskStatusChanged: false },
    B1: { TaskAssigned: false, TaskStatusChanged: false },
    B2: { TaskAssigned: false, TaskStatusChanged: false },
  };

  const hook = (conn, key) => {
    conn.on("TaskAssigned", () => { received[key].TaskAssigned = true; });
    conn.on("TaskStatusChanged", () => { received[key].TaskStatusChanged = true; });
  };

  hook(connA1, "A1");
  hook(connA2, "A2");
  hook(connB1, "B1");
  hook(connB2, "B2");

  await Promise.all([
    connA1.start(),
    connA2.start(),
    connB1.start(),
    connB2.start(),
  ]);

  console.log("All clients connected.");

  // Trigger a TaskStatusChanged on instance A for agent A. Use the agent
  // status endpoint; the caller must be the task's assigned agent (agent A),
  // so we call with agent A's token against instance A.
  const triggerUrl = process.env.TRIGGER_URL;
  if (!triggerUrl) {
    throw new Error(
      "TRIGGER_URL (e.g. PATCH /api/v1/agent/tasks/{taskId}/status) is required."
    );
  }
  const triggerBody = process.env.TRIGGER_BODY || '{"status":"FeePaid"}';

  console.log(`Triggering publish on ${INSTANCE_A}...`);
  const resp = await fetch(`${INSTANCE_A}${triggerUrl}`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${tokenA}`,
    },
    body: triggerBody,
  });
  const triggerBodyText = await resp.text();
  console.log(`  Trigger returned ${resp.status}: ${triggerBodyText}`);
  if (resp.status >= 400) {
    throw new Error(
      `Trigger failed with ${resp.status}. Is instance A running and is the task assigned to agent A?`
    );
  }

  // Allow the Redis backplane to fan out the event to the other instance.
  await waitFor(parseInt(process.env.SETTLE_MS || "4000", 10));

  // 1. Same-instance delivery (instance A -> connection A1 on instance A).
  assert(
    "Same-instance delivery (TaskStatusChanged) reaches agent A on instance A",
    received.A1.TaskStatusChanged,
    JSON.stringify(received.A1)
  );

  // 2. Cross-instance delivery (instance A -> connection A2 on instance B).
  assert(
    "Cross-instance delivery (TaskStatusChanged) reaches agent A on instance B",
    received.A2.TaskStatusChanged,
    JSON.stringify(received.A2)
  );

  // 3. Group isolation: agent B's clients must not receive agent A's event.
  assert(
    "Group isolation: agent B on instance A receives nothing",
    !received.B1.TaskAssigned && !received.B1.TaskStatusChanged,
    JSON.stringify(received.B1)
  );
  assert(
    "Group isolation: agent B on instance B receives nothing",
    !received.B2.TaskAssigned && !received.B2.TaskStatusChanged,
    JSON.stringify(received.B2)
  );

  await Promise.all([
    connA1.stop(),
    connA2.stop(),
    connB1.stop(),
    connB2.stop(),
  ]);

  const failed = results.filter((r) => !r.ok);
  console.log(
    `\n${failed.length === 0 ? "ALL " + results.length + " CHECKS PASSED" : failed.length + " CHECK(S) FAILED"}`
  );
  process.exit(failed.length === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error("\nERROR: " + err.message);
  process.exit(1);
});
