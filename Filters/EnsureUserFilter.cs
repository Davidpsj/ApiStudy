// PSEUDOCÓDIGO (plano detalhado):
// 1. Criar um filtro de ação (IAsyncActionFilter) que encapsula o trecho de validação do usuário.
//    - Ao executar, o filtro deve:
//      a) Ler o usuário do contexto (HttpContext.User).
//      b) Tentar obter o Claim com ClaimTypes.NameIdentifier e convertê-lo para Guid.
//      c) Se falhar, interromper a execução e retornar 401 Unauthorized com mensagem apropriada.
//      d) Se succeed, armazenar o Guid em HttpContext.Items usando uma chave bem conhecida (ex: "LoggedInUserId").
//      e) Chamar next() para permitir execução da action.
// 2. Expor um atributo `[EnsureUser]` que aplica o filtro via TypeFilterAttribute para permitir injeção/registro simples.
// 3. Fornecer métodos de extensão para HttpContext para recuperar o LoggedInUserId de forma prática nas controllers/actions.
// 4. Mostrar (em comentário) como usar o atributo em uma controller e como recuperar o ID dentro das actions.
// 5. Implementar tudo em um único arquivo para fácil inclusão no projeto e registro no DI (não é necessário registrar o filtro quando usamos TypeFilterAttribute).
//
// Observações de implementação:
// - Retornar `UnauthorizedObjectResult` com a mesma mensagem do trecho original.
// - Chave em HttpContext.Items: "LoggedInUserId".
// - Métodos de extensão:
//    - `bool TryGetLoggedInUserId(this HttpContext, out Guid id)`
//    - `Guid GetLoggedInUserId(this HttpContext)` (lança InvalidOperationException se não existir).
//
// O código abaixo segue esse plano.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApiStudy.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class EnsureUserAttribute : TypeFilterAttribute
{
    public EnsureUserAttribute() : base(typeof(EnsureUserFilter)) { }
}

public sealed class EnsureUserFilter : IAsyncActionFilter
{
    private const string LoggedInUserIdKey = "LoggedInUserId";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user is null ||
            !Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Guid loggedInUserId))
        {
            context.Result = new UnauthorizedObjectResult("Usuário não autenticado ou ID inválido.");
            return;
        }

        // Armazena o Guid no Items para acesso posterior na action/controller
        httpContext.Items[LoggedInUserIdKey] = loggedInUserId;

        await next();
    }
}

public static class HttpContextExtensions
{
    private const string LoggedInUserIdKey = "LoggedInUserId";

    // Tenta obter o LoggedInUserId previamente setado pelo filtro.
    public static bool TryGetLoggedInUserId(this HttpContext httpContext, out Guid loggedInUserId)
    {
        loggedInUserId = Guid.Empty;

        if (httpContext is null)
            return false;

        if (httpContext.Items.TryGetValue(LoggedInUserIdKey, out var value) && value is Guid g)
        {
            loggedInUserId = g;
            return true;
        }

        return false;
    }

    // Obtém o LoggedInUserId ou lança InvalidOperationException se não encontrado.
    public static Guid GetLoggedInUserId(this HttpContext httpContext)
    {
        if (httpContext.TryGetLoggedInUserId(out var id))
            return id;

        throw new InvalidOperationException("LoggedInUserId não está disponível no HttpContext. Verifique se o filtro [EnsureUser] foi aplicado.");
    }
}
/*
USO (exemplo):

// 1) Aplicar o atributo na Controller ou em ações específicas:
[EnsureUser]
public class CardController : Controller
{
    // ...
    public async Task<IActionResult> GetCollectionsAsync()
    {
        // 2) Recuperar o ID do usuário de forma simples:
        var loggedInUserId = HttpContext.GetLoggedInUserId();

        // restante da lógica...
    }
}

// Observação: Não é necessário registrar nada no DI para este padrão com TypeFilterAttribute.
// Se preferir registrar explicitamente para injeção, registre EnsureUserFilter no IServiceCollection.
*/