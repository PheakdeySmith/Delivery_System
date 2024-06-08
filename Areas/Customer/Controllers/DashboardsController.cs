using System.Diagnostics;
using System.Security.Claims;
using Delivery_System.DataAccess.Repository.IRepository;
using Delivery_System.Models;
using Delivery_System.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis;

namespace Delivery_System.Areas.Customer.Controllers;

[Area("Customer")]
public class DashboardsController : Controller
{
  private readonly IUnitOfWork _unitOfWork;
  private readonly IWebHostEnvironment _webHostEnvironment;

  public DashboardsController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
  {
    _unitOfWork = unitOfWork;
    _webHostEnvironment = webHostEnvironment;
  }

  public IActionResult Index()
  {
    IEnumerable<Product> ProductList = _unitOfWork.Product.GetAll(includeProperties: "Category");
    return View(ProductList);
  }

  [HttpGet]
  public IActionResult Detail(int id)
  {

    ShoppingCart cart = new()
    {
      Product = _unitOfWork.Product.Get(u => u.Id == id, includeProperties: "Category"),
      Count = 1,
      ProductId = id
    };
    return View(cart);
  }

  [HttpPost]
  [Authorize]
  public IActionResult Detail(ShoppingCart shoppingCart)
  {
    var claimIdentity = (ClaimsIdentity)User.Identity;
    var userId = claimIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
    shoppingCart.ApplicationUserId = userId;

    shoppingCart.Id = 0;

    ShoppingCart cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.ApplicationUserId == userId && u.ProductId == shoppingCart.ProductId);

    if (cartFromDb != null)
    {
      cartFromDb.Count += shoppingCart.Count;
      _unitOfWork.ShoppingCart.Update(cartFromDb);
    }
    else
    {
      _unitOfWork.ShoppingCart.Add(shoppingCart);
    }

    _unitOfWork.Save();

    return RedirectToAction("Index");
  }
}
