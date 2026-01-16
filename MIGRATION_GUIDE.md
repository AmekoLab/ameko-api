# 📚 Hướng dẫn Migration cho AMKCollective

## ✅ Đã setup sẵn:

- ✔️ DbContext: `ApplicationDbContext`
- ✔️ Connection String: MySQL (đã config trong appsettings.Development.json)
- ✔️ EF Core Tools: v8.0.11 (đã cài trong Infrastructure.csproj)
- ✔️ Tất cả Entity configurations

---

## 🚀 Các bước tạo Migration

### 1️⃣ **Mở Terminal ở thư mục gốc solution** (nơi có file .sln)

```powershell
cd "d:\FPTU_LearningMaterial\Semester 9\SP26_CAPSTONE\FPTU.Capstone.AMKCollective"
```

### 2️⃣ **Tạo Migration đầu tiên**

```powershell
dotnet ef migrations add InitialCreate --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api --output-dir Data/Migrations
```

**Giải thích:**

- `InitialCreate`: Tên migration (có thể đổi thành tên khác như `Initial`, `CreateDatabase`, v.v.)
- `--project`: Project chứa DbContext (Infrastructure)
- `--startup-project`: Project có appsettings.json và Program.cs (API)
- `--output-dir`: Thư mục lưu migration (Data/Migrations)

### 3️⃣ **Kiểm tra Migration vừa tạo**

Sau khi chạy lệnh, sẽ có thư mục mới:

```
Infrastructure/Data/Migrations/
  └── 20260116xxxxxx_InitialCreate.cs
  └── ApplicationDbContextModelSnapshot.cs
```

### 4️⃣ **Apply Migration vào Database**

```powershell
dotnet ef database update --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api
```

---

## 🔄 Các lệnh Migration thường dùng

### Tạo migration mới (sau khi thay đổi entity)

```powershell
dotnet ef migrations add TenMigration --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api --output-dir Data/Migrations
```

### Xem danh sách migrations

```powershell
dotnet ef migrations list --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api
```

### Xóa migration cuối cùng (nếu chưa apply)

```powershell
dotnet ef migrations remove --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api
```

### Rollback về migration trước đó

```powershell
dotnet ef database update TenMigrationTruocDo --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api
```

### Xóa toàn bộ database

```powershell
dotnet ef database drop --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api
```

### Tạo SQL script từ migration (không chạy trực tiếp)

```powershell
dotnet ef migrations script --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api --output migration.sql
```

---

## 📋 Best Practices: Khi nào nên tạo Migration?

### ✅ **NÊN tạo migration khi:**

1. **Thêm/xóa Entity mới** (bảng mới)

   ```powershell
   Add-Migration "AddWalletTable"
   Add-Migration "RemoveOldLogTable"
   ```

2. **Thêm/xóa/sửa Field quan trọng**

   ```powershell
   Add-Migration "AddUserProfilePicture"
   Add-Migration "UpdateOrderStatusEnum"
   ```

3. **Thay đổi Relationships** (foreign key, navigation)

   ```powershell
   Add-Migration "UpdateOrderCustomerRelationship"
   ```

4. **Thêm Index cho Performance**

   ```powershell
   Add-Migration "AddEmailAndUsernameIndexes"
   ```

5. **Thêm/sửa Seed Data**

   ```powershell
   Add-Migration "AddDefaultCategories"
   ```

6. **Deploy lên Environment khác**
   - Migration đã apply ở DEV → Apply lên STAGING/PRODUCTION

### ❌ **KHÔNG NÊN tạo migration khi:**

1. **Đang thử nghiệm, chưa chắc chắn**

   - Dùng `dotnet ef migrations remove` nếu chưa push Git
   - Hoặc `dotnet ef database update TenMigrationTruoc` để rollback

2. **Chỉ đổi tên biến trong code** (không ảnh hưởng DB schema)

3. **Thay đổi Business Logic** (không liên quan DB)

4. **Tạo quá nhiều migration nhỏ** (EntityV1, V2, V3...)
   - ❌ BAD: 33 migrations cho initial setup
   - ✅ GOOD: 1 migration `InitialCreate` cho toàn bộ schema ban đầu

### 🎯 **Naming Convention:**

| ❌ Tên không tốt | ✅ Tên tốt                 | Lý do                  |
| ---------------- | -------------------------- | ---------------------- |
| `EntityV1`       | `InitialCreate`            | Rõ ràng đây là lần đầu |
| `Update`         | `AddUserBioField`          | Cụ thể thay đổi gì     |
| `Fix`            | `FixEmailUniqueConstraint` | Biết fix vấn đề gì     |
| `New`            | `AddShopRatingSystem`      | Rõ feature được thêm   |
| `Change`         | `UpdateOrderStatusToEnum`  | Rõ ràng thay đổi gì    |

**Quy tắc đặt tên:**

- Bắt đầu bằng động từ: `Add`, `Update`, `Remove`, `Fix`, `Rename`
- Cụ thể, rõ ràng: `AddUserProfilePicture` > `UpdateUser`
- Tiếng Anh chuẩn để team dễ đọc

### 📈 **Workflow đúng cho dự án:**

```
Phase 1: Initial Development (BẠN ĐANG Ở ĐÂY ✅)
├── InitialCreate
│   ├── Tất cả 26 tables
│   ├── All relationships
│   ├── Seed data (3 Roles)
│   └── All indexes and constraints
└── ✅ DONE!

Phase 2: Feature Development (SAU NÀY)
├── AddWalletSystem
│   ├── Wallet table
│   ├── WalletTransaction table
│   └── Relationships với User
│
├── AddProductReviewSystem
│   ├── Review table
│   ├── ReviewImage table
│   └── Add rating to ShopProfile
│
└── AddNotificationPreferences
    └── Add NotificationSettings to User

Phase 3: Optimization & Fixes
├── AddPerformanceIndexes
│   ├── Index on Orders.CreatedAt
│   ├── Index on Models.CategoryId
│   └── Composite indexes
│
├── UpdateOrderStatusEnum
│   └── Add new status values
│
└── FixEmailCaseSensitivity
    └── Update email index to case-insensitive
```

### 🔄 **So sánh cụ thể:**

| Aspect             | ❌ Approach không tốt (như ảnh) | ✅ Approach đúng (hiện tại) |
| ------------------ | ------------------------------- | --------------------------- |
| **Số lượng files** | 33 migrations (EntityV1-V33)    | 1 migration InitialCreate   |
| **Khả năng đọc**   | Không biết migration làm gì     | Tên rõ ràng, dễ hiểu        |
| **Quản lý**        | Khó track, dễ conflict          | Dễ quản lý, ít conflict     |
| **Review code**    | Phải mở 33 files                | Chỉ cần xem 1 file          |
| **Rollback**       | Phức tạp                        | Đơn giản                    |
| **Git history**    | 33 commits rối                  | Clean history               |

### 💡 **Tips thực tế:**

1. **Trong Development:**

   - Nếu migration chưa push Git → Có thể `remove` và tạo lại
   - Nếu đã push nhưng chưa ai pull → Có thể force push sau khi sửa
   - Nếu team đã pull → Tạo migration mới để fix

2. **Trước Production:**

   - Review tất cả migrations cẩn thận
   - Test migration trên staging trước
   - Backup database trước khi migrate
   - Có plan rollback

3. **Squash Migrations (Nâng cao):**
   - Khi có quá nhiều migrations trong dev
   - Có thể gộp thành 1 migration trước khi release
   - Chỉ làm khi chưa deploy production

---

## ⚡ Lệnh rút gọn (để gõ nhanh hơn)

Tạo file PowerShell script trong thư mục gốc: `migrate.ps1`

```powershell
# Tạo migration mới
function Add-Migration {
    param([string]$Name)
    dotnet ef migrations add $Name --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api --output-dir Data/Migrations
}

# Apply migration
function Update-Database {
    dotnet ef database update --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api
}

# Xóa migration cuối
function Remove-LastMigration {
    dotnet ef migrations remove --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api
}

# List migrations
function Get-Migrations {
    dotnet ef migrations list --project FPTU.Capstone.AMKCollective.Infrastructure --startup-project FPTU.Capstone.AMKCollective.Api
}
```

**Cách dùng:**

```powershell
# Load script
. .\migrate.ps1

# Tạo migration
Add-Migration "AddUserRoleRelationship"

# Apply
Update-Database

# Xem danh sách
Get-Migrations
```

---

## 🎯 Các bảng sẽ được tạo

Migration sẽ tạo các bảng sau trong database MySQL:

1. **Roles** (có sẵn 3 roles: Admin, Customer, Artisan)
2. **Users** (với RoleId foreign key)
3. **Categories**
4. **Models**
5. **ShopProfiles**
6. **AssembledProducts**
7. **KitDesignOptions**
8. **ProductAssembledDetails**
9. **Orders**
10. **OrderGroups**
11. **Payments**
12. **OrderItems**
13. **OrderItemComponents**
14. **Vouchers**
15. **VoucherUsageLogs**
16. **Follows**
17. **Feedbacks**
18. **Conversations**
19. **Messages**
20. **Notifications**
21. **RefreshTokens**
22. **CommunityPosts**
23. **PostReactions**
24. **PostComments**
25. **CommunityAttachments**
26. **\_\_EFMigrationsHistory** (bảng của EF Core tracking migrations)

---

## ⚠️ Lưu ý quan trọng

1. **Backup database trước khi chạy migration** (nếu có data quan trọng)
2. **Kiểm tra connection string** trong appsettings.Development.json
3. **Đảm bảo MySQL server đang chạy** và có thể connect được
4. **Không xóa file migration** đã apply vào database
5. **BaseEntity** đã được áp dụng cho tất cả entities → tất cả bảng sẽ có: Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted

---

## 🐛 Troubleshooting

### Lỗi: "Unable to create an object of type 'ApplicationDbContext'"

➡️ Chạy từ thư mục có file .sln và đảm bảo --startup-project đúng

### Lỗi: "A network-related or instance-specific error occurred"

➡️ Kiểm tra connection string và đảm bảo MySQL server đang chạy

### Lỗi: "The migration '...' has already been applied to the database"

➡️ Migration đã được apply rồi, không cần chạy lại

### Lỗi: "Could not load file or assembly 'Microsoft.EntityFrameworkCore.Design'"

➡️ Cài EF Core tools globally:

```powershell
dotnet tool install --global dotnet-ef
```

---

## 📖 Tham khảo

- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [EF Core CLI Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
