using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.API.Contracts
{
    //form data
    public class CreateUpdatePartApiRequest
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PartType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Description { get; set; }
        public string? Specifications { get; set; } // JSON String

        public IFormFile? ThumbnailImage { get; set; }
        public IFormFile? LayerImage { get; set; }
    }

    //Validate Builder (JSON)
    public class ValidateBuilderRequest
    {
        public Guid BaseKitId { get; set; }
        public List<Guid> ComponentIds { get; set; } = new List<Guid>();
    }

    //Check Stock (JSON)
    public class CheckStockRequest
    {
        public List<Guid> ProductIds { get; set; } = new List<Guid>();
    }
    public class CreateKitOptionRequest
    {
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }
        public string StepName { get; set; }
        public int StepOrder { get; set; }
        public bool IsDefault { get; set; }
        public IFormFile? LayerImageFile { get; set; } 
    }

    public class CreateCategoryApiRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public Guid? ParentId { get; set; }
        public bool IsActive { get; set; } = true;

        public IFormFile? ThumbnailImage { get; set; }
    }

    public class UpdateCategoryApiRequest
    {
        public string? Name { get; set; }
        public Guid? ParentId { get; set; }
        public bool? IsActive { get; set; }

        public IFormFile? ThumbnailImage { get; set; }
    }

    public class CreateShopApiRequest
    {
        [Required]
        public string ShopName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }

        [Required]
        public string CitizenId { get; set; } = string.Empty ;
        public string? TaxCode { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber {  get; set; }
        public string? BankAccountName {  get; set; }

        public IFormFile? LogoImage { get; set; }
        public IFormFile? BannerImage { get; set; }
    }

        public class UpdateShopApiRequest
    {
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set;}
        public bool? IsActive { get; set; }
       
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
        public IFormFile? LogoImage { get; set; }
        public IFormFile? BannerImage { get; set; }
    }
    
}
