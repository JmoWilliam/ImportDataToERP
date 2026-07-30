using ImportDataToERP.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImportDataToERP.ViewComponents;

public class CompanySelectorViewComponent : ViewComponent
{
    private readonly ErpCompanyService _companyService;
    private readonly ErpConnectionAccessor _erpConnectionAccessor;

    public CompanySelectorViewComponent(ErpCompanyService companyService, ErpConnectionAccessor erpConnectionAccessor)
    {
        _companyService = companyService;
        _erpConnectionAccessor = erpConnectionAccessor;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        List<ErpCompany> companies;
        try
        {
            companies = await _companyService.GetCompaniesAsync();
        }
        catch
        {
            // ERP登錄庫(DSCSYS)無法連線時，不顯示下拉選單，不影響其他功能
            companies = new List<ErpCompany>();
        }

        ViewBag.SelectedDatabase = _erpConnectionAccessor.GetSelectedDatabase();
        return View(companies);
    }
}
