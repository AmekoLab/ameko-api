# Hướng dẫn Tích hợp Frontend: Trạng thái sản phẩm giỏ hàng

Hệ thống đã bổ sung cơ chế kiểm tra tính khả dụng của sản phẩm lắp ráp (`AssembledProduct`) dựa trên trạng thái của tất cả linh kiện thành phần. Frontend cần cập nhật để xử lý các thuộc tính mới trong API giỏ hàng.

## 1. DTO Thay đổi

Trong `OrderItemResponse` (API `GetMyCart`), có 2 trường mới quan trọng:
- `IsAvailable` (bool): Xác định mục này có thể tiến hành thanh toán hay không.
- `StatusMessage` (string?): Chứa thông báo lỗi chi tiết nếu sản phẩm không khả dụng.

### Ví dụ JSON Response:
```json
{
  "orderItemId": "...",
  "productName": "Keyboard Assembled",
  "isAvailable": false,
  "statusMessage": "Linh kiện 'Base Kit X' không đủ tồn kho.",
  "unitPrice": 1500000,
  "quantity": 1,
  "isCustom": false
}
```

## 2. Các trường hợp không khả dụng

Frontend nên kiểm tra `isAvailable` và hiển thị cảnh báo cho người dùng trong các trường hợp sau:
- **Linh kiện bị xóa**: Một trong các Model cấu thành đã bị shop xóa mềm (`IsDeleted`).
- **Linh kiện bị khóa**: Một trong các Model cấu thành bị shop đặt `IsActive = false`.
- **Hết hàng**: Một trong các Model cấu thành không đủ `StockQuantity` cho số lượng yêu cầu trong giỏ.
- **Sản phẩm cha lỗi**: Bản thân `AssembledProduct` bị xóa hoặc khóa.

## 3. Khuyến nghị UI/UX

1.  **Vô hiệu hóa Checkout**: Nếu giỏ hàng có ít nhất một mục `isAvailable == false`, nút "Thanh toán" (Checkout) nên bị vô hiệu hóa hoặc chuyển hướng người dùng quay lại giỏ để xóa mục lỗi.
2.  **Hiển thị lỗi**: Sử dụng `statusMessage` để hiển thị ngay dưới tên sản phẩm trong giỏ hàng (ví dụ: text màu đỏ).
3.  **Xử lý AddToCart**: Nếu người dùng cố gắng thêm một sản phẩm lắp ráp không hợp lệ, API sẽ trả về lỗi `400 Bad Request` với message tương tự. Hãy hiển thị Toast thông báo lỗi này.

> [!IMPORTANT]
> Nhờ logic **Lọc động**, các sản phẩm không hợp lệ sẽ tự động biến mất khỏi danh sách tìm kiếm/AI Recommend. Tuy nhiên, nếu sản phẩm đã nằm sẵn trong giỏ hàng của người dùng trước khi nó bị hỏng, các thuộc tính trên là cách duy nhất để thông báo cho khách hàng.
