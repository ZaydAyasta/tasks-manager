using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Identity.Provisioning;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    PrintUsage();
    return 0;
}

if (!TryReadArguments(args, out var fullName, out var email))
{
    PrintUsage();
    return 2;
}

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();
var connectionString = configuration.GetConnectionString("NakamaDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__NakamaDatabase must be configured.");
    return 2;
}

var options = new DbContextOptionsBuilder<NakamaDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var dbContext = new NakamaDbContext(options);
var provisioner = new FirstAdminProvisioner(dbContext, new PasswordHasher<User>(), new SystemClock());
try
{
    var result = await provisioner.CreateAsync(fullName!, email!);
    Console.WriteLine("Admin created successfully.");
    Console.WriteLine($"Email: {result.Email}");
    Console.WriteLine($"Temporary password: {result.TemporaryPassword}");
    return 0;
}
catch (FirstAdminProvisioningException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static bool TryReadArguments(string[] arguments, out string? fullName, out string? email)
{
    fullName = null;
    email = null;
    if (arguments.Length != 5 || !string.Equals(arguments[0], "create-first-admin", StringComparison.Ordinal))
        return false;

    for (var index = 1; index < arguments.Length; index += 2)
    {
        if (index + 1 >= arguments.Length)
            return false;

        if (arguments[index] == "--name") fullName = arguments[index + 1];
        else if (arguments[index] == "--email") email = arguments[index + 1];
        else return false;
    }

    return !string.IsNullOrWhiteSpace(fullName) && !string.IsNullOrWhiteSpace(email);
}

static void PrintUsage() => Console.Error.WriteLine(
    "Usage: Nakama.Provisioning create-first-admin --name <full-name> --email <email>");
