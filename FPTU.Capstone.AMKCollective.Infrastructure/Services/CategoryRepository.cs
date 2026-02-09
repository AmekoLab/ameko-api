using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationDbContext _context;
    
    public CategoryRepository(ApplicationDbContext context) {
            _context = context;
        }
        public async Task<Category?> GetByIdAsync(Guid id, bool includeSubCategories = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories
                .Where(c => c.Id == id && !c.IsDeleted);

            if (includeSubCategories)
            {
                query = query.Include(c => c.SubCategories.Where(sc => !sc.IsDeleted));
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<Category>> GetAllAsync(bool? isActive = null, Guid? parentId = null, bool includeSubCategories = false, Guid? shopId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories.Where(c => !c.IsDeleted);

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            // Filter by shop (null = global admin categories only, specific Guid = shop categories)
            if (shopId.HasValue)
            {
                query = query.Where(c => c.ShopId == shopId.Value);
            }
            else
            {
                query = query.Where(c => c.ShopId == null); // Get only global categories
            }

            if (parentId.HasValue)
            {
                query = query.Where(c => c.ParentId == parentId.Value);
            }
            else 
            {
                query = query.Where(c => c.ParentId == null);
            }

            if (includeSubCategories)
            {
                query = query.Include(c => c.SubCategories.Where(sc => !sc.IsDeleted));
            }

            return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        }

        public async Task<(IEnumerable<Category> Items, int TotalCount)> GetPagedAsync(
                int pageNumber, int pageSize, bool? isActive, Guid? parentId, bool  includeSubCategories,
                Guid? shopId, // Tham số này là "Context Shop"
                CancellationToken cancellationToken = default)
        {
            var query = _context.Categories.AsQueryable();
            if (isActive.HasValue)
                query = query.Where(c => c.IsActive == isActive.Value);

            // Filter by shop (null = global admin categories only, specific Guid = shop categories)
            if (shopId.HasValue)
            {
                // Nếu có ShopId (User đang xem Shop A, hoặc Shop A đang quản lý):
                // Lấy danh mục Global (ShopId == null) HOẶC Danh mục của Shop đó
                query = query.Where(c => c.ShopId == null || c.ShopId == shopId);
            }
            else
            {
                // Nếu không truyền ShopId (Khách xem trang chủ):
                // CHỈ lấy danh mục Global
                query = query.Where(c => c.ShopId == null);
            }

            if (parentId.HasValue)
            {
                query = query.Where(c => c.ParentId == parentId.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            if (includeSubCategories)
            {
                query = query.Include(c => c.SubCategories.Where(sc => !sc.IsDeleted));
            }

            var items = await query
                .OrderBy(c => c.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<Category> CreateAsync(Category category, CancellationToken cancellationToken = default)
        {
            category.CreatedAt = DateTime.UtcNow;
            category.UpdatedAt = DateTime.UtcNow;

            await _context.Categories.AddAsync(category, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return category;
        }

        public async Task<Category> UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            category.UpdatedAt = DateTime.UtcNow;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync(cancellationToken);

            return category;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories.FindAsync(new object[] { id }, cancellationToken);

            if (category == null)
                return false;

            category.IsDeleted = true;
            category.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AnyAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        }

        public async Task<bool> HasSubCategoriesAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AnyAsync(c => c.ParentId == categoryId && !c.IsDeleted, cancellationToken);
        }

        public async Task<bool> HasPartsAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Models
                .AnyAsync(m => m.CategoryId == categoryId, cancellationToken);
        }

        public async Task<IEnumerable<Category>> GetSubCategoriesAsync(Guid parentId, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories
                .Where(c => c.ParentId == parentId && !c.IsDeleted);

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Category>> GetRootCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories
                .Where(c => c.ParentId == null && !c.IsDeleted);

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            return await query
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted && (includeInactive || sc.IsActive)))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<(IEnumerable<Model> Items, int TotalCount)> GetPartsInCategoryAsync(Guid categoryId, int pageNumber, int pageSize, bool? isActive = null, string? partType = null, Guid? shopId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Models
                .Include(m => m.Shop)
                .Where(m => m.CategoryId == categoryId);

            if (isActive.HasValue)
            {
                query = query.Where(m => m.Shop.IsActive == isActive.Value);
            }

            if (!string.IsNullOrEmpty(partType))
            {
                query = query.Where(m => m.PartType == partType);
            }

            if (shopId.HasValue)
            {
                query = query.Where(m => m.ShopId == shopId.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderBy(m => m.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<Category?> GetBySlugAsync(string slug, Guid? shopId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories
                .Where(c => c.Slug == slug && !c.IsDeleted && c.IsActive);

            // Filter by shop
            if (shopId.HasValue)
            {
                query = query.Where(c => c.ShopId == shopId.Value);
            }
            else
            {
                query = query.Where(c => c.ShopId == null); // Only global categories
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        // Get all GLOBAL categories (created by admin, ShopId = null)
        public async Task<IEnumerable<Category>> GetGlobalCategoriesAsync(bool? isActive = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories
                .Where(c => !c.IsDeleted && c.ShopId == null);

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            return await query
                .Where(c => c.ParentId == null) // Root categories only
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        // Get all categories for a specific SHOP (ShopId != null)
        public async Task<IEnumerable<Category>> GetShopCategoriesAsync(Guid shopId, bool? isActive = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories
                .Where(c => !c.IsDeleted && c.ShopId == shopId);

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            return await query
                .Where(c => c.ParentId == null) // Root categories only
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        // Get category by ID with ownership verification
        public async Task<Category?> GetByIdForShopAsync(Guid id, Guid shopId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .Where(c => c.Id == id && c.ShopId == shopId && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Check if category belongs to a shop
        public async Task<bool> IsShopCategoryAsync(Guid categoryId, Guid shopId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AnyAsync(c => c.Id == categoryId && c.ShopId == shopId && !c.IsDeleted, cancellationToken);
        }

        // Check if category is global (admin categories)
        public async Task<bool> IsGlobalCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AnyAsync(c => c.Id == categoryId && c.ShopId == null && !c.IsDeleted, cancellationToken);
        }
        public async Task<bool> IsSlugDuplicateAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Categories.AsQueryable();

            if (excludeId.HasValue)
            {
                return await query.AnyAsync(c => c.Slug == slug && c.Id != excludeId.Value, cancellationToken);
            }
            return await query.AnyAsync(c => c.Slug == slug, cancellationToken);
        }
    }


}

