using ImportDataToERP.Models;
using ImportDataToERP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImportDataToERP.Controllers;

[Authorize]
public class QuotationImportController : Controller
{
    private readonly QuotationImportService _service;

    public QuotationImportController(QuotationImportService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _service.GetAllAsync();
        return View(items);
    }

    public IActionResult Create()
    {
        return View(new QuotationImport());
    }

    [HttpPost]
    public async Task<IActionResult> Create(QuotationImport item)
    {
        if (!ModelState.IsValid) return View(item);
        await _service.CreateAsync(item);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Import(int id)
    {
        await _service.ImportToErpAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
