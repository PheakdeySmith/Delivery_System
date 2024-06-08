using Delivery_System.DataAccess.Repository.IRepository;
using Delivery_System.Models;
using Delivery_System.Models.ViewModels;
using Delivery_System_Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace Delivery_System.Areas.Customer.Controllers
{
  [Area("Customer")]
  [Authorize]
  public class ShoppingCartController : Controller
  {
    private readonly IUnitOfWork _unitOfWork;
    [BindProperty]
    public ShoppingCartVM ShoppingCartVM { get; set; }

    public ShoppingCartController(IUnitOfWork unitOfWork)
    {
      _unitOfWork = unitOfWork;
    }

    public IActionResult Index()
    {
      var claimIdentity = (ClaimsIdentity)User.Identity;
      var userId = claimIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

      ShoppingCartVM = new ShoppingCartVM
      {
        ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product"),
        OrderHeader = new()
      };

      foreach (var cart in ShoppingCartVM.ShoppingCartList)
      {
        cart.Price = GetPriceBaseOnQuantity(cart);
        ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
      }

      return View(ShoppingCartVM);
    }

    public IActionResult Summary()
    {
      var claimIdentity = (ClaimsIdentity)User.Identity;
      var userId = claimIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

      ShoppingCartVM = new ShoppingCartVM
      {
        ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product"),
        OrderHeader = new()
      };

      var applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
      ShoppingCartVM.OrderHeader.ApplicationUser = applicationUser;

      ShoppingCartVM.OrderHeader.Name = applicationUser.Name;
      ShoppingCartVM.OrderHeader.PhoneNumber = applicationUser.PhoneNumber;
      ShoppingCartVM.OrderHeader.StreetAddress = applicationUser.StreetAddress;
      ShoppingCartVM.OrderHeader.City = applicationUser.City;
      ShoppingCartVM.OrderHeader.State = applicationUser.State;
      ShoppingCartVM.OrderHeader.PostalCode = applicationUser.PostalCode;

      foreach (var cart in ShoppingCartVM.ShoppingCartList)
      {
        cart.Price = GetPriceBaseOnQuantity(cart);
        ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
      }

      return View(ShoppingCartVM);
    }

    [HttpPost]
    [ActionName("Summary")]
    public IActionResult SummaryPost()
    {
      var claimIdentity = (ClaimsIdentity)User.Identity;
      var userId = claimIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

      ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product");

      ShoppingCartVM.OrderHeader.OrderDate = System.DateTime.Now;
      ShoppingCartVM.OrderHeader.ApplicationUserId = userId;

      ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);

      foreach (var cart in ShoppingCartVM.ShoppingCartList)
      {
        cart.Price = GetPriceBaseOnQuantity(cart);
        ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
      }

      if (applicationUser.CompanyId.GetValueOrDefault() == 0)
      {
        //Customer Order
        ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
        ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
      }
      else
      {
        //Comapany Order
        ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
        ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
      }

      _unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
      _unitOfWork.Save();

      foreach (var cart in ShoppingCartVM.ShoppingCartList)
      {
        OrderDetail orderDetail = new()
        {
          ProductId = cart.ProductId,
          OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
          Price = cart.Price,
          Count = cart.Count,
        };

        _unitOfWork.OrderDetail.Add(orderDetail);
        _unitOfWork.Save();

      }

      if (applicationUser.CompanyId.GetValueOrDefault() == 0)
      {
        var domain = "http://localhost:5055";
        var options = new Stripe.Checkout.SessionCreateOptions
        {
          SuccessUrl = domain + $"/customer/shoppingcart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
          CancelUrl = domain + "/customer/shoppingcart/index",
          LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
          Mode = "payment",
        };

        foreach (var item in ShoppingCartVM.ShoppingCartList)
        {
          var sessionLineItem = new SessionLineItemOptions
          {
            PriceData = new SessionLineItemPriceDataOptions
            {
              UnitAmount = (long)(item.Price * 100),
              Currency = "usd",
              ProductData = new SessionLineItemPriceDataProductDataOptions
              {
                Name = item.Product.Title
              }
            },
            Quantity = item.Count
          };

          options.LineItems.Add(sessionLineItem);

        }

        var service = new Stripe.Checkout.SessionService();
        Session session = service.Create(options);

        _unitOfWork.OrderHeader.UpdateStripePaymentID(ShoppingCartVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
        _unitOfWork.Save();

        Response.Headers.Add("Location", session.Url);

        return new StatusCodeResult(303);
      }


      return RedirectToAction("OrderConfirmation", new { id = ShoppingCartVM.OrderHeader.Id });
    }

    public IActionResult OrderConfirmation(int id)
    {
      OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser");
      if(orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
      {
        // Customer Order
        var service = new SessionService();
        Session session = service.Get(orderHeader.SessionId);

        if(session.PaymentStatus.ToLower() == "paid")
        {
          _unitOfWork.OrderHeader.UpdateStripePaymentID(id, session.Id, session.PaymentIntentId);
          _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
          _unitOfWork.Save();
        }
      }

      List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
      _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
      _unitOfWork.Save();

      return View(id);
    }

    private double GetPriceBaseOnQuantity(ShoppingCart shoppingCart)
    {
      if (shoppingCart.Count <= 50)
      {
        return (double)shoppingCart.Product.Price;
      }
      else if (shoppingCart.Count <= 100)
      {
        return (double)shoppingCart.Product.Price50;
      }
      else
      {
        return (double)shoppingCart.Product.Price100;
      }
    }


    public IActionResult Plus(int CartId)
    {
      var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == CartId);
      cartFromDb.Count += 1;
      _unitOfWork.ShoppingCart.Update(cartFromDb);
      _unitOfWork.Save();
      return RedirectToAction("Index");
    }

    public IActionResult Minus(int CartId)
    {
      var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == CartId);
      if (cartFromDb.Count < 1)
      {
        _unitOfWork.ShoppingCart.Remove(cartFromDb);
      }
      else
      {
        cartFromDb.Count -= 1;
        _unitOfWork.ShoppingCart.Update(cartFromDb);
      }

      _unitOfWork.Save();
      return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
      var shoppingCart = _unitOfWork.ShoppingCart.Get(u => u.Id == id);
      if (shoppingCart == null)
      {
        return NotFound();
      }

      _unitOfWork.ShoppingCart.Remove(shoppingCart);
      _unitOfWork.Save();
      return RedirectToAction("Index");
    }
  }
}
