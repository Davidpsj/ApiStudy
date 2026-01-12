# ===================================================
# 1. ESTÁGIO DE BUILD (SDK)
# ===================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia os arquivos de projeto (Ex: SuaApi.csproj)
# É mais seguro copiar apenas os projetos e restaurar primeiro
COPY ./*.csproj ./
RUN dotnet restore

# Copia o restante dos arquivos e publica
COPY . .
# A variável PROJECT_NAME deve ser o nome do seu arquivo .csproj (ex: MinhaAPI)
# Usaremos 'ApiStudy' como exemplo, baseado na sua citação.
ARG PROJECT_NAME=ApiStudy 
# Se o .csproj foi copiado DIRETAMENTE para /src (sem subpastas)
RUN dotnet publish "${PROJECT_NAME}.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ===================================================
# 2. ESTÁGIO FINAL (RUNTIME)
# ===================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copia os arquivos publicados
COPY --from=build /app/publish .

# Define a variável de ambiente para o host, corrigindo o erro de acesso
# (Escutar em todas as interfaces)
ENV ASPNETCORE_URLS=http://+:8080

# Define a porta de escuta
EXPOSE 8080

# Define o ENTRYPOINT usando o nome do projeto/dll
# Se o seu projeto principal se chama "ApiStudy", o arquivo é "ApiStudy.dll"
ENTRYPOINT ["dotnet", "ApiStudy.dll"]