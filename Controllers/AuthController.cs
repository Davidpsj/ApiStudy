using ApiStudy.Models.Auth;
using ApiStudy.Repository;
using ApiStudy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ApiStudy.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly IRepository<User> _userRepository;

    public AuthController(TokenService tokenService, IRepository<User> userRepository)
    {
        _tokenService = tokenService;
        _userRepository = userRepository;
    }

    [HttpPost("login")]
    [AllowAnonymous] // Permite acesso sem token
    public async Task<IActionResult> Login([FromBody] Login model)
    {
        var email = model.Email;
        var senha = model.Senha?.ToSha256();

        if (email != null && senha != null)
        {
            var query = await _userRepository.GetByQuery(u => u.Email == email && u.Senha == senha);
            var user = query.FirstOrDefault();

            if (user != null)
            {
                //// TODO: Acrescentar as Features do Usuário no Token futuramente.
                var token = _tokenService.GenerateToken(user.Id.ToString(), email, "Admin");
                return Ok(new { Token = token });
            }
        }

        return Unauthorized(new { Message = "Credenciais Inválidas" });
    }

    [HttpPost("reset-pwd")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetLogin model)
    {
        if (model.Email != null && model.Senha != null && model.Confirmation != null)
        {
            if (model.Senha != model.Confirmation)
            {
                return BadRequest(new { Message = "Senha e Confirmação são diferentes" });
            }

            model.Senha = model.Senha.ToSha256();

            var response = await _userRepository.GetByQuery(u => u.Email == model.Email && u.Senha == model.SenhaAntiga);

            var user = response.FirstOrDefault();

            if (user != null)
            {
                user.Senha = model.Senha;

                user = await _userRepository.UpdateAsync(user.Id, user);
            }

            return Ok(user);
        }

        return BadRequest(new { Message = "Não foi possível atualizar as credenciais do usuário" });
    }
}
