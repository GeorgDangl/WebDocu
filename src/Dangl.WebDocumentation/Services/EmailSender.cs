using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace Dangl.WebDocumentation.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;

        public EmailSender(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public Task<bool> SendForgotPasswordEmail(string userEmail, string passwordResetUrl)
        {
            var message = new MimeMessage();
            message.To.Add(new MailboxAddress(userEmail));
            message.Subject = "Reset your documentation password";
            var messageBodyHtml = "<h3>Hi!</h3>"
                                  + "<p>This email was sent to you because you've requested to reset your password.</p>"
                                  + "<p><b>If you did not request this, you don't have to take any action.</b></p>"
                                  + "<br />"
                                  + $"<p><a href=\"{passwordResetUrl}\">Click here to set a new password.</a></p>";
            message.Body = new TextPart(TextFormat.Html)
            {
                Text = messageBodyHtml
            };
            return SendMessage(message);
        }

        private async Task<bool> SendMessage(MimeMessage message)
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.FromAddress))
            {
                return false;
            }
            message.From.Add(new MailboxAddress(_emailSettings.FromAddress));
            try
            {
                using (var smtpClient = new SmtpClient())
                {
                    await smtpClient.ConnectAsync(_emailSettings.ServerAddress, _emailSettings.ServerPort, _emailSettings.UseTls);
                    if (_emailSettings.RequiresAuthentication)
                    {
                        var username = _emailSettings.Username;
                        var password = _emailSettings.Password;
                        await smtpClient.AuthenticateAsync(username, password);
                    }
                    await smtpClient.SendAsync(message);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
