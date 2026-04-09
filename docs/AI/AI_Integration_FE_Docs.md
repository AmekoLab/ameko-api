# AMKCollective - AI Integration Documentation (FE/Admin Team)

Hệ thống AI hiện đã được tích hợp vào Backend để hỗ trợ Recommendation, Semantic Search và Order Issue Analysis. Dưới đây là các thông tin chi tiết.

---

## 1. AI Controller (`/api/AI`)

### A. Gợi ý Build bàn phím (Recommend)
Sử dụng AI để tư vấn cấu hình bàn phím (Kit, Switch, Keycap) dựa trên mô tả tự nhiên của người dùng.
- **Endpoint**: `POST /api/AI/recommend`
- **Request Body**:
  ```json
  {
    "userPrompt": "string",  // Mô tả yêu cầu (vđ: 'bàn phím 75% gõ đầm, led nền, giá rẻ')
    "shopId": "guid?",      // (Optional) Lọc linh kiện chỉ từ 1 shop cụ thể
    "baseKitId": "guid?"    // (Optional) Nếu user bắt đầu từ 1 kit xác định
  }
  ```
- **Response Body**:
  ```json
  {
    "kitId": "guid",          // ID của Keyboard Kit gợi ý
    "switchId": "guid",       // ID của Switch gợi ý
    "keycapId": "guid",       // ID của Keycap gợi ý
    "reasoning": "string",    // Giải thích tại sao AI chọn bộ set này
    "totalEstimatedPrice": 0  // Tổng giá dự kiến của 3 món
  }
  ```

### B. Tìm kiếm thông minh (Semantic Search)
Tìm kiếm theo ý nghĩa thay vì chỉ từ khóa.
- **Shop Search**: `GET /api/AI/search-shops?query=...&limit=10`
- **Build Search**: `GET /api/AI/search-builds?query=...&limit=10`
- **Response**: Trả về danh sách JSON Array các GUID (ID của Shop hoặc Build).
- **Cách dùng**: FE lấy danh sách ID này để hiển thị hoặc gọi API chi tiết tương ứng.

### C. Quản trị: Đồng bộ dữ liệu (Admin Only)
Endpoint này dùng để cập nhật (Sync) toàn bộ thông tin từ DB lên Qdrant khi có thay đổi lớn hoặc khi khởi tạo hệ thống lần đầu.
- **Endpoint**: `POST /api/AI/sync-all`
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
    "sentiment": "Neutral",           // Thái độ khách hàng
    "summary": "...",                 // Tóm tắt ngắn gọn lý do
    "recommendation": "Approve",      // Gợi ý cho Shop (Approve/Reject)
    "confidenceScore": 0.8            // Độ tin cậy của AI (0-1)
  }
  ```

---

## 3. Lưu ý kỹ thuật cho FE

> [!WARNING]
> **Latency (Độ trễ)**: Các API AI (đặc biệt là `recommend`) sử dụng LLM nên có thể mất 3-7 giây để phản hồi. FE **bắt buộc** phải có hiệu ứng chờ (Loading Spinner/Skeleton).

> [!NOTE]
> **Trường Embedding**: Các model `Shop`, `Build`, `Part` giờ đây có thêm trường `embedding` trong DB. FE có thể nhận được trường này nhưng **không được chỉnh sửa** nó khi gọi các API Update/Create. READ-ONLY
