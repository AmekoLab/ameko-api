# Chat Node Tester

CLI nho de test chat 1-1 khong can FE UI.

## 1. Cai dat

```bash
cd tools/chat-node-tester
npm install
```

## 2. Chay

```bash
npm start
```

Nhap:

1. Base URL API (mac dinh: http://localhost:5000)
2. JWT A
3. JWT B

Tool se:

1. Connect 2 SignalR client A/B vao `/hub`
2. In realtime events: `messageReceived`, `typingChanged`, `readReceipt`, `reactionChanged`

## 3. Lenh chinh

1. `help`
2. `whoami`
3. `conv <targetUserId>`: Tao/get direct conversation bang token A
4. `joinA <conversationId>`
5. `joinB <conversationId>`
6. `joinBoth <conversationId>`
7. `sendA <conversationId> <text>`
8. `sendB <conversationId> <text>`
9. `replyA <conversationId> <parentMessageId> <text>`
10. `replyB <conversationId> <parentMessageId> <text>`
11. `reactA <conversationId> <messageId> <reaction|null>`
12. `reactB <conversationId> <messageId> <reaction|null>`
13. `readA <conversationId> <upToMessageId>`
14. `readB <conversationId> <upToMessageId>`
15. `msgsA <conversationId> [pageSize]`
16. `msgsB <conversationId> [pageSize]`
17. `exit`

Reaction map:

1. 0 Like
2. 1 Love
3. 2 Haha
4. 3 Yay
5. 4 Wow
6. 5 Sad
7. 6 Angry

## 4. Quick flow de test

1. `whoami` de lay user id trong token
2. `conv <userId_B>` tao room
3. `joinBoth <conversationId>`
4. `sendA <conversationId> hello`
5. `replyB <conversationId> <messageId> hi`
6. `reactA <conversationId> <messageId> 1`
7. `reactA <conversationId> <messageId> null`
