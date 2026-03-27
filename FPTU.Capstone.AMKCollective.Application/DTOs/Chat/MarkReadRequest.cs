using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Chat
{
    /// <summary>
    /// Mark-read request payload.
    /// </summary>
    public class MarkReadRequest
    {
        /// <summary>
        /// Mark as read all messages with id less than or equal to this value.
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Invalid message id.")]
        public int UpToMessageId { get; set; }
    }
}
