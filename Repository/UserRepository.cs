using ApiStudy.Models.Auth;
using ApiStudy.Repository.Context;
using System.Linq.Expressions;

namespace ApiStudy.Repository;

public class UserRepository
{
    private readonly DatabaseContext _context;
    private readonly BaseRepository<User> _repository;

    public UserRepository(DatabaseContext context)
    {
        _context = context;
        _repository = new BaseRepository<User>(context);
    }

    public async Task<User> CreateAsync(User entity)
    {
        var result = await _repository.CreateAsync(entity);

        return result;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var result = await _repository.DeleteAsync(id);

        return result;
    }

    public async Task<List<User>> GetAllAsync()
    {
        var usersList = await _repository.GetAllAsync();

        return usersList;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        var user = await _repository.GetByIdAsync(id);

        return user;
    }

    public async Task<IQueryable<User>> GetByQuery(Expression<Func<User, bool>> predicate)
    {
        var users = await _repository.GetByQuery(predicate);
        return users;
    }

    public async Task<User?> UpdateAsync(Guid id, User entity)
    {
        User? user = await GetByIdAsync(id);

        if (user is null)
            return await Task.FromResult<User?>(null);

        user.Name = entity.Name;
        user.Senha = entity.Senha;
        user.Email = entity.Email;

        return await _repository.UpdateAsync(id, user);
    }
}
