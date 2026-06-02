using ImportDataToERP.Models;
using ImportDataToERP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImportDataToERP.Controllers;

[Authorize]
public class UserController : Controller
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userService.GetAllAsync();
        return View(users);
    }

    public IActionResult Create()
    {
        return View(new UserCreateViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = new User
        {
            Account = vm.Account,
            Name = vm.Name,
            Email = vm.Email,
            IsActive = vm.IsActive
        };
        await _userService.CreateAsync(user, vm.Password);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null) return NotFound();

        var vm = new UserEditViewModel
        {
            Id = user.Id,
            Account = user.Account,
            Name = user.Name,
            Email = user.Email ?? "",
            IsActive = user.IsActive
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UserEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = new User
        {
            Id = vm.Id,
            Account = vm.Account,
            Name = vm.Name,
            Email = vm.Email,
            IsActive = vm.IsActive
        };

        string? newPassword = !string.IsNullOrEmpty(vm.NewPassword) ? vm.NewPassword : null;
        await _userService.UpdateAsync(user, newPassword);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _userService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
