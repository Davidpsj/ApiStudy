using ApiStudy.Repository.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ApiStudy.Repository;

// A restrição "where T : class" é importante para o Entity Framework Core
// E T deve ser a sua classe de modelo (User, Product, etc.)
public class BaseRepository<T> : IRepository<T>, IDisposable where T : class
{
    private readonly DatabaseContext _context;
    private readonly DbSet<T> _dbSet; // Referência direta à tabela (DbSet

    public bool UsingTransaction { get; set; }

    public BaseRepository(DatabaseContext context)
    {
        _context = context;
        // Obtém o DbSet<T> que representa a tabela para o tipo T
        _dbSet = _context.Set<T>();
    }

    // --- Implementação do IRepository<T> ---

    // Método para adicionar uma nova entidade
    public async Task<T> CreateAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        if (UsingTransaction) await _context.SaveChangesAsync();
        return entity;
    }

    // Método para listar todas as entidades
    public async Task<List<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<List<T>> GetAllAsync(params Expression<Func<T, object>>[] includes)
    {
        // Inicia a query com o DbSet
        IQueryable<T> query = _dbSet.AsQueryable(); // Usa AsQueryable para garantir que é a base da query

        // Aplica cada Include
        foreach (var include in includes)
        {
            // Usa a sobrecarga de Include que aceita a expressão
            query = query.Include(include);
        }

        // Executa a query e retorna a lista.
        // Retornar 'List<T>' (vazio se não houver dados) é mais seguro do que 'List<T>?' (que pode ser null).
        return await query.ToListAsync();
    }

    // Método para buscar entidade por ID
    public async Task<T?> GetByIdAsync(long id)
    {
        // O FindAsync é ideal para buscar pela chave primária
        return await _dbSet.FindAsync(id);
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<T?> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return await query.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);
    }

    public async Task<IQueryable<T>> GetByQuery(Expression<Func<T, bool>> predicate)
    {
        // Note que o uso de Func<T, bool> força a avaliação em memória
        // Para consultas mais eficientes, considere usar Expression<Func<T, bool>>
        var result = _dbSet.AsQueryable().Where(predicate).AsQueryable();
        return await Task.FromResult(result);
    }

    public async Task<IQueryable<T>> GetByQuery(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        // 1. Aplica todos os Includes (o query é atualizado a cada iteração)
        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        // 2. Aplica o filtro (será traduzido para SQL WHERE)
        query = query.Where(predicate);

        // 3. Retorna a query *não executada*. 
        // O chamador (ex: Controller/Service) deve chamar .ToListAsync() ou .FirstOrDefaultAsync()
        return await Task.FromResult(query);
    }

    // Método para deletar por ID
    public async Task<bool> DeleteAsync(long id)
    {
        // O EF Core precisa rastrear a entidade para deletar
        var entity = await GetByIdAsync(id);

        if (entity is null)
            return false;

        _dbSet.Remove(entity);

        if (UsingTransaction) await _context.SaveChangesAsync();
            
        return true;
    }

    // Método para atualizar
    public async Task<T?> UpdateAsync(long id, T entity)
    {
        // NOTA: Para um repositório genérico, assumimos que o ID está na entidade
        // e que o EF Core rastreará as mudanças.

        // O EF Core precisa saber qual entidade está sendo atualizada.
        // Aqui, estamos delegando ao EF Core para rastrear a entidade
        // ATENÇÃO: Se o ID não for atribuído à entidade 'entity' (o que é comum em APIs),
        // você precisa buscar a entidade primeiro (como você faz no seu UserRepository)

        // Opção 1: Anexar a entidade e marcar como modificada (EF Core rastreia todas as propriedades)
        _context.Entry(entity).State = EntityState.Modified;

        // Opção 2 (Mais robusta para garantir que a entidade existe):
        // var existingEntity = await GetByIdAsync(id);
        // if (existingEntity == null) return null;
        // // Copiar as propriedades de 'entity' para 'existingEntity'
        // // Isso requer Reflection ou um Mapper (ex: AutoMapper)

        // Para um repositório genérico simples, a Opção 1 é mais direta.
        // Para o seu caso (UserRepository), a lógica de busca e atualização é feita no Decorator.

        try
        {
            if (UsingTransaction) await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Verifica se a entidade ainda existe, se não, lança a exceção ou retorna null.
            if (await GetByIdAsync(id) is null)
            {
                return null;
            }
            throw;
        }

        return entity;
    }

    public async Task<T?> UpdateAsync(Guid id, T entity)
    {
        // Verifica se o EF já está observando esta entidade
        var entry = _context.Entry(entity);

        if (entry.State == EntityState.Detached)
        {
            // Se NÃO estiver rastreada (ex: veio de um app externo sem busca prévia),
            // aí sim usamos o Update para anexar e marcar como modificado.
            _dbSet.Update(entity);
        }
        else
        {
            // Se JÁ estiver rastreada (Unchanged, Modified, Added), 
            // NÃO force o _dbSet.Update(entity). 
            // O Change Tracker do EF já sabe que você adicionou um Card à lista.

            // Apenas garanta que o estado esteja correto se houver mudança escalar, 
            // mas para adição de filhos, o estado 'Unchanged' no pai é aceitável.
            // Se você quiser garantir, pode usar:
            // if (entry.State == EntityState.Unchanged) entry.State = EntityState.Modified;
        }

        try
        {
            // O EF vai detectar o novo Card na lista (estado Added) e fará o INSERT dele.
            // Ele NÃO tentará fazer UPDATE na Collection se ela não mudou, evitando o erro.
            if (UsingTransaction) await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (await GetByIdAsync(id) is null)
            {
                return null;
            }
            throw;
        }

        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity is null)
            return false;
        _dbSet.Remove(entity);
        if (UsingTransaction) await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}