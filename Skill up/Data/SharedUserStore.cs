using System.Text.Json;
using Skill_up.Models;

namespace Skill_up.Data;

public interface ISharedUserStore
{
    IReadOnlyList<AdminUserViewModel> GetAll();
    AdminUserViewModel UpsertLogin(string email, string? displayName = null, string loginMethod = "Email");
    AdminUserViewModel Add(string name, string email, string careerGoal);
    bool UpdateStatus(string id, string status);
    bool Delete(string id);
}

public sealed class SharedUserStore : ISharedUserStore
{
    private static readonly object FileLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public SharedUserStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration["SharedUserStorage:Path"];
        var dataDirectory = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data")
            : (Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));

        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "users.json");
    }

    public IReadOnlyList<AdminUserViewModel> GetAll()
    {
        lock (FileLock)
        {
            return ReadUsers().ToList();
        }
    }

    public AdminUserViewModel UpsertLogin(string email, string? displayName = null, string loginMethod = "Email")
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        lock (FileLock)
        {
            var users = ReadUsers();
            var normalizedEmail = email.Trim();
            var user = users.FirstOrDefault(item =>
                string.Equals(item.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (user is null)
            {
                user = new AdminUserViewModel
                {
                    Id = $"u-{Guid.NewGuid():N}",
                    Name = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim(),
                    Email = normalizedEmail,
                    LoginMethod = loginMethod,
                    LastActive = FormatLastActive(DateTime.Now),
                    Status = "Active"
                };
                users.Add(user);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    user.Name = displayName.Trim();
                }

                user.LastActive = FormatLastActive(DateTime.Now);
                user.LoginMethod = loginMethod;
            }

            WriteUsers(users);
            return user;
        }
    }

    public AdminUserViewModel Add(string name, string email, string careerGoal)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        lock (FileLock)
        {
            var users = ReadUsers();
            var normalizedEmail = email.Trim();
            var existing = users.FirstOrDefault(item =>
                string.Equals(item.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.Name = string.IsNullOrWhiteSpace(name) ? existing.Name : name.Trim();
                existing.CareerGoal = careerGoal?.Trim() ?? string.Empty;
                existing.LastActive = FormatLastActive(DateTime.Now);
                existing.Status = "Active";
                WriteUsers(users);
                return existing;
            }

            var user = new AdminUserViewModel
            {
                Id = $"u-{Guid.NewGuid():N}",
                Name = name?.Trim() ?? string.Empty,
                Email = normalizedEmail,
                LastActive = FormatLastActive(DateTime.Now),
                LoginMethod = "Email",
                CareerGoal = careerGoal?.Trim() ?? string.Empty,
                Status = "Active"
            };

            users.Add(user);
            WriteUsers(users);
            return user;
        }
    }

    public bool UpdateStatus(string id, string status)
    {
        lock (FileLock)
        {
            var users = ReadUsers();
            var user = users.FirstOrDefault(item => item.Id == id);
            if (user is null) return false;

            user.Status = status;
            WriteUsers(users);
            return true;
        }
    }

    public bool Delete(string id)
    {
        lock (FileLock)
        {
            var users = ReadUsers();
            var removed = users.RemoveAll(item => item.Id == id) > 0;
            if (removed) WriteUsers(users);
            return removed;
        }
    }

    private List<AdminUserViewModel> ReadUsers()
    {
        if (!File.Exists(_filePath)) return [];

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<AdminUserViewModel>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void WriteUsers(List<AdminUserViewModel> users)
    {
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(users, JsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _filePath, true);
    }

    private static string FormatLastActive(DateTime localTime)
    {
        return localTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
