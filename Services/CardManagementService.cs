using ApiStudy.Models;
using ApiStudy.Repository;
using ApiStudy.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace ApiStudy.Services;

public class CardManagementService<T> : BaseRepository<T> where T : class
{
    private readonly DatabaseContext _context;

    // 🏆 Passo 1: Injeção do Contexto no Construtor
    public CardManagementService(DatabaseContext context) : base(context)
    {
        _context = context;
    }

    // Exemplo de uso: Método que precisa acessar o banco diretamente
    //public async Task<int> GetTotalUserCardsCountAsync()
    //{
    //    // Passo 2: Acesso ao DbSet através da instância injetada
    //    return await _context.Set<T>().CountAsync();
    //}
}
