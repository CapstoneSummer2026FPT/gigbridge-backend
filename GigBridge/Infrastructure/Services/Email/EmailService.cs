using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Auth.VerifyEmail.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly IApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public EmailService(IApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        public async Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default)
        {
            /*
            // ==========================================
            // OLD RESEND API METHOD (for reusing later)
            // ==========================================
            var apiKey = _configuration["Resend:ApiKey"];
            var fromEmail = _configuration["Resend:From"] ?? "onboarding@resend.dev";

            var emailData = new
            {
                from = fromEmail,
                to = emailRequestDTO.To,
                subject = emailRequestDTO.Subject,
                html = emailRequestDTO.Body,
                attachments = new List<object>()
            };

            if (emailRequestDTO.Attachments != null && emailRequestDTO.Attachments.Any())
            {
                foreach (var filePath in emailRequestDTO.Attachments)
                {
                    if (System.IO.File.Exists(filePath))
                    {
                        var fileName = System.IO.Path.GetFileName(filePath);
                        var contentBytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
                        var contentBase64 = Convert.ToBase64String(contentBytes);

                        ((List<object>)emailData.attachments).Add(new
                        {
                            content = contentBase64,
                            filename = fileName
                        });
                    }
                }
            }

            var json = JsonSerializer.Serialize(emailData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            Console.WriteLine($"[EmailService] Sending email via Resend API to {emailRequestDTO.To}...");

            try
            {
                var response = await _httpClient.PostAsync("https://api.resend.com/emails", content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[EmailService] Email sent successfully! Response: {responseBody}");
                }
                else
                {
                    Console.WriteLine($"[EmailService] Failed to send email. Status: {response.StatusCode}, Error: {responseBody}");
                    throw new Exception($"Resend API error: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] Exception during Resend API call: {ex.Message}");
                throw;
            }
            // ==========================================
            */

            // SMTP Gmail Implementation for local development
            var mail = "DE180924ngoanhquan@gmail.com";
            var pass = "uvjs reiv emzl dlsk";

            using var client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new System.Net.NetworkCredential(mail, pass),
                EnableSsl = true
            };

            using var message = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(mail),
                Subject = emailRequestDTO.Subject,
                Body = emailRequestDTO.Body,
                IsBodyHtml = true
            };

            message.To.Add(emailRequestDTO.To);

            if (emailRequestDTO.Attachments != null && emailRequestDTO.Attachments.Any())
            {
                foreach (var filePath in emailRequestDTO.Attachments)
                {
                    if (System.IO.File.Exists(filePath))
                    {
                        message.Attachments.Add(new System.Net.Mail.Attachment(filePath));
                    }
                }
            }

            Console.WriteLine($"[EmailService] Sending SMTP email via Gmail to {emailRequestDTO.To}...");
            await client.SendMailAsync(message, cancellationToken);
        }

        public async Task VerifyEmailAsync(VerifyEmailRequest emailRequestDTO, CancellationToken cancellationToken = default)
        {
            var user = await _context.Set<User>()
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == emailRequestDTO.Token, cancellationToken: cancellationToken);

            if (user == null)
                throw new Exception("Invalid token");

            // 🔥 EXPIRY CHECK HAPPENS HERE
            if (user.TokenExpiry < DateTime.UtcNow)
                throw new Exception("Token has expired");

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.TokenExpiry = null;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}