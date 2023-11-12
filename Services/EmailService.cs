using System.Net.Mail;
using PlayOffsApi.Resilience;
using Resource = PlayOffsApi.Resources.Services.EmailService;

namespace PlayOffsApi.Services;

public class EmailService
{
    private static string Email => Environment.GetEnvironmentVariable("EMAILCLIENT");
    private static string Senha => Environment.GetEnvironmentVariable("EMAILPASSWORD");

    public static bool SendEmailPasswordReset(string userEmail, string userName, string link)
    {
        var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress(Email);
        mailMessage.To.Add(new MailAddress(userEmail));
        mailMessage.Subject = Resource.SendEmailPasswordResetRedefinicaoSenha;
        mailMessage.IsBodyHtml = true;
        // mailMessage.Body = link;
        mailMessage.Body = $"<div style=\"text-align: center;\"><div style=\"padding: 10px; text-align: left\"><h1>{Resource.SendEmailPasswordResetTitulo}</h1>\n" +
            $"<p>{Resource.SendEmailPasswordResetOla}, " + userName + ".</p>\n" +
            $"<p>{Resource.SendEmailPasswordResetUseTheButton}</p>\n" +
            "<a href=\"" + link +"\" target=\"_blank\" style=\"max-width: 280px; text-decoration: none; display: inline-block; background-color: #4caf50; color: #ffffff; height: 36px; border-radius: 5px; font-weight: bold; font-size: 18px; margin: 20px 0; width: 100%; text-align: center; padding-top: 10px; \">" +
            $"  {Resource.SendEmailPasswordResetAlterarSenha}" +
            "</a>" +
            $"<p>{Resource.SendEmailPasswordResetLink}</p>\n" +
            "<p>"+ link + "</p>\n" +
            $"<p>{Resource.SendEmailPasswordResetAtenciosamente},</p>\n" +
            $"<p>{Resource.SendEmailPasswordResetEquipeRoDaMo}</p></div></div>";

        var client = new SmtpClient("smtp.gmail.com");
        client.Port = 587;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.UseDefaultCredentials = false;
        var credential = new System.Net.NetworkCredential(Email, Senha); 
        client.EnableSsl = true;
        client.Credentials = credential;
        try
        {
            ExceptionHanderFactory.BuildForHttpClient().ConfigureRetry().Execute(() => {
                client.Send(mailMessage);
            });
            return true; 
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    public static bool SendConfirmationEmail(string userEmail, string userName, string link)
    {
        var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress(Email);
        mailMessage.To.Add(new MailAddress(userEmail));
        mailMessage.Subject = Resource.SendConfirmationEmailConfirmacaoDeEmail;
        mailMessage.IsBodyHtml = true;
        mailMessage.Body = $"<div style=\"text-align: center;\"><div style=\"padding: 10px; text-align: left\"><h1>{Resource.SendConfirmationEmailConfirmeSeuEmail}</h1>\n" +
                $"<p>{Resource.SendEmailPasswordResetOla}, " + userName + ".</p>\n" +
                $"<p>{Resource.SendConfirmationEmailPlayoffs}</p>\n" +
                $"<p>{Resource.SendConfirmationEmailUseTheButtonBellow}</p>\n" +
                $"<a href=\"" + link +"\" target=\"_blank\" style=\"max-width: 280px; text-decoration: none; display: inline-block; background-color: #4caf50; color: #ffffff; height: 36px; border-radius: 5px; font-weight: bold; font-size: 18px; margin: 20px 0; width: 100%; text-align: center; padding-top: 10px; \">" +
                $"  {Resource.SendConfirmationEmailConfirmarEmail}" +
                $"</a>" +
                $"<p>{Resource.SendEmailPasswordResetLink}</p>\n" +
                $"<p>"+ link + "</p>\n" +
                $"<p>{Resource.SendEmailPasswordResetAtenciosamente},</p>\n" +
                $"<p>{Resource.SendEmailPasswordResetEquipeRoDaMo}</p></div></div>";


        SmtpClient client = new SmtpClient("smtp.gmail.com");
        client.Port = 587;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.UseDefaultCredentials = false;
        System.Net.NetworkCredential credential = new System.Net.NetworkCredential(Email, Senha); 
        client.EnableSsl = true;
        client.Credentials = credential;
        try
        {
            ExceptionHanderFactory.BuildForHttpClient().ConfigureRetry().Execute(() => {
                client.Send(mailMessage);
            });
            return true; 
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    public static bool SendConfirmationPermissionToJoinInTeam(string userEmail, string userName, string nameOfTeam, string link)
    {
        return true;
        var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress(Email);
        mailMessage.To.Add(new MailAddress(userEmail));
        mailMessage.Subject = "Confirmação de Entrada em Time";
        mailMessage.IsBodyHtml = true;
        // mailMessage.Body = link;
        mailMessage.Body = $"<div style=\"text-align: center;\"><div style=\"padding: 10px; text-align: left\"><h1>Confirmação de Entrada em Time</h1>\n" +
            $"<p>Olá, " + userName + ".</p>\n" +
            $"<p>Utilize o botão abaixo para confirmar sua entrada no time "+ nameOfTeam +"</p>\n" +
            "<a href=\"" + link +"\" target=\"_blank\" style=\"max-width: 280px; text-decoration: none; display: inline-block; background-color: #4caf50; color: #ffffff; height: 36px; border-radius: 5px; font-weight: bold; font-size: 18px; margin: 20px 0; width: 100%; text-align: center; padding-top: 10px; \">" +
            $"Confirmar" +
            "</a>" +
            $"<p>{Resource.SendEmailPasswordResetLink}</p>\n" +
            "<p>"+ link + "</p>\n" +
            $"<p>{Resource.SendEmailPasswordResetAtenciosamente},</p>\n" +
            $"<p>{Resource.SendEmailPasswordResetEquipeRoDaMo}</p></div></div>";

        var client = new SmtpClient("smtp.gmail.com");
        client.Port = 587;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.UseDefaultCredentials = false;
        var credential = new System.Net.NetworkCredential(Email, Senha); 
        client.EnableSsl = true;
        client.Credentials = credential;
        try
        {
            ExceptionHanderFactory.BuildForHttpClient().ConfigureRetry().Execute(() => {
                client.Send(mailMessage);
            });
            return true; 
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

     public static bool SendConfirmationPermissionToJoinInChampionship(string userEmail, string userName, string nameOfChampionship, string link)
     {
         return true;
        var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress(Email);
        mailMessage.To.Add(new MailAddress(userEmail));
        mailMessage.Subject = "Confirmação de Entrada em Campeonato";
        mailMessage.IsBodyHtml = true;
        // mailMessage.Body = link;
        mailMessage.Body = $"<div style=\"text-align: center;\"><div style=\"padding: 10px; text-align: left\"><h1>Confirmação de Entrada em Campeonato</h1>\n" +
            $"<p>Olá, " + userName + ".</p>\n" +
            $"<p>Utilize o botão abaixo para confirmar a entrada de seu time no campeonato "+ nameOfChampionship +"</p>\n" +
            "<a href=\"" + link +"\" target=\"_blank\" style=\"max-width: 280px; text-decoration: none; display: inline-block; background-color: #4caf50; color: #ffffff; height: 36px; border-radius: 5px; font-weight: bold; font-size: 18px; margin: 20px 0; width: 100%; text-align: center; padding-top: 10px; \">" +
            $"Confirmar" +
            "</a>" +
            $"<p>{Resource.SendEmailPasswordResetLink}</p>\n" +
            "<p>"+ link + "</p>\n" +
            $"<p>{Resource.SendEmailPasswordResetAtenciosamente},</p>\n" +
            $"<p>{Resource.SendEmailPasswordResetEquipeRoDaMo}</p></div></div>";

        var client = new SmtpClient("smtp.gmail.com");
        client.Port = 587;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.UseDefaultCredentials = false;
        var credential = new System.Net.NetworkCredential(Email, Senha); 
        client.EnableSsl = true;
        client.Credentials = credential;
        try
        {
            ExceptionHanderFactory.BuildForHttpClient().ConfigureRetry().Execute(() => {
                client.Send(mailMessage);
            });
            return true; 
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }
}