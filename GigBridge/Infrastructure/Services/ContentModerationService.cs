using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Application.Common.Interfaces.IService;

namespace Infrastructure.Services;

/// <summary>
/// Rule-based first-layer moderation for JobPost content. This is not a complete legal classifier.
/// </summary>
public class ContentModerationService : IContentModerationService
{
    private const int BlockThreshold = 100;
    private const int HardIllegalScore = 100;
    private const int StrongSuspiciousScore = 60;
    private const int MediumRiskScore = 30;
    private const int OffPlatformScore = 20;

    private static readonly Regex SeparatorRegex = new(@"[.\-_/,\\]+", RegexOptions.Compiled);
    private static readonly Regex PunctuationRegex = new(@"[^\p{L}\p{Nd}\s]", RegexOptions.Compiled);
    private static readonly Regex SpacesRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly IReadOnlyList<ModerationCategory> Categories =
    [
        new(
            Name: "Illegal drugs / narcotics",
            Violation: "Job post appears to request or promote illegal drug-related work.",
            HardBlock: true,
            HardPhrases:
            [
                "ma tuy", "can sa", "heroin", "cocain", "cocaine", "ma tuy da", "ketamine",
                "thuoc lac", "bong cuoi", "hang trang", "buon ma tuy", "van chuyen ma tuy",
                "giao ma tuy", "drug trafficking", "narcotics", "weed delivery", "meth",
                "ecstasy", "illegal drugs"
            ]),

        new(
            Name: "Gambling / betting / casino",
            Violation: "Job post appears to contain gambling or betting-related work.",
            HardBlock: true,
            HardPhrases:
            [
                "ca do", "co bac", "danh bac", "casino", "nha cai", "keo bong da", "lo de",
                "so de", "tai xiu", "xoc dia", "game bai doi thuong", "gambling", "betting",
                "sportsbook", "lottery", "lottery scam", "online betting", "bookmaker"
            ]),

        new(
            Name: "Prostitution / sexual services / adult escort",
            Violation: "Job post appears to contain adult sexual service content.",
            HardBlock: true,
            HardPhrases:
            [
                "mai dam", "gai goi", "trai bao", "massage kich duc", "dich vu tinh duc",
                "tiep khach nguoi lon", "escort", "prostitution", "escort service",
                "sexual service", "adult service", "erotic massage", "sex work"
            ]),

        new(
            Name: "Human trafficking / forced labor / exploitative recruitment",
            Violation: "Job post appears to contain human trafficking, forced labor, or exploitative recruitment content.",
            HardBlock: true,
            HardPhrases:
            [
                "moi gioi trai phep", "dua nguoi qua bien gioi", "giu giay to", "giu ho chieu",
                "lam viec tra no", "viec nhe luong cao khong can giay to", "tuyen nguoi di campuchia",
                "viec lam campuchia luong cao", "human trafficking", "forced labor", "debt bondage",
                "confiscate passport", "recruitment fee", "no documents needed", "border crossing job"
            ]),

        new(
            Name: "Weapons / explosives / violence",
            Violation: "Job post appears to request or promote weapons, explosives, or violent illegal activity.",
            HardBlock: true,
            HardPhrases:
            [
                "vu khi", "sung", "dan duoc", "bom", "thuoc no", "dao kiem", "che tao sung",
                "buon sung", "vat lieu no", "weapon", "firearm", "ammunition", "bomb",
                "explosive", "grenade", "gun manufacturing", "illegal weapon", "buy gun", "sell gun"
            ]),

        new(
            Name: "Fraud / scam / phishing / identity theft",
            Violation: "Job post appears to contain fraud, phishing, or identity theft-related work.",
            HardBlock: true,
            HardPhrases:
            [
                "lua dao", "scam", "fraud", "gia mao", "danh cap tai khoan", "lay otp",
                "lay mat khau", "lay thong tin ngan hang", "phishing", "chiem doat",
                "hack facebook", "hack zalo", "identity theft", "steal account", "steal password",
                "steal otp", "bank info", "carding"
            ]),

        new(
            Name: "Money laundering / mule account / suspicious payment transfer",
            Violation: "Job post appears to contain money laundering or suspicious payment transfer activity.",
            HardBlock: true,
            HardPhrases:
            [
                "rua tien", "tai khoan mule", "cho thue tai khoan", "nhan tien ho", "chuyen tien ho",
                "rut tien ho", "mo tai khoan ngan hang ho", "trung gian nhan tien",
                "money laundering", "mule account", "rent bank account", "receive money for someone",
                "transfer money for someone", "cash out"
            ]),

        new(
            Name: "Fake documents / fake certificates",
            Violation: "Job post appears to request or promote fake documents, certificates, or IDs.",
            HardBlock: true,
            HardPhrases:
            [
                "lam bang gia", "giay to gia", "cmnd gia", "cccd gia", "ho chieu gia",
                "bang dai hoc gia", "chung chi gia", "fake id", "fake passport",
                "fake certificate", "fake diploma", "forged document", "fake documents"
            ]),

        new(
            Name: "Cybercrime / malware / hacking",
            Violation: "Job post appears to contain cybercrime, malware, hacking, or credential theft-related work.",
            HardBlock: true,
            HardPhrases:
            [
                "viet malware", "malware", "virus", "ddos", "tan cong mang", "hack tai khoan",
                "crack phan mem", "keylogger", "botnet", "ransomware", "credential theft",
                "account hacking", "hack account", "software cracking"
            ]),

        new(
            Name: "Bribery / corruption / tax evasion",
            Violation: "Job post appears to contain bribery, corruption, or tax evasion-related work.",
            HardBlock: true,
            HardPhrases:
            [
                    "hoi lo", "dua hoi lo", "nhan hoi lo", "tron thue", "lam gia hoa don",
                    "hoa don khong", "bribery", "corruption", "tax evasion", "fake invoice"
            ]),

        new(
            Name: "Counterfeit / piracy",
            Violation: "Job post appears to request or promote counterfeit goods, piracy, or illegal streaming.",
            HardBlock: true,
            HardPhrases:
            [
                "hang gia", "hang nhai", "fake brand", "ban key crack", "phim lau",
                "web phim lau", "phan mem crack", "counterfeit", "pirated software",
                "cracked software", "illegal streaming", "warez", "keygen"
            ]),

        new(
            Name: "Illegal debt collection / threats",
            Violation: "Job post appears to contain illegal debt collection, threats, or intimidation.",
            HardBlock: true,
            HardPhrases:
            [
                "doi no thue", "de doa", "khung bo", "uy hiep", "xu ly con no", "dan mat",
                "illegal debt collection", "threaten debtor", "intimidation", "hired intimidation"
            ]),

        new(
            Name: "Suspicious illegal recruitment patterns",
            Violation: "Job post appears to contain suspicious recruitment or platform-bypass activity.",
            HardBlock: false,
            HardPhrases: [],
            StrongPhrases:
            [
                "viec nhe luong cao", "easy job high salary", "no experience high pay",
                "quick cash", "keep passport", "no contract", "khong can hop dong",
                "luong cao bat thuong", "urgent overseas job"
            ],
            MediumPhrases:
            [
                "khong can kinh nghiem", "nhan tien nhanh", "bao an o", "di lam gap",
                "khong can giay to", "no experience", "high pay", "high salary"
            ],
            OffPlatformPhrases:
            [
                "ngoai san", "off platform", "outside platform", "khong qua nen tang",
                "khong qua san", "khong qua he thong", "bypass platform rules",
                "avoid platform fee", "tranh phi", "ne phi", "telegram", "whatsapp",
                "zalo", "crypto payment", "thanh toan rieng", "chuyen khoan rieng"
            ])
    ];

    public ContentModerationResult ValidateJobPostContent(string? title, string? description)
    {
        var combinedText = $"{title} {description}";
        var normalized = Normalize(combinedText);
        var result = new ContentModerationResult { IsAllowed = true };

        if (string.IsNullOrWhiteSpace(normalized.NormalizedText))
        {
            return result;
        }

        var hardBlockDetected = false;

        foreach (var category in Categories)
        {
            var hardMatches = CountMatches(category.HardPhrases, normalized);
            var strongMatches = CountMatches(category.StrongPhrases, normalized);
            var mediumMatches = CountMatches(category.MediumPhrases, normalized);
            var offPlatformMatches = CountMatches(category.OffPlatformPhrases, normalized);

            var categoryScore =
                hardMatches * HardIllegalScore +
                strongMatches * StrongSuspiciousScore +
                mediumMatches * MediumRiskScore +
                offPlatformMatches * OffPlatformScore;

            if (categoryScore == 0)
            {
                continue;
            }

            result.RiskScore += categoryScore;
            result.MatchedCategories.Add(category.Name);
            result.Violations.Add(category.Violation);

            if (category.HardBlock && hardMatches > 0)
            {
                hardBlockDetected = true;
            }
        }

        result.MatchedCategories = result.MatchedCategories.Distinct().ToList();
        result.Violations = result.Violations.Distinct().ToList();
        result.IsAllowed = !hardBlockDetected && result.RiskScore < BlockThreshold;

        return result;
    }

    private static int CountMatches(IReadOnlyCollection<string> phrases, NormalizedContent normalized)
    {
        return phrases.Count(phrase => ContainsPhrase(normalized, phrase));
    }

    private static bool ContainsPhrase(NormalizedContent normalized, string phrase)
    {
        var normalizedPhrase = Normalize(phrase);

        if (string.IsNullOrWhiteSpace(normalizedPhrase.NormalizedText))
        {
            return false;
        }

        var paddedText = $" {normalized.NormalizedText} ";
        var paddedPhrase = $" {normalizedPhrase.NormalizedText} ";

        if (paddedText.Contains(paddedPhrase, StringComparison.Ordinal))
        {
            return true;
        }

        // Compact matching catches simple obfuscation like "m.a-t_u/y" without fuzzy matching.
        return normalizedPhrase.CompactText.Length >= 4 &&
               normalized.CompactText.Contains(normalizedPhrase.CompactText, StringComparison.Ordinal);
    }

    private static NormalizedContent Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new NormalizedContent(string.Empty, string.Empty);
        }

        var lowerText = text.ToLowerInvariant();
        var leetspeakNormalized = ReplaceLeetspeak(lowerText);
        var diacriticsRemoved = RemoveDiacritics(leetspeakNormalized).Replace('\u0111', 'd');
        var separatorsReplaced = SeparatorRegex.Replace(diacriticsRemoved, " ");
        var punctuationRemoved = PunctuationRegex.Replace(separatorsReplaced, " ");
        var normalizedText = SpacesRegex.Replace(punctuationRemoved, " ").Trim();
        var compactText = normalizedText.Replace(" ", string.Empty);

        return new NormalizedContent(normalizedText, compactText);
    }

    private static string ReplaceLeetspeak(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            builder.Append(character switch
            {
                '0' => 'o',
                '1' => 'i',
                '3' => 'e',
                '4' => 'a',
                '5' => 's',
                '7' => 't',
                '@' => 'a',
                '$' => 's',
                _ => character
            });
        }

        return builder.ToString();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed class ModerationCategory
    {
        public ModerationCategory(
            string Name,
            string Violation,
            bool HardBlock,
            IReadOnlyCollection<string> HardPhrases,
            IReadOnlyCollection<string>? StrongPhrases = null,
            IReadOnlyCollection<string>? MediumPhrases = null,
            IReadOnlyCollection<string>? OffPlatformPhrases = null)
        {
            this.Name = Name;
            this.Violation = Violation;
            this.HardBlock = HardBlock;
            this.HardPhrases = HardPhrases;
            this.StrongPhrases = StrongPhrases ?? [];
            this.MediumPhrases = MediumPhrases ?? [];
            this.OffPlatformPhrases = OffPlatformPhrases ?? [];
        }

        public string Name { get; }

        public string Violation { get; }

        public bool HardBlock { get; }

        public IReadOnlyCollection<string> HardPhrases { get; }

        public IReadOnlyCollection<string> StrongPhrases { get; }

        public IReadOnlyCollection<string> MediumPhrases { get; }

        public IReadOnlyCollection<string> OffPlatformPhrases { get; }
    }

    private sealed record NormalizedContent(string NormalizedText, string CompactText);
}
