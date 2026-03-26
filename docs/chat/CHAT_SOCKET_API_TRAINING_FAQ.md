# Chat Training: API va Socket phoi hop nhu the nao?

Tai lieu nay dung de training team ve tu duy dung chat 1-1 trong he thong hien tai.

## 1. Y chinh can nho

1. API la kenh thao tac nghiep vu, luu tru, validate, phan trang, truy van lich su.
2. Socket la kenh thong bao realtime, giam do tre cap nhat UI.
3. Khong co API thi khong co nguon su that de dong bo lai khi mat ket noi, doi thiet bi, reload app.
4. Khong co Socket thi van chat duoc, nhung trai nghiem cham va ton nhieu polling.

## 2. Tra loi nhanh cau hoi lon

### Hoi 1: Da co Socket roi, tai sao can them API?

Vi Socket khong thay the toan bo backend contract:

1. Socket gioi ve truyen su kien realtime, khong phai noi uu tien cho pagination, filter, retry nghiep vu phuc tap.
2. API dam nhan luu DB, validate rule, authz, tra ve loi co cau truc va ho tro test/deploy on dinh.
3. API la fallback khi Socket ngat, reconnect cham, app mo lai.
4. API la nguon dong bo lich su va state tai thoi diem bat ky.

### Hoi 2: Socket cung dua data, sao phai code them API?

Data realtime va data truy van la hai bai toan khac nhau:

1. Realtime data: ban tin moi vua phat sinh, can day ngay den client online.
2. Query data: lay danh sach conversation, lay lich su, page tiep, unread count.
3. Governance: API de version hoa, log, monitor, bao mat, gioi han tan suat.

### Hoi 3: Socket va API co moi quan he gi?

Moi quan he dung nhat la bo sung cho nhau:

1. API ghi va doc du lieu chuan.
2. Socket phat su kien cap nhat nhanh den nhung client lien quan.
3. Client nhan su kien tu Socket, neu thieu du lieu hoac nghi ngo lech thi goi API de sync lai.

## 3. Mo hinh de nho de day team

1. API = Source of Truth (nguon su that).
2. Socket = Real-time Notification (chuong bao ngay).
3. Client state = Cache tam + merge tu API va Socket.

## 4. Luong ket noi giua Socket Client va API

### Luong khoi tao man hinh chat

1. Client login, lay JWT.
2. Client goi API lay danh sach conversation.
3. Client mo ket noi hub voi access_token.
4. Client join conversation dang mo.
5. Khi co su kien moi, UI cap nhat ngay.
6. Neu reconnect hoac nghi mat su kien, goi API lay lai history theo cursor.

### Luong gui tin nhan

1. Client gui API send message hoac invoke hub send message (duong nao cung ve service va DB).
2. Server luu message vao DB.
3. Server phat su kien messageReceived qua Socket cho participant.
4. Client nhan su kien, append vao UI.
5. Client co the goi API de doi chieu neu can.

### Luong reply

1. Reply dung chung API send message.
2. Truyen them parentMessageId cua tin goc.
3. Backend validate parentMessageId ton tai va thuoc dung conversation.

### Luong reaction va unreact

1. Dung 1 API duy nhat set reaction.
2. reaction co gia tri: dat/cap nhat reaction.
3. reaction = null: bo reaction (unreact).
4. Backend phat su kien reactionChanged de cac client cap nhat ngay.

## 5. Cac endpoint va method lien quan trong du an

### API

1. POST /api/v1/chat/conversations/direct/{targetUserId}
2. POST /api/v1/chat/messages
3. GET /api/v1/chat/conversations/{conversationId}/messages
4. GET /api/v1/chat/conversations
5. POST /api/v1/chat/conversations/{conversationId}/read
6. PUT /api/v1/chat/conversations/{conversationId}/messages/{messageId}/reaction

### Hub methods

1. JoinConversation(conversationId)
2. LeaveConversation(conversationId)
3. SendMessage(sendMessageRequest)
4. Typing(conversationId, isTyping)
5. MarkRead(conversationId, upToMessageId)
6. SetReaction(conversationId, messageId, reaction)

### Hub events

1. messageReceived
2. typingChanged
3. readReceipt
4. reactionChanged

## 6. Vi du tinh huong de training

### Tinh huong A: User dang online ca 2 ben

1. A gui tin.
2. B nhan messageReceived ngay lap tuc.
3. Khong can polling.

### Tinh huong B: B tam mat mang 30 giay

1. A gui 5 tin trong luc B mat mang.
2. Socket khong deliver ngay cho B.
3. B reconnect xong, client goi API lay messages theo cursor.
4. B van thay du 5 tin, khong mat du lieu.

### Tinh huong C: App B bi kill, mo lai

1. Socket session cu mat.
2. App mo lai, login va connect lai.
3. Goi API lay conversation list + unread count.
4. Vao room, goi API lay history tiep.

## 7. FAQ mo rong cho team

1. Co the chi dung Socket khong dung API duoc khong?
   Khong nen. Ban se gap kho khi dong bo lai state, pagination, permission va monitor.

2. Co the chi dung API polling, bo Socket duoc khong?
   Duoc, nhung trai nghiem chat kem, delay cao, ton tai nguyen.

3. Tai sao can JoinConversation?
   De server biet connection nao dang quan tam room nao, phat dung nguoi.

4. Tai sao van can auth cho Hub neu API da auth?
   Vi Hub la endpoint khac. Moi kenh vao he thong deu phai auth.

5. Khi nao client nen goi API sau khi nhan event Socket?
   Khi can bo sung du lieu, nghi ngoi out-of-order, reconnect, hoac app resume.

6. Event den truoc API response thi sao?
   UI nen dedupe theo messageId va uu tien ban ghi moi nhat theo timestamp/messageId.

7. Unreact la xoa row hay set null?
   Hien tai la set MessageReaction = null tren recipient record cua user.

8. Reply co tao endpoint rieng khong?
   Khong can. Dung chung endpoint send message voi parentMessageId.

9. Neu parentMessageId sai conversation thi sao?
   Backend reject de tranh du lieu reply sai ngu canh.

10. Socket co thay duoc lich su cu khong?
    Khong phai muc tieu chinh. Lich su lay qua API.

## 8. Nguyen tac code FE de tranh loi

1. Tao ChatService tach 2 module: ChatApiClient va ChatRealtimeClient.
2. Quan ly state tap trung theo conversationId.
3. Dedupe message theo messageId.
4. Co co che resync sau reconnect: goi API voi cursor.
5. Khong coi event Socket la nguon su that duy nhat.
6. Khi send optimistic UI, phai co logic reconcile voi ket qua that tu backend.

## 9. Checklist huan luyen cho team moi

1. Hieu ro API va Socket giai bai toan gi.
2. Demo duoc 3 tinh huong A/B/C o tren.
3. Lam duoc reply bang parentMessageId.
4. Lam duoc react va unreact bang 1 endpoint.
5. Viet duoc luong reconnect + resync.
6. Giai thich duoc vi sao van can API khi da co Socket.

## 10. Tham chieu tai lieu lien quan trong repo

1. CHAT_MANUAL_TEST_GUIDE.md
2. GUIDELINE_SOCKET.md
