using Delivery_System.DataAccess.Repository.IRepository;
using Delivery_System.Models;
using Delivery_System.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;
using Delivery_System_Utility;
using Microsoft.AspNetCore.Authorization;

namespace Delivery_System.Areas.Admin.Controllers
{
  [Area("Admin")]
  [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
  public class ProductController : Controller
  {
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
    {
      _unitOfWork = unitOfWork;
      _webHostEnvironment = webHostEnvironment;
    }

    public IActionResult Index()
    {
      List<Product> product = _unitOfWork.Product.GetAll(includeProperties:"Category").ToList();
      return View(product);
    }

    public IActionResult Upsert(int? id)
{
    ProductVM productVM = new()
    {
        CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
        {
            Text = u.Name,
            Value = u.Id.ToString()
        }),
        Product = new Product()
    };

    if (id.HasValue && id.Value != 0)
    {
        productVM.Product = _unitOfWork.Product.Get(u => u.Id == id.Value);
        if (productVM.Product == null)
        {
            return NotFound();
        }

        // Log or debug output
        Console.WriteLine($"Fetched Product: {productVM.Product.Id}, {productVM.Product.Title}");
    }
    return View(productVM);
}


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Upsert(ProductVM productVM, IFormFile? file)
    {
      if (ModelState.IsValid)
      {
        try
        {
          string wwwRootPath = _webHostEnvironment.WebRootPath;
          if (file != null)
          {
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string productPath = Path.Combine(wwwRootPath, @"images\product");

            if (!string.IsNullOrEmpty(productVM.Product.ImageUrl))
            {
              var oldImage = Path.Combine(wwwRootPath, productVM.Product.ImageUrl.TrimStart('\\'));

              if (System.IO.File.Exists(oldImage))
              {
                System.IO.File.Delete(oldImage);
              }
            }

            using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
            {
              file.CopyTo(fileStream);
            }

            productVM.Product.ImageUrl = @"\images\product\" + fileName;
          }
        }
        catch (Exception ex)
        {
          ModelState.AddModelError("", "An error occurred while uploading the file: " + ex.Message);
          productVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
          {
            Text = u.Name,
            Value = u.Id.ToString()
          });
          return View(productVM);
        }

        if (productVM.Product.Id == 0)
        {
          _unitOfWork.Product.Add(productVM.Product);
        }
        else
        {
          _unitOfWork.Product.Update(productVM.Product);
        }

        _unitOfWork.Save();
        return RedirectToAction("Index");
      }
      else
      {
        productVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
        {
          Text = u.Name,
          Value = u.Id.ToString()
        });
        return View(productVM);
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
      var product = _unitOfWork.Product.Get(u => u.Id == id);
      if (product == null)
      {
        return NotFound();
      }

      _unitOfWork.Product.Remove(product);
      _unitOfWork.Save();
      return RedirectToAction("Index");
    }
  }
}
