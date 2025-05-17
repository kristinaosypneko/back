using Microsoft.AspNetCore.Mvc;
using WeightApiService.Core.Models;
using WeightApiService.Core.Interfaces;
using FluentResults;

namespace WeigthApiService.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Создает нового пользователя
    /// </summary>
    /// <param name="user">Данные пользователя</param>
    /// <returns>Результат создания</returns>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] User user)
    {
        if (user == null)
        {
            return BadRequest("User data is required");
        }
        
        user.Id = Guid.NewGuid();


        var result = await _userService.AddAsync(user);

        if (result.IsSuccess)
        {
            return Ok(new { 
                message = "User created successfully",
                userId = user.Id 
            });
        }

        return BadRequest(new
        {
            message = "Failed to create user",
            errors = result.Errors.Select(e => e.Message)
        });
    }

    /// <summary>
    /// Получает пользователя по Telegram ID
    /// </summary>
    /// <param name="tgId">Telegram ID пользователя</param>
    /// <returns>Данные пользователя</returns>
    [HttpGet("{tgId}")]
    public async Task<IActionResult> GetUser(string tgId)
    {
        var result = await _userService.GetByIdAsync(tgId);

        if (result.IsFailed)
        {
            return NotFound(new
            {
                message = "User not found",
                errors = result.Errors.Select(e => e.Message)
            });
        }

        return Ok(result.Value);
    }
}