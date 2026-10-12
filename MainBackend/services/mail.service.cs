using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Net.Imap;
using MailKit;
using MailKit.Search;
using AngleSharp.Text;

namespace App.Services.MailService;

public interface IMailService {
    public Task<string> sendMailMessage(string fromUser, string text, string toUser, string subject);
    public Task<string> getMailMessages();
}
public class MailService : IMailService {
    public async Task<string> sendMailMessage(string fromUser, string text, string toUser, string subject) {
        string? mailUser = Environment.GetEnvironmentVariable("MAIL_USER");
        string? mailUserPassword = Environment.GetEnvironmentVariable("MAIL_PASSWORD");
        string? mailServer = Environment.GetEnvironmentVariable("MAIL_SERVER");
        int smtpPort = Environment.GetEnvironmentVariable("SMTP_PORT").ToInteger(0);
        if(mailUser != null && mailUserPassword != null && mailServer != null && smtpPort != 0){
            MimeMessage message = new ();
            message.From.Add(new MailboxAddress(fromUser, mailUser));
            message.To.Add(new MailboxAddress("null", toUser));
            message.Subject = subject;
            message.Body = new TextPart(){
                Text = text
            };

            SmtpClient client = new ();
            await client.ConnectAsync(mailServer, smtpPort, MailKit.Security.SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(mailUser, mailUserPassword);
            
            string req = await client.SendAsync(message);
            await client.DisconnectAsync(true);
            return req;
        }
        Console.WriteLine("[WARN] почтовый клиент для отправки и получения писем не подключён");
        return "MAIL SERVER NO CONNECT";
        
    }
    public async Task<string> getMailMessages() {
        string? mailUser = Environment.GetEnvironmentVariable("MAIL_USER");
        string? mailUserPassword = Environment.GetEnvironmentVariable("MAIL_PASSWORD");
        string? mailServer = Environment.GetEnvironmentVariable("MAIL_SERVER");
        int imapPort = Environment.GetEnvironmentVariable("IMAP_PORT").ToInteger(0);
        if(mailUser != null && mailUserPassword != null && mailServer != null && imapPort != 0){
            ImapClient client = new ();
            await client.ConnectAsync(mailServer, imapPort, MailKit.Security.SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(mailUser, mailUserPassword);
            IMailFolder inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);
            foreach (UniqueId messageId in inbox.Search(SearchQuery.All)){
                MimeMessage? message = await inbox.GetMessageAsync(messageId);
                if(message != null){
                    System.Console.WriteLine($"Message Author: ${message.From}; Theme: {message.Subject}");
                }
                
            }
            await client.DisconnectAsync(true);
            return "OK";
        }
        Console.WriteLine("[WARN] почтовый клиент для отправки и получения писем не подключён");
        return "MAIL SERVER NO CONNECT";
    }
}