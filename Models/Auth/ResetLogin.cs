namespace ApiStudy.Models.Auth
{
    public class ResetLogin
    {
        public string Email { get; set; } = string.Empty;
        public string SenhaAntiga { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string Confirmation { get; set; } = string.Empty;
    }
}
