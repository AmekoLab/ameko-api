# Chat Realtime Manual Test Guide (2 Accounts)

This guide verifies direct 1-1 chat realtime using SignalR and API fallback.

## Chat API Quick Reference

### 1) Create/Get Direct Conversation

```http
POST /api/v1/chat/conversations/direct/{targetUserId}
Authorization: Bearer <JWT>
```

### 2) Send Message (Normal + Reply in one API)

```http
POST /api/v1/chat/messages
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "conversationId": 123,
  "content": "hello",
  "messageType": 0,
  "parentMessageId": null
}
```

Notes:

1. Send normal message: set `parentMessageId = null` or omit.
2. Send reply: set `parentMessageId` to the old message id in the same conversation.

### 3) Get Conversation Messages

```http
GET /api/v1/chat/conversations/{conversationId}/messages?pageSize=20&cursor=<nextCursor>
Authorization: Bearer <JWT>
```

### 4) Get Conversation List

```http
GET /api/v1/chat/conversations?pageSize=20&cursor=<nextCursor>
Authorization: Bearer <JWT>
```

### 5) Mark Read

```http
POST /api/v1/chat/conversations/{conversationId}/read
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "upToMessageId": 456
}
```

### 6) Set Reaction (React + Unreact in one API)

```http
PUT /api/v1/chat/conversations/{conversationId}/messages/{messageId}/reaction
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "reaction": 1
}
```

Reaction enum values:

1. 0 = Like
2. 1 = Love
3. 2 = Haha
4. 3 = Yay
5. 4 = Wow
6. 5 = Sad
7. 6 = Angry

Unreact example:

```http
PUT /api/v1/chat/conversations/{conversationId}/messages/{messageId}/reaction
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "reaction": null
}
```

### 7) Hub Methods and Events

Hub endpoint:

1. `/hub?access_token=<JWT>`

Client -> Server methods:

1. `JoinConversation(conversationId)`
2. `LeaveConversation(conversationId)`
3. `SendMessage(sendMessageRequest)`
4. `Typing(conversationId, isTyping)`
5. `MarkRead(conversationId, upToMessageId)`
6. `SetReaction(conversationId, messageId, reaction)` where `reaction = null` means unreact.

Server -> Client events:

1. `messageReceived`
2. `typingChanged`
3. `readReceipt`
4. `reactionChanged`

## Prerequisites

1. Database is up and latest migrations are applied.
2. API is running.
3. Two test users exist: Account A and Account B.
4. Both users can login and get JWT tokens.
5. Hub endpoint is available at /hub.

## Start API

```powershell
cd d:\FPTU_LearningMaterial\Semester 9\SP26_CAPSTONE\FPTU.Capstone.AMKCollective
dotnet run --project FPTU.Capstone.AMKCollective.Api
```

## Step 1: Login Both Accounts

1. Login as account A and store JWT_A.
2. Login as account B and store JWT_B.

## Step 2: Create/Get Direct Conversation

Call API with account A:

```http
POST /api/v1/chat/conversations/direct/{targetUserId_of_B}
Authorization: Bearer JWT_A
```

Expected:

1. Response returns ConversationId.
2. OtherUserId equals account B id.

## Step 3: Connect Hub with JWT Query Token

Open two clients (or browser tabs) and connect:

1. Client A: /hub?access_token=JWT_A
2. Client B: /hub?access_token=JWT_B

Expected:

1. Both clients connect successfully.
2. No Unauthorized error on handshake.

## Step 4: Join Conversation Group

From both clients, invoke:

1. JoinConversation(conversationId)

Expected:

1. Both can join without HubException.

## Step 5: Send Message Realtime

From client A, invoke:

1. SendMessage({ conversationId, content: "hello from A", messageType: 0 })

Expected:

1. Client B receives event messageReceived in realtime.
2. Payload includes ConversationId, SenderId, Content, CreatedAt.

## Step 6: Typing Event

From client A, invoke:

1. Typing(conversationId, true)
2. Typing(conversationId, false)

Expected:

1. Client B receives typingChanged with correct user id and state.

## Step 7: Mark Read Event

From client B, invoke:

1. MarkRead(conversationId, upToMessageId)

Expected:

1. Client A receives readReceipt event.
2. API GET messages should reflect read state in recipient records.

## Step 8: REST Fallback

From account A, call:

```http
POST /api/v1/chat/messages
Authorization: Bearer JWT_A
Content-Type: application/json

{
  "conversationId": 123,
  "content": "fallback message",
  "messageType": 0
}
```

Expected:

1. API returns success with message payload.
2. If clients are connected and joined, receiver still gets messageReceived.

## Step 9: History Pagination

Call:

```http
GET /api/v1/chat/conversations/{conversationId}/messages?pageSize=20
Authorization: Bearer JWT_A
```

Then call next page using nextCursor.

Expected:

1. Items sorted by CreatedAt then Id.
2. nextCursor and hasMore behavior are consistent.

## Step 10: Conversation List

Call:

```http
GET /api/v1/chat/conversations?pageSize=20
Authorization: Bearer JWT_A
```

Expected:

1. Returns conversation rows with lastMessage and unreadCount.
2. Other user profile fields are correct.

## Common Failures Checklist

1. 401 on hub connect: check access_token query and JWT secret config.
2. HubException no access: user is not participant of conversation.
3. No realtime delivery: receiver did not join conversation group.
4. Messages saved but no event: check client event name messageReceived.
5. Duplicate messages in UI: ensure frontend deduplicates by message id.
