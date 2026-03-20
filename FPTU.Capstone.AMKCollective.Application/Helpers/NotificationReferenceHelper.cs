using System;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Helpers;

public static class NotificationReferenceHelper
{
    public const string TypePost = "Post";
    public const string TypeOrder = "Order";
    public const string TypeChat = "Chat";

    public static int? GetPostId(Notification notification)
    {
        if (notification.ReferenceType == TypePost && int.TryParse(notification.ReferenceId, out var id))
        {
            return id;
        }
        return null;
    }

    public static Guid? GetOrderId(Notification notification)
    {
        if (notification.ReferenceType == TypeOrder && Guid.TryParse(notification.ReferenceId, out var id))
        {
            return id;
        }
        return null;
    }
}
