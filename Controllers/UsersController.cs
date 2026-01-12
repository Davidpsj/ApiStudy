using Microsoft.AspNetCore.Mvc;
using ApiStudy.Repository;
using Microsoft.AspNetCore.Authorization;
using ApiStudy.Services;
using ApiStudy.Models.Auth;

namespace ApiStudy.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : Controller
    {
        private readonly IRepository<User> _repository;

        public UsersController(IRepository<User> repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<ActionResult<List<User>>> Index()
        {
            var result = await _repository.GetAllAsync();

            return Ok(result);
        }

        [HttpGet]
        [Route("/api/[controller]/{id}")]
        public async Task<ActionResult<User>> GetById(Guid id)
        {
            var result = await _repository.GetByIdAsync(id);

            if (result == null)
            {
                return NotFound($"User {id} não encontrado");
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("/api/[controller]")]
        public async Task<ActionResult<User>> Create(User user)
        {
            user.Senha = user.Senha.ToSha256();
            var posted = await _repository.CreateAsync(user);
            if (!_repository.UsingTransaction) await _repository.SaveChangesAsync();

            return Ok(posted);
        }

        [HttpPut]
        [Route("/api/[controller]")]
        public async Task<ActionResult<User>> Update(User user)
        {
            if (user == null)
            {
                return BadRequest("User could not updated, no user passed!");
            }

            var targetUser = await _repository.GetByIdAsync(user.Id);

            if (targetUser == null)
            {
                return NotFound(new { Message = $"User not found to Id {user.Id}" });
            }

            targetUser.Name = user.Name;
            targetUser.Email = user.Email;

            var result = await _repository.UpdateAsync(user.Id, targetUser);

            return Ok(result);
        }

        [HttpDelete]
        [Route("/api/[controller]/{id}")]
        public async Task<ActionResult<bool>> Delete(Guid id)
        {
            var result = await _repository.DeleteAsync(id);
            return Ok(result);
        }
    }
}
