using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string folderName = "products");
        Task DeleteAsync(string fileUrl);
    }
}
