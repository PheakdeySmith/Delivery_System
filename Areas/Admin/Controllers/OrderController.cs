using Delivery_System.DataAccess.Repository.IRepository;
using Delivery_System.Models;
using Delivery_System_Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Delivery_System.Models.ViewModels;

namespace Delivery_System.Areas.Admin.Controllers
{
  [Area("Admin")]
  [Authorize]
  public class OrderController : Controller
  {
    private readonly IUnitOfWork _unitOfWork;
    [BindProperty]
    public OrderVM OrderVM { get; set; }

    public OrderController(IUnitOfWork unitOfWork)
    {
      _unitOfWork = unitOfWork;
    }

    public IActionResult Index()
    {
      List<OrderHeader> orderHeaders;

      if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
      {
        // Admin or Employee: Retrieve all orders
        orderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser").ToList();
      }
      else
      {
        // Regular User: Retrieve only the user's orders
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value;
        orderHeaders = _unitOfWork.OrderHeader.GetAll(
            u => u.ApplicationUserId == userId,
            includeProperties: "ApplicationUser"
        ).ToList();
      }

      return View(orderHeaders);
    }

    public IActionResult Detail(int id)
    {
      OrderVM OrderVM = new()
      {
        OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser"),
        OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == id, includeProperties: "Product")
      };

      return View(OrderVM);
    }

    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    [HttpPost]
    public IActionResult UpdateOrderDetail()
    {
      var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);

      orderHeaderFromDb.Name = OrderVM.OrderHeader.Name;
      orderHeaderFromDb.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
      orderHeaderFromDb.StreetAddress = OrderVM.OrderHeader.StreetAddress;
      orderHeaderFromDb.City = OrderVM.OrderHeader.City;
      orderHeaderFromDb.State = OrderVM.OrderHeader.State;
      orderHeaderFromDb.PostalCode = OrderVM.OrderHeader.PostalCode;

      if (!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
      {
        orderHeaderFromDb.Carrier = OrderVM.OrderHeader.Carrier;
      }
      if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
      {
        orderHeaderFromDb.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
      }

      _unitOfWork.OrderHeader.Update(orderHeaderFromDb);
      _unitOfWork.Save();

      return RedirectToAction("Detail", new { id = orderHeaderFromDb.Id });
    }

    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    [HttpPost]
    public IActionResult StartProcessing(int orderId)
    {
      var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == orderId);
      if (orderHeaderFromDb == null)
      {
        return NotFound();
      }

      _unitOfWork.OrderHeader.UpdateStatus(orderId, SD.StatusInProcess);
      _unitOfWork.Save();

      return RedirectToAction("Detail", new { id = orderId });
    }


    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    [HttpPost]
    public IActionResult ShipOrder(int orderId, string carrier, string trackingNumber)
    {
      var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == orderId);
      if (orderHeaderFromDb == null)
      {
        return NotFound();
      }

      orderHeaderFromDb.TrackingNumber = trackingNumber;
      orderHeaderFromDb.Carrier = carrier;
      orderHeaderFromDb.OrderStatus = SD.StatusShipped;
      orderHeaderFromDb.ShippingDate = DateTime.Now;

      if (orderHeaderFromDb.PaymentStatus == SD.PaymentStatusDelayedPayment)
      {
        orderHeaderFromDb.PaymentDueDate = DateTime.Now.AddDays(30);
      }

      _unitOfWork.OrderHeader.Update(orderHeaderFromDb);
      _unitOfWork.Save();

      return RedirectToAction("Detail", new { id = orderId });
    }


  }
}
