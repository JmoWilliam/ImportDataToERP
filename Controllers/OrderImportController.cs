using System.Text.Json;
using ImportDataToERP.Models;
using ImportDataToERP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImportDataToERP.Controllers;

[Authorize]
public class OrderImportController : Controller
{
    private readonly OrderImportService _service;

    public OrderImportController(OrderImportService service)
    {
        _service = service;
    }

    // ========== 列表 ==========

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index()
    {
        var headers = await _service.GetAllHeadersAsync();
        return View(headers);
    }

    // ========== 手動新增單頭 ==========

    [HttpGet]
    public IActionResult Create()
    {
        return View("Edit", new OrderImportHeader());
    }

    [HttpPost]
    public async Task<IActionResult> Create(OrderImportHeader header)
    {
        if (!ModelState.IsValid) return View("Edit", header);

        var id = await _service.CreateHeaderAsync(header);
        TempData["Success"] = $"已新增單頭，匯入單號：{header.ImportBatchNo}";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ========== 編輯單頭 + 單身 ==========

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Edit(int id)
    {
        var header = await _service.GetHeaderByIdAsync(id);
        if (header == null) return NotFound();

        var details = await _service.GetDetailsByHeaderIdAsync(id);
        ViewBag.Details = details;
        return View(header);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, OrderImportHeader header)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Details = await _service.GetDetailsByHeaderIdAsync(id);
            return View(header);
        }

        header.Id = id;
        await _service.UpdateHeaderAsync(header);
        TempData["Success"] = "單頭已更新";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ========== 拋轉 ERP ==========

    [HttpPost]
    public async Task<IActionResult> TransferToErp(int id)
    {
        var result = await _service.TransferToErpAsync(id);

        if (result.StartsWith("OK|"))
        {
            var erpNo = result[3..];
            TempData["Success"] = $"拋轉ERP成功！ERP單號：{erpNo}";
        }
        else if (result.StartsWith("ERR|"))
        {
            TempData["Error"] = $"拋轉ERP失敗：{result[4..]}";
        }
        else
        {
            TempData["Warning"] = result;
        }

        return RedirectToAction(nameof(Index));
    }

    // ========== 單身 CRUD (AJAX) ==========

    [HttpPost]
    public async Task<IActionResult> CreateDetail([FromBody] OrderImportDetail detail)
    {
        if (detail.HeaderId <= 0)
            return BadRequest("HeaderId 不可為空");

        var id = await _service.CreateDetailAsync(detail);
        detail.Id = id;
        return Json(new { success = true, detail });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateDetail([FromBody] OrderImportDetail detail)
    {
        if (detail.Id <= 0)
            return BadRequest("Id 不可為空");

        await _service.UpdateDetailAsync(detail);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDetail([FromBody] DeleteDetailRequest req)
    {
        await _service.DeleteDetailAsync(req.Id);
        return Json(new { success = true });
    }

    public class DeleteDetailRequest { public int Id { get; set; } }

    // ========== 刪除單頭 ==========

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteHeaderAsync(id);
        TempData["Success"] = "已刪除";
        return RedirectToAction(nameof(Index));
    }

    // ========== Excel 匯入 ==========

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "請選擇檔案";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (ext != ".xlsx" && ext != ".xls")
        {
            TempData["Error"] = "僅支援 .xlsx 或 .xls 格式";
            return RedirectToAction(nameof(Index));
        }

        using var stream = file.OpenReadStream();
        var model = await _service.ParseExcelAsync(stream, file.FileName);

        if (model.Errors.Count > 0)
        {
            TempData["Error"] = string.Join("; ", model.Errors);
            return RedirectToAction(nameof(Index));
        }

        var json = JsonSerializer.Serialize(model.HeaderGroups);
        TempData["ParsedData"] = json;
        TempData["FileName"] = model.FileName;
        TempData["TotalHeaders"] = model.TotalHeaders.ToString();
        TempData["TotalDetails"] = model.TotalDetailRows.ToString();

        return View("Import", model);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm()
    {
        var json = TempData["ParsedData"] as string;
        if (string.IsNullOrEmpty(json))
        {
            TempData["Error"] = "無匯入資料，請重新上傳";
            return RedirectToAction(nameof(Index));
        }

        var groups = JsonSerializer.Deserialize<List<OrderImportHeaderGroup>>(json);
        if (groups == null || groups.Count == 0)
        {
            TempData["Error"] = "無匯入資料，請重新上傳";
            return RedirectToAction(nameof(Index));
        }

        var (headers, details, errors) = await _service.ConfirmImportAsync(groups);

        if (errors.Count > 0)
            TempData["Warning"] = $"部分略過：{string.Join("; ", errors)}";

        TempData["Success"] = $"匯入完成！單頭 {headers} 筆，明細 {details} 筆";
        return RedirectToAction(nameof(Index));
    }

    // ========== 下載範本 / 查看明細 ==========

    public IActionResult DownloadTemplate()
    {
        var bytes = _service.GenerateTemplateExcel();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "匯入格式_客戶訂單.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var details = await _service.GetDetailsByHeaderIdAsync(id);
        return Json(details);
    }
}
