const signalR = require("@microsoft/signalr");
const WebSocket = require("ws");
const readline = require("node:readline/promises");
const { stdin: input, stdout: output } = require("node:process");

global.WebSocket = WebSocket;

function createConnection(hubUrl, token, forceWebSocketOnly = false) {
  const options = {
    accessTokenFactory: () => token,
  };

  if (forceWebSocketOnly) {
    options.transport = signalR.HttpTransportType.WebSockets;
    options.skipNegotiation = true;
  }

  return new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, options)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}

function decodeJwtPayload(token) {
  const parts = token.split(".");
  if (parts.length < 2) return null;
  const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
  try {
    const json = Buffer.from(padded, "base64").toString("utf8");
    return JSON.parse(json);
  } catch {
    return null;
  }
}

function extractUserIdFromJwt(token) {
  const payload = decodeJwtPayload(token);
  if (!payload) return null;
  return (
    payload.accountId || payload.nameid || payload.sub || payload.id || null
  );
}

function parseArgs(inputLine) {
  const args = [];
  const regex = /"([^"]*)"|'([^']*)'|(\S+)/g;
  let match;
  while ((match = regex.exec(inputLine)) !== null) {
    args.push(match[1] ?? match[2] ?? match[3]);
  }
  return args;
}

async function apiRequest(baseUrl, token, method, path, body) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  const text = await response.text();
  let data = text;
  try {
    data = JSON.parse(text);
  } catch {
    // Keep plain text body when not JSON.
  }

  return {
    ok: response.ok,
    status: response.status,
    data,
  };
}

function printHelp() {
  console.log("\nCommands:");
  console.log("  help");
  console.log("  whoami");
  console.log("  conv <targetUserId>");
  console.log("  joinA <conversationId>");
  console.log("  joinB <conversationId>");
  console.log("  joinBoth <conversationId>");
  console.log("  leaveA <conversationId>");
  console.log("  leaveB <conversationId>");
  console.log("  sendA <conversationId> <text>");
  console.log("  sendB <conversationId> <text>");
  console.log("  replyA <conversationId> <parentMessageId> <text>");
  console.log("  replyB <conversationId> <parentMessageId> <text>");
  console.log("  reactA <conversationId> <messageId> <reaction|null>");
  console.log("  reactB <conversationId> <messageId> <reaction|null>");
  console.log("  readA <conversationId> <upToMessageId>");
  console.log("  readB <conversationId> <upToMessageId>");
  console.log("  msgsA <conversationId> [pageSize]");
  console.log("  msgsB <conversationId> [pageSize]");
  console.log("  hubSendA <conversationId> <text>");
  console.log("  hubSendB <conversationId> <text>");
  console.log("  hubReplyA <conversationId> <parentMessageId> <text>");
  console.log("  hubReplyB <conversationId> <parentMessageId> <text>");
  console.log("  typingA <conversationId> <true|false>");
  console.log("  typingB <conversationId> <true|false>");
  console.log("  hubReadA <conversationId> <upToMessageId>");
  console.log("  hubReadB <conversationId> <upToMessageId>");
  console.log("  hubReactA <conversationId> <messageId> <reaction|null>");
  console.log("  hubReactB <conversationId> <messageId> <reaction|null>");
  console.log("  exit\n");
}

function registerEvents(connection, label) {
  connection.on("messageReceived", (payload) => {
    console.log(`\n[${label}] event messageReceived:`, payload);
  });
  connection.on("typingChanged", (payload) => {
    console.log(`\n[${label}] event typingChanged:`, payload);
  });
  connection.on("readReceipt", (payload) => {
    console.log(`\n[${label}] event readReceipt:`, payload);
  });
  connection.on("reactionChanged", (payload) => {
    console.log(`\n[${label}] event reactionChanged:`, payload);
  });

  connection.onreconnecting((err) => {
    console.log(`\n[${label}] reconnecting...`, err?.message || "");
  });
  connection.onreconnected(() => {
    console.log(`\n[${label}] reconnected`);
  });
  connection.onclose((err) => {
    console.log(`\n[${label}] closed`, err?.message || "");
  });
}

async function main() {
  const rl = readline.createInterface({ input, output });

  try {
    const baseUrlInput = await rl.question(
      "Base URL API (default http://localhost:5000): ",
    );
    const baseUrl = (baseUrlInput || "http://localhost:5000").replace(
      /\/$/,
      "",
    );

    const jwtA = (await rl.question("JWT A: ")).trim();
    const jwtB = (await rl.question("JWT B: ")).trim();

    if (!jwtA || !jwtB) {
      console.error("JWT A/B are required.");
      return;
    }

    const userIdA = extractUserIdFromJwt(jwtA);
    const userIdB = extractUserIdFromJwt(jwtB);

    const hubUrl = `${baseUrl}/hub`;

    // Local HTTPS commonly uses a dev certificate.
    if (
      baseUrl.startsWith("https://localhost") ||
      baseUrl.startsWith("https://127.0.0.1")
    ) {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";
      console.log(
        "[WARN] TLS verification disabled for localhost development.",
      );
    }

    let connectionA = createConnection(hubUrl, jwtA, false);
    let connectionB = createConnection(hubUrl, jwtB, false);

    registerEvents(connectionA, "A");
    registerEvents(connectionB, "B");

    try {
      await connectionA.start();
      await connectionB.start();
    } catch (firstErr) {
      console.log(
        "[WARN] Default SignalR connect failed. Retrying with WebSocket-only mode...",
      );
      await Promise.allSettled([connectionA.stop(), connectionB.stop()]);

      connectionA = createConnection(hubUrl, jwtA, true);
      connectionB = createConnection(hubUrl, jwtB, true);
      registerEvents(connectionA, "A");
      registerEvents(connectionB, "B");

      await connectionA.start();
      await connectionB.start();
      console.log("[INFO] Connected using WebSocket-only fallback.");
    }

    console.log("\nConnected A and B to SignalR hub.");
    printHelp();

    while (true) {
      const line = (await rl.question("> ")).trim();
      if (!line) continue;

      const [cmd, ...rest] = parseArgs(line);

      try {
        if (cmd === "exit" || cmd === "quit") {
          break;
        }

        if (cmd === "help") {
          printHelp();
          continue;
        }

        if (cmd === "whoami") {
          console.log({ userIdA, userIdB });
          continue;
        }

        if (cmd === "conv") {
          const targetUserId = rest[0];
          if (!targetUserId) {
            console.log("Usage: conv <targetUserId>");
            continue;
          }
          const res = await apiRequest(
            baseUrl,
            jwtA,
            "POST",
            `/api/v1/chat/conversations/direct/${targetUserId}`,
          );
          console.log(res.status, res.data);
          continue;
        }

        if (cmd === "joinA" || cmd === "joinB" || cmd === "joinBoth") {
          const conversationId = Number(rest[0]);
          if (!Number.isInteger(conversationId)) {
            console.log(`Usage: ${cmd} <conversationId>`);
            continue;
          }
          if (cmd === "joinA" || cmd === "joinBoth") {
            await connectionA.invoke("JoinConversation", conversationId);
            console.log("A joined", conversationId);
          }
          if (cmd === "joinB" || cmd === "joinBoth") {
            await connectionB.invoke("JoinConversation", conversationId);
            console.log("B joined", conversationId);
          }
          continue;
        }

        if (cmd === "leaveA" || cmd === "leaveB") {
          const conversationId = Number(rest[0]);
          if (!Number.isInteger(conversationId)) {
            console.log(`Usage: ${cmd} <conversationId>`);
            continue;
          }
          if (cmd === "leaveA") {
            await connectionA.invoke("LeaveConversation", conversationId);
            console.log("A left", conversationId);
          } else {
            await connectionB.invoke("LeaveConversation", conversationId);
            console.log("B left", conversationId);
          }
          continue;
        }

        if (cmd === "sendA" || cmd === "sendB") {
          const conversationId = Number(rest[0]);
          const text = rest.slice(1).join(" ");
          if (!Number.isInteger(conversationId) || !text) {
            console.log(`Usage: ${cmd} <conversationId> <text>`);
            continue;
          }
          const token = cmd === "sendA" ? jwtA : jwtB;
          const res = await apiRequest(
            baseUrl,
            token,
            "POST",
            "/api/v1/chat/messages",
            {
              conversationId,
              content: text,
              messageType: 0,
            },
          );
          console.log(res.status, res.data);
          continue;
        }

        if (cmd === "replyA" || cmd === "replyB") {
          const conversationId = Number(rest[0]);
          const parentMessageId = Number(rest[1]);
          const text = rest.slice(2).join(" ");
          if (
            !Number.isInteger(conversationId) ||
            !Number.isInteger(parentMessageId) ||
            !text
          ) {
            console.log(
              `Usage: ${cmd} <conversationId> <parentMessageId> <text>`,
            );
            continue;
          }
          const token = cmd === "replyA" ? jwtA : jwtB;
          const res = await apiRequest(
            baseUrl,
            token,
            "POST",
            "/api/v1/chat/messages",
            {
              conversationId,
              content: text,
              messageType: 0,
              parentMessageId,
            },
          );
          console.log(res.status, res.data);
          continue;
        }

        if (cmd === "reactA" || cmd === "reactB") {
          const conversationId = Number(rest[0]);
          const messageId = Number(rest[1]);
          const reactionRaw = rest[2];
          if (
            !Number.isInteger(conversationId) ||
            !Number.isInteger(messageId) ||
            reactionRaw == null
          ) {
            console.log(
              `Usage: ${cmd} <conversationId> <messageId> <reaction|null>`,
            );
            continue;
          }
          const reaction =
            reactionRaw.toLowerCase() === "null" ? null : Number(reactionRaw);
          if (reaction !== null && !Number.isInteger(reaction)) {
            console.log("reaction must be integer 0..6 or null");
            continue;
          }
          const token = cmd === "reactA" ? jwtA : jwtB;
          const res = await apiRequest(
            baseUrl,
            token,
            "PUT",
            `/api/v1/chat/conversations/${conversationId}/messages/${messageId}/reaction`,
            { reaction },
          );
          console.log(res.status, res.data);
          continue;
        }

        if (cmd === "readA" || cmd === "readB") {
          const conversationId = Number(rest[0]);
          const upToMessageId = Number(rest[1]);
          if (
            !Number.isInteger(conversationId) ||
            !Number.isInteger(upToMessageId)
          ) {
            console.log(`Usage: ${cmd} <conversationId> <upToMessageId>`);
            continue;
          }
          const token = cmd === "readA" ? jwtA : jwtB;
          const res = await apiRequest(
            baseUrl,
            token,
            "POST",
            `/api/v1/chat/conversations/${conversationId}/read`,
            { upToMessageId },
          );
          console.log(res.status, res.data);
          continue;
        }

        if (cmd === "msgsA" || cmd === "msgsB") {
          const conversationId = Number(rest[0]);
          const pageSize = Number(rest[1] || 20);
          if (!Number.isInteger(conversationId)) {
            console.log(`Usage: ${cmd} <conversationId> [pageSize]`);
            continue;
          }
          const token = cmd === "msgsA" ? jwtA : jwtB;
          const res = await apiRequest(
            baseUrl,
            token,
            "GET",
            `/api/v1/chat/conversations/${conversationId}/messages?pageSize=${Number.isInteger(pageSize) ? pageSize : 20}`,
          );
          console.log(res.status, JSON.stringify(res.data, null, 2));
          continue;
        }

        if (cmd === "hubSendA" || cmd === "hubSendB") {
          const conversationId = Number(rest[0]);
          const text = rest.slice(1).join(" ");
          if (!Number.isInteger(conversationId) || !text) {
            console.log(`Usage: ${cmd} <conversationId> <text>`);
            continue;
          }
          const connection = cmd === "hubSendA" ? connectionA : connectionB;
          await connection.invoke("SendMessage", {
            conversationId,
            content: text,
            messageType: 0,
          });
          console.log("Hub SendMessage invoked.");
          continue;
        }

        if (cmd === "hubReplyA" || cmd === "hubReplyB") {
          const conversationId = Number(rest[0]);
          const parentMessageId = Number(rest[1]);
          const text = rest.slice(2).join(" ");
          if (
            !Number.isInteger(conversationId) ||
            !Number.isInteger(parentMessageId) ||
            !text
          ) {
            console.log(
              `Usage: ${cmd} <conversationId> <parentMessageId> <text>`,
            );
            continue;
          }
          const connection = cmd === "hubReplyA" ? connectionA : connectionB;
          await connection.invoke("SendMessage", {
            conversationId,
            content: text,
            messageType: 0,
            parentMessageId,
          });
          console.log("Hub SendMessage(reply) invoked.");
          continue;
        }

        if (cmd === "typingA" || cmd === "typingB") {
          const conversationId = Number(rest[0]);
          const isTypingRaw = (rest[1] || "").toLowerCase();
          if (
            !Number.isInteger(conversationId) ||
            !["true", "false"].includes(isTypingRaw)
          ) {
            console.log(`Usage: ${cmd} <conversationId> <true|false>`);
            continue;
          }
          const connection = cmd === "typingA" ? connectionA : connectionB;
          await connection.invoke(
            "Typing",
            conversationId,
            isTypingRaw === "true",
          );
          console.log("Hub Typing invoked.");
          continue;
        }

        if (cmd === "hubReadA" || cmd === "hubReadB") {
          const conversationId = Number(rest[0]);
          const upToMessageId = Number(rest[1]);
          if (
            !Number.isInteger(conversationId) ||
            !Number.isInteger(upToMessageId)
          ) {
            console.log(`Usage: ${cmd} <conversationId> <upToMessageId>`);
            continue;
          }
          const connection = cmd === "hubReadA" ? connectionA : connectionB;
          await connection.invoke("MarkRead", conversationId, upToMessageId);
          console.log("Hub MarkRead invoked.");
          continue;
        }

        if (cmd === "hubReactA" || cmd === "hubReactB") {
          const conversationId = Number(rest[0]);
          const messageId = Number(rest[1]);
          const reactionRaw = rest[2];
          if (
            !Number.isInteger(conversationId) ||
            !Number.isInteger(messageId) ||
            reactionRaw == null
          ) {
            console.log(
              `Usage: ${cmd} <conversationId> <messageId> <reaction|null>`,
            );
            continue;
          }
          const reaction =
            reactionRaw.toLowerCase() === "null" ? null : Number(reactionRaw);
          if (reaction !== null && !Number.isInteger(reaction)) {
            console.log("reaction must be integer 0..6 or null");
            continue;
          }
          const connection = cmd === "hubReactA" ? connectionA : connectionB;
          await connection.invoke(
            "SetReaction",
            conversationId,
            messageId,
            reaction,
          );
          console.log("Hub SetReaction invoked.");
          continue;
        }

        console.log("Unknown command. Type help.");
      } catch (err) {
        console.error("Command failed:", err?.message || err);
      }
    }

    await Promise.allSettled([connectionA.stop(), connectionB.stop()]);
    console.log("Disconnected.");
  } finally {
    rl.close();
  }
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
