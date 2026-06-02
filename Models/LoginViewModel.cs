using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "帳號為必填")]
    [Display(Name = "帳號")]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "密碼為必填")]
    [Display(Name = "密碼")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "記住我")]
    public bool RememberMe { get; set; }
}
