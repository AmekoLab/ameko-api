using System.ComponentModel.DataAnnotations;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Chat
{
    /// <summary>
    /// Send message request for normal message or reply.
    /// </summary>
    public class SendMessageRequest
    {
        /// <summary>
        /// Existing conversation id. If omitted, targetUserId must be provided.
        /// </summary>
        public int? ConversationId { get; set; }

        /// <summary>
        /// Target user id for direct-conversation auto create/get when conversationId is not provided.
        /// </summary>
        public Guid? TargetUserId { get; set; }

        [Required(ErrorMessage = "Message content is required.")]
        [MaxLength(4000, ErrorMessage = "Message is too long.")]

        /// <summary>
        /// Message text content.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Message media type enum. 0 is text.
        /// </summary>
        public MediaType MessageType { get; set; } = MediaType.Text;

        /// <summary>
        /// Parent message id for reply. Null means normal message.
        /// </summary>
        public int? ParentMessageId { get; set; }
    }
}
