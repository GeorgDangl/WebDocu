using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IEmailSender
    {
        Task<bool> SendForgotPasswordEmail(string userEmail, string passwordResetUrl);
    }
}
