using Delivery_System;
using Delivery_System.DataAccess.Data;
using Delivery_System.DataAccess.Repository;
using Delivery_System.DataAccess.Repository.IRepository;
using Delivery_System.Models;
using Delivery_System_Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Area("Admin")]
[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
public class CategoryController : Controller
{
  private readonly IUnitOfWork _unitOfWork;

  public CategoryController(IUnitOfWork unitOfWork)
  {
    _unitOfWork = unitOfWork;
  }
  public IActionResult Index()
  {
    List<Category> categories = _unitOfWork.Category.GetAll().ToList();
    return View(categories);
  }

  public IActionResult Create()
  {
    return View();
  }

  [HttpPost]
  [ValidateAntiForgeryToken]
  public IActionResult Create(Category category)
  {
    if (ModelState.IsValid)
    {
      _unitOfWork.Category.Add(category);
      _unitOfWork.Save();
      return RedirectToAction("Index");
    }
    return View(category);
  }

  public IActionResult Edit(int id)
  {
    var category = _unitOfWork.Category.Get(u => u.Id == id);
    if (category == null)
    {
      return NotFound();
    }
    return View(category);
  }

  [HttpPost]
  [ValidateAntiForgeryToken]
  public IActionResult Edit(int id, Category category)
  {
    if (id != category.Id)
    {
      return BadRequest();
    }

    if (ModelState.IsValid)
    {
      try
      {
        _unitOfWork.Category.Update(category);
        _unitOfWork.Save();
      }
      catch (DbUpdateConcurrencyException)
      {
        return NotFound();
      }
      return RedirectToAction("Index");
    }
    return View(category);
  }

  [HttpPost]
  [ValidateAntiForgeryToken]
  public IActionResult Delete(int id)
  {
    var category = _unitOfWork.Category.Get(u => u.Id == id);
    if (category == null)
    {
      return NotFound();
    }

    _unitOfWork.Category.Remove(category);
    _unitOfWork.Save();
    return RedirectToAction("Index");
  }
}
