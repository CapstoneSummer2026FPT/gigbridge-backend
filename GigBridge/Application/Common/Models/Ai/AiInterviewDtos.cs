using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public class AiInterviewStartRequestDto
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = null!;

    [JsonPropertyName("freelancer_id")]
    public string FreelancerId { get; set; } = null!;

    [JsonPropertyName("job_title")]
    public string JobTitle { get; set; } = null!;

    [JsonPropertyName("job_description")]
    public string? JobDescription { get; set; }

    [JsonPropertyName("job_skills")]
    public List<string> JobSkills { get; set; } = new();

    [JsonPropertyName("job_phonetic_aliases")]
    public Dictionary<string, List<string>> JobPhoneticAliases { get; set; } = new();

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "voice";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "auto";

    [JsonPropertyName("question_count")]
    public int? QuestionCount { get; set; }

    [JsonPropertyName("definition_reference")]
    public string? DefinitionReference { get; set; }
}

public class AiInterviewDefinitionRequestDto
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = null!;

    [JsonPropertyName("job_title")]
    public string JobTitle { get; set; } = null!;

    [JsonPropertyName("job_description")]
    public string? JobDescription { get; set; }

    [JsonPropertyName("job_skills")]
    public List<string> JobSkills { get; set; } = new();

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "voice";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "auto";

    [JsonPropertyName("question_count")]
    public int QuestionCount { get; set; } = 5;
}

public class AiInterviewDefinitionResponseDto
{
    [JsonPropertyName("definition_reference")]
    public string DefinitionReference { get; set; } = null!;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "voice";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "auto";

    [JsonPropertyName("question_count")]
    public int QuestionCount { get; set; }
}

public class AiInterviewConfirmRequestDto
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = null!;

    [JsonPropertyName("corrected_text")]
    public string? CorrectedText { get; set; }
}

public class AiInterviewQuestionResponseDto
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = null!;

    [JsonPropertyName("audio_access_token")]
    public string? AudioAccessToken { get; set; }

    [JsonPropertyName("question_index")]
    public int QuestionIndex { get; set; }

    [JsonPropertyName("question_text")]
    public string? QuestionText { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("audio_base64")]
    public string? AudioBase64 { get; set; }

    [JsonPropertyName("audio_mime_type")]
    public string? AudioMimeType { get; set; }

    [JsonPropertyName("tts_provider")]
    public string? TtsProvider { get; set; }

    [JsonPropertyName("fallback_used")]
    public bool FallbackUsed { get; set; }

    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("feedback")]
    public AiInterviewFeedbackDto? Feedback { get; set; }
}

public class AiInterviewFeedbackDto
{
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("technical_skills")]
    public List<string> TechnicalSkills { get; set; } = new();

    [JsonPropertyName("soft_skills")]
    public List<string> SoftSkills { get; set; } = new();

    [JsonPropertyName("recommended_hire")]
    public bool RecommendedHire { get; set; }
}

public class AiInterviewDraftResponseDto
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = null!;

    [JsonPropertyName("draft_id")]
    public string DraftId { get; set; } = null!;

    [JsonPropertyName("question_index")]
    public int QuestionIndex { get; set; }

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "auto";

    [JsonPropertyName("stt_provider")]
    public string SttProvider { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("fallback_used")]
    public bool FallbackUsed { get; set; }

    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; set; } = string.Empty;
}

public class AiInterviewQuestionAudioResponseDto
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = null!;

    [JsonPropertyName("question_index")]
    public int QuestionIndex { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("audio_base64")]
    public string? AudioBase64 { get; set; }

    [JsonPropertyName("audio_mime_type")]
    public string? AudioMimeType { get; set; }

    [JsonPropertyName("tts_provider")]
    public string? TtsProvider { get; set; }

    [JsonPropertyName("fallback_used")]
    public bool FallbackUsed { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class AiInterviewAudioStreamDto
{
    public Stream AudioStream { get; init; } = Stream.Null;
    public string ContentType { get; init; } = "audio/mpeg";
}
