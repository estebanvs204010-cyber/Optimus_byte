using System.Net;
using System.Net.Mail;

namespace Optimus_byte.Services
{
    public class CorreoService
    {
        private readonly IConfiguration _configuration;

        public CorreoService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpo)
        {
            var host = _configuration["EmailSettings:SmtpHost"];
            var port = int.Parse(_configuration["EmailSettings:SmtpPort"]!);
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"];
            var password = _configuration["EmailSettings:Password"];

            using var cliente = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(senderEmail, password),
                EnableSsl = true
            };

            using var mensaje = new MailMessage
            {
                From = new MailAddress(senderEmail!, senderName),
                Subject = asunto,
                Body = cuerpo,
                IsBodyHtml = true
            };

            mensaje.To.Add(destinatario);

            await cliente.SendMailAsync(mensaje);
        }
    }
}