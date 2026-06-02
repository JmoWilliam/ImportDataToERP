using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

public class UserEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "帳號為必填")]
    [Display(Name = "帳號")]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "姓名為必填")]
    [Display(Name = "姓名")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email格式不正確")]
    public string? Email { get; set; }

    [Display(Name = "新密碼（留空不變更）")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "密碼至少6碼")]
    public string? NewPassword { get; set; }

    [Display(Name = "狀態")]
    public bool IsActive { get; set; } = true;
}
