using System.ComponentModel.DataAnnotations;

namespace ImportDataToERP.Models;

public class User
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

    [Display(Name = "密碼Hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Display(Name = "狀態")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "建立時間")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "更新時間")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
