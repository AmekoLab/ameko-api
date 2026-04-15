using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using Microsoft.Extensions.Configuration;
using FPTU.Capstone.AMKCollective.Application.Helpers;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class WarrantyService : IWarrantyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly IWalletService _walletService;
        private readonly INotificationService _notificationService;
        private readonly IAIService _aiService;

        public WarrantyService(
            IUnitOfWork unitOfWork, 
            IPaymentService paymentService, 
            IConfiguration configuration,
            IWalletService walletService,
            INotificationService notificationService,
            IAIService aiService)
        {
            _unitOfWork = unitOfWork;
            _paymentService = paymentService;
            _configuration = configuration;
            _walletService = walletService;
            _notificationService = notificationService;
            _aiService = aiService;
        }

        // =================================================================
        // PHASE 1 — CUSTOMER CREATES WARRANTY / RETURN ISSUE
        // =================================================================

        public async Task<IEnumerable<WarrantyIssueResponse>> CreateWarrantyIssueAsync(Guid userId, Guid orderGroupId, CreateWarrantyIssueDto? dto, CancellationToken ct = default)
        {
            // ── 1. Get all Orders in Group ──
            var groupOrders = await _unitOfWork.Orders.GetOrdersByGroupIdAsync(orderGroupId);
            if (groupOrders == null || !groupOrders.Any())
                throw new KeyNotFoundException($"Order Group {orderGroupId} not found.");

            // ── 2. Validate Ownership + Window ──
            if (groupOrders.First().CustomerId != userId)
                throw new UnauthorizedAccessException("This order group does not belong to you.");

            var windowDays = _configuration.GetValue<int>("WarrantySettings:WarrantyRequestWindowDays", 7);
            foreach (var order in groupOrders)
            {
                if (order.OrderStatus == OrderStatus.Completed)
                {
                    var completedDate = order.UpdatedAt ?? order.CreatedAt;
                    if ((DateTime.UtcNow - completedDate).TotalDays > windowDays)
                    {
                        throw new InvalidOperationException($"The warranty/return request window (max {windowDays} days after completion) has closed for Order #{order.Id}.");
                    }
                }
            }

            // ── Use default values if dto is null ──
            var itemIds = dto?.OrderItemIds ?? new List<Guid>();
            var type = dto?.Type ?? OrderIssueType.WarrantyClaim;
            var reason = dto?.Reason ?? "Customer request";
            var description = dto?.Description ?? "Warranty/Return request for order group";
            var evidenceUrl = dto?.EvidenceUrl;

            // ── 2b. Strict Delivery Check ──
            // Nếu là Warranty hoặc Return -> Bắt buộc tất cả đơn trong group phải Completed
            if (type == OrderIssueType.WarrantyClaim || type == OrderIssueType.ReturnRequest)
            {
                var nonCompletedOrders = groupOrders.Where(o => o.OrderStatus != OrderStatus.Completed).ToList();
                if (nonCompletedOrders.Any())
                {
                    var ids = string.Join(", ", nonCompletedOrders.Select(o => o.Id));
                    throw new InvalidOperationException($"You can only create a Warranty/Return request after the order has been successfully delivered (Completed). Non-completed orders: {ids}");
                }
            }

            // ── 3. Group items by OrderId + VALIDATE Existence ──
            var ordersToProcess = new Dictionary<Guid, List<Guid>>();

            if (itemIds.Any())
            {
                foreach (var itemId in itemIds)
                {
                    var parentOrder = groupOrders.FirstOrDefault(o => o.OrderItems != null && o.OrderItems.Any(oi => oi.Id == itemId));
                    if (parentOrder == null)
                        throw new KeyNotFoundException($"Item with ID {itemId} was not found in Order Group {orderGroupId}.");

                    if (!ordersToProcess.ContainsKey(parentOrder.Id))
                        ordersToProcess[parentOrder.Id] = new List<Guid>();

                    ordersToProcess[parentOrder.Id].Add(itemId);
                }
            }
            else
            {
                // Whole Group Level -> All orders in group
                foreach (var o in groupOrders)
                    ordersToProcess[o.Id] = new List<Guid>();
            }

            var responses = new List<WarrantyIssueResponse>();

            // ── 4. Process each Order ──
            foreach (var kvp in ordersToProcess)
            {
                var orderId = kvp.Key;
                var currentOrderItems = kvp.Value;
                var order = groupOrders.First(o => o.Id == orderId);

                // Skip cancelled/refunded
                if (order.OrderStatus == OrderStatus.Cancelled || order.OrderStatus == OrderStatus.Refunded)
                    continue;

                // Determine Tag
                string searchTag = currentOrderItems.Any() ? $"[Items:{string.Join(",", currentOrderItems)}]" : "[OrderLevel]";

                // Overlap Check
                var existingIssues = await _unitOfWork.OrderIssues.GetByOrderIdAsync(orderId);
                var activeIssues = existingIssues.Where(i =>
                    i.Status != OrderIssueStatus.Rejected &&
                    i.Status != OrderIssueStatus.AutoCancelled).ToList();

                foreach (var active in activeIssues)
                {
                    if (string.IsNullOrEmpty(active.Description)) continue;
                    
                    if (active.Description.StartsWith("[OrderLevel]"))
                        throw new InvalidOperationException($"An active order-level issue already exists for Order {orderId} (Issue ID: {active.Id}, Status: {active.Status}).");
                    
                    if (searchTag == "[OrderLevel]")
                        throw new InvalidOperationException($"Order {orderId} already has active item-level issues (e.g., Issue ID: {active.Id}, Status: {active.Status}).");

                    if (active.Description.StartsWith("[Items:") && searchTag.StartsWith("[Items:"))
                    {
                        var existingIds = active.Description.Split(']')[0].Substring(7).Split(',');
                        if (existingIds.Intersect(currentOrderItems.Select(x => x.ToString())).Any())
                            throw new InvalidOperationException($"One or more items in Order {orderId} are already in an active request (Issue ID: {active.Id}, Status: {active.Status}).");
                    }
                }

                var issueToCreate = new OrderIssue
                {
                    OrderId = orderId,
                    UserId = userId,
                    Type = type,
                    Reason = reason,
                    Description = $"{searchTag} {description}",
                    EvidenceUrl = evidenceUrl,
                    IsSystemValid = true,
                    Status = OrderIssueStatus.InProgress,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Order = order // Ensure Order is attached for MapToResponse
                };

                await _unitOfWork.OrderIssues.AddAsync(issueToCreate);

                // AI Design Support: Add AI analysis to the warranty issue
                try
                {
                    issueToCreate.AIAnalysisResult = await _aiService.AnalyzeOrderIssueAsync(issueToCreate, order);
                }
                catch
                {
                    issueToCreate.AIAnalysisResult = "AI processing failed.";
                }

                await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
                {
                    OrderIssueId = issueToCreate.Id,
                    ActionById = userId,
                    ActionByRole = RoleType.Customer,
                    Action = OrderIssueAction.Create,
                    Comment = $"Warranty/Return request created for Order {orderId}. Items: {searchTag}",
                    CreatedAt = DateTime.UtcNow
                });

                // ── Notification to Shop Owner ──
                if (order.ShopId.HasValue)
                {
                    var shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
                    if (shop != null)
                    {
                        await _notificationService.SendNotificationAsync(shop.UserId, 
                            "New Warranty Request", 
                            $"A new warranty/return request has been created for Order #{order.Id}.", 
                            "Warranty",
                            referenceId: issueToCreate.Id.ToString(),
                            referenceType: "OrderIssue",
                            redirectUrl: $"/shop/warranty-requests/{issueToCreate.Id}",
                            actorId: userId);
                    }
                }

                responses.Add(MapToResponse(issueToCreate));
            }

            await _unitOfWork.CommitAsync();
            return responses;
        }

        public async Task<WarrantyIssueResponse> UpdateWarrantyIssueAsync(Guid userId, Guid issueId, UpdateWarrantyIssueDto dto, CancellationToken ct = default)
        {
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(issueId, ct);
            if (issue == null) throw new KeyNotFoundException("Warranty issue not found.");

            // Guard: Ownership
            if (issue.UserId != userId)
                throw new UnauthorizedAccessException("This warranty issue does not belong to you.");

            // Guard: Status MUST be InProgress
            if (issue.Status != OrderIssueStatus.InProgress)
                throw new InvalidOperationException("You can only edit a warranty request that is still InProgress (has not been reviewed).");

            // Update fields
            if (dto.Type.HasValue) issue.Type = dto.Type.Value;
            if (!string.IsNullOrWhiteSpace(dto.Reason)) issue.Reason = dto.Reason;
            
            if (!string.IsNullOrWhiteSpace(dto.Description))
            {
                // Preserve the [Items:...] or [OrderLevel] tag if it exists in the current description
                if (issue.Description != null && issue.Description.StartsWith("["))
                {
                    int endIdx = issue.Description.IndexOf("]");
                    if (endIdx != -1)
                    {
                        var tag = issue.Description.Substring(0, endIdx + 1);
                        issue.Description = $"{tag} {dto.Description}";
                    }
                    else
                    {
                        issue.Description = dto.Description;
                    }
                }
                else
                {
                    issue.Description = dto.Description;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(dto.EvidenceUrl)) issue.EvidenceUrl = dto.EvidenceUrl;

            issue.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.OrderIssues.Update(issue);

            await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
            {
                OrderIssueId = issue.Id,
                ActionById = userId,
                ActionByRole = RoleType.Customer,
                Action = OrderIssueAction.UserUpdate,
                Comment = "Customer updated warranty request details.",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CommitAsync();
            return MapToResponse(issue);
        }

        public async Task DeleteWarrantyIssueAsync(Guid userId, Guid issueId, CancellationToken ct = default)
        {
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(issueId, ct);
            if (issue == null) throw new KeyNotFoundException("Warranty issue not found.");

            // Guard: Ownership
            if (issue.UserId != userId)
                throw new UnauthorizedAccessException("This warranty issue does not belong to you.");

            // Guard: Status MUST be InProgress
            if (issue.Status != OrderIssueStatus.InProgress)
                throw new InvalidOperationException("You can only delete/cancel a warranty request that is still InProgress.");

            // Soft Delete
            issue.IsDeleted = true;
            issue.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.OrderIssues.Update(issue);

            await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
            {
                OrderIssueId = issue.Id,
                ActionById = userId,
                ActionByRole = RoleType.Customer,
                Action = OrderIssueAction.UserCancel,
                Comment = "Customer deleted (cancelled) the warranty request.",
                CreatedAt = DateTime.UtcNow
            });

            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // PHASE 2 — SHOP REVIEWS THE ISSUE
        // =================================================================

        public async Task ShopReviewWarrantyAsync(
            Guid shopOwnerId, ShopWarrantyResponseDto dto, CancellationToken ct = default)
        {
            // ── Load issue ──
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(dto.IssueId, ct);
            if (issue == null)
                throw new KeyNotFoundException("Warranty issue not found.");

            // ── Guard: State must be InProgress ──
            if (issue.Status != OrderIssueStatus.InProgress)
                throw new InvalidOperationException("Invalid state transition. Issue must be InProgress for shop review.");

            // ── Guard: Ownership — order belongs to this shop ──
            var order = await _unitOfWork.Orders.GetByIdAsync(issue.OrderId, ct);
            if (order == null)
                throw new KeyNotFoundException("Related order not found.");

            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopOwnerId, ct);
            if (shop == null || shop.Id != order.ShopId)
                throw new UnauthorizedAccessException("This issue does not belong to your shop.");

            if (dto.Approve)
            {
                // ── Shop Approves ──
                issue.Status = OrderIssueStatus.ShopAccepted;
                issue.ShopResponse = dto.ShopResponse;
                issue.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
                {
                    OrderIssueId = issue.Id,
                    ActionById = shopOwnerId,
                    ActionByRole = RoleType.Shop,
                    Action = OrderIssueAction.ShopApprove,
                    Comment = dto.ShopResponse ?? "Shop approved the request.",
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // ── Shop Rejects ──
                issue.Status = OrderIssueStatus.Rejected;
                issue.ShopResponse = dto.ShopResponse;
                issue.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
                {
                    OrderIssueId = issue.Id,
                    ActionById = shopOwnerId,
                    ActionByRole = RoleType.Shop,
                    Action = OrderIssueAction.ShopReject,
                    Comment = dto.ShopResponse ?? "Shop rejected the request.",
                    CreatedAt = DateTime.UtcNow
                });
            }

            _unitOfWork.OrderIssues.Update(issue);
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // PHASE 3 — ADMIN DECISION (ORDER-STATE DRIVEN)
        // =================================================================

        public async Task AdminDecisionWarrantyAsync(
            Guid adminId, AdminWarrantyDecisionDto dto, CancellationToken ct = default)
        {
            // ── Load issue ──
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(dto.IssueId, ct);
            if (issue == null)
                throw new KeyNotFoundException("Warranty issue not found.");

            // ── Guard: State must be ShopAccepted ──
            if (issue.Status != OrderIssueStatus.ShopAccepted)
                throw new InvalidOperationException("Invalid state transition. Issue must be ShopAccepted for admin decision.");

            // ── Load Order ──
            var order = await _unitOfWork.Orders.GetByIdAsync(issue.OrderId, ct);
            if (order == null)
                throw new KeyNotFoundException("Related order not found.");

            // ── CASE 3: Order already cancelled/refunded → force reject ──
            if (order.OrderStatus == OrderStatus.Cancelled || order.OrderStatus == OrderStatus.Refunded)
            {
                issue.Status = OrderIssueStatus.Rejected;
                issue.AdminNote = dto.AdminNote ?? "Order is already cancelled or refunded.";
                issue.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
                {
                    OrderIssueId = issue.Id,
                    ActionById = adminId,
                    ActionByRole = RoleType.Admin,
                    Action = OrderIssueAction.AdminDecision,
                    AdminDecision = false,
                    Comment = "Rejected: order already cancelled/refunded.",
                    CreatedAt = DateTime.UtcNow
                });

                _unitOfWork.OrderIssues.Update(issue);
                await _unitOfWork.CommitAsync();
                return;
            }

            // ── Admin Rejects ──
            if (!dto.Approve)
            {
                issue.Status = OrderIssueStatus.Rejected;
                issue.AdminNote = dto.AdminNote;
                issue.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
                {
                    OrderIssueId = issue.Id,
                    ActionById = adminId,
                    ActionByRole = RoleType.Admin,
                    Action = OrderIssueAction.AdminDecision,
                    AdminDecision = false,
                    Comment = dto.AdminNote ?? "Admin rejected the request.",
                    CreatedAt = DateTime.UtcNow
                });

                _unitOfWork.OrderIssues.Update(issue);
                await _unitOfWork.CommitAsync();
                return;
            }

            // ── Admin Approves — system determines path based on Type and Status ──
            // Yêu cầu của User: Type 1 (Return) mới cần hoàn hàng, Type 2 (Warranty) không cần hoàn hàng
            bool requiresReturn = issue.Type == OrderIssueType.ReturnRequest && 
                                 (order.OrderStatus == OrderStatus.Completed || order.OrderStatus == OrderStatus.Shipped);

            if (requiresReturn)
            {
                // ══════════════════════════════════════════════════════════
                // CASE 2: ITEM SHIPPED/DELIVERED → Return required before refund
                // ══════════════════════════════════════════════════════════
                issue.Status = OrderIssueStatus.AwaitingReturn;
                issue.AdminNote = dto.AdminNote;
                issue.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
                {
                    OrderIssueId = issue.Id,
                    ActionById = adminId,
                    ActionByRole = RoleType.Admin,
                    Action = OrderIssueAction.AdminDecision,
                    AdminDecision = true,
                    Comment = dto.AdminNote ?? "Approved: return required (item already shipped/delivered).",
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // ══════════════════════════════════════════════════════════
                // CASE 1: ITEM NOT SHIPPED OR CANCELLATION → Refund only, no return
                // ══════════════════════════════════════════════════════════
                // Calculate Pro-rated Refund
                decimal refundAmount = await CalculateRefundAmountAsync(issue, order);

                // CASE 1: No return — refund immediately to Wallet (internal only)
                await _walletService.RefundToWalletAsync(order.CustomerId, refundAmount, $"Refund for warranty/return (Order #{order.Id}, Issue #{issue.Id})");
                bool isReleased = order.PaymentStatus == PaymentStatus.Released;
                await _walletService.DeductFundsForRefundAsync(order.ShopId!.Value, order.Id, refundAmount, isReleased);

                await _notificationService.SendNotificationAsync(order.CustomerId, 
                    "Refund Successful", 
                    $"Your refund of {refundAmount:N0} for order #{order.Id} has been processed to your wallet.", 
                    "Warranty",
                    referenceId: issue.Id.ToString(),
                    referenceType: "OrderIssue",
                    redirectUrl: $"/warranty-requests/{issue.Id}",
                    actorId: adminId);

                // ── Stock Reintegration (No Return Case) ──
                await ReintegrateStockForIssueAsync(issue);

                issue.Status = OrderIssueStatus.Completed;
                issue.AdminNote = dto.AdminNote;
                issue.UpdatedAt = DateTime.UtcNow;

                // Update OrderStatus to Refunded if it's a full order refund
                if (issue.Description != null && issue.Description.StartsWith("[OrderLevel]"))
                {
                    order.OrderStatus = OrderStatus.Refunded;
                    await _unitOfWork.Orders.UpdateOrderAsync(order, ct);
                }

                await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
                {
                    OrderIssueId = issue.Id,
                    ActionById = adminId,
                    ActionByRole = RoleType.Admin,
                    Action = OrderIssueAction.AdminDecision,
                    AdminDecision = true,
                    Comment = dto.AdminNote ?? "Approved: refund only (item not shipped or cancellation) + stock reintegrated.",
                    CreatedAt = DateTime.UtcNow
                });
            }

            _unitOfWork.OrderIssues.Update(issue);
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // PHASE 4a — CUSTOMER CONFIRMS RETURN SHIPMENT
        // =================================================================

        public async Task CustomerConfirmShipmentAsync(
            Guid userId, ReturnWarrantyShipmentDto dto, CancellationToken ct = default)
        {
            // ── Load issue ──
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(dto.IssueId, ct);
            if (issue == null)
                throw new KeyNotFoundException("Warranty issue not found.");

            // ── Guard: State must be AwaitingReturn ──
            if (issue.Status != OrderIssueStatus.AwaitingReturn)
                throw new InvalidOperationException("Invalid state transition. Issue must be AwaitingReturn to confirm shipment.");

            // ── Guard: Ownership ──
            if (issue.UserId != userId)
                throw new UnauthorizedAccessException("This issue does not belong to you.");

            // ── Guard: Evidence required ──
            if (string.IsNullOrWhiteSpace(dto.EvidenceUrl))
                throw new InvalidOperationException("Shipping evidence is required.");

            // ── Transition: AwaitingReturn → Returning ──
            issue.Status = OrderIssueStatus.Returning;
            issue.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
            {
                OrderIssueId = issue.Id,
                ActionById = userId,
                ActionByRole = RoleType.Customer,
                Action = OrderIssueAction.UserShippedReturn,
                Comment = dto.Comment ?? "Customer confirmed return shipment.",
                EvidenceUrl = dto.EvidenceUrl,
                CreatedAt = DateTime.UtcNow
            });

            _unitOfWork.OrderIssues.Update(issue);
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // PHASE 4b — SHOP CONFIRMS RECEIPT (STATUS CHANGE ONLY)
        // =================================================================

        public async Task ShopConfirmReceiveAsync(
            Guid shopOwnerId, Guid issueId, CancellationToken ct = default)
        {
            // ── Load issue ──
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(issueId, ct);
            if (issue == null)
                throw new KeyNotFoundException("Warranty issue not found.");

            // ── Guard: State must be Returning ──
            if (issue.Status != OrderIssueStatus.Returning)
                throw new InvalidOperationException("Invalid state transition. Issue must be Returning to confirm receipt.");

            // ── Guard: Ownership ──
            var order = await _unitOfWork.Orders.GetByIdAsync(issue.OrderId, ct);
            if (order == null)
                throw new KeyNotFoundException("Related order not found.");

            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopOwnerId, ct);
            if (shop == null || shop.Id != order.ShopId)
                throw new UnauthorizedAccessException("This issue does not belong to your shop.");

            // ── Transition: Returning → Returned → Completed ──
            // ── Transition: Returning → Returned → Completed ──
            issue.Status = OrderIssueStatus.Completed;
            issue.UpdatedAt = DateTime.UtcNow;

            // Update OrderStatus to Refunded if it's a full order refund
            if (issue.Description != null && issue.Description.StartsWith("[OrderLevel]"))
            {
                order.OrderStatus = OrderStatus.Refunded;
                await _unitOfWork.Orders.UpdateOrderAsync(order, ct);
            }

            // ── Stock Reintegration (Returned Case) ──
            await ReintegrateStockForIssueAsync(issue);

            // Calculate Pro-rated Refund
            decimal refundAmount = await CalculateRefundAmountAsync(issue, order);

            // ── Internal Refund to Wallet ──
            await _walletService.RefundToWalletAsync(issue.UserId, refundAmount, $"Refund for warranty/return (Order #{order.Id}, Issue #{issue.Id})");
            bool isReleasedNow = order.PaymentStatus == PaymentStatus.Released;
            await _walletService.DeductFundsForRefundAsync(order.ShopId!.Value, order.Id, refundAmount, isReleasedNow);

            // ── Notification ──
            await _notificationService.SendNotificationAsync(issue.UserId, 
                "Refund Successful", 
                $"Your refund of {refundAmount:N0} for order #{order.Id} has been processed to your wallet after successful return and stock reintegration.", 
                "Warranty",
                referenceId: issue.Id.ToString(),
                referenceType: "OrderIssue",
                redirectUrl: $"/warranty-requests/{issue.Id}",
                actorId: shopOwnerId);

            await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
            {
                OrderIssueId = issue.Id,
                ActionById = shopOwnerId,
                ActionByRole = RoleType.Shop,
                Action = OrderIssueAction.ShopReceivedReturn,
                Comment = "Shop confirmed receipt of returned product. Stock reintegrated. Issue completed.",
                CreatedAt = DateTime.UtcNow
            });

            _unitOfWork.OrderIssues.Update(issue);
            await _unitOfWork.CommitAsync();
        }

        // =================================================================
        // BACKGROUND JOB — AUTO CANCEL EXPIRED ISSUES
        // =================================================================

        public async Task AutoCancelExpiredIssuesAsync(CancellationToken ct = default)
        {
            // Issues in AwaitingReturn for more than X days (based on UpdatedAt)
            int timeoutDays = _configuration.GetValue<int>("WarrantySettings:ReturnShippingTimeoutDays", 3);
            var threshold = DateTime.UtcNow.AddDays(-timeoutDays);
            var expiredIssues = await _unitOfWork.OrderIssues.GetExpiredIssuesAsync(threshold);

            foreach (var issue in expiredIssues)
            {
                issue.Status = OrderIssueStatus.AutoCancelled;
                issue.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
                {
                    OrderIssueId = issue.Id,
                    ActionById = Guid.Empty, // System
                    ActionByRole = RoleType.Admin,
                    Action = OrderIssueAction.SystemCancel,
                    Comment = $"Auto-cancelled: customer did not ship return within {timeoutDays} days.",
                    CreatedAt = DateTime.UtcNow
                });

                _unitOfWork.OrderIssues.Update(issue);
            }

            if (expiredIssues.Any())
            {
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task<FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>> GetAllWarrantyIssuesAsync(OrderIssueStatus? status, int currentPage, int pageSize, CancellationToken ct = default)
        {
            var (items, totalCount) = await _unitOfWork.OrderIssues.GetAllPagedAsync(status, currentPage, pageSize, ct);
            var mappedItems = items.Select(MapToResponse);

            return new FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>(
                mappedItems, totalCount, currentPage, pageSize);
        }

        public async Task<FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>> GetMyWarrantyIssuesAsync(Guid userId, OrderIssueStatus? status, int currentPage, int pageSize, CancellationToken ct = default)
        {
            var (items, totalCount) = await _unitOfWork.OrderIssues.GetByUserIdPagedAsync(userId, status, currentPage, pageSize, ct);
            var mappedItems = items.Select(MapToResponse);

            return new FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>(
                mappedItems, totalCount, currentPage, pageSize);
        }

        public async Task<FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>> GetShopWarrantyIssuesAsync(Guid shopOwnerId, OrderIssueStatus? status, int currentPage, int pageSize, CancellationToken ct = default)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopOwnerId, ct);
            if (shop == null)
                throw new UnauthorizedAccessException("This account is not a Shop Owner.");

            var (items, totalCount) = await _unitOfWork.OrderIssues.GetByShopIdPagedAsync(shop.Id, status, currentPage, pageSize, ct);
            var mappedItems = items.Select(MapToResponse);

            return new FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>(
                mappedItems, totalCount, currentPage, pageSize);
        }

        public async Task WithdrawWarrantyAsync(Guid userId, Guid issueId, CancellationToken ct = default)
        {
            // ── Load issue ──
            var issue = await _unitOfWork.OrderIssues.GetByIdAsync(issueId, ct);
            if (issue == null)
                throw new KeyNotFoundException("Warranty issue not found.");

            // ── Guard: Ownership ──
            if (issue.UserId != userId)
                throw new UnauthorizedAccessException("This issue does not belong to you.");

            // ── Guard: Withdrawal limit ──
            int maxWithdraw = _configuration.GetValue<int>("WarrantySettings:MaxWithdrawCount", 1);
            
            // Check how many withdrawn requests this user has for this SPECIFIC order
            var orderIssues = await _unitOfWork.OrderIssues.GetByOrderIdAsync(issue.OrderId);
            int withdrawnCount = 0;
            foreach (var oi in orderIssues)
            {
                var logs = await _unitOfWork.OrderIssueLogs.GetByOrderIssueIdAsync(oi.Id);
                if (logs.Any(l => l.ActionByRole == RoleType.Customer && l.Action == OrderIssueAction.UserCancel))
                {
                    withdrawnCount++;
                }
            }

            if (withdrawnCount >= maxWithdraw)
            {
                throw new InvalidOperationException($"You have exceeded the withdrawal limit of {maxWithdraw} time(s) for this order.");
            }

            // ── Guard: Current Status must allow withdrawal ──
            // Can withdraw if not already finished (Completed/Rejected/AutoCancelled)
            if (issue.Status == OrderIssueStatus.Completed || 
                issue.Status == OrderIssueStatus.Rejected || 
                issue.Status == OrderIssueStatus.AutoCancelled)
            {
                throw new InvalidOperationException($"Cannot withdraw issue in its current status: {issue.Status}");
            }

            // ── Transition: Current → Rejected ──
            issue.Status = OrderIssueStatus.Rejected;
            issue.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.OrderIssueLogs.AddAsync(new OrderIssueLog
            {
                OrderIssueId = issue.Id,
                ActionById = userId,
                ActionByRole = RoleType.Customer,
                Action = OrderIssueAction.UserCancel,
                Comment = "Customer withdrawn warranty request.",
                CreatedAt = DateTime.UtcNow
            });

            _unitOfWork.OrderIssues.Update(issue);
            await _unitOfWork.CommitAsync();
        }

        public async Task<IEnumerable<OrderIssueLogResponse>> GetWarrantyIssueHistoryAsync(Guid issueId, CancellationToken ct = default)
        {
            var logs = await _unitOfWork.OrderIssueLogs.GetByOrderIssueIdAsync(issueId);
            return logs.Select(l => new OrderIssueLogResponse
            {
                Id = l.Id,
                OrderIssueId = l.OrderIssueId,
                ActorId = l.ActionById,
                ActorRole = l.ActionByRole,
                ActorRoleName = l.ActionByRole.ToString(),
                ActionType = l.Action,
                ActionName = l.Action.ToString(),
                Comment = l.Comment ?? string.Empty,
                CreatedAt = l.CreatedAt.ConvertToLocalTime()
            }).OrderByDescending(l => l.CreatedAt);
        }

        // =================================================================
        // PRIVATE HELPERS
        // =================================================================

        private async Task<decimal> CalculateRefundAmountAsync(OrderIssue issue, Order order)
        {
            if (string.IsNullOrEmpty(issue.Description)) return 0;

            if (issue.Description.StartsWith("[OrderLevel]"))
            {
                return order.TotalAmount;
            }

            if (issue.Description.StartsWith("[Items:"))
            {
                int endIdx = issue.Description.IndexOf("]");
                if (endIdx > 7)
                {
                    try
                    {
                        var idsStr = issue.Description.Substring(7, endIdx - 7);
                        var itemIds = idsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(id => Guid.Parse(id.Trim()))
                                           .ToList();

                        // Ensure items are loaded
                        if (order.OrderItems == null || !order.OrderItems.Any())
                        {
                            var fullOrder = await _unitOfWork.Orders.GetByIdAsync(order.Id);
                            if (fullOrder != null) order = fullOrder;
                        }

                        if (order.OrderItems == null) return 0;

                        return order.OrderItems
                            .Where(oi => itemIds.Contains(oi.Id))
                            .Sum(oi => oi.TotalPrice);
                    }
                    catch { return 0; }
                }
            }

            return 0;
        }

        // =================================================================
        // PRIVATE HELPERS — STOCK REINTEGRATION
        // =================================================================

        private async Task ReintegrateStockForIssueAsync(OrderIssue issue)
        {
            if (string.IsNullOrEmpty(issue.Description)) return;

            var order = await _unitOfWork.Orders.GetByIdAsync(issue.OrderId);
            if (order == null || order.OrderItems == null) return;

            if (issue.Description.StartsWith("[OrderLevel]"))
            {
                foreach (var item in order.OrderItems)
                {
                    await ReintegrateSingleItemStockAsync(item);
                }
            }
            else if (issue.Description.StartsWith("[Items:"))
            {
                int endIdx = issue.Description.IndexOf("]");
                if (endIdx > 7)
                {
                    var idsStr = issue.Description.Substring(7, endIdx - 7);
                    var itemIds = idsStr.Split(',').Select(id => Guid.Parse(id.Trim())).ToList();

                    foreach (var itemId in itemIds)
                    {
                        var item = order.OrderItems.FirstOrDefault(oi => oi.Id == itemId);
                        if (item != null)
                        {
                            await ReintegrateSingleItemStockAsync(item);
                        }
                    }
                }
            }
        }

        private async Task ReintegrateSingleItemStockAsync(OrderItem item)
        {
            // 1. Basic Product Stock
            var product = await _unitOfWork.Models.GetByIdAsync(item.ProductId);
            if (product != null)
            {
                product.StockQuantity += item.Quantity;
                await _unitOfWork.Models.UpdateAsync(product);
            }

            // 2. Custom Components Stock
            // Note: OrderItemComponents should be included in order.OrderItems if repository supports it.
            // If not, we might need to load them explicitly.
            if (item.IsCustom && item.OrderItemComponents != null && item.OrderItemComponents.Any())
            {
                foreach (var comp in item.OrderItemComponents)
                {
                    var part = await _unitOfWork.Models.GetByIdAsync(comp.PartId);
                    if (part != null)
                    {
                        part.StockQuantity += (comp.Quantity * item.Quantity);
                        await _unitOfWork.Models.UpdateAsync(part);
                    }
                }
            }
        }

        private static WarrantyIssueResponse MapToResponse(OrderIssue issue)
        {
            decimal refund = 0;
            List<Guid>? itemIdsInResponse = null;

            if (!string.IsNullOrEmpty(issue.Description))
            {
                if (issue.Description.StartsWith("[OrderLevel]"))
                {
                    if (issue.Order != null) refund = issue.Order.TotalAmount;
                }
                else if (issue.Description.StartsWith("[Items:"))
                {
                    int endIdx = issue.Description.IndexOf("]");
                    if (endIdx > 7)
                    {
                        try
                        {
                            var idsStr = issue.Description.Substring(7, endIdx - 7);
                            var itemIds = idsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                               .Select(id => Guid.Parse(id.Trim()))
                                               .ToList();

                            itemIdsInResponse = itemIds;

                            if (issue.Order?.OrderItems != null)
                            {
                                refund = issue.Order.OrderItems
                                    .Where(oi => itemIds.Contains(oi.Id))
                                    .Sum(oi => oi.TotalPrice);
                            }
                        }
                        catch { /* Fallback to 0 if tag is corrupted */ }
                    }
                }
            }

            return new WarrantyIssueResponse
            {
                Id = issue.Id,
                OrderId = issue.OrderId,
                UserId = issue.UserId,
                Type = issue.Type,
                TypeName = issue.Type.ToString(),
                Status = issue.Status,
                StatusName = issue.Status.ToString(),
                RefundAmount = refund,
                Reason = issue.Reason,
                Description = issue.Description,
                EvidenceUrl = issue.EvidenceUrl,
                CustomerName = issue.User != null ? $"{issue.User.FirstName} {issue.User.LastName}" : null,
                CustomerAvatar = issue.User?.Image,
                RequiresReturn = issue.Type == OrderIssueType.ReturnRequest && 
                                 (issue.Order != null && (issue.Order.OrderStatus == OrderStatus.Completed || issue.Order.OrderStatus == OrderStatus.Shipped)),
                ExpectedAction = (issue.Type == OrderIssueType.ReturnRequest && 
                                 (issue.Order != null && (issue.Order.OrderStatus == OrderStatus.Completed || issue.Order.OrderStatus == OrderStatus.Shipped)))
                                 ? "Return Request: Approval will proceed with the Return then Refund flow" 
                                 : (issue.Type == OrderIssueType.WarrantyClaim ? "Warranty Claim: Approval will process an Immediate Refund" : "Cancellation Request: Approval will process an Immediate Refund"),
                IsSystemValid = issue.IsSystemValid,
                ShopResponse = issue.ShopResponse,
                AdminNote = issue.AdminNote,
                CreatedAt = issue.CreatedAt.ConvertToLocalTime(),
                UpdatedAt = issue.UpdatedAt?.ConvertToLocalTime(),
                AIAnalysisResult = issue.AIAnalysisResult,
                OrderItemIds = itemIdsInResponse
            };
        }
    }
}
