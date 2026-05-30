namespace Application.Features.Profile.UpdateFreelancerProfile.DTOs
{
    public class UpdateFreelancerProfileDto
    {
        public string Title { get; set; } = null!;
        public string Bio { get; set; } = null!;
        public decimal HourlyRate { get; set; }
        public int ExperienceLevel { get; set; }
        public int Availability { get; set; }
        public string Location { get; set; } = null!;
    }
}
