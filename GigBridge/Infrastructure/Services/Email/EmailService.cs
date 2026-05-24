using Application.Common.Interfaces.IRepository;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.DTOs;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public EmailService(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        public async Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default)
        {
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
        }

        public async Task VerifyEmailAsync(VerifyEmailRequest emailRequestDTO, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.UserRepository
                .GetAsync(u => u.EmailVerificationToken == emailRequestDTO.Token);

            if (user == null)
                throw new Exception("Invalid token");

            // 🔥 EXPIRY CHECK HAPPENS HERE
            if (user.TokenExpiry < DateTime.UtcNow)
                throw new Exception("Token has expired");

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.TokenExpiry = null;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}