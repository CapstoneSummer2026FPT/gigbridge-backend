using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class DbSeeder
{
    private static readonly MethodInfo DbContextSetMethod = typeof(DbContext)
        .GetMethods()
        .Single(method =>
            method.Name == nameof(DbContext.Set)
            && method.IsGenericMethodDefinition
            && method.GetParameters().Length == 0);

    private static readonly MethodInfo AnyAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
        .GetMethods()
        .Single(method =>
        {
            if (method.Name != nameof(EntityFrameworkQueryableExtensions.AnyAsync)
                || !method.IsGenericMethodDefinition)
            {
                return false;
            }

            var parameters = method.GetParameters();

            return parameters.Length == 2
                   && parameters[0].ParameterType.IsGenericType
                   && parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>)
                   && parameters[1].ParameterType == typeof(CancellationToken);
        });

    public static async Task SeedLocalOnlyAsync<TContext>(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        using var scope = services.CreateScope();

        var environment = scope.ServiceProvider.GetService<IHostEnvironment>();

        if (environment is not null && !IsLocalLikeEnvironment(environment.EnvironmentName))
        {
            return;
        }

        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        await SeedAsync(context, cancellationToken);
    }

    public static async Task SeedAsync(
        DbContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = context.Database.GetConnectionString() ?? string.Empty;

        if (LooksLikeSupabase(connectionString))
        {
            throw new InvalidOperationException(
                "DbSeeder refused to run because the current connection string looks like Supabase. Use a local database connection string only.");
        }

        var now = DateTime.UtcNow;
        var tables = BuildSeedData(now);

        foreach (var table in tables)
        {
            var entityType = FindEntityType(context, table.TableName);

            if (await HasAnyRowsAsync(context, entityType.ClrType, cancellationToken))
            {
                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(
                entityType.GetTableName()!,
                entityType.GetSchema());

            foreach (var row in table.Rows)
            {
                var entity = Activator.CreateInstance(entityType.ClrType)
                             ?? throw new InvalidOperationException(
                                 $"Cannot create entity instance for table '{table.TableName}'.");

                var entry = context.Entry(entity);

                for (var columnIndex = 0; columnIndex < table.Columns.Length; columnIndex++)
                {
                    var columnName = table.Columns[columnIndex];
                    var property = FindProperty(entityType, storeObject, columnName);
                    var value = ConvertValue(row[columnIndex], property.ClrType);

                    entry.Property(property.Name).CurrentValue = value;
                }

                entry.State = EntityState.Added;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsLocalLikeEnvironment(string environmentName)
    {
        return string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
               || string.Equals(environmentName, "Local", StringComparison.OrdinalIgnoreCase)
               || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
               || string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeSupabase(string connectionString)
    {
        return connectionString.Contains("supabase", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains("pooler.supabase", StringComparison.OrdinalIgnoreCase);
    }

    private static IEntityType FindEntityType(DbContext context, string tableName)
    {
        var entityType = context.Model
            .GetEntityTypes()
            .FirstOrDefault(type =>
                string.Equals(type.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase));

        return entityType
               ?? throw new InvalidOperationException(
                   $"No EF Core entity mapping was found for table '{tableName}'. Check your entity ToTable mapping or DbSet configuration.");
    }

    private static IProperty FindProperty(
        IEntityType entityType,
        StoreObjectIdentifier storeObject,
        string columnName)
    {
        var property = entityType
            .GetProperties()
            .FirstOrDefault(property =>
                string.Equals(property.GetColumnName(storeObject), columnName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(property.Name, columnName, StringComparison.OrdinalIgnoreCase));

        return property
               ?? throw new InvalidOperationException(
                   $"No EF Core property mapping was found for column '{columnName}' on entity '{entityType.ClrType.Name}'.");
    }

    private static async Task<bool> HasAnyRowsAsync(
        DbContext context,
        Type clrType,
        CancellationToken cancellationToken)
    {
        var set = (IQueryable)(DbContextSetMethod.MakeGenericMethod(clrType).Invoke(context, null)
                  ?? throw new InvalidOperationException($"Cannot create DbSet for '{clrType.Name}'."));

        var task = (Task<bool>)(AnyAsyncMethod.MakeGenericMethod(clrType).Invoke(
            null,
            new object?[] { set, cancellationToken }) ?? throw new InvalidOperationException("Cannot invoke AnyAsync."));

        return await task;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (nonNullableType.IsInstanceOfType(value))
        {
            return value;
        }

        if (nonNullableType.IsEnum)
        {
            return value is string text
                ? Enum.Parse(nonNullableType, text, ignoreCase: true)
                : Enum.ToObject(nonNullableType, value);
        }

        if (nonNullableType == typeof(Guid))
        {
            return value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        }

        if (nonNullableType == typeof(DateTime))
        {
            if (value is DateTime dateTime)
            {
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.UtcDateTime;
            }
        }

        if (nonNullableType == typeof(DateTimeOffset))
        {
            if (value is DateTime dateTime)
            {
                return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset;
            }
        }

        if (nonNullableType == typeof(JsonDocument) && value is string jsonDocumentText)
        {
            return JsonDocument.Parse(jsonDocumentText);
        }

        if (nonNullableType == typeof(JsonElement) && value is string jsonElementText)
        {
            using var jsonDocument = JsonDocument.Parse(jsonElementText);
            return jsonDocument.RootElement.Clone();
        }

        if (nonNullableType != typeof(string)
            && value is string jsonText
            && (jsonText.StartsWith("[", StringComparison.Ordinal) || jsonText.StartsWith("{", StringComparison.Ordinal)))
        {
            return JsonSerializer.Deserialize(jsonText, nonNullableType);
        }

        if (nonNullableType == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(value, nonNullableType, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<SeedTableData> BuildSeedData(DateTime now)
    {
        return new List<SeedTableData>
        {
            new SeedTableData(
                "PlatformSettings",
                new[] { "PlatformSettingsId", "Key", "Value", "Description", "DataType" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "min_withdrawal_amount", "50000", "Số tiền tối thiểu được phép rút (VND)", "integer" },
                new object?[] { Guid.NewGuid(), "platform_commission_percentage", "10", "Phần trăm hoa hồng nền tảng giữ lại từ hợp đồng", "integer" },
                new object?[] { Guid.NewGuid(), "allow_ai_interviews", "true", "Cho phép tính năng phỏng vấn thử bằng AI", "boolean" },
                new object?[] { Guid.NewGuid(), "maintenance_mode", "false", "Chế độ bảo trì hệ thống", "boolean" },
                new object?[] { Guid.NewGuid(), "contact_email", "support@gigbridge.vn", "Email hỗ trợ khách hàng", "string" }
                }),

            new SeedTableData(
                "FAQCategories",
                new[] { "FAQCategoriesId", "Name", "NameVi", "Slug", "SortOrder", "IsActive", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380001", "General Questions", "Câu hỏi chung", "general", 1, true, now },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380002", "For Clients", "Dành cho khách hàng", "clients", 2, true, now },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380003", "For Freelancers", "Dành cho freelancer", "freelancers", 3, true, now }
                }),

            new SeedTableData(
                "FAQs",
                new[] { "FAQsId", "FAQCategoriesId", "Question", "QuestionVi", "Answer", "AnswerVi", "SortOrder", "IsActive", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380111", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380001", "What is GigBridge?", "GigBridge là gì?", "GigBridge is a freelance platform connecting enterprises with talented freelancers in Vietnam.", "GigBridge là nền tảng kết nối các doanh nghiệp và cá nhân tuyển dụng với những lập trình viên và nhà thiết kế tài năng tại Việt Nam.", 1, true, now },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380112", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380002", "How do I post a job?", "Làm thế nào để đăng tin tuyển dụng?", "Log in to your account, navigate to \"My Jobs\", and click \"Create New Job Post\". Fill in details and publish.", "Đăng nhập tài khoản Khách hàng, vào mục \"Công việc của tôi\" và chọn \"Đăng tin mới\". Điền thông tin mô tả, ngân sách và đăng tải.", 1, true, now },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380113", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380003", "When do I get paid?", "Khi nào tôi nhận được tiền?", "Payments are held securely in milestones. Once the client approves your work, money is released to your balance.", "Tiền thanh toán được giữ an toàn trong hệ thống Milestone. Khi khách hàng phê duyệt sản phẩm bạn bàn giao, tiền sẽ được mở khóa vào ví của bạn.", 1, true, now }
                }),

            new SeedTableData(
                "Categories",
                new[] { "CategoriesId", "Name", "NameVi", "Slug", "Description", "ParentCategoryId", "IsActive", "SortOrder", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d01", "Software Development", "Lập trình & Công nghệ", "software-development", null, null, true, 1, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d05", "Design & Creative", "Thiết kế & Sáng tạo", "design-creative", null, null, true, 2, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d08", "Writing & Translation", "Viết lách & Dịch thuật", "writing-translation", null, null, true, 3, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d20", "AI & Machine Learning", "Trí tuệ nhân tạo & Máy học", "ai-machine-learning", null, null, true, 4, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d30", "Blockchain & Web3", "Mạng lưới Blockchain & Web3", "blockchain-web3", null, null, true, 5, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", "Web Development", "Phát triển Web", "web-development", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d01", true, 1, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d03", "Mobile App Development", "Phát triển Ứng dụng Di động", "mobile-development", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d01", true, 2, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d04", "Database Administration", "Quản trị Cơ sở Dữ liệu", "database-administration", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d01", true, 3, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d06", "UI/UX Design", "Thiết kế giao diện UI/UX", "ui-ux-design", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d05", true, 1, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d07", "Graphic Design", "Thiết kế đồ họa", "graphic-design", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d05", true, 2, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d09", "Content Writing", "Viết nội dung", "content-writing", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d08", true, 1, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d10", "Translation", "Dịch thuật", "translation", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d08", true, 2, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d21", "LLMs & NLP", "Mô hình Ngôn ngữ Lớn & Xử lý Ngôn ngữ", "llm-nlp", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d20", true, 1, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d22", "Data Science", "Khoa học Dữ liệu", "data-science", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d20", true, 2, now },
                new object?[] { "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d31", "Smart Contracts", "Hợp đồng thông minh & DApps", "smart-contracts", null, "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d30", true, 1, now }
                }),

            new SeedTableData(
                "Skills",
                new[] { "SkillsId", "CategoriesId", "Name", "NameVi", "IsActive", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e01", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", "React", "ReactJS", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e02", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", "Angular", "AngularJS", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e03", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", "Vue.js", "VueJS", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e04", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", "NodeJS", "NodeJS Backend", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e05", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", "ASP.NET Core", "C# ASP.NET Core", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e06", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", "Tailwind CSS", "Tailwind CSS Layout", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e07", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d03", "Flutter", "Lập trình Flutter", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e08", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d03", "React Native", "React Native App", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e09", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d03", "Swift", "iOS Swift Development", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e10", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d04", "PostgreSQL", "Tối ưu hóa PostgreSQL", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e11", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d04", "SQL Server", "Cơ sở dữ liệu SQL Server", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e12", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d04", "MongoDB", "NoSQL MongoDB", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e13", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d06", "Figma", "Thiết kế Figma", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e14", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d06", "Adobe XD", "Adobe XD Layout", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e15", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d07", "Photoshop", "Photoshop chỉnh sửa", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e16", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d07", "Illustrator", "Illustrator Vẽ vector", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e17", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d09", "SEO Copywriting", "Viết bài chuẩn SEO", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e18", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d09", "Creative Writing", "Viết sáng tạo content", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e19", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d10", "English-Vietnamese", "Dịch thuật Anh - Việt", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e20", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d10", "Japanese-Vietnamese", "Dịch thuật Nhật - Việt", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e31", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d21", "OpenAI API", "Tích hợp OpenAI API", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e32", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d21", "Prompt Engineering", "Kỹ nghệ Gợi ý (Prompt)", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e33", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d21", "LangChain", "Sử dụng LangChain", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e34", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d22", "Python", "Lập trình Python", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e36", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d31", "Solidity", "Smart Contract Solidity", true, now },
                new object?[] { "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e37", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d31", "Rust Web3", "Blockchain Rust", true, now }
                }),

            new SeedTableData(
                "SubscriptionPlans",
                new[] { "SubscriptionPlansId", "Name", "NameVi", "Description", "Price", "Currency", "DurationInDays", "Features", "TargetRole", "IsActive", "SortOrder", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", "Freelancer Basic", "Freelancer Cơ bản", "Free plan for new freelancers with basic features", 0.0m, "VND", 365, "[\"Nộp tối đa 10 báo cáo đề xuất/tháng\", \"Hỗ trợ chat trực tiếp\", \"Hồ sơ hiển thị tiêu chuẩn\"]", 1, true, 0, now },
                new object?[] { "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b02", "Freelancer Pro", "Freelancer Chuyên nghiệp", "Pro plan for advanced freelancers with premium features", 150000.0m, "VND", 30, "[\"Không giới hạn số lượng đề xuất\", \"Huy hiệu Pro trên hồ sơ\", \"Ưu tiên hiển thị trên bảng tìm kiếm\", \"Xem phân tích thị trường\"]", 1, true, 0, now },
                new object?[] { "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b03", "Client Basic", "Doanh nghiệp Cơ bản", "Free plan for clients posting limited jobs", 0.0m, "VND", 365, "[\"Đăng tối đa 3 tin tuyển dụng/tháng\", \"Quản lý hợp đồng cơ bản\", \"Hỗ trợ qua email\"]", 0, true, 0, now },
                new object?[] { "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b04", "Client Premium", "Doanh nghiệp Cao cấp", "Premium plan for active hiring companies", 500000.0m, "VND", 30, "[\"Đăng không giới hạn tin tuyển dụng\", \"Lọc hồ sơ nâng cao bằng AI\", \"Được đề xuất các freelancer xuất sắc nhất\", \"Hỗ trợ 24/7\"]", 0, true, 0, now }
                }),

            new SeedTableData(
                "Users",
                new[] { "UserId", "FullName", "Email", "Password", "Avatar", "PhoneNumber", "Role", "IsEmailVerified", "IsActive", "PreferredLanguage", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11", "Nguyễn Văn Admin", "admin@gigbridge.vn", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/adventurer/svg?seed=admin1", "0911222333", 2, true, true, "vi", now.AddDays(-30) },
                new object?[] { "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a12", "Trần Thị Admin", "admin2@gigbridge.vn", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/adventurer/svg?seed=admin2", "0922333444", 2, true, true, "vi", now.AddDays(-30) },
                new object?[] { "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "Lê Quang Huy", "huy.le@fpt.com.vn", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/avataaars/svg?seed=client0", "0934567890", 0, true, true, "vi", now.AddDays(-25) },
                new object?[] { "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c12", "Nguyễn Hoàng Nam", "nam.nguyen@vng.com.vn", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/avataaars/svg?seed=client1", "0934567891", 0, true, true, "vi", now.AddDays(-25) },
                new object?[] { "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c13", "Phạm Minh Tuấn", "tuan.pham@geminishop.vn", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/avataaars/svg?seed=client2", "0934567892", 0, true, true, "vi", now.AddDays(-25) },
                new object?[] { "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", "Trần Thu Trang", "trang.tran@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/avataaars/svg?seed=client3", "0934567893", 0, true, true, "vi", now.AddDays(-25) },
                new object?[] { "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "Hoàng Đức Minh", "minh.hoang@startupx.vn", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/avataaars/svg?seed=client4", "0934567894", 0, true, true, "vi", now.AddDays(-25) },
                new object?[] { "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c16", "Bùi Anh Tuấn", "tuan.bui@blockchainlabs.vn", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/avataaars/svg?seed=client5", "0934567895", 0, true, true, "vi", now.AddDays(-25) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "Nguyễn Minh Trí", "tri.nguyen@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free0", "0987654320", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f12", "Trần Quốc Bảo", "bao.tran@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free1", "0987654321", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f13", "Lê Thị Hoa", "hoa.le@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free2", "0987654322", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f14", "Phạm Thanh Sơn", "son.pham@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free3", "0987654323", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "Vũ Hoàng Anh", "anh.vu@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free4", "0987654324", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "Đặng Minh Quân", "quan.dang@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free5", "0987654325", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f17", "Đỗ Quốc Khánh", "khanh.do@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free6", "0987654326", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f18", "Bùi Thị Mai", "mai.bui@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free7", "0987654327", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", "Hoàng Văn Dũng", "dung.hoang@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free8", "0987654328", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f20", "Nguyễn Hà Linh", "linh.nguyen@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free9", "0987654329", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "Trịnh Quốc Hùng", "hung.trinh@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free10", "0987654330", 1, true, true, "vi", now.AddDays(-20) },
                new object?[] { "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "Ngô Phương Thảo", "thao.ngo@gmail.com", "$2a$12$R9h/lIPzMRgFXhY1t4wXoe9FmZ/P0tN7qD7b9jQ/JjZ2wYtGv6g2O", "https://api.dicebear.com/7.x/bottts/svg?seed=free11", "0987654331", 1, true, true, "vi", now.AddDays(-20) }
                }),

            new SeedTableData(
                "ClientProfiles",
                new[] { "ClientProfilesId", "UserId", "CompanyName", "CompanyWebsite", "CompanySize", "Industry", "CompanyDescription", "Location", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "FPT Software", "https://fptsoftware.com", 3, "IT & Software", "Tập đoàn công nghệ hàng đầu Việt Nam cung cấp dịch vụ xuất khẩu phần mềm.", "Hà Nội, Việt Nam", now.AddDays(-25) },
                new object?[] { "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c12", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c12", "VNG Corporation", "https://vng.com.vn", 3, "Internet & Technology", "Công ty công nghệ internet hàng đầu Việt Nam nổi tiếng với Zalo và game phát hành.", "TP. Hồ Chí Minh, Việt Nam", now.AddDays(-25) },
                new object?[] { "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c13", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c13", "Gemini Shop", "https://geminishop.vn", 1, "Fashion & Retail", "Shop thời trang thiết kế trẻ trung dành cho giới trẻ.", "Đà Nẵng, Việt Nam", now.AddDays(-25) },
                new object?[] { "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", null, null, 0, "Personal Project", "Dự án cá nhân tìm kiếm cộng tác viên viết lách.", "Hà Nội, Việt Nam", now.AddDays(-25) },
                new object?[] { "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "Startup X", "https://startupx.vn", 1, "E-Commerce", "Công ty khởi nghiệp đang phát triển giải pháp thương mại điện tử thế hệ mới.", "TP. Hồ Chí Minh, Việt Nam", now.AddDays(-25) },
                new object?[] { "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c16", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c16", "Blockchain Labs", "https://blockchainlabs.vn", 1, "Blockchain & Web3", "Trung tâm nghiên cứu phát triển các giải pháp hợp đồng thông minh cho doanh nghiệp.", "TP. Hồ Chí Minh, Việt Nam", now.AddDays(-25) }
                }),

            new SeedTableData(
                "FreelancerProfiles",
                new[] { "FreelancerProfilesId", "UserId", "Title", "Bio", "HourlyRate", "ExperienceLevel", "Availability", "Location", "ProfileCompletionScore", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "Senior Web Developer", "Kỹ sư lập trình Web chuyên nghiệp với 5 năm kinh nghiệm về React, Node.js và PostgreSQL. Đã làm nhiều hệ thống SaaS lớn.", 300000.0m, 2, 0, "Hà Nội, Việt Nam", 95, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f12", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f12", "Mobile App Developer (Flutter)", "Lập trình viên Flutter 3 năm kinh nghiệm phát triển ứng dụng trên iOS và Android. Tối ưu hóa UI/UX mượt mà.", 250000.0m, 1, 0, "TP. Hồ Chí Minh, Việt Nam", 90, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f13", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f13", "UI/UX Designer", "Thiết kế giao diện hiện đại, tối giản trên Figma và Adobe Creative Suite. Chú trọng trải nghiệm người dùng cuối.", 200000.0m, 1, 1, "Đà Nẵng, Việt Nam", 85, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f14", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f14", "Backend ASP.NET Core Developer", "Lập trình viên Backend chuyên sâu C#, ASP.NET Core, SQL Server, Docker. Thiết kế API chuẩn RESTful, an toàn bảo mật.", 280000.0m, 2, 0, "Hà Nội, Việt Nam", 92, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "Content Writer & SEO Specialist", "Sáng tạo nội dung số, viết bài chuẩn SEO cho website, quản lý fanpage, dịch thuật chuyên nghiệp Anh-Việt.", 120000.0m, 1, 1, "Hải Phòng, Việt Nam", 80, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "Database Administrator & DevOps", "Quản trị cơ sở dữ liệu PostgreSQL chuyên nghiệp, tối ưu query, thiết lập CI/CD, AWS Cloud Architect.", 350000.0m, 2, 1, "TP. Hồ Chí Minh, Việt Nam", 90, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f17", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f17", "Frontend React Developer", "Cựu sinh viên FPT đam mê Frontend ReactJS, HTML5, CSS3, Tailwind CSS. Mong muốn học hỏi và phát triển.", 100000.0m, 0, 0, "Đà Nẵng, Việt Nam", 75, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f18", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f18", "Bilingual Translator (English - Vietnamese)", "Biên dịch viên Anh-Việt, Việt-Anh tự do. 4 năm kinh nghiệm dịch thuật sách, tài liệu kỹ thuật công nghệ thông tin.", 150000.0m, 1, 1, "Cần Thơ, Việt Nam", 88, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", "Graphic Designer", "Nhận diện thương hiệu, thiết kế Logo, Banner quảng cáo chuyên nghiệp trên Illustrator và Photoshop.", 180000.0m, 1, 0, "Nha Trang, Việt Nam", 82, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f20", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f20", "Fullstack Developer (NodeJS & VueJS)", "Thiết kế và lập trình website trọn gói. Sử dụng thành thạo NestJS, Vue 3, Tailwind CSS và Docker.", 260000.0m, 1, 0, "Hà Nội, Việt Nam", 90, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "AI & Machine Learning Engineer", "Kỹ sư AI/ML chuyên nghiệp. Đam mê thiết kế các giải pháp RAG, Fine-tuning LLMs (GPT, Llama), kỹ nghệ Prompt và tích hợp API.", 400000.0m, 2, 0, "Hà Nội, Việt Nam", 95, now.AddDays(-20) },
                new object?[] { "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "Smart Contract & Blockchain Engineer", "Xây dựng và kiểm thử smart contracts an toàn trên Ethereum (Solidity) và Solana (Rust). Có kiến thức sâu về Web3.", 380000.0m, 2, 1, "TP. Hồ Chí Minh, Việt Nam", 93, now.AddDays(-20) }
                }),

            new SeedTableData(
                "Subscriptions",
                new[] { "SubscriptionsId", "UserId", "SubscriptionPlansId", "Status", "StartDate", "EndDate", "AutoRenew", "PaymentReference", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b04", 0, now.AddDays(-5), now.AddDays(25), true, "REF_PAY_CLIENT_HUY", now.AddDays(-5) },
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c12", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b04", 0, now.AddDays(-5), now.AddDays(25), true, "REF_PAY_CLIENT_NAM", now.AddDays(-5) },
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c13", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b03", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b03", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b03", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c16", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b04", 0, now.AddDays(-5), now.AddDays(25), true, "REF_PAY_CLIENT_TBUAN", now.AddDays(-5) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b02", 0, now.AddDays(-10), now.AddDays(20), true, "REF_PAY_FL_TRI", now.AddDays(-10) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f12", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f13", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f14", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b02", 0, now.AddDays(-10), now.AddDays(20), true, "REF_PAY_FL_SON", now.AddDays(-10) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f17", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f18", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f20", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b01", 0, now.AddDays(-15), now.AddDays(350), false, null, now.AddDays(-15) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b02", 0, now.AddDays(-10), now.AddDays(20), true, "REF_PAY_FL_HUNG", now.AddDays(-10) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b02", 0, now.AddDays(-10), now.AddDays(20), true, "REF_PAY_FL_THAO", now.AddDays(-10) }
                }),

            new SeedTableData(
                "FreelancerSkills",
                new[] { "FreelancerSkillsId", "FreelancerId", "SkillsId", "YearsOfExperience", "ProficiencyLevel" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e01", 5, 3 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e04", 4, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e10", 3, 3 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f12", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e07", 3, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f12", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e04", 2, 1 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f13", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e13", 4, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f13", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e14", 2, 1 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f14", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e05", 5, 3 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f14", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e11", 5, 3 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f14", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e10", 2, 1 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e17", 3, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e19", 4, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e10", 6, 3 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e04", 4, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f17", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e01", 1, 0 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f17", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e06", 1, 1 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f18", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e19", 5, 3 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f18", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e20", 3, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e15", 4, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e16", 4, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f20", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e03", 3, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f20", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e04", 3, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e34", 5, 3 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e31", 3, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e33", 2, 2 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e36", 4, 3 },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e37", 3, 2 }
                }),

            new SeedTableData(
                "WorkExperiences",
                new[] { "WorkExperiencesId", "FreelancerId", "CompanyName", "Title", "StartDate", "EndDate", "Description", "IsCurrentJob" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "TMA Solutions", "Software Engineer", "2021-06-01", "2023-12-31", "Lập trình ReactJS và NodeJS cho khách hàng châu Âu.", false },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "FPT Software", "Senior Web Developer", "2024-01-01", null, "Trưởng nhóm kỹ thuật lập trình hệ thống cổng thanh toán lớn.", true },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f12", "VNG Corporation", "Mobile Developer", "2022-03-15", null, "Xây dựng các ứng dụng phục vụ thanh toán điện tử bằng Flutter.", true },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f14", "NashTech", "Backend Developer", "2021-01-15", "2024-03-30", "Phát triển và bảo trì microservices bằng ASP.NET Core và Docker.", false },
                new object?[] { Guid.NewGuid(), "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "Viettel AI", "AI Researcher", "2023-01-10", "2025-02-28", "Nghiên cứu mô hình ngôn ngữ lớn tiếng Việt và phát triển công cụ tìm kiếm ngữ nghĩa.", false }
                }),

            new SeedTableData(
                "PortfolioItems",
                new[] { "PortfolioItemsId", "FreelancerId", "ProjectUrl" },
                new object?[][]
                {
                new object?[] { "80eebc99-9c0b-4ef8-bb6d-6bb9bd380801", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "https://github.com/tri-nguyen/e-commerce-nextjs" },
                new object?[] { "80eebc99-9c0b-4ef8-bb6d-6bb9bd380802", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f12", "https://github.com/bao-tran/flutter-chat-app" },
                new object?[] { "80eebc99-9c0b-4ef8-bb6d-6bb9bd380803", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f13", "https://behance.net/hoa-le/redesign-travel-app" },
                new object?[] { "80eebc99-9c0b-4ef8-bb6d-6bb9bd380804", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "https://github.com/hung-trinh/rag-pdf-chatbot" },
                new object?[] { "80eebc99-9c0b-4ef8-bb6d-6bb9bd380805", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "https://github.com/thao-solidity/nft-marketplace-contract" }
                }),

            new SeedTableData(
                "JobPosts",
                new[] { "JobPostsId", "ClientProfilesId", "Title", "Description", "CategoryId", "BudgetType", "BudgetMin", "BudgetMax", "Currency", "EstimatedDuration", "MaxHires", "ExperienceLevelRequired", "LocationType", "Location", "Status", "Visibility", "ApplicationDeadline", "IsAIGenerated", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380101", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "Tuyển lập trình viên ReactJS xây dựng giao diện Dashboard Admin", "FPT Software cần tìm 1 Freelancer ReactJS có kinh nghiệm thiết kế dashboard quản lý tài chính. Dự án kéo dài khoảng 1 tháng, yêu cầu code sạch, sử dụng Tailwind CSS và tối ưu hóa performance tốt.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", 0, 15000000.0m, 25000000.0m, "VND", "1 tháng", 1, 1, 0, null, 1, 0, "2026-06-30", false, now.AddDays(-15) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380102", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c12", "Cần tìm chuyên gia Flutter phát triển tính năng Audio Streaming", "Tìm lập trình viên Flutter có kinh nghiệm xử lý luồng audio, làm việc theo giờ. Phối hợp với team backend phát triển tính năng nghe nhạc trực tuyến chất lượng cao.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d03", 1, 250000.0m, 400000.0m, "VND", "2 tháng", 1, 2, 2, null, 1, 0, "2026-07-15", false, now.AddDays(-15) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380103", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c13", "Thiết kế bộ nhận diện thương hiệu cho Gemini Shop", "Yêu cầu thiết kế Logo mới, danh thiếp, túi giấy và bao bì cho shop thời trang thiết kế. Ưu tiên các freelancer có kinh nghiệm trong ngành thời trang.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d07", 0, 3000000.0m, 5000000.0m, "VND", "2 tuần", 1, 1, 0, null, 2, 0, "2026-05-15", false, now.AddDays(-15) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380104", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", "Viết 5 bài chuẩn SEO về chủ đề Chăm sóc da (Skincare)", "Cần freelancer viết 5 bài viết chuẩn SEO, độ dài khoảng 1200 từ/bài về quy trình chăm sóc da cho blog làm đẹp. Nội dung hấp dẫn, không copy-paste.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d09", 0, 1000000.0m, 3000000.0m, "VND", "1 tuần", 1, 1, 0, null, 3, 0, "2026-05-10", false, now.AddDays(-15) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380105", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "Tối ưu hóa hiệu năng PostgreSQL và cấu hình Master-Slave Replication", "Cơ sở dữ liệu của chúng tôi đang gặp hiện tượng chậm khi truy vấn đồng thời cao. Cần chuyên gia tối ưu chỉ mục (indexing), tối ưu query và thiết lập backup tự động, Master-Slave Replication trên AWS.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d04", 0, 8000000.0m, 15000000.0m, "VND", "10 ngày", 1, 2, 0, null, 2, 0, "2026-05-20", false, now.AddDays(-15) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380106", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "Tích hợp OpenAI Chatbot chăm sóc khách hàng vào ứng dụng Web", "Tích hợp mô hình GPT-4o vào website bán hàng, xây dựng kịch bản trả lời và thiết kế cơ chế RAG để truy xuất tài liệu sản phẩm nhằm phản hồi khách hàng tự động.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d21", 0, 20000000.0m, 40000000.0m, "VND", "3 tuần", 1, 2, 0, null, 1, 0, "2026-06-25", false, now.AddDays(-10) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380107", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c12", "Xây dựng luồng xử lý và phân tích dữ liệu lớn bằng Python", "Thu thập và làm sạch log hệ thống, xây dựng ETL pipeline bằng Python (Pandas/PySpark) để phân tích hành vi người dùng và trực quan hóa kết quả trực tiếp lên dashboard.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d22", 0, 15000000.0m, 30000000.0m, "VND", "1 tháng", 1, 1, 0, null, 1, 0, "2026-06-20", false, now.AddDays(-8) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380108", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c16", "Kiểm thử và Audit bảo mật Hợp đồng thông minh Solidity", "Thực hiện rà soát, đánh giá lỗ hổng bảo mật cho 3 hợp đồng thông minh Solidity (ERC-20, Staking, Governance). Phát hiện các rủi ro reentrancy, overflow, access control và viết báo cáo chi tiết.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d31", 0, 30000000.0m, 50000000.0m, "VND", "2 tuần", 1, 2, 0, null, 2, 0, "2026-06-10", false, now.AddDays(-6) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380109", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "Thiết kế giao diện UI/UX ứng dụng đặt lịch khám bệnh trên Figma", "Thiết kế wireframe, luồng người dùng (user flow) và bản thiết kế trực quan UI hoàn chỉnh khoảng 25 màn hình cho app khám bệnh trên thiết bị iOS/Android. Yêu cầu tính thẩm mỹ cao, phù hợp người lớn tuổi.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d06", 0, 10000000.0m, 20000000.0m, "VND", "3 tuần", 1, 1, 0, null, 1, 0, "2026-06-15", false, now.AddDays(-5) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380110", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "Dịch thuật tài liệu hướng dẫn kỹ thuật IT từ tiếng Anh sang tiếng Việt", "Cần dịch tài liệu đặc tả hệ thống và hướng dẫn vận hành AWS cloud khoảng 150 trang. Đảm bảo sử dụng đúng các thuật ngữ chuyên ngành IT/Cloud và dịch lưu loát.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d10", 0, 2000000.0m, 4000000.0m, "VND", "1 tuần", 1, 1, 0, null, 3, 0, "2026-05-12", false, now.AddDays(-20) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380111", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "Phát triển nền tảng học trực tuyến E-learning bằng Next.js", "Xây dựng website học trực tuyến hỗ trợ streaming video bài giảng, làm bài trắc nghiệm và quản lý khóa học. Frontend sử dụng Next.js (App Router), Backend tích hợp NestJS API.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d02", 0, 30000000.0m, 50000000.0m, "VND", "1.5 tháng", 1, 1, 0, null, 2, 0, "2026-06-18", false, now.AddDays(-9) },
                new object?[] { "111ebc99-9c0b-4ef8-bb6d-6bb9bd380112", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c13", "Xây dựng ứng dụng giao hàng nội bộ bằng React Native", "Shop cần app giao hàng riêng kết nối shipper với kho. Tuy nhiên do chuyển đổi mô hình kinh doanh nên tạm hoãn dự án này.", "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380d03", 0, 20000000.0m, 30000000.0m, "VND", "1 tháng", 1, 1, 0, null, 4, 0, "2026-05-18", false, now.AddDays(-18) }
                }),

            new SeedTableData(
                "JobPostSkills",
                new[] { "JobPostSkillsId", "JobPostsId", "SkillsId", "IsRequired" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380101", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e01", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380101", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e06", false },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380102", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e07", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380103", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e15", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380103", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e16", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380104", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e17", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380105", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e10", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380106", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e31", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380106", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e33", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380107", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e34", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380108", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e36", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380109", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e13", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380110", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e19", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380111", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e01", true },
                new object?[] { Guid.NewGuid(), "111ebc99-9c0b-4ef8-bb6d-6bb9bd380111", "d0eebc99-9c0b-4ef8-bb6d-6bb9bd380e04", false }
                }),

            new SeedTableData(
                "Proposals",
                new[] { "ProposalsId", "JobPostsId", "FreelancerProfilesId", "CoverLetter", "ProposedRate", "ProposedDuration", "Status", "SubmittedAt", "IsAIGenerated" },
                new object?[][]
                {
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380201", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380101", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "Xin chào FPT. Tôi có 5 năm kinh nghiệm về lập trình Frontend, từng xây dựng nhiều dashboard tài chính phức tạp. Tôi cam kết hoàn thành xuất sắc dự án.", 20000000.0m, "1 tháng", 2, now.AddDays(-14), false },
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380202", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380101", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f17", "Em chào anh chị. Em mới tốt nghiệp FPT Polytechnic, có làm một số bài tập lớn về ReactJS. Em mong muốn có cơ hội cọ xát thực tế.", 12000000.0m, "1 tháng", 3, now.AddDays(-13), false },
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380203", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380101", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f20", "Tôi là một lập trình viên Fullstack, thế mạnh về Node/Vue nhưng có thể code React hoàn toàn trơn tru. Có thể xem qua portfolio của tôi.", 18000000.0m, "1 tháng", 0, now.AddDays(-12), false },
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380204", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380103", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", "Chào Gemini Shop, tôi rất ấn tượng với phong cách thời trang của shop. Tôi có 4 năm kinh nghiệm thiết kế nhận diện thương hiệu. Hãy tham khảo portfolio của tôi.", 4000000.0m, "10 ngày", 2, now.AddDays(-14), false },
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380205", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380103", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f13", "Chào anh Tuấn, tôi thiết kế UI/UX và Branding. Phong cách thiết kế của tôi là hiện đại, thanh lịch, cực kỳ phù hợp với shop thời trang nam nữ.", 4500000.0m, "2 tuần", 3, now.AddDays(-13), false },
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380206", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380104", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "Chào chị Trang, tôi đã có kinh nghiệm viết bài chuẩn SEO cho các thương hiệu mỹ phẩm lớn. Cam kết bài viết độc quyền 100%, chuẩn chỉ và hấp dẫn.", 2500000.0m, "5 ngày", 2, now.AddDays(-12), false },
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380207", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380105", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "Chào Startup X, tôi có kinh nghiệm tối ưu hệ thống database PostgreSQL lớn. Tôi có thể xử lý tối ưu truy vấn nhanh chóng và thiết lập backup master-slave trên AWS trong 7 ngày.", 10000000.0m, "7 ngày", 2, now.AddDays(-14), false },
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380208", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380108", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "Chào quý công ty, tôi là kỹ sư Smart Contract. Tôi có chứng chỉ audit smart contract quốc tế và có kinh nghiệm rà soát mã nguồn Solidity tìm các lỗi logic và tràn bộ nhớ.", 45000000.0m, "10 ngày", 2, now.AddDays(-5), false },
                new object?[] { "222ebc99-9c0b-4ef8-bb6d-6bb9bd380209", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380111", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "Chào anh Minh, dự án e-learning Next.js này rất phù hợp với thế mạnh của tôi. Tôi đã làm 2 sản phẩm tương tự phục vụ hàng ngàn học viên.", 40000000.0m, "1 tháng", 2, now.AddDays(-8), false }
                }),

            new SeedTableData(
                "Contracts",
                new[] { "ContractsId", "JobPostsId", "ClientProfilesId", "FreelancerProfilesId", "ProposalsId", "Title", "Description", "TotalBudget", "PaymentType", "Status", "StartDate", "EndDate", "CompletedAt", "ESignContractPdfUrl", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "333ebc99-9c0b-4ef8-bb6d-6bb9bd380301", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380101", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "222ebc99-9c0b-4ef8-bb6d-6bb9bd380201", "Hợp đồng thiết kế Dashboard Admin - FPT Software", "Lập trình ReactJS Dashboard Admin tích hợp biểu đồ ChartJS.", 20000000.0m, 0, 0, "2026-05-18", "2026-06-18", null, null, now.AddDays(-12) },
                new object?[] { "333ebc99-9c0b-4ef8-bb6d-6bb9bd380302", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380103", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c13", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", "222ebc99-9c0b-4ef8-bb6d-6bb9bd380204", "Hợp đồng thiết kế bộ nhận diện thương hiệu Gemini Shop", "Thiết kế logo và túi giấy đựng quần áo.", 4000000.0m, 0, 0, "2026-05-20", "2026-06-03", null, null, now.AddDays(-10) },
                new object?[] { "333ebc99-9c0b-4ef8-bb6d-6bb9bd380303", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380104", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "222ebc99-9c0b-4ef8-bb6d-6bb9bd380206", "Hợp đồng viết bài chuẩn SEO chủ đề Skincare - Trần Thu Trang", "Viết 5 bài chia sẻ kinh nghiệm dưỡng da chuẩn SEO.", 2500000.0m, 0, 1, "2026-05-20", "2026-05-25", now.AddDays(-5), null, now.AddDays(-10) },
                new object?[] { "333ebc99-9c0b-4ef8-bb6d-6bb9bd380304", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380105", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "222ebc99-9c0b-4ef8-bb6d-6bb9bd380207", "Hợp đồng tối ưu PostgreSQL & AWS Cluster - Startup X", "Xử lý tối ưu cơ sở dữ liệu lớn và thiết lập clustering master-slave.", 10000000.0m, 0, 3, "2026-05-18", "2026-05-28", null, null, now.AddDays(-12) },
                new object?[] { "333ebc99-9c0b-4ef8-bb6d-6bb9bd380305", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380108", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c16", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", "222ebc99-9c0b-4ef8-bb6d-6bb9bd380208", "Hợp đồng Audit và bảo mật Smart Contract - Blockchain Labs", "Rà soát lỗ hổng bảo mật 3 smart contract staking và governance.", 45000000.0m, 0, 0, "2026-05-25", "2026-06-08", null, null, now.AddDays(-5) },
                new object?[] { "333ebc99-9c0b-4ef8-bb6d-6bb9bd380306", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380111", "c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "222ebc99-9c0b-4ef8-bb6d-6bb9bd380209", "Hợp đồng phát triển web e-learning Next.js - Startup X", "Xây dựng web e-learning tích hợp video player bài học, trắc nghiệm.", 40000000.0m, 0, 0, "2026-05-23", "2026-07-07", null, null, now.AddDays(-7) }
                }),

            new SeedTableData(
                "ESignTemplates",
                new[] { "ESignTemplatesId", "Name", "NameVi", "HtmlContent", "Version", "PlaceholderSchema", "Description", "IsActive", "CreatedBy", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "444ebc99-9c0b-4ef8-bb6d-6bb9bd380401", "Standard Freelance Agreement", "Hợp đồng lao động tự do chuẩn", "<html><body><h1>Hợp đồng dịch vụ</h1><p>Bên A: {client_name}</p><p>Bên B: {freelancer_name}</p><p>Công việc: {job_title}</p><p>Tổng ngân sách: {budget} VND</p></body></html>", 1, "{\"client_name\": \"string\", \"freelancer_name\": \"string\", \"budget\": \"number\", \"job_title\": \"string\"}", "Hợp đồng mặc định cho các công việc", true, "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11", now.AddDays(-29) }
                }),

            new SeedTableData(
                "ESignDocuments",
                new[] { "ESignDocumentsId", "ESignTemplatesId", "JobPostsId", "ContractsId", "DocumentCode", "RenderedHtmlContent", "Status", "DocumentHash", "ExpiresAt", "FinalizedAt", "ExportedPdfUrl", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "444ebc99-9c0b-4ef8-bb6d-6bb9bd380411", "444ebc99-9c0b-4ef8-bb6d-6bb9bd380401", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380101", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380301", "GB-2026-DOC-000", "<html><body><h1>Hợp đồng dịch vụ</h1><p>Bên A: c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c11</p><p>Bên B: f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11</p><p>Công việc: Hợp đồng thiết kế Dashboard Admin - FPT Software</p><p>Tổng ngân sách: 20000000.0 VND</p></body></html>", 3, "abc123hash0", now.AddDays(30), now.AddDays(-11), "https://cloudinary.com/gigbridge/pdf/GB-2026-DOC-000.pdf", now.AddDays(-12) },
                new object?[] { "444ebc99-9c0b-4ef8-bb6d-6bb9bd380412", "444ebc99-9c0b-4ef8-bb6d-6bb9bd380401", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380103", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380302", "GB-2026-DOC-001", "<html><body><h1>Hợp đồng dịch vụ</h1><p>Bên A: c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c13</p><p>Bên B: f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f19</p><p>Công việc: Hợp đồng thiết kế bộ nhận diện thương hiệu Gemini Shop</p><p>Tổng ngân sách: 4000000.0 VND</p></body></html>", 3, "abc123hash1", now.AddDays(30), now.AddDays(-9), "https://cloudinary.com/gigbridge/pdf/GB-2026-DOC-001.pdf", now.AddDays(-10) },
                new object?[] { "444ebc99-9c0b-4ef8-bb6d-6bb9bd380413", "444ebc99-9c0b-4ef8-bb6d-6bb9bd380401", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380104", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380303", "GB-2026-DOC-002", "<html><body><h1>Hợp đồng dịch vụ</h1><p>Bên A: c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c14</p><p>Bên B: f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f15</p><p>Công việc: Hợp đồng viết bài chuẩn SEO chủ đề Skincare - Trần Thu Trang</p><p>Tổng ngân sách: 2500000.0 VND</p></body></html>", 3, "abc123hash2", now.AddDays(30), now.AddDays(-9), "https://cloudinary.com/gigbridge/pdf/GB-2026-DOC-002.pdf", now.AddDays(-10) },
                new object?[] { "444ebc99-9c0b-4ef8-bb6d-6bb9bd380414", "444ebc99-9c0b-4ef8-bb6d-6bb9bd380401", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380105", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380304", "GB-2026-DOC-003", "<html><body><h1>Hợp đồng dịch vụ</h1><p>Bên A: c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c15</p><p>Bên B: f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f16</p><p>Công việc: Hợp đồng tối ưu PostgreSQL & AWS Cluster - Startup X</p><p>Tổng ngân sách: 10000000.0 VND</p></body></html>", 3, "abc123hash3", now.AddDays(30), now.AddDays(-11), "https://cloudinary.com/gigbridge/pdf/GB-2026-DOC-003.pdf", now.AddDays(-12) },
                new object?[] { "444ebc99-9c0b-4ef8-bb6d-6bb9bd380415", "444ebc99-9c0b-4ef8-bb6d-6bb9bd380401", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380108", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380305", "GB-2026-DOC-004", "<html><body><h1>Hợp đồng dịch vụ</h1><p>Bên A: c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c16</p><p>Bên B: f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f22</p><p>Công việc: Hợp đồng Audit và bảo mật Smart Contract</p><p>Tổng ngân sách: 45000000.0 VND</p></body></html>", 3, "abc123hash4", now.AddDays(30), now.AddDays(-4), "https://cloudinary.com/gigbridge/pdf/GB-2026-DOC-004.pdf", now.AddDays(-5) },
                new object?[] { "444ebc99-9c0b-4ef8-bb6d-6bb9bd380416", "444ebc99-9c0b-4ef8-bb6d-6bb9bd380401", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380111", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380306", "GB-2026-DOC-005", "<html><body><h1>Hợp đồng dịch vụ</h1><p>Bên A: c1eebc99-9c0b-4ef8-bb6d-6bb9bd380c15</p><p>Bên B: f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11</p><p>Công việc: Hợp đồng phát triển web e-learning Next.js</p><p>Tổng ngân sách: 40000000.0 VND</p></body></html>", 3, "abc123hash5", now.AddDays(30), now.AddDays(-6), "https://cloudinary.com/gigbridge/pdf/GB-2026-DOC-005.pdf", now.AddDays(-7) }
                }),

            new SeedTableData(
                "ESignSignatures",
                new[] { "ESignSignaturesId", "ESignDocumentsId", "UserId", "SignerRole", "SignatureImageUrl", "SignatureWidth", "SignatureHeight", "Status", "SignedAt", "IpAddress", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380411", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", 0, "https://cloudinary.com/gigbridge/signatures/sig_client_0.png", 150, 60, 1, now.AddDays(-11), "192.168.1.100", now.AddDays(-12) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380411", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", 1, "https://cloudinary.com/gigbridge/signatures/sig_fl_0.png", 150, 60, 1, now.AddDays(-11), "192.168.1.200", now.AddDays(-12) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380412", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c13", 0, "https://cloudinary.com/gigbridge/signatures/sig_client_1.png", 150, 60, 1, now.AddDays(-9), "192.168.1.101", now.AddDays(-10) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380412", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f19", 1, "https://cloudinary.com/gigbridge/signatures/sig_fl_1.png", 150, 60, 1, now.AddDays(-9), "192.168.1.201", now.AddDays(-10) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380413", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", 0, "https://cloudinary.com/gigbridge/signatures/sig_client_2.png", 150, 60, 1, now.AddDays(-9), "192.168.1.102", now.AddDays(-10) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380413", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", 1, "https://cloudinary.com/gigbridge/signatures/sig_fl_2.png", 150, 60, 1, now.AddDays(-9), "192.168.1.202", now.AddDays(-10) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380414", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", 0, "https://cloudinary.com/gigbridge/signatures/sig_client_3.png", 150, 60, 1, now.AddDays(-11), "192.168.1.103", now.AddDays(-12) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380414", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", 1, "https://cloudinary.com/gigbridge/signatures/sig_fl_3.png", 150, 60, 1, now.AddDays(-11), "192.168.1.203", now.AddDays(-12) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380415", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c16", 0, "https://cloudinary.com/gigbridge/signatures/sig_client_4.png", 150, 60, 1, now.AddDays(-4), "192.168.1.104", now.AddDays(-5) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380415", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", 1, "https://cloudinary.com/gigbridge/signatures/sig_fl_4.png", 150, 60, 1, now.AddDays(-4), "192.168.1.204", now.AddDays(-5) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380416", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", 0, "https://cloudinary.com/gigbridge/signatures/sig_client_5.png", 150, 60, 1, now.AddDays(-6), "192.168.1.105", now.AddDays(-7) },
                new object?[] { Guid.NewGuid(), "444ebc99-9c0b-4ef8-bb6d-6bb9bd380416", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", 1, "https://cloudinary.com/gigbridge/signatures/sig_fl_5.png", 150, 60, 1, now.AddDays(-6), "192.168.1.205", now.AddDays(-7) }
                }),

            new SeedTableData(
                "Milestones",
                new[] { "MilestonesId", "ContractsId", "Title", "Amount", "DueDate", "Status", "SortOrder", "SubmittedAt", "ApprovedAt", "PaidAt", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380501", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380301", "Milestone 1: Hoàn thành thiết kế khung và giao diện thô (Wireframe & Basic UI)", 10000000.0m, "2026-05-28", 3, 1, now.AddDays(-6), now.AddDays(-4), null, now.AddDays(-11) },
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380502", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380301", "Milestone 2: Tích hợp API và hoàn thiện biểu đồ", 10000000.0m, "2026-06-18", 1, 2, null, null, null, now.AddDays(-11) },
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380503", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380302", "Milestone 1: Giao file Logo Vector và nhận diện thương hiệu hoàn chỉnh", 4000000.0m, "2026-06-03", 1, 1, null, null, null, now.AddDays(-10) },
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380504", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380303", "Milestone 1: Giao 5 bài viết chuẩn SEO skincare độc quyền", 2500000.0m, "2026-05-25", 5, 1, now.AddDays(-6), now.AddDays(-5), now.AddDays(-5), now.AddDays(-10) },
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380505", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380304", "Milestone 1: Khảo sát và tối ưu hóa index các bảng chính", 5000000.0m, "2026-05-22", 5, 1, now.AddDays(-10), now.AddDays(-8), now.AddDays(-8), now.AddDays(-11) },
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380506", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380304", "Milestone 2: Thiết lập Master-Slave Replication & Backup tự động", 5000000.0m, "2026-05-28", 6, 2, now.AddDays(-4), null, null, now.AddDays(-11) },
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380507", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380305", "Milestone 1: Audit code staking contract và viết báo cáo nháp", 45000000.0m, "2026-06-08", 1, 1, null, null, null, now.AddDays(-5) },
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380508", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380306", "Milestone 1: Xây dựng cấu trúc dự án Next.js và thiết kế các trang tĩnh", 20000000.0m, "2026-06-15", 1, 1, null, null, null, now.AddDays(-7) },
                new object?[] { "555ebc99-9c0b-4ef8-bb6d-6bb9bd380509", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380306", "Milestone 2: Tích hợp trình phát video streaming và backend API", 20000000.0m, "2026-07-07", 0, 2, null, null, null, now.AddDays(-7) }
                }),

            new SeedTableData(
                "PaymentProofs",
                new[] { "PaymentProofsId", "MilestonesId", "UploadedById", "FileName", "FileUrl", "FileSize", "Note", "Status", "ConfirmedAt", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "555ebc99-9c0b-4ef8-bb6d-6bb9bd380504", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", "skincare_articles_receipt.jpg", "https://cloudinary.com/gigbridge/receipts/skincare_articles_receipt.jpg", 154200, "Thanh toán qua chuyển khoản ngân hàng Techcombank", 1, now.AddDays(-5), now.AddDays(-5) },
                new object?[] { Guid.NewGuid(), "555ebc99-9c0b-4ef8-bb6d-6bb9bd380505", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "db_optimize_m1_receipt.jpg", "https://cloudinary.com/gigbridge/receipts/db_optimize_m1_receipt.jpg", 204800, "Thanh toán milestone 1 tối ưu database cho Quân", 1, now.AddDays(-8), now.AddDays(-8) }
                }),

            new SeedTableData(
                "Reviews",
                new[] { "ReviewsId", "ContractsId", "ReviewerId", "RevieweeId", "Rating", "Comment", "CommunicationRating", "QualityRating", "TimelinessRating", "IsVisible", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "333ebc99-9c0b-4ef8-bb6d-6bb9bd380303", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", 5, "Bài viết viết rất chỉnh chu, chuẩn SEO tốt, nộp bài đúng tiến độ. Sẽ tiếp tục hợp tác lâu dài với bạn!", 5, 5, 5, true, now.AddDays(-5) },
                new object?[] { Guid.NewGuid(), "333ebc99-9c0b-4ef8-bb6d-6bb9bd380303", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f15", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c14", 5, "Chị khách hàng vô cùng thân thiện, hướng dẫn và đưa đề cương cực kỳ chi tiết, thanh toán nhanh gọn lẹ.", 5, 5, 5, true, now.AddDays(-5) }
                }),

            new SeedTableData(
                "Conversations",
                new[] { "ConversationsId", "User1Id", "User2Id", "ContractsId", "Type", "LastMessageAt", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "666ebc99-9c0b-4ef8-bb6d-6bb9bd380601", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", null, 0, now.AddHours(-1), now.AddDays(-14) },
                new object?[] { "666ebc99-9c0b-4ef8-bb6d-6bb9bd380602", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380301", 1, now.AddDays(-2), now.AddDays(-12) }
                }),

            new SeedTableData(
                "Messages",
                new[] { "MessagesId", "ConversationsId", "SenderId", "Content", "Type", "IsRead", "IsEdited", "IsDeleted", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "666ebc99-9c0b-4ef8-bb6d-6bb9bd380601", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "Chào Trí, anh thấy hồ sơ của em rất tốt về ReactJS. Bên anh đang có dự án dashboard tài chính cần làm gấp.", 0, true, false, false, now.AddDays(-14) },
                new object?[] { Guid.NewGuid(), "666ebc99-9c0b-4ef8-bb6d-6bb9bd380601", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "Dạ em chào anh Huy. Dự án dashboard đó bên mình đã có thiết kế Figma chưa ạ?", 0, true, false, false, now.AddDays(-14).AddMinutes(10) },
                new object?[] { Guid.NewGuid(), "666ebc99-9c0b-4ef8-bb6d-6bb9bd380601", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "Đã có Figma chi tiết rồi em. Anh có gửi link trong tin tuyển dụng đó. Em xem đề xuất giá khoảng bao nhiêu nhé.", 0, true, false, false, now.AddDays(-14).AddMinutes(30) },
                new object?[] { Guid.NewGuid(), "666ebc99-9c0b-4ef8-bb6d-6bb9bd380601", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "Em đã xem và gửi báo giá 20 triệu VND. Em tự tin hoàn thành trong 1 tháng như anh yêu cầu.", 0, true, false, false, now.AddDays(-14).AddHours(1) },
                new object?[] { Guid.NewGuid(), "666ebc99-9c0b-4ef8-bb6d-6bb9bd380602", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "Chào em, hợp đồng đã được tạo và ký điện tử. Em tiến hành làm milestone 1 nhé.", 0, true, false, false, now.AddDays(-12) },
                new object?[] { Guid.NewGuid(), "666ebc99-9c0b-4ef8-bb6d-6bb9bd380602", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "Dạ vâng anh, em đã bắt đầu xây dựng dự án và tạo layout.", 0, true, false, false, now.AddDays(-12).AddHours(1) },
                new object?[] { Guid.NewGuid(), "666ebc99-9c0b-4ef8-bb6d-6bb9bd380602", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "Anh Huy ơi, em đã hoàn thành layout thô và đẩy lên Github/Netlify, anh kiểm tra milestone 1 giúp em nhé.", 0, true, false, false, now.AddDays(-6) },
                new object?[] { Guid.NewGuid(), "666ebc99-9c0b-4ef8-bb6d-6bb9bd380602", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "Anh thấy giao diện rất đẹp và đúng Figma. Đã phê duyệt milestone 1 cho em rồi nhé. Tiếp tục làm milestone 2 nào.", 0, true, false, false, now.AddDays(-4) }
                }),

            new SeedTableData(
                "Disputes",
                new[] { "DisputesId", "ContractsId", "InitiatorId", "MilestonesId", "Reason", "Status", "Resolution", "ResolutionNote", "ResolvedByAdminId", "ResolvedAt", "CreatedAt" },
                new object?[][]
                {
                new object?[] { "777ebc99-9c0b-4ef8-bb6d-6bb9bd380701", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380304", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "555ebc99-9c0b-4ef8-bb6d-6bb9bd380506", "Tôi đã hoàn thành cấu hình master-slave và kiểm tra hoạt động tốt, gửi tài liệu hướng dẫn nhưng khách hàng không phản hồi và từ chối duyệt giải ngân thanh toán.", 1, null, null, null, null, now.AddDays(-3) }
                }),

            new SeedTableData(
                "DisputeEvidence",
                new[] { "DisputeEvidenceId", "DisputesId", "UploadedById", "FileName", "FileUrl", "FileSize", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "777ebc99-9c0b-4ef8-bb6d-6bb9bd380701", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "Replication_Setup_Guide.pdf", "https://cloudinary.com/gigbridge/dispute/Replication_Setup_Guide.pdf", 105430, now.AddDays(-3) }
                }),

            new SeedTableData(
                "DisputeMessages",
                new[] { "DisputeMessagesId", "DisputesId", "SenderId", "Content", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "777ebc99-9c0b-4ef8-bb6d-6bb9bd380701", "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f16", "Gửi Admin, tôi đã làm đúng các yêu cầu tối ưu database và replication, mong admin giải quyết giúp.", now.AddDays(-2) },
                new object?[] { Guid.NewGuid(), "777ebc99-9c0b-4ef8-bb6d-6bb9bd380701", "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c15", "Hệ thống replication chạy bị delay dữ liệu khoảng 1 phút, tôi chưa hài lòng và muốn cấu hình lại đồng bộ thời gian thực.", now.AddDays(-1.5) }
                }),

            new SeedTableData(
                "Notifications",
                new[] { "NotificationsId", "UserId", "Type", "Title", "Content", "ReferenceId", "ReferenceType", "IsRead", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", 3, "Hợp đồng đã bắt đầu", "Hợp đồng thiết kế Dashboard Admin đã bắt đầu.", "333ebc99-9c0b-4ef8-bb6d-6bb9bd380301", "Contract", false, now.AddDays(-12) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", 4, "Milestone 1 đã được phê duyệt", "Milestone 1 giao diện thô đã được phê duyệt thanh toán.", "555ebc99-9c0b-4ef8-bb6d-6bb9bd380501", "Milestone", false, now.AddDays(-4) },
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", 1, "Đề xuất mới nhận được", "Nguyễn Minh Trí đã nộp báo cáo đề xuất cho công việc của bạn.", "222ebc99-9c0b-4ef8-bb6d-6bb9bd380201", "Proposal", true, now.AddDays(-14) }
                }),

            new SeedTableData(
                "SavedJobs",
                new[] { "SavedJobsId", "UserId", "JobPostsId", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380102", now.AddDays(-5) },
                new object?[] { Guid.NewGuid(), "f0eebc99-9c0b-4ef8-bb6d-6bb9bd380f21", "111ebc99-9c0b-4ef8-bb6d-6bb9bd380106", now.AddDays(-2) }
                }),

            new SeedTableData(
                "SavedFreelancers",
                new[] { "SavedFreelancersId", "UserId", "FreelancerProfilesId", "CreatedAt" },
                new object?[][]
                {
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c11", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f11", now.AddDays(-10) },
                new object?[] { Guid.NewGuid(), "c0eebc99-9c0b-4ef8-bb6d-6bb9bd380c16", "f1eebc99-9c0b-4ef8-bb6d-6bb9bd380f22", now.AddDays(-3) }
                })
        };
    }

    private sealed class SeedTableData
    {
        public SeedTableData(string tableName, string[] columns, object?[][] rows)
        {
            TableName = tableName;
            Columns = columns;
            Rows = rows;
        }

        public string TableName { get; }
        public string[] Columns { get; }
        public object?[][] Rows { get; }
    }
}
