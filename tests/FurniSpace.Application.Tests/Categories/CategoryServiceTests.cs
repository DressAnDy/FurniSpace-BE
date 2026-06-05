#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Categories;
using FurniSpace.Application.Services.Categories;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Categories;

public sealed class CategoryServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesActiveCategory()
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);
        var request = new CreateCategoryRequestDto
        {
            CategoryName = " Lighting ",
            Description = " Lighting and decorative lighting items "
        };

        var result = await service.CreateAsync(request);

        Assert.Equal(201, result.Status);
        Assert.Equal("Category created successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.NotEqual(Guid.Empty, result.Data.CategoryId);
        Assert.Equal("Lighting", result.Data.CategoryName);
        Assert.Equal("Lighting and decorative lighting items", result.Data.Description);
        Assert.Equal("ACTIVE", result.Data.Status);
        Assert.Equal(1, repository.NameExistsCallCount);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Single(repository.Categories);
    }

    [Fact]
    public async Task CreateAsync_WithBlankDescription_CreatesCategoryWithNullDescription()
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.CreateAsync(new CreateCategoryRequestDto
        {
            CategoryName = "Lighting",
            Description = " "
        });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateAsync_WithMissingCategoryName_ReturnsBadRequest(string categoryName)
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.CreateAsync(new CreateCategoryRequestDto
        {
            CategoryName = categoryName,
            Description = "Lighting and decorative lighting items"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Category name is required.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.NameExistsCallCount);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithTooLongCategoryName_ReturnsBadRequest()
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.CreateAsync(new CreateCategoryRequestDto
        {
            CategoryName = new string('A', 101)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Category name must not exceed 100 characters.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.NameExistsCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCategoryName_ReturnsConflict()
    {
        var repository = new FakeCategoryRepository(
        [
            new Category
            {
                CategoryId = Guid.NewGuid(),
                CategoryName = "Lighting",
                Status = "ACTIVE"
            }
        ]);
        var service = new CategoryService(repository);

        var result = await service.CreateAsync(new CreateCategoryRequestDto
        {
            CategoryName = " lighting "
        });

        Assert.Equal(409, result.Status);
        Assert.Equal("Category name already exists.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.NameExistsCallCount);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesCategory()
    {
        var categoryId = Guid.NewGuid();
        var repository = new FakeCategoryRepository(
        [
            new Category
            {
                CategoryId = categoryId,
                CategoryName = "Old Lighting",
                Description = "Old description",
                Status = "ACTIVE"
            }
        ]);
        var service = new CategoryService(repository);

        var result = await service.UpdateAsync(categoryId, new UpdateCategoryRequestDto
        {
            CategoryName = " Lighting ",
            Description = " Lighting and decorative lighting items "
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Category updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(categoryId, result.Data.CategoryId);
        Assert.Equal("Lighting", result.Data.CategoryName);
        Assert.Equal("Lighting and decorative lighting items", result.Data.Description);
        Assert.Equal("ACTIVE", result.Data.Status);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.NameExistsExcludingCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithNullStatus_KeepsResponseActive()
    {
        var categoryId = Guid.NewGuid();
        var repository = new FakeCategoryRepository(
        [
            new Category
            {
                CategoryId = categoryId,
                CategoryName = "Lighting",
                Status = null
            }
        ]);
        var service = new CategoryService(repository);

        var result = await service.UpdateAsync(categoryId, new UpdateCategoryRequestDto
        {
            CategoryName = "Lighting",
            Description = " "
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("ACTIVE", result.Data.Status);
        Assert.Null(result.Data.Description);
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyCategoryId_ReturnsBadRequest()
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.UpdateAsync(Guid.Empty, new UpdateCategoryRequestDto
        {
            CategoryName = "Lighting"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Category id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task UpdateAsync_WithMissingCategoryName_ReturnsBadRequest(string categoryName)
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateCategoryRequestDto
        {
            CategoryName = categoryName
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Category name is required.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithTooLongCategoryName_ReturnsBadRequest()
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateCategoryRequestDto
        {
            CategoryName = new string('A', 101)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Category name must not exceed 100 characters.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithMissingCategory_ReturnsNotFound()
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateCategoryRequestDto
        {
            CategoryName = "Lighting"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("Category not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.NameExistsExcludingCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateCategoryName_ReturnsConflict()
    {
        var categoryId = Guid.NewGuid();
        var repository = new FakeCategoryRepository(
        [
            new Category { CategoryId = categoryId, CategoryName = "Lighting", Status = "ACTIVE" },
            new Category { CategoryId = Guid.NewGuid(), CategoryName = "Decor", Status = "ACTIVE" }
        ]);
        var service = new CategoryService(repository);

        var result = await service.UpdateAsync(categoryId, new UpdateCategoryRequestDto
        {
            CategoryName = " decor "
        });

        Assert.Equal(409, result.Status);
        Assert.Equal("Category name already exists.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.NameExistsExcludingCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetAllAsync_WithValidPagination_ReturnsPagedCategories()
    {
        var repository = new FakeCategoryRepository(
        [
            new Category
            {
                CategoryId = Guid.NewGuid(),
                CategoryName = "Counter",
                Description = "Counter and cashier furniture",
                Status = "ACTIVE"
            },
            new Category
            {
                CategoryId = Guid.NewGuid(),
                CategoryName = "Display",
                Description = "Display furniture",
                Status = "INACTIVE"
            }
        ]);
        var service = new CategoryService(repository);

        var result = await service.GetAllAsync(page: 1, limit: 20);

        Assert.Equal(200, result.Status);
        Assert.Equal(string.Empty, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.Limit);
        Assert.Equal(2, result.Data.Total);
        Assert.Collection(
            result.Data.Items,
            item =>
            {
                Assert.Equal("Counter", item.CategoryName);
                Assert.Equal("Counter and cashier furniture", item.Description);
                Assert.Equal("ACTIVE", item.Status);
            },
            item => Assert.Equal("Display", item.CategoryName));
        Assert.Equal(1, repository.GetPagedCallCount);
        Assert.Equal(1, repository.CountCallCount);
    }

    [Fact]
    public async Task GetAllAsync_WithSecondPage_ReturnsOnlyRequestedPage()
    {
        var repository = new FakeCategoryRepository(
        [
            new Category { CategoryId = Guid.NewGuid(), CategoryName = "A", Status = "ACTIVE" },
            new Category { CategoryId = Guid.NewGuid(), CategoryName = "B", Status = "ACTIVE" },
            new Category { CategoryId = Guid.NewGuid(), CategoryName = "C", Status = "ACTIVE" }
        ]);
        var service = new CategoryService(repository);

        var result = await service.GetAllAsync(page: 2, limit: 2);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Total);
        Assert.Single(result.Data.Items);
        Assert.Equal("C", result.Data.Items[0].CategoryName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAllAsync_WithInvalidPage_ReturnsBadRequest(int page)
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.GetAllAsync(page, limit: 20);

        Assert.Equal(400, result.Status);
        Assert.Equal("Page must be greater than zero.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetPagedCallCount);
        Assert.Equal(0, repository.CountCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GetAllAsync_WithInvalidLimit_ReturnsBadRequest(int limit)
    {
        var repository = new FakeCategoryRepository([]);
        var service = new CategoryService(repository);

        var result = await service.GetAllAsync(page: 1, limit: limit);

        Assert.Equal(400, result.Status);
        Assert.Equal("Limit must be between 1 and 100.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetPagedCallCount);
        Assert.Equal(0, repository.CountCallCount);
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        private readonly List<Category> _categories;

        public FakeCategoryRepository(IReadOnlyList<Category> categories)
        {
            _categories = categories.ToList();
        }

        public IReadOnlyList<Category> Categories => _categories;
        public int NameExistsCallCount { get; private set; }
        public int NameExistsExcludingCallCount { get; private set; }
        public int GetByIdCallCount { get; private set; }
        public int AddCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public int GetPagedCallCount { get; private set; }
        public int CountCallCount { get; private set; }

        public Task<bool> NameExistsAsync(string categoryName, CancellationToken cancellationToken = default)
        {
            NameExistsCallCount++;
            return Task.FromResult(_categories.Any(category =>
                string.Equals(
                    category.CategoryName.Trim(),
                    categoryName.Trim(),
                    StringComparison.OrdinalIgnoreCase)));
        }

        public Task<bool> NameExistsAsync(
            string categoryName,
            Guid excludedCategoryId,
            CancellationToken cancellationToken = default)
        {
            NameExistsExcludingCallCount++;
            return Task.FromResult(_categories.Any(category =>
                category.CategoryId != excludedCategoryId &&
                string.Equals(
                    category.CategoryName.Trim(),
                    categoryName.Trim(),
                    StringComparison.OrdinalIgnoreCase)));
        }

        public Task<IReadOnlyList<Category>> GetPagedAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
        {
            GetPagedCallCount++;
            return Task.FromResult<IReadOnlyList<Category>>(
                _categories
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .ToList());
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            CountCallCount++;
            return Task.FromResult(_categories.Count);
        }

        public IQueryable<Category> Query() => _categories.AsQueryable();
        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(_categories.FirstOrDefault(category => category.CategoryId == id));
        }

        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Category>>(_categories);
        public Task AddAsync(Category entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _categories.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Category> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Category entity) { }
        public void Remove(Category entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
