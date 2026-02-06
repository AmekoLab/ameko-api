using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty
{
    public class StorageService : IStorageService
    {
        private readonly Cloudinary _cloudinary;

        public StorageService(IConfiguration configuration)
        {
            var cloudName = configuration["CloudinarySettings:CloudName"];
            var apiKey = configuration["CloudinarySettings:ApiKey"];
            var apiSecret = configuration["CloudinarySettings:ApiSecret"];

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string folderName = "products")
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(fileName, fileStream),
                Folder = $"amk-collective/{folderName}",
                PublicId = Path.GetFileNameWithoutExtension(fileName) + "_" + Guid.NewGuid()
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult.SecureUrl.ToString();
        }

        public async Task DeleteAsync(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return;

            var publicId = GetPublicIdFromUrl(fileUrl);

            if (string.IsNullOrEmpty(publicId)) return;

            var deletionParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deletionParams);
        }
        private string GetPublicIdFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath; 
                var uploadIndex = path.IndexOf("upload");
                if (uploadIndex == -1) return null;

                var pathAfterUpload = path.Substring(uploadIndex + 7); 

                if (pathAfterUpload.StartsWith("v") && pathAfterUpload.IndexOf('/') > 0)
                {
                    var slashIndex = pathAfterUpload.IndexOf('/');
                    pathAfterUpload = pathAfterUpload.Substring(slashIndex + 1);
                }

                var lastDotIndex = pathAfterUpload.LastIndexOf('.');
                if (lastDotIndex > 0)
                {
                    return pathAfterUpload.Substring(0, lastDotIndex);
                }

                return pathAfterUpload;
            }
            catch
            {

                return null;
            }
        }
    }
}