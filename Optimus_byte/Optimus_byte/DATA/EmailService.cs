using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Optimus_byte.DATA
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task EnviarCorreoAsync(
            string destinatario,
            string nombre,
            string asunto,
            string cuerpoHtml)
        {
            var settings = _config.GetSection("EmailSettings");

            string host = settings["SmtpHost"] ?? "smtp.gmail.com";
            int port = int.Parse(settings["SmtpPort"] ?? "587");
            string senderEmail = settings["SenderEmail"] ?? "";
            string senderName = settings["SenderName"] ?? "Taller";
            string password = settings["Password"] ?? "";

            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(senderName, senderEmail));
            mensaje.To.Add(new MailboxAddress(nombre, destinatario));
            mensaje.Subject = asunto;
            mensaje.Body = new TextPart("html") { Text = cuerpoHtml };

            using var cliente = new SmtpClient();
            await cliente.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await cliente.AuthenticateAsync(senderEmail, password);
            await cliente.SendAsync(mensaje);
            await cliente.DisconnectAsync(true);
        }
    }
}