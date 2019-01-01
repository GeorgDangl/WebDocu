using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace Dangl.WebDocumentation.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger _logger;

        public EmailSender(IOptions<EmailSettings> emailSettings,
            ILoggerFactory loggerFactory)
        {
            _emailSettings = emailSettings.Value;
            _logger = loggerFactory.CreateLogger<EmailSender>();
        }

        public Task<bool> SendForgotPasswordEmail(string userEmail, string passwordResetUrl)
        {
            var subject = "Reset your documentation password";
            var messageBodyHtml = $"<h3>Hi {userEmail}!</h3>"
                                  + "<p>This email was sent to you because you've requested to reset your password for DanglDocu.</p>"
                                  + "<p><b>If you did not request this, you don't have to take any action.</b></p>"
                                  + "<br />"
                                  + $"<p><a href=\"{passwordResetUrl}\">Click here to set a new password.</a></p>";

            return SendMessage(userEmail, subject, messageBodyHtml);
        }

        public async Task<bool> SendMessage(string emailTo, string subject, string bodyHtml)
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.FromAddress))
            {
                _logger.Log(LogLevel.Error, "There is no FromAddress configured in the email settings, can not send email");
                return false;
            }

            var message = new MimeMessage();
            message.To.Add(new MailboxAddress(emailTo));
            message.Subject = subject;
            message.Body = new TextPart(TextFormat.Html)
            {
                Text = bodyHtml
            };

            if (string.IsNullOrWhiteSpace(_emailSettings.FromName))
            {
                message.From.Add(new MailboxAddress(_emailSettings.FromAddress));
            }
            else
            {
                message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromAddress));
            }

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
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, "Failed to send email, exception was:\r\n" + e.ToString());
                return false;
            }
        }
    }
}
