using System.Security.Cryptography;
using ManpowerAllocation.Infrastructure.BreakGlass;

// Generates the PBKDF2 salt and hash for the emergency break-glass secret. The plain secret is
// never written anywhere by this tool; only the derived, non-reversible values are printed, for
// pasting into protected configuration. Usage:
//   dotnet run --project tools/BreakGlassHasher -- "<secret>" [iterations]
// If no secret is supplied on the command line it is read from the console without echoing.

const int DefaultIterations = 210_000;
const int SaltLengthBytes = 16;

var secret = args.Length > 0 ? args[0] : ReadSecretFromConsole();
if (string.IsNullOrWhiteSpace(secret))
{
    Console.Error.WriteLine("The secret must not be empty.");
    return 1;
}

var iterations = args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0
    ? parsed
    : DefaultIterations;

var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
var hash = BreakGlassSecretHasher.Derive(secret, salt, iterations);

Console.WriteLine();
Console.WriteLine("Break-glass configuration values (store in protected configuration, never in source):");
Console.WriteLine();
Console.WriteLine("As environment variables:");
Console.WriteLine($"  BreakGlass__SaltBase64={Convert.ToBase64String(salt)}");
Console.WriteLine($"  BreakGlass__SecretHashBase64={Convert.ToBase64String(hash)}");
Console.WriteLine($"  BreakGlass__Iterations={iterations}");
Console.WriteLine();
Console.WriteLine("Keep the plain secret only in IT's password vault. Rotating it means re-running this tool.");
return 0;

// Reads a secret from the console, masking each character. Falls back to a plain read when input
// is redirected (for example inside a CI shell), where key-by-key reading is not available.
static string ReadSecretFromConsole()
{
    Console.Write("Enter break-glass secret: ");

    if (Console.IsInputRedirected)
    {
        return Console.ReadLine() ?? string.Empty;
    }

    var buffer = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (buffer.Length > 0)
            {
                buffer.Length--;
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            buffer.Append(key.KeyChar);
        }
    }

    return buffer.ToString();
}
