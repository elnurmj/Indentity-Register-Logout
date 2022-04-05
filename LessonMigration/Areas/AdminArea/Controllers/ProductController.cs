using LessonMigration.Data;
using LessonMigration.Models;
using LessonMigration.Utilities.File;
using LessonMigration.Utilities.Helpers;
using LessonMigration.Utilities.Pagination;
using LessonMigration.ViewModels.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LessonMigration.Areas.AdminArea.Controllers
{
    [Area("AdminArea")]
    public class ProductController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private async Task<int> GetPageCount(int take)
        {
            var count = await _context.Products.CountAsync();
            return (int)Math.Ceiling((decimal)count / take);
        }
        private List<ProductListVM> GetMapDatas(List<Product> products)
        {
            List<ProductListVM> productList = new List<ProductListVM>();
            foreach (var product in products)
            {
                ProductListVM newProduct = new ProductListVM
                {
                    Id = product.Id,
                    Name = product.Name,
                    Image = product.Images.Where(m => m.IsMain).FirstOrDefault()?.Image,
                    CategoryName = product.Category.Name,
                    Count = product.Count,
                    Price = product.Price
                };
                productList.Add(newProduct);
            }
            return productList;
        }
        private async Task<SelectList> GetCategoriesByProduct()
        {
            var categories = await _context.Categories.Where(m => !m.IsDeleted).ToListAsync();
            return new SelectList(categories, "Id", "Name");
        }
        public ProductController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        public async Task<IActionResult> Index(int page = 1, int take = 10)
        {
            var products = await _context.Products
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.Id)
                .Include(m => m.Category)
                .Include(m => m.Images)
                .Skip((page - 1) * take)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
            var productsVM = GetMapDatas(products);
            int count = await GetPageCount(take);
            Paginate<ProductListVM> result = new Paginate<ProductListVM>(productsVM, page, count);
            return View(result);
        }
        public async Task<IActionResult> Create()
        {
            ViewBag.categories = await GetCategoriesByProduct();
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductCreateVM productVM)
        {
            ViewBag.categories = await GetCategoriesByProduct();
            if (!ModelState.IsValid) return View();
            List<ProductImage> imageList = new List<ProductImage>();
            if (productVM.Photos != null)
            {
                foreach (var photo in productVM.Photos)
                {
                    string fileName = Guid.NewGuid().ToString() + "_" + photo.FileName;
                    string path = Helper.GetFilePath(_env.WebRootPath, "img", fileName);
                    await photo.SaveFile(path);
                    ProductImage productImage = new ProductImage
                    {
                        Image = fileName
                    };
                    imageList.Add(productImage);
                }
                imageList.FirstOrDefault().IsMain = true;
            }
            Product product = new Product()
            {
                Name = productVM.Name,
                Count = productVM.Count,
                Price = productVM.Price,
                CategoryId = productVM.CategoryId,
                Images = imageList
            };
            await _context.ProductImages.AddRangeAsync(imageList);
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            Product product = await _context.Products.Include(m => m.Images).Where(m => !m.IsDeleted && m.Id == id).FirstOrDefaultAsync();
            if (product is null) return NotFound();
            foreach (var item in product.Images)
            {
                string path = Helper.GetFilePath(_env.WebRootPath, "img", item.Image);
                Helper.DeleteFile(path);
                item.IsDeleted = true;
            }
            product.IsDeleted = true;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.categories = await GetCategoriesByProduct();
            Product product = await _context.Products.Include(m => m.Images).Include(m => m.Category).Where(m => !m.IsDeleted && m.Id == id).FirstOrDefaultAsync();
            if (product is null) return NotFound();
            ProductEditVM result = new ProductEditVM
            {
                Id = product.Id,
                Name = product.Name,
                Count = product.Count,
                CategoryId = product.CategoryId,
                Images = product.Images,
                Price = product.Price
            };
            return View(result);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductEditVM productEditVM)
        {
            ViewBag.categories = await GetCategoriesByProduct();
            if (!ModelState.IsValid) return View(productEditVM);
            Product product = await _context.Products.Include(m => m.Images).Include(m => m.Category).Where(m => !m.IsDeleted && m.Id == id).FirstOrDefaultAsync();
            if (product is null) return NotFound();
            List<ProductImage> imageList = new List<ProductImage>();
            if (productEditVM.Photos == null)
            {
                foreach (var item in product.Images)
                {
                    string path = Helper.GetFilePath(_env.WebRootPath, "img", item.Image);
                    Helper.DeleteFile(path);
                    item.IsDeleted = true;
                }
                foreach (var item in productEditVM.Photos)
                {
                    string fileName = Guid.NewGuid().ToString() + "_" + item.FileName;
                    string path = Helper.GetFilePath(_env.WebRootPath, "img", item.FileName);
                    await item.SaveFile(path);
                    ProductImage productImage = new ProductImage
                    {
                        Image = fileName
                    };
                    imageList.Add(productImage);
                }
                imageList.FirstOrDefault().IsMain = true;
                product.Images = imageList;
            }
            product.Name = productEditVM.Name;
            product.Price = productEditVM.Price;
            product.Count = productEditVM.Count;
            product.CategoryId = productEditVM.CategoryId;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> SetDefaultImage(DefaultImageVM model)
        {
            List<ProductImage> productImages = await _context.ProductImages.Where(m =>m.ProductId == model.ProductId).ToListAsync();
            foreach (var item in productImages)
            {
                if (item.Id == model.ImageId)
                {
                    item.IsMain = true;
                }
                else
                {
                    item.IsMain = false;
                }
            }
            await _context.SaveChangesAsync();
            return Ok(productImages);
        }
    }
}