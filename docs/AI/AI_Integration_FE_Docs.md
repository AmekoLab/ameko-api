# AMKCollective - AI Integration Documentation (FE/Admin Team)

Hệ thống AI hiện đã được tích hợp vào Backend để hỗ trợ Recommendation, Semantic Search và Order Issue Analysis. Dưới đây là các thông tin chi tiết.

---

## 1. AI Controller (`/api/v1/AI`)

### A. Gợi ý Build bàn phím (Recommend)

Sử dụng AI để tư vấn cấu hình bàn phím (Kit, Switch, Keycap) dựa trên mô tả tự nhiên của người dùng.

- **Endpoint**: `POST /api/v1/AI/recommend`
- **Request Body**:
  ```json
  {
    "userPrompt": "string", // Mô tả yêu cầu (vđ: 'bàn phím 75% gõ đầm, led nền, giá rẻ')
    "shopId": "guid?", // (Optional) Lọc linh kiện chỉ từ 1 shop cụ thể
    "baseKitId": "guid?", // (Optional) Context cho flow part-based/custom builder
    "assembledProductId": "guid?" // (Optional) Context cho flow assembled-product
  }
  ```
- **Response Body**:
  ```json
  {
    "kitId": "guid?",
    "switchId": "guid?",
    "keycapId": "guid?",
    "assembledProductId": "guid?",
    "reasoning": "string",
    "totalEstimatedPrice": 0,
    "items": [
      {
        "id": "guid",
        "recommendationKind": "part|assembled",
        "name": "string",
        "price": 0,
        "imageUrl": "string?",
        "detailPath": "string?",
        "shopId": "guid?",
        "shopName": "string?",
        "shopAvatarUrl": "string?"
      }
    ]
  }
  ```

#### Payload mẫu FE (dùng ngay)

1. Flow Part Builder (gợi ý Kit/Switch/Keycap)

Request:

```json
{
  "userPrompt": "Mình cần build gõ văn phòng, êm và budget 3 triệu",
  "shopId": "6f85cfd0-3c58-4a17-8970-7c8f5fb7ef1f",
  "baseKitId": "f4f1a0b9-c27b-4a9d-94c6-0f2f47744626"
}
```

Response (ví dụ):

```json
{
  "kitId": "f4f1a0b9-c27b-4a9d-94c6-0f2f47744626",
  "switchId": "53cc798a-b8b3-44f2-b0aa-c5f9f5d2e964",
  "keycapId": "a4deeb11-d447-4fcf-bb7f-786cb8e2cc89",
  "assembledProductId": null,
  "reasoning": "...",
  "totalEstimatedPrice": 2890000,
  "items": [
    {
      "id": "f4f1a0b9-c27b-4a9d-94c6-0f2f47744626",
      "recommendationKind": "part",
      "name": "Neo65 Kit",
      "price": 1800000,
      "imageUrl": "https://.../neo65.png",
      "detailPath": "/shop/product/f4f1a0b9-c27b-4a9d-94c6-0f2f47744626",
      "shopId": "6f85cfd0-3c58-4a17-8970-7c8f5fb7ef1f",
      "shopName": "AmekoLab",
      "shopAvatarUrl": "https://.../shop-logo.png"
    }
  ]
}
```

2. Flow Assembled Product (gợi ý nguyên bộ assembled)

Request:

```json
{
  "userPrompt": "Mình muốn mua nguyên bộ assembled để dùng ngay",
  "assembledProductId": "1e2fecaf-4945-4886-815f-5adcbbe8a5e8"
}
```

Response (ví dụ):

```json
{
  "kitId": null,
  "switchId": null,
  "keycapId": null,
  "assembledProductId": "1e2fecaf-4945-4886-815f-5adcbbe8a5e8",
  "reasoning": "...",
  "totalEstimatedPrice": 4500000,
  "items": [
    {
      "id": "1e2fecaf-4945-4886-815f-5adcbbe8a5e8",
      "recommendationKind": "assembled",
      "name": "Assembled Neo65 Office Edition",
      "price": 4500000,
      "imageUrl": "https://.../assembled.png",
      "detailPath": "/shop/assembled-product/1e2fecaf-4945-4886-815f-5adcbbe8a5e8",
      "shopId": "6f85cfd0-3c58-4a17-8970-7c8f5fb7ef1f",
      "shopName": "AmekoLab",
      "shopAvatarUrl": "https://.../shop-logo.png"
    }
  ]
}
```

> FE note: Ưu tiên render từ `items[]` (ảnh, avatar shop, link detail). Các field `kitId/switchId/keycapId` được giữ để backward compatibility.

### B. Tìm kiếm thông minh (Semantic Search)

Tìm kiếm theo ý nghĩa thay vì chỉ từ khóa.

- **Shop Search**: `GET /api/AI/search-shops?query=...&limit=10`
- **Build Search**: `GET /api/AI/search-builds?query=...&limit=10`
- **Response**: Trả về danh sách JSON Array các GUID (ID của Shop hoặc Build).
- **Cách dùng**: FE lấy danh sách ID này để hiển thị hoặc gọi API chi tiết tương ứng.

### C. Quản trị: Đồng bộ dữ liệu (Admin Only)

Endpoint này dùng để cập nhật (Sync) toàn bộ thông tin từ DB lên Qdrant khi có thay đổi lớn hoặc khi khởi tạo hệ thống lần đầu.

- **Endpoint**: `POST /api/v1/AI/sync-all`
- **Chức năng**:
  - Lấy toàn bộ dữ liệu Shop, Build, Part từ DB.
  - Tạo Vector Embedding cho mỗi đối tượng.
  - Lưu vào DB (cột `Embedding`) và đẩy lên Qdrant (Vector Database).
- **Response**: `{"message": "Sync process started."}`

---

## 2. Phân tích khiếu nại (Order Issue AI Analysis)

Tự động hỗ trợ Shop Owner đánh giá yêu cầu khách hàng.

- **Dữ liệu**: Nằm trong trường `aiAnalysisResult` của `OrderIssueResponse`.
- **API**: Thừa hưởng từ các API Order Issue hiện có (`/api/OrderIssues/{id}`).
- **Format dữ liệu**: Trả về một chuỗi JSON string (Cần `JSON.parse` ở FE để lấy object).
- **Cấu trúc Object sau khi parse**:
  ```json
  {
    "category": "OrderCancellation", // Phân loại
    "sentiment": "Neutral", // Thái độ khách hàng
    "summary": "...", // Tóm tắt ngắn gọn lý do
    "recommendation": "Approve", // Gợi ý cho Shop (Approve/Reject)
    "confidenceScore": 0.8 // Độ tin cậy của AI (0-1)
  }
  ```

---

## 3. Lưu ý kỹ thuật cho FE

> [!WARNING]
> **Latency (Độ trễ)**: Các API AI (đặc biệt là `recommend`) sử dụng LLM nên có thể mất 3-7 giây để phản hồi. FE **bắt buộc** phải có hiệu ứng chờ (Loading Spinner/Skeleton).

> [!NOTE]
> **Trường Embedding**: Các model `Shop`, `Build`, `Part` giờ đây có thêm trường `embedding` trong DB. FE có thể nhận được trường này nhưng **không được chỉnh sửa** nó khi gọi các API Update/Create. READ-ONLY
