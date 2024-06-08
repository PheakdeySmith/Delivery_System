using Delivery_System.DataAccess.Repository.IRepository;
using Delivery_System.Models;
using Delivery_System.Models.ViewModels;
using Delivery_System_Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Delivery_System.Areas.Admin.Controllers
{
  [Area("Admin")]
  [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
  public class CompanyController : Controller
  {
    private readonly IUnitOfWork _unitOfWork;
    public CompanyController(IUnitOfWork unitOfWork)
    {
      _unitOfWork = unitOfWork;
    }
    public IActionResult Index()
    {
      List<Company> companies = _unitOfWork.Company.GetAll().ToList();
      return View(companies);
    }
    [HttpGet]
    public IActionResult Upsert(int? id)
    {
      if (id.HasValue && id.Value != 0)
      {
        Company company = _unitOfWork.Company.Get(u => u.Id == id.Value);
        return View(company);
      }
      else
      {
        return View(new Company());
      }
      
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Upsert(Company company)
    {
      if (ModelState.IsValid)
      {
        if (company.Id == 0)
        {
          _unitOfWork.Company.Add(company);
        }
        else
        {
          _unitOfWork.Company.Update(company);
        }

        _unitOfWork.Save();
        return RedirectToAction("Index");
      }
      else
      {
        return View(company);
      }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
      var company = _unitOfWork.Company.Get(u => u.Id == id);
      if (company == null)
      {
        return NotFound();
      }

      _unitOfWork.Company.Remove(company);
      _unitOfWork.Save();
      return RedirectToAction("Index");
    }

  }
}
