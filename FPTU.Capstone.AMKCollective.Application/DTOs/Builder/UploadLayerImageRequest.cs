using Microsoft.AspNetCore.Http;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class UploadLayerImageRequest
    {
        public IFormFile File { get; set; } = null!;
    }
}
