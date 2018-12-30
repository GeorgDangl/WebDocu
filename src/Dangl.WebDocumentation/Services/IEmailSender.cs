using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IEmailSender
    {
        Task<bool> SendMessage(string emailTo, string subject, string bodyHtml);
        Task<bool> SendForgotPasswordEmail(string userEmail, string passwordResetUrl);
    }
}
