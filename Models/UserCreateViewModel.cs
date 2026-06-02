using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

public class UserCreateViewModel
{
    [Required(ErrorMessage = "帳號為必填")]
    [Display(Name = "帳號")]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "姓名為必填")]
    [Display(Name = "姓名")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email格式不正確")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "密碼為必填")]
    [Display(Name = "密碼")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "密碼至少6碼")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "狀態")]
    public bool IsActive { get; set; } = true;
}
